using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>
/// Shields, hull and the eight resistances against Coriolis (EDCD/coriolis, develop branch), read
/// 2026-11-06. The expected figures are what a port of Coriolis's <c>shieldStrength</c>,
/// <c>shieldMetrics</c>, <c>armourMetrics</c> and <c>stackArmourResistances</c> returns for these
/// loadouts and this table's stock rows; the calculator's page was not driven. The engineered modifier
/// values in the first build are typical of a grade 5 roll, not read from a commander's ship. Where
/// Coriolis and the game differ the game's figure applies: Blueprints.tsv's "All Resistances" replaces
/// the per-type lines of the same recipe, as engineered loadouts in the journals show.
/// </summary>
public class CombatFiguresMatchTheReferenceCalculatorTests
{
    private static string Module(string slot, string item, params (string Label, double Value)[] modifiers)
    {
        var head = "{\"Slot\":\"" + slot + "\",\"Item\":\"" + item + "\",\"On\":true,\"Priority\":1,\"Health\":1.0";

        if (modifiers.Length == 0)
        {
            return head + "}";
        }

        var list = string.Join(
            ",",
            modifiers.Select(m => "{\"Label\":\"" + m.Label + "\",\"Value\":"
                                  + m.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                                  + ",\"OriginalValue\":0.0,\"LessIsGood\":0}"));

        return head + ",\"Engineering\":{\"BlueprintName\":\"Test\",\"Level\":5,\"Quality\":1.0,\"Modifiers\":[" + list + "]}}";
    }

    private static ShipLoadout Flown(string ship, params string[] modules)
    {
        var line = $$"""{"timestamp":"2026-11-06T09:00:00Z","event":"Loadout","Ship":"{{ship}}","ShipID":1,"UnladenMass":300.0,"CargoCapacity":0,"Modules":[{{string.Join(",", modules)}}]}""";

        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var journalEvent));

        return ShipLoadout.Unknown.Apply(journalEvent!);
    }

    private static CombatGauge Read(ShipLoadout loadout)
    {
        var build = new ShipBuild("F1", "ship-1", loadout.Type!, loadout.ShipId, loadout.Name, []);
        var combat = ShipGauges.Read(build, loadout).Combat;

        Assert.NotNull(combat);

        return combat;
    }

    private static void Resists(ResistanceSet? actual, double kinetic, double thermal, double explosive, double caustic)
    {
        Assert.NotNull(actual);
        Assert.Equal(kinetic, actual.Kinetic, 4);
        Assert.Equal(thermal, actual.Thermal, 4);
        Assert.Equal(explosive, actual.Explosive, 4);
        Assert.Equal(caustic, actual.Caustic, 4);
    }

    [Fact]
    public void AnEngineeredCombatPythonMatchesTheReference()
    {
        var combat = Read(Flown(
            "python",
            Module("Armour", "python_armour_grade3",
                ("DefenceModifierHealthMultiplier", 311.5), ("KineticResistance", -17.4),
                ("ThermicResistance", 2.15), ("ExplosiveResistance", -37.0)),
            Module("Slot04_Size5", "int_shieldgenerator_size5_class5_strong",
                ("ShieldGenStrength", 202.5), ("ShieldGenOptimalMass", 405.0),
                ("KineticResistance", 51.0), ("ThermicResistance", -2.5), ("ExplosiveResistance", 57.0)),
            Booster("TinyHardpoint1"), Booster("TinyHardpoint2"), Booster("TinyHardpoint3"), Booster("TinyHardpoint4"),
            Module("Slot05_Size5", "int_hullreinforcement_size5_class2",
                ("DefenceModifierHealthAddition", 449.5), ("KineticResistance", 3.0),
                ("ThermicResistance", 3.0), ("ExplosiveResistance", 3.0)),
            Module("Slot06_Size4", "int_hullreinforcement_size4_class2",
                ("DefenceModifierHealthAddition", 224.0), ("KineticResistance", 2.4),
                ("ThermicResistance", 2.4), ("ExplosiveResistance", 2.4))));

        Assert.Equal(1157.3923, combat.Shields!.Value, 2);
        Assert.Equal(1743.4, combat.Hull!.Value, 2);
        Resists(combat.ShieldResistance, 0.702642, 0.377977, 0.739054, 0);
        Resists(combat.HullResistance, -0.111449, 0.073634, -0.297006, 0);
        Assert.Equal(FigureKind.Measured, combat.Kind);
    }

    private static string Booster(string slot) =>
        Module(slot, "hpt_shieldbooster_size0_class5",
            ("DefenceModifierShieldMultiplier", 26.0), ("KineticResistance", 15.34),
            ("ThermicResistance", 15.34), ("ExplosiveResistance", 15.34));

    [Fact]
    public void AnExplorerWithGuardianReinforcementMatchesTheReference()
    {
        var combat = Read(Flown(
            "asp",
            Module("Armour", "asp_armour_grade1"),
            Module("Slot01_Size6", "int_shieldgenerator_size6_class3_fast"),
            Module("Slot03_Size3", "int_guardianshieldreinforcement_size3_class2"),
            Module("Slot04_Size3", "int_guardianhullreinforcement_size3_class2")));

        Assert.Equal(335.897031, combat.Shields!.Value, 2);
        Assert.Equal(703.0, combat.Hull!.Value, 2);
        Resists(combat.ShieldResistance, 0.4, -0.2, 0.5, 0);
        Resists(combat.HullResistance, -0.2, 0.02, -0.4, 0.05);
    }

    [Fact]
    public void ABuildCloseToStockMatchesTheReference()
    {
        var combat = Read(Flown(
            "cobramkiii",
            Module("Armour", "cobramkiii_armour_grade1"),
            Module("Slot01_Size4", "int_shieldgenerator_size4_class3"),
            Module("TinyHardpoint1", "hpt_shieldbooster_size0_class3")));

        Assert.Equal(120.971506, combat.Shields!.Value, 2);
        Assert.Equal(216.0, combat.Hull!.Value, 2);
        Resists(combat.ShieldResistance, 0.4, -0.2, 0.5, 0);
        Resists(combat.HullResistance, -0.2, 0.0, -0.4, 0);
    }
}

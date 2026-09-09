using D47.Core.Checklists;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>Where a ship carries two of a module, the checklist line says which one it means.</summary>
public class TwoOfTheSameModuleAreToldApartTests
{
    /// <summary>Two identical 2D hull reinforcements, and one 4D beside them.</summary>
    private static CommanderGameState Flying(params string[] slots)
    {
        var store = new GameStateStore();

        var modules = string.Join(",", slots.Select(slot =>
            $$"""{"Slot":"{{slot}}","Item":"{{(slot.EndsWith("Size2", StringComparison.Ordinal)
                ? "int_hullreinforcement_size2_class2"
                : "int_hullreinforcement_size4_class2")}}","On":true,"Priority":1,"Health":1.0}"""));

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-26T00:55:33Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     $$"""{"timestamp":"2026-08-26T00:55:33Z","event":"Loadout","Ship":"smallcombat01_nx","ShipID":49,"ShipName":"Tulimiekka","ShipIdent":"TU-1","HullValue":1,"ModulesValue":1,"Rebuy":1,"Modules":[{{modules}}]}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    private static string Line(CommanderGameState state, string slot)
    {
        var item = new ChecklistItem
        {
            Key = "one",
            Kind = ChecklistItemKind.Derived,
            Scope = new ChecklistScope(ChecklistGroup.Ship, "49"),
            Text = $"Deep Plating on {slot}",
            Hull = "smallcombat01_nx",
            Intent = new ChecklistIntent(ChecklistIntentKind.Experimental, slot) { Detail = "Deep Plating" },
        };

        return ChecklistWording.Said(item, state);
    }

    [Fact]
    public void OneOfAKindIsNamedByItsTypeAlone()
    {
        // The 2026-08-24 ruling, untouched: with one of them fitted there is nothing to disambiguate, and the
        // mounting point is not what the Commander wants to read.
        var said = Line(Flying("Slot04_Size2", "Slot02_Size4"), "Slot04_Size2");

        Assert.Contains("Hull Reinforcement", said, StringComparison.Ordinal);
        Assert.DoesNotContain(" in ", said, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoOfAKindEachNameTheirMountingPoint()
    {
        var state = Flying("Slot04_Size2", "Slot05_Size2", "Slot02_Size4");

        var first = Line(state, "Slot04_Size2");
        var second = Line(state, "Slot05_Size2");

        // The whole of the ask: two lines that used to be identical no longer are.
        Assert.NotEqual(first, second);
        Assert.Contains(" in ", first, StringComparison.Ordinal);
        Assert.Contains(" in ", second, StringComparison.Ordinal);
    }

    [Fact]
    public void AndTheTypeStillLeads()
    {
        var said = Line(Flying("Slot04_Size2", "Slot05_Size2"), "Slot04_Size2");

        // Type first, mounting point after it.
        var qualifier = said.LastIndexOf(" in ", StringComparison.Ordinal);

        Assert.True(qualifier > 0, said);
        Assert.Contains("2D Hull Reinforcement Package", said[..qualifier], StringComparison.Ordinal);
        Assert.NotEqual(string.Empty, said[(qualifier + 4)..].Trim());
        Assert.DoesNotContain("Slot04_Size2", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// The condition is the ship's rather than the list's, ruled 2026-08-26: a line reads the same
    /// wherever it appears, rather than changing with whatever happens to be beside it.
    /// </summary>
    [Fact]
    public void TheOtherSizeIsUnaffectedByTheTwins()
    {
        var said = Line(Flying("Slot04_Size2", "Slot05_Size2", "Slot02_Size4"), "Slot02_Size4");

        // One 4D on the ship, so it keeps the plain form even though two 2Ds are qualified.
        Assert.DoesNotContain(" in ", said, StringComparison.Ordinal);
    }
}

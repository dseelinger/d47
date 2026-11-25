using D47.Core.Journal;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>
/// A plot uses the range the ship jumps now, with the fuel and cargo aboard, not the game's
/// <c>MaxJumpRange</c>, which assumes one jump's fuel and an empty hold.
/// </summary>
public class ARouteIsPlottedAtTheCurrentRangeTests
{
    private const double Unladen = 1318.66;

    // The stock Mk II Supercharge Optimised drive's row in EliteSpecifications.tsv.
    private const double OptimalMass = 4670;
    private const double FuelPerJump = 6.8;
    private const double FuelPower = 2.5025;
    private const double FuelMultiplier = 0.011;

    private static ShipLoadout Caspian(bool withDrive = true)
    {
        var drive = withDrive
            ? """{"Slot":"FrameShiftDrive","Item":"int_hyperdrive_overcharge_size8_class5_overchargebooster_mkii","On":true,"Health":1.0}"""
            : """{"Slot":"MainEngines","Item":"int_engine_size7_class5","On":true,"Health":1.0}""";

        var store = new GameStateStore();
        Apply(store, """{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""");
        Apply(
            store,
            $$"""{"timestamp":"2026-01-01T00:00:01Z","event":"Loadout","Ship":"explorer_nx","ShipID":7,"UnladenMass":{{Unladen}},"FuelCapacity":{"Main":128.0,"Reserve":1.14},"CargoCapacity":32,"MaxJumpRange":84.57,"Modules":[{{drive}}]}""");

        return store.Active!.Ship;
    }

    private static void Apply(GameStateStore store, string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        store.Apply(parsed!, null, false);
    }

    private static GameStatus InShip(double fuel, double cargo) => new()
    {
        Flags = StatusFlags.InMainShip,
        FuelMain = fuel,
        Cargo = cargo,
        ReadAt = DateTimeOffset.UnixEpoch,
    };

    private static double At(double mass, double fuel) =>
        JumpRange.At(mass, OptimalMass, fuel, FuelPower, FuelMultiplier);

    [Fact]
    public void InTheShipTheFuelAndCargoAboardSetTheRange()
    {
        var range = ShipGauges.PlottingRange(Caspian(), InShip(fuel: 40, cargo: 20));

        Assert.Equal(At(Unladen + 40 + 20, FuelPerJump), range!.Value, 6);
    }

    [Fact]
    public void LessThanOneJumpsFuelShortensTheJumpItself()
    {
        var range = ShipGauges.PlottingRange(Caspian(), InShip(fuel: 3, cargo: 0));

        Assert.Equal(At(Unladen + 3, 3), range!.Value, 6);
    }

    [Fact]
    public void OutOfTheShipTheRangeIsAFullTankAndAnEmptyHold()
    {
        var onFoot = new GameStatus { FuelMain = 40, Cargo = 20, ReadAt = DateTimeOffset.UnixEpoch };

        var expected = At(Unladen + 128, FuelPerJump);

        Assert.Equal(expected, ShipGauges.PlottingRange(Caspian(), onFoot)!.Value, 6);
        Assert.Equal(expected, ShipGauges.PlottingRange(Caspian(), null)!.Value, 6);
        Assert.True(expected < 84.57);
    }

    [Fact]
    public void WithNoDriveToWorkFromItIsTheGamesOwnFigure() =>
        Assert.Equal(84.57, ShipGauges.PlottingRange(Caspian(withDrive: false), InShip(fuel: 40, cargo: 0)));
}

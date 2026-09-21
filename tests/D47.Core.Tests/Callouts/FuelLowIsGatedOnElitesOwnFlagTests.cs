using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>The tank-level half of <see cref="FuelCallout"/>, gated on Elite's own low-fuel flag (#341).</summary>
public class FuelLowIsGatedOnElitesOwnFlagTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("3311-01-01T00:00:00Z");

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CommanderGameState StateFrom(params string[] lines)
    {
        var store = new GameStateStore();
        store.Apply(Event("""{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));

        foreach (var line in lines)
        {
            store.Apply(Event(line));
        }

        return store.Active!;
    }

    private static GameStatus Status(StatusFlags flags, double fuel) => new()
    {
        Flags = flags | StatusFlags.InMainShip,
        FuelMain = fuel,
        ReadAt = Start,
    };

    private static CalloutContext Context(CommanderGameState state, GameStatus status) =>
        new(Start, IsPriming: false, state, status, NavRoute.None, []);

    /// <summary>Built from the ShipyardSwap and Loadout pair in #341: a Cobra's 16 t tank over a Caspian's 128 t capacity.</summary>
    [Fact]
    public void ASwapToABiggerTankIsNotWarnedOnTheOldShipsLevelAlone()
    {
        var state = StateFrom(
            """{"timestamp":"3311-01-01T00:21:06Z","event":"ShipyardSwap","ShipType":"explorer_nx","StoreOldShip":"CobraMkV"}""",
            """{"timestamp":"3311-01-01T00:21:07Z","event":"Loadout","Ship":"explorer_nx","FuelCapacity":{"Main":128.0,"Reserve":1.14}}""");

        // Status.json still reports the Cobra's full 16 t tank: 16 / 128 reads as 12%, but the flag is clear.
        Assert.Empty(new FuelCallout().Examine(Context(state, Status(StatusFlags.None, fuel: 16))));
    }

    [Fact]
    public void TheSameFractionWithTheFlagSetIsWarned()
    {
        var state = StateFrom(
            """{"timestamp":"3311-01-01T00:21:06Z","event":"ShipyardSwap","ShipType":"explorer_nx","StoreOldShip":"CobraMkV"}""",
            """{"timestamp":"3311-01-01T00:21:07Z","event":"Loadout","Ship":"explorer_nx","FuelCapacity":{"Main":128.0,"Reserve":1.14}}""");

        var announced = Assert.Single(
            new FuelCallout().Examine(Context(state, Status(StatusFlags.LowFuel, fuel: 15))));

        Assert.Equal("fuel.low", announced.Key);
        Assert.Contains("12 percent in the main tank", announced.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void CriticalIsGatedTheSameWay()
    {
        var state = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Loadout","Ship":"explorer_nx","FuelCapacity":{"Main":128.0,"Reserve":1.14}}""");

        // 9% of the tank, but the flag is clear.
        Assert.Empty(new FuelCallout().Examine(Context(state, Status(StatusFlags.None, fuel: 11.5))));

        var announced = Assert.Single(
            new FuelCallout().Examine(Context(state, Status(StatusFlags.LowFuel, fuel: 11.5))));

        Assert.Equal("fuel.critical", announced.Key);
    }

    [Fact]
    public void AnUnknownCapacityFallsBackToTheFlagAlone()
    {
        var state = StateFrom();

        var announced = Assert.Single(
            new FuelCallout().Examine(Context(state, Status(StatusFlags.LowFuel, fuel: 5))));

        Assert.Equal("fuel.low", announced.Key);
        Assert.Equal("Fuel low.", announced.Text);
    }
}

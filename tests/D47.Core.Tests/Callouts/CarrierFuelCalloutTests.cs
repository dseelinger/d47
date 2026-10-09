using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>The carrier captain warning that the tank holds less than two full jumps.</summary>
public class CarrierFuelCalloutTests
{
    private const string CallSign = "K7Q-B4X";

    private static readonly DateTimeOffset Noon = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string kind, DateTimeOffset at, params (string Key, object? Value)[] fields)
    {
        var payload = new Dictionary<string, object?>
        {
            ["timestamp"] = at.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["event"] = kind,
        };

        foreach (var (key, value) in fields)
        {
            payload[key] = value;
        }

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(payload), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static JournalEvent Stats(int fuel, string type = "FleetCarrier", string callsign = CallSign, double max = 500, int free = 8_946) =>
        Event(
            "CarrierStats",
            Noon,
            ("CarrierType", type),
            ("Callsign", callsign),
            ("Name", "Sacred Fire"),
            ("CarrierID", 3700000000L),
            ("FuelLevel", fuel),
            ("JumpRangeMax", max),
            ("SpaceUsage", new Dictionary<string, int> { ["TotalCapacity"] = 17_000, ["FreeSpace"] = free }));

    private static JournalEvent Dock(string station = CallSign, DateTimeOffset? at = null, long market = 3700000000L) =>
        Event(
            "Docked",
            at ?? Noon.AddMinutes(1),
            ("StationName", station),
            ("StationType", "FleetCarrier"),
            ("MarketID", market));

    private static JournalEvent Request(DateTimeOffset? at = null) =>
        Event("CarrierJumpRequest", at ?? Noon.AddMinutes(1), ("CarrierID", 3700000000L), ("SystemName", "Sol"));

    private static CommanderGameState State(params JournalEvent[] events)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));

        foreach (var journalEvent in events)
        {
            state.Apply(journalEvent);
        }

        return state;
    }

    private static CalloutContext Context(CommanderGameState state, bool priming, params JournalEvent[] events) =>
        new(Noon, priming, state, GameStatus.Unknown, NavRoute.None, events);

    private static List<Announcement> Say(CarrierFuelCallout callout, CommanderGameState state, params JournalEvent[] events) =>
        [.. callout.Examine(Context(state, false, events))];

    [Fact]
    public void FullJumpCostReproducesTheGamesOwnRangeAtALowTank()
    {
        var state = State(Stats(16, max: 66.525551, free: 17_000 - 8_054));

        Assert.Equal(16, CarrierFuel.FullJumpCost(state.Carrier)!.Value, 4);
    }

    [Fact]
    public void DockingAtYourOwnCarrierOnALowTankNamesTheTonnesAndTheCost()
    {
        var dock = Dock();
        var state = State(Stats(150, free: 17_000 - 8_054), dock);

        var said = Assert.Single(Say(new CarrierFuelCallout(), state, dock));

        Assert.Equal(CarrierFuelCallout.Key, said.Key);
        Assert.Equal(VoiceRole.CarrierCaptain, said.Voice);
        Assert.Equal("Sacred Fire has 150 tonnes of tritium; a full jump at this load burns 88.", said.Text);
    }

    [Fact]
    public void ARoomyTankIsSilent()
    {
        var dock = Dock();
        var state = State(Stats(400, free: 17_000 - 8_054), dock);

        Assert.Empty(Say(new CarrierFuelCallout(), state, dock));
    }

    [Fact]
    public void ASecondJumpRequestOnTheSameReadingIsSilent()
    {
        var callout = new CarrierFuelCallout();
        var request = Request();
        var state = State(Stats(150, free: 17_000 - 8_054), request);

        Assert.Single(Say(callout, state, request));
        Assert.Empty(Say(callout, state, Request(Noon.AddMinutes(2))));
    }

    [Fact]
    public void ALoadGameLetsTheSameReadingSpeakAgain()
    {
        var callout = new CarrierFuelCallout();
        var request = Request();
        var state = State(Stats(150, free: 17_000 - 8_054), request);

        Assert.Single(Say(callout, state, request));
        Assert.Single(Say(callout, state, Event("LoadGame", Noon.AddMinutes(3)), Request(Noon.AddMinutes(4))));
    }

    [Fact]
    public void FuelDepositedAboveTwoJumpsIsSilent()
    {
        var request = Request();
        var state = State(
            Stats(150, free: 17_000 - 8_054),
            Event("CarrierDepositFuel", Noon, ("Total", 400)),
            request);

        Assert.Empty(Say(new CarrierFuelCallout(), state, request));
    }

    [Fact]
    public void ASquadronCarrierIsSilent()
    {
        var request = Request();
        var state = State(Stats(150, type: "SquadronCarrier", free: 17_000 - 8_054), request);

        Assert.Empty(Say(new CarrierFuelCallout(), state, request));
    }

    [Fact]
    public void ACarrierTheCommanderDoesNotOwnIsSilent()
    {
        var dock = Dock("OTH-ER1", market: 3700000001L);
        var state = State(Stats(150, free: 17_000 - 8_054), dock);

        Assert.Empty(Say(new CarrierFuelCallout(), state, dock));
    }

    [Fact]
    public void ACarrierThatMovedSinceTheReadingSaysTheFigureIsOldAndHowOld()
    {
        var move = Event("CarrierJump", Noon.AddMinutes(30), ("StarSystem", "Sol"), ("CarrierID", 3700000000L));
        var dock = Dock(at: Noon.AddHours(2));
        var state = State(Stats(150, free: 17_000 - 8_054), move, dock);

        var said = Assert.Single(Say(new CarrierFuelCallout(), state, dock));

        Assert.EndsWith("That is the reading from 2 hours ago. The tank can only be lower.", said.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void NothingIsSaidWhileCatchingUp()
    {
        var dock = Dock();
        var state = State(Stats(150, free: 17_000 - 8_054), dock);

        Assert.Empty(new CarrierFuelCallout().Examine(Context(state, true, dock)));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void TheRowExistsAndDefaultsOn()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var row = surface.Registry.All
            .SelectMany(capability => capability.Descriptor.Settings)
            .Single(row => row.Key == CalloutCapability.CarrierFuelKey);

        Assert.Equal(SettingKind.Toggle, row.Kind);
        Assert.True(new CalloutSettings().CarrierFuel);
        Assert.Equal("true", row.Binding!.Read(D47Settings.Defaults));
        Assert.False(row.Binding!.Write!(D47Settings.Defaults, "false")!.Callouts.CarrierFuel);
    }
}

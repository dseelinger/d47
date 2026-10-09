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

/// <summary>The carrier captain warning that the balance covers under four weeks of upkeep.</summary>
public class CarrierUpkeepCalloutTests
{
    private const string CallSign = "K7Q-B4X";

    private const long Weekly = 10_000_000;

    private static readonly DateTimeOffset Recorded = new(2026, 8, 27, 20, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset SameWeek = Recorded.AddDays(2);

    private static readonly DateTimeOffset TwoTicksLater = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

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

    private static JournalEvent Stats(DateTimeOffset at, long balance, string type = "FleetCarrier") =>
        Event(
            "CarrierStats",
            at,
            ("CarrierType", type),
            ("Callsign", CallSign),
            ("Name", "Sacred Fire"),
            ("CarrierID", 3700000000L),
            ("Finance", new Dictionary<string, long> { ["CarrierBalance"] = balance }));

    private static JournalEvent Dock(DateTimeOffset at, string station = CallSign, long market = 3700000000L) =>
        Event("Docked", at, ("StationName", station), ("StationType", "FleetCarrier"), ("MarketID", market));

    private static JournalEvent Request(DateTimeOffset at) =>
        Event("CarrierJumpRequest", at, ("CarrierID", 3700000000L), ("SystemName", "Sol"));

    /// <summary>A carrier with a weekly figure of 10,000,000 and <paramref name="balance"/> recorded on 27 Aug.</summary>
    private static CommanderGameState State(long balance, string type = "FleetCarrier", params JournalEvent[] after)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));

        state.Apply(Stats(Recorded.AddDays(-7), balance + Weekly, type));
        state.Apply(Stats(Recorded, balance, type));

        foreach (var journalEvent in after)
        {
            state.Apply(journalEvent);
        }

        return state;
    }

    private static List<Announcement> Say(
        CarrierUpkeepCallout callout, CommanderGameState state, DateTimeOffset now, bool priming = false, params JournalEvent[] events) =>
        [.. callout.Examine(new CalloutContext(now, priming, state, GameStatus.Unknown, NavRoute.None, events))];

    [Fact]
    public void ABalanceUnderFourWeeksNamesTheWeeksAndTheWeeklyFigure()
    {
        var dock = Dock(SameWeek);
        var state = State(30_000_000, after: dock);

        var said = Assert.Single(Say(new CarrierUpkeepCallout(), state, SameWeek, events: dock));

        Assert.Equal(CarrierUpkeepCallout.Key, said.Key);
        Assert.Equal(VoiceRole.CarrierCaptain, said.Voice);
        Assert.Equal("Sacred Fire's account covers three more weeks of upkeep at 10,000,000 a week.", said.Text);
    }

    [Fact]
    public void AFiveWeekBalanceWithTwoTicksSinceWarnsAndSaysThreeWeeks()
    {
        var request = Request(TwoTicksLater);
        var state = State(50_000_000, after: request);

        var said = Assert.Single(Say(new CarrierUpkeepCallout(), state, TwoTicksLater, events: request));

        Assert.Equal(
            "Sacred Fire's account covers three more weeks of upkeep at 10,000,000 a week. "
            + "That is the recorded balance less the upkeep since.",
            said.Text);
    }

    [Fact]
    public void ABalanceCoveringFourWeeksIsSilent()
    {
        var dock = Dock(SameWeek);
        var state = State(40_000_000, after: dock);

        Assert.Empty(Say(new CarrierUpkeepCallout(), state, SameWeek, events: dock));
    }

    [Fact]
    public void NoWeeklyFigureIsSilent()
    {
        var request = Request(SameWeek);
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));
        state.Apply(Stats(Recorded, 1_000));
        state.Apply(request);

        Assert.Empty(Say(new CarrierUpkeepCallout(), state, SameWeek, events: request));
    }

    [Fact]
    public void ASquadronCarrierIsSilent()
    {
        var request = Request(SameWeek);
        var state = State(30_000_000, "SquadronCarrier", request);

        Assert.Empty(Say(new CarrierUpkeepCallout(), state, SameWeek, events: request));
    }

    [Fact]
    public void ACarrierTheCommanderDoesNotOwnIsSilent()
    {
        var dock = Dock(SameWeek, "OTH-ER1", 3700000001L);
        var state = State(30_000_000, after: dock);

        Assert.Empty(Say(new CarrierUpkeepCallout(), state, SameWeek, events: dock));
    }

    [Fact]
    public void ASecondJumpRequestOnTheSameBalanceIsSilent()
    {
        var callout = new CarrierUpkeepCallout();
        var request = Request(SameWeek);
        var state = State(30_000_000, after: request);

        Assert.Single(Say(callout, state, SameWeek, events: request));
        Assert.Empty(Say(callout, state, SameWeek, events: Request(SameWeek.AddMinutes(1))));
    }

    [Fact]
    public void ANewRecordedBalanceSpeaksAgain()
    {
        var callout = new CarrierUpkeepCallout();
        var request = Request(SameWeek);
        var state = State(30_000_000, after: request);

        Assert.Single(Say(callout, state, SameWeek, events: request));

        state.Apply(Stats(SameWeek.AddMinutes(5), 20_000_000));

        Assert.Single(Say(callout, state, SameWeek.AddMinutes(6), events: Request(SameWeek.AddMinutes(6))));
    }

    [Fact]
    public void ALoadGameLetsTheSameBalanceSpeakAgain()
    {
        var callout = new CarrierUpkeepCallout();
        var request = Request(SameWeek);
        var state = State(30_000_000, after: request);

        Assert.Single(Say(callout, state, SameWeek, events: request));
        Assert.Single(Say(
            callout, state, SameWeek, events: [Event("LoadGame", SameWeek.AddMinutes(3)), Request(SameWeek.AddMinutes(4))]));
    }

    [Fact]
    public void NothingIsSaidWhileCatchingUp()
    {
        var dock = Dock(SameWeek);
        var state = State(30_000_000, after: dock);

        Assert.Empty(Say(new CarrierUpkeepCallout(), state, SameWeek, priming: true, events: dock));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void TheRowExistsAndDefaultsOn()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var row = surface.Registry.All
            .SelectMany(capability => capability.Descriptor.Settings)
            .Single(row => row.Key == CalloutCapability.CarrierUpkeepKey);

        Assert.Equal(SettingKind.Toggle, row.Kind);
        Assert.True(new CalloutSettings().CarrierUpkeep);
        Assert.Equal("true", row.Binding!.Read(D47Settings.Defaults));
        Assert.False(row.Binding!.Write!(D47Settings.Defaults, "false")!.Callouts.CarrierUpkeep);
    }
}

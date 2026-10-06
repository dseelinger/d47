using System.Text.Json;
using D47.Core.Callouts;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>"Noted" and the snoozes answer a carrier warning that has just been spoken (#837).</summary>
public class ANotedOrASnoozeAnswersACarrierWarningTests
{
    private const string Commander = "F1";

    private static readonly DateTimeOffset Noon = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly StandingWarnings _warnings = new();

    private readonly CarrierFuelCallout _fuel = new();

    private readonly CommanderGameState _state = new(new CommanderIdentity(Commander, "Fixture"));

    public ANotedOrASnoozeAnswersACarrierWarningTests() => _fuel.Warnings = _warnings;

    private static JournalEvent Event(string kind, int minutes, params (string Key, object? Value)[] fields)
    {
        var payload = new Dictionary<string, object?>
        {
            ["timestamp"] = Noon.AddMinutes(minutes).ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["event"] = kind,
        };

        foreach (var (key, value) in fields)
        {
            payload[key] = value;
        }

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(payload), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static JournalEvent Stats(int fuel) =>
        Event(
            "CarrierStats",
            0,
            ("CarrierType", "FleetCarrier"),
            ("Callsign", "K7Q-B4X"),
            ("Name", "Sacred Fire"),
            ("CarrierID", 3700000000L),
            ("FuelLevel", fuel),
            ("JumpRangeMax", 500.0),
            ("SpaceUsage", new Dictionary<string, int> { ["TotalCapacity"] = 17_000, ["FreeSpace"] = 8_946 }));

    private static JournalEvent Dock(int minutes) =>
        Event(
            "Docked",
            minutes,
            ("StationName", "K7Q-B4X"),
            ("StationType", "FleetCarrier"),
            ("MarketID", 3700000000L));

    private List<Announcement> Say(params JournalEvent[] events)
    {
        foreach (var journalEvent in events)
        {
            _state.Apply(journalEvent);
        }

        return [.. _fuel.Examine(new CalloutContext(Noon, false, _state, GameStatus.Unknown, NavRoute.None, events))];
    }

    private async Task<string> SaidAsync(string input)
    {
        var registry = CapabilityRegistry.Build(
            [RemindersCapability.Create(null, () => Commander, () => Noon, _warnings)]);
        var model = FakeLlmProvider.Answering("From the model.");
        var loop = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            model,
            clock: new InstantClock());

        loop.Retry = RetryPolicy.Default with { Attempts = 1 };

        var text = new System.Text.StringBuilder();

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
            if (turnEvent is TurnEvent.TextDelta delta)
            {
                text.Append(delta.Text);
            }
        }

        return text.ToString();
    }

    [Fact]
    public async Task NotedSilencesTheWarningUntilTheTankRecoversAndFallsAgain()
    {
        Assert.Single(Say(Stats(150), Dock(1)));

        Assert.Equal("Noted.", await SaidAsync("noted"));

        Assert.Empty(Say(Dock(2)));
        Assert.Empty(Say(Event("CarrierDepositFuel", 3, ("Total", 150)), Dock(3)));
        Assert.Empty(Say(Event("CarrierDepositFuel", 4, ("Total", 400)), Dock(4)));
        Assert.Single(Say(Stats(150), Dock(5)));
    }

    [Fact]
    public async Task RemindMeNextTimeSpeaksAtTheNextDockOnTheSameReading()
    {
        Assert.Single(Say(Stats(150), Dock(1)));
        Assert.Empty(Say(Dock(2)));

        var said = await SaidAsync("remind me next time");

        Assert.Equal("I'll bring up the carrier's fuel warning again the next time.", said);
        Assert.Single(Say(Dock(3)));
        Assert.Empty(Say(Dock(4)));
    }

    [Theory]
    [InlineData("remind me next session")]
    [InlineData("remind me tomorrow")]
    public async Task RemindMeNextSessionStaysQuietUntilTheNextLoadGame(string answer)
    {
        Assert.Single(Say(Stats(150), Dock(1)));

        Assert.Equal("I'll stay quiet about the carrier's fuel warning until your next session.", await SaidAsync(answer));

        Assert.Empty(Say(Dock(2)));
        Assert.Empty(Say(Event("CarrierDepositFuel", 3, ("Total", 100)), Dock(3)));
        Assert.Single(Say(Event("LoadGame", 4), Dock(5)));
    }

    [Fact]
    public async Task WithNothingFiredNotedIsNotMatched()
    {
        Assert.Equal("From the model.", await SaidAsync("noted"));
    }

    [Fact]
    public async Task AnAnsweredWarningIsNotAnsweredTwice()
    {
        Assert.Single(Say(Stats(150), Dock(1)));
        Assert.Equal("Noted.", await SaidAsync("noted"));

        Assert.Equal("From the model.", await SaidAsync("noted"));
    }

    [Fact]
    public void TheUpkeepWarningAnswersToo()
    {
        var upkeep = new CarrierUpkeepCallout { Warnings = _warnings };
        _warnings.Fired(Commander, CarrierUpkeepCallout.Key, "the carrier's upkeep warning", Noon);

        Assert.True(_warnings.Acknowledge(Commander));
        Assert.True(_warnings.Silenced(Commander, CarrierUpkeepCallout.Key));
        Assert.Empty(upkeep.Examine(new CalloutContext(Noon, false, _state, GameStatus.Unknown, NavRoute.None, [])));
    }
}

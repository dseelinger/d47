using System.Globalization;
using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Seats;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Seats;

/// <summary>The board asked for without the model is read by the First Officer seat on the ship flown, in the core's words.</summary>
public class TheFirstOfficerReadsTheBoardTests
{
    internal static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-30T10:00:00Z", CultureInfo.InvariantCulture);

    internal static readonly CrewSeat Ilo = new("0000000a", CrewRole.FirstOfficer, null, "Ilo Varga");

    internal static TurnLoop Build(Func<ShipSeats?> seats, ILlmProvider? provider = null)
    {
        var registry = CapabilityRegistry.Build([MissionsCapability.Create(Board, () => Now)]);

        return new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(provider is not null),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider,
            clock: new InstantClock())
        {
            SeatsFlown = seats,
        };
    }

    internal static ShipSeats Aboard(int shipId, params CrewSeat[] seats) => new("F100", shipId, "python", seats);

    internal static async Task<List<TurnEvent>> RunAsync(TurnLoop loop, string input)
    {
        List<TurnEvent> events = [];

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
            events.Add(turnEvent);
        }

        return events;
    }

    internal static string Spoken(IEnumerable<TurnEvent> events) =>
        string.Concat(events.OfType<TurnEvent.TextDelta>().Select(delta => delta.Text));

    [Fact]
    public async Task TheFirstOfficerIsAddressedBeforeTheBoardIsRead()
    {
        var events = await RunAsync(Build(() => Aboard(7, Ilo)), "mission board");

        var addressed = Assert.Single(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(VoiceRole.Crew, addressed.Role);
        Assert.Equal("Ilo Varga", addressed.Name);
        Assert.True(
            events.IndexOf(addressed) < events.FindIndex(turnEvent => turnEvent is TurnEvent.TextDelta),
            "Addressed comes before the text.");
    }

    [Fact]
    public async Task TheSeatSaysWhatTheCoreWouldHaveSaid()
    {
        var withSeat = Spoken(await RunAsync(Build(() => Aboard(7, Ilo)), "mission board"));
        var withoutSeat = Spoken(await RunAsync(Build(() => null), "mission board"));

        Assert.Equal(withoutSeat, withSeat);
        Assert.Equal(MissionsCapability.Describe(Board(), Now), withSeat);
    }

    [Fact]
    public async Task TheReadTheMissionBoardCommandIsReadByTheSeatToo()
    {
        var events = await RunAsync(Build(() => Aboard(7, Ilo)), "read the mission board");

        Assert.Equal("Ilo Varga", Assert.Single(events.OfType<TurnEvent.Addressed>()).Name);
    }

    [Fact]
    public async Task WhatsOnTheSlateReachesTheBoardWithoutTheModel()
    {
        var provider = FakeLlmProvider.Answering("The model should not have been asked.");
        var events = await RunAsync(Build(() => null, provider), "what's on the slate");

        Assert.Null(provider.LastRequest);
        Assert.Equal(MissionsCapability.Describe(Board(), Now), Spoken(events));
    }

    [Fact]
    public async Task TheNextModelTurnHearsThatIloVargaAnswered()
    {
        var provider = FakeLlmProvider.Answering("Noted.");
        var loop = Build(() => Aboard(7, Ilo), provider);

        await RunAsync(loop, "mission board");
        await RunAsync(loop, "anything else I should know");

        var carried = string.Concat(
            provider.LastRequest!.Prompt.History[0].Content.OfType<ConversationContent.Text>().Select(text => text.Value));

        Assert.Contains("The Commander said \"mission board\", and Ilo Varga answered \"You have", carried, StringComparison.Ordinal);
    }

    internal static CommanderGameState Board()
    {
        var store = new GameStateStore();
        store.Apply(Event("""{ "timestamp":"2026-09-30T09:00:00Z", "event":"LoadGame", "FID":"F100", "Commander":"Fixture" }"""));
        store.Apply(Event(
            """{ "timestamp":"2026-09-30T09:30:00Z", "event":"MissionAccepted", "Faction":"Party of Yoru", "Name":"Mission_Delivery", "LocalisedName":"Polymers Run", "DestinationSystem":"Yoru", "DestinationStation":"Dock 7", "Expiry":"2026-09-30T14:00:00Z", "Reward":12000, "MissionID":7 }"""));

        return store.Active!;
    }

    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var journalEvent));
        return journalEvent!;
    }
}

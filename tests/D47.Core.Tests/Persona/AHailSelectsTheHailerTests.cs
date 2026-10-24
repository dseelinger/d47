using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Listening;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>A hail moves the comms selection to the hailer, so the comms key reaches them and the primary key still reaches the ship.</summary>
public class AHailSelectsTheHailerTests
{
    private const string ShipAi = "Warden";

    private const string ShipAiBrief = "You are the ship's AI aboard this vessel.";

    private const string Hailer = "Odalys Fenn";

    /// <summary><c>ElevenLabsScribeTranscriber.MostKeyterms</c>: Scribe keeps only this many names from the front.</summary>
    private const int MostKeyterms = 99;

    private static readonly DateTimeOffset At = new(3312, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private sealed class World
    {
        public string System { get; set; } = "Shinrarta Dezhra";

        public NpcsHeardHere Roster { get; } = new();

        public CommsChannels Channels { get; } = new();
    }

    private static (TurnLoop Loop, ChatterLine Line, World World) Build(ILlmProvider provider)
    {
        var world = new World();
        var surface = TestSurface.For(new MemoryInstall());

        var line = new ChatterLine(() => world.System, roster: () => world.Roster);

        var loop = new TurnLoop(
            surface.Registry,
            surface.Router,
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider)
        {
            Persona = ShipAiBrief,
            ShipAiNames = () => [ShipAi],
            CommsSelected = () => world.Channels.Selected,
        };

        loop.Lines.Add(line);

        return (loop, line, world);
    }

    private static void Heard(ChatterLine line, World world, string name, string text, int exchangeIndex, bool hail)
    {
        var said = new NpcChatterLine(name, text);

        world.Roster.Heard(
            new Announcement(NpcChatter.LineKey, text) { Speaker = name, Invented = new NpcChatterHeard(said, exchangeIndex, hail) },
            null,
            At);
        line.Heard(said, exchangeIndex);
    }

    /// <summary>A hail as the host handles it: heard, the roster pushed to the channels, then the hailer selected.</summary>
    private static void Hail(ChatterLine line, World world)
    {
        Heard(line, world, "Brannock", "Cargo's late again.", 3, hail: false);
        Heard(line, world, Hailer, "Commander, that was some fine flying back there.", 7, hail: true);

        world.Channels.Update(new CommsInputs { Heard = world.Roster.Present });
        Assert.True(world.Channels.Select(CommsChannel.System, "Brannock"));
        Assert.True(world.Channels.Hailed(Hailer));
    }

    [Theory]
    [InlineData("Thank you")]
    [InlineData("Where are you headed?")]
    [InlineData("landing gear")]
    public async Task ACommsTurnAfterAHailIsAnsweredByTheHailer(string said)
    {
        var provider = FakeLlmProvider.Answering("Any time, Commander.");
        var (loop, line, world) = Build(provider);
        Hail(line, world);

        var events = await OnComms.SayAsync(loop, said);

        Assert.Equal(Hailer, Assert.Single(events.OfType<TurnEvent.Addressed>()).Name);
        Assert.Contains($"You are {Hailer}", provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
        Assert.Contains($"Commander: {said}", provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APrimaryTurnAfterAHailGoesToTheShipAi()
    {
        var provider = FakeLlmProvider.Answering("You're welcome.");
        var (loop, line, world) = Build(provider);
        Hail(line, world);

        var events = await OnComms.AskAsync(loop, "Thank you");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.DoesNotContain(Hailer, provider.LastRequest?.Prompt.Persona ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSpeakersHeardHereFollowTheShipAiNewestFirst()
    {
        var (_, line, world) = Build(FakeLlmProvider.Answering("Noted."));
        Hail(line, world);

        var nouns = ProperNouns.From(Somewhere(), shipAi: [ShipAi, "D47"], speakers: line.Speakers);

        Assert.Equal([ShipAi, "D47", Hailer, "Brannock"], nouns.Take(4));
        Assert.Contains(Hailer, nouns.Take(MostKeyterms));
    }

    [Fact]
    public void NoSpeakerIsHeldOnceTheCommanderIsInAnotherSystem()
    {
        var (_, line, world) = Build(FakeLlmProvider.Answering("Noted."));
        Hail(line, world);

        Assert.Contains(Hailer, line.Speakers);

        world.System = "Sol";

        Assert.Empty(line.Speakers);
    }

    private static CommanderGameState Somewhere()
    {
        var store = new GameStateStore();

        foreach (var json in new[]
                 {
                     """{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"3311-01-01T00:01:00Z","event":"Docked","StarSystem":"Shinrarta Dezhra","StationName":"Jameson Memorial"}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }
}

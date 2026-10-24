using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Listening;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>After a hail, what the Commander says next goes to the hailer, named or not, while the exchange lasts.</summary>
public class AHailIsAnsweredWithoutItsNameTests
{
    private const string ShipAi = "Warden";

    private const string ShipAiBrief = "You are the ship's AI aboard this vessel.";

    private const string Hailer = "Odalys Fenn";

    private const string ModelFreeCommand = "landing gear";

    /// <summary><c>ElevenLabsScribeTranscriber.MostKeyterms</c>: Scribe keeps only this many names from the front.</summary>
    private const int MostKeyterms = 99;

    private sealed class World
    {
        public DateTimeOffset Now { get; set; } = new(3312, 5, 1, 12, 0, 0, TimeSpan.Zero);

        public string System { get; set; } = "Shinrarta Dezhra";

        public AmbientSituation Situation { get; set; } = AmbientSituation.Docked;
    }

    private static (TurnLoop Loop, ChatterLine Line, World World) Build(ILlmProvider provider)
    {
        var world = new World();
        var surface = TestSurface.For(new MemoryInstall());

        var line = new ChatterLine(
            () => world.Now,
            () => world.System,
            () => world.Situation,
            modelFree: input => surface.Router.MatchSetting(input) is not null
                                || surface.Router.MatchToolCommand(input) is not null
                                || surface.Router.Match(input, InputSource.Spoken) is not null
                                || surface.Router.MatchSettingBySound(input) is { Certain: true });

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
        };

        loop.Lines.Add(line);

        return (loop, line, world);
    }

    private static void Hail(ChatterLine line, World world) =>
        line.Heard(
            new NpcChatterLine(Hailer, "Commander, that was some fine flying back there."),
            NpcChatter.MayNotice(NpcChatterKind.Hail, 7),
            7,
            world.System,
            world.Situation,
            hail: true);

    private static void Passersby(ChatterLine line, World world)
    {
        var noticing = Enumerable.Range(0, 20).First(index => NpcChatter.MayNotice(NpcChatterKind.Passersby, index));

        line.Heard(new NpcChatterLine(Hailer, "Cargo's late again."), true, noticing, world.System, world.Situation);
        line.Heard(new NpcChatterLine("Brannock", "It's always late."), true, noticing, world.System, world.Situation);
    }

    private static async Task<List<TurnEvent>> RunAsync(TurnLoop loop, string input)
    {
        List<TurnEvent> events = [];

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
            events.Add(turnEvent);
        }

        return events;
    }

    [Theory]
    [InlineData("Thank you")]
    [InlineData("Thanks")]
    [InlineData("Okay")]
    [InlineData("Yes")]
    [InlineData("Where are you headed?")]
    public async Task AnUnnamedReplyIsAnsweredByTheHailer(string said)
    {
        var provider = FakeLlmProvider.Answering("Any time, Commander.");
        var (loop, line, world) = Build(provider);
        Hail(line, world);

        var events = await RunAsync(loop, said);

        Assert.Equal(Hailer, Assert.Single(events.OfType<TurnEvent.Addressed>()).Name);
        Assert.Contains($"You are {Hailer}", provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
        Assert.Contains($"Commander: {said}", provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NamingTheShipAiAfterAHailGoesToTheShipAiAndClosesTheLine()
    {
        var provider = FakeLlmProvider.Answering("Fuel is at eighty percent.");
        var (loop, line, world) = Build(provider);
        Hail(line, world);

        var events = await RunAsync(loop, $"{ShipAi}, what's my fuel?");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.DoesNotContain(Hailer, provider.LastRequest?.Prompt.Persona ?? string.Empty, StringComparison.Ordinal);
        Assert.False(line.IsOpen);
    }

    [Fact]
    public async Task AfterTheWindowAnUnnamedReplyGoesToTheShipAi()
    {
        var provider = FakeLlmProvider.Answering("You're welcome.");
        var (loop, line, world) = Build(provider);
        Hail(line, world);

        world.Now += ChatterLine.Window + TimeSpan.FromSeconds(1);
        var events = await RunAsync(loop, "Thank you");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.False(line.IsOpen);
    }

    [Fact]
    public async Task AnUnnamedReplyAfterPassersbyGoesToTheShipAi()
    {
        var provider = FakeLlmProvider.Answering("You're welcome.");
        var (loop, line, world) = Build(provider);
        Passersby(line, world);

        var events = await RunAsync(loop, "Thank you");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
    }

    [Theory]
    [InlineData("Oh, Odalys Fenn. Thank you")]
    [InlineData("Hey Fenn, thanks")]
    public async Task ANameAfterAnInterjectionReachesItsSpeaker(string said)
    {
        var provider = FakeLlmProvider.Answering("No trouble.");
        var (loop, line, world) = Build(provider);
        Passersby(line, world);

        var events = await RunAsync(loop, said);

        Assert.Equal(Hailer, Assert.Single(events.OfType<TurnEvent.Addressed>()).Name);
        Assert.Contains($"Commander: {said}", provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AModelFreeCommandAfterAHailGoesToTheShipAndLeavesTheLineOpen()
    {
        var provider = FakeLlmProvider.Answering("Noted.");
        var (loop, line, world) = Build(provider);
        Hail(line, world);

        var events = await RunAsync(loop, ModelFreeCommand);

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.True(line.IsOpen);
    }

    [Fact]
    public void TheHeldSpeakersAreAmongTheNamesScribeReceives()
    {
        var (_, line, world) = Build(FakeLlmProvider.Answering("Noted."));
        Hail(line, world);

        var nouns = ProperNouns.From(Somewhere(), shipAi: [ShipAi, "D47"], speakers: line.Speakers);

        Assert.Equal([ShipAi, "D47", Hailer], nouns.Take(3));
        Assert.Contains(Hailer, nouns.Take(MostKeyterms));
    }

    [Fact]
    public void NoSpeakerIsHeldOnceTheExchangeCannotBeAnswered()
    {
        var (_, line, world) = Build(FakeLlmProvider.Answering("Noted."));
        Hail(line, world);

        Assert.Equal([Hailer], line.Speakers);

        world.Now += ChatterLine.Window + TimeSpan.FromSeconds(1);

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

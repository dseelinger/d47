using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>"Vance, ..." after Vance was overheard hailing the Commander is answered by Vance, while the exchange lasts.</summary>
public class AnInventedSpeakerAnswersWhenNamedTests
{
    private const string ShipAi = "Warden";

    private const string ShipAiBrief = "You are the ship's AI aboard this vessel.";

    private const string Background = "claude-haiku-background";

    private sealed class World
    {
        public DateTimeOffset Now { get; set; } = new(3312, 5, 1, 12, 0, 0, TimeSpan.Zero);

        public string System { get; set; } = "Shinrarta Dezhra";

        public AmbientSituation Situation { get; set; } = AmbientSituation.Docked;
    }

    private static (TurnLoop Loop, ChatterLine Line, World World) Build(TestSurface surface, ILlmProvider provider)
    {
        var world = new World();

        var line = new ChatterLine(
            () => world.Now,
            () => world.System,
            () => world.Situation,
            () => ShipAi,
            model: () => Background,
            accentOf: voice => voice == "bf_emma" ? "British" : null);

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
        };

        loop.Lines.Add(line);

        return (loop, line, world);
    }

    private static void Hail(ChatterLine line, World world, int exchangeIndex = 7, string name = "Vance") =>
        line.Heard(
            new NpcChatterLine(name, "Nice ship, Commander. She fly as good as she looks?", VoiceId: "bf_emma"),
            NpcChatter.MayNotice(NpcChatterKind.Hail, exchangeIndex),
            exchangeIndex,
            world.System,
            world.Situation);

    private static async Task<List<TurnEvent>> RunAsync(TurnLoop loop, string input)
    {
        List<TurnEvent> events = [];

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
            events.Add(turnEvent);
        }

        return events;
    }

    private static TurnResult Result(List<TurnEvent> events) =>
        Assert.Single(events.OfType<TurnEvent.Completed>()).Result;

    [Trait("Category", "Integration")]
    [Fact]
    public async Task VanceByNameIsAnsweredByVanceAndSoIsTheNextUnnamedLine()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Like a dream, friend.");
        var (loop, line, world) = Build(TestSurface.For(install), provider);
        Hail(line, world);

        var events = await RunAsync(loop, "Vance, she's fine");

        var addressed = Assert.Single(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(VoiceRole.Comms, addressed.Role);
        Assert.Equal("Vance", addressed.Name);
        Assert.Equal("Like a dream, friend.", Result(events).Text);

        var request = provider.LastRequest!;
        Assert.Equal(Background, request.Model);
        Assert.Empty(request.Prompt.Tools);
        Assert.Contains("Vance: Nice ship, Commander.", request.Prompt.Persona, StringComparison.Ordinal);
        Assert.Contains("Commander: she's fine", request.Prompt.Persona, StringComparison.Ordinal);
        Assert.Contains("British accent", request.Prompt.Persona, StringComparison.Ordinal);
        Assert.True(line.IsOpen);

        var next = await RunAsync(loop, "where are you headed");

        Assert.Equal("Vance", Assert.Single(next.OfType<TurnEvent.Addressed>()).Name);
        Assert.Contains("Vance: Like a dream, friend.", provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheLastWordOfANameReachesItsSpeaker()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Aye.");
        var (loop, line, world) = Build(TestSurface.For(install), provider);
        Hail(line, world, name: "Dock hand Ressa");

        var events = await RunAsync(loop, "Ressa, thanks for the fuel");

        Assert.Equal("Dock hand Ressa", Assert.Single(events.OfType<TurnEvent.Addressed>()).Name);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task NamingTheShipAiGoesToTheShipAiAndClosesTheLine()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Noted.");
        var (loop, line, world) = Build(TestSurface.For(install), provider);
        Hail(line, world);

        await RunAsync(loop, "Vance, she's fine");
        var events = await RunAsync(loop, $"{ShipAi}, who was that");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.Contains(ShipAiBrief, provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
        Assert.False(line.IsOpen);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task NinetyOneSecondsAfterTheLastLineVanceIsOffTheChannel()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Hey.");
        var (loop, line, world) = Build(TestSurface.For(install), provider);
        Hail(line, world);

        world.Now += TimeSpan.FromSeconds(91);
        var events = await RunAsync(loop, "Vance, she's fine");

        AssertOffTheChannel(events, provider);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ASystemChangeShutsTheExchange()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Hey.");
        var (loop, line, world) = Build(TestSurface.For(install), provider);
        Hail(line, world);

        world.System = "Sol";
        var events = await RunAsync(loop, "Vance, she's fine");

        AssertOffTheChannel(events, provider);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ASituationChangeShutsTheExchange()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Hey.");
        var (loop, line, world) = Build(TestSurface.For(install), provider);
        Hail(line, world);

        world.Situation = AmbientSituation.Supercruise;
        var events = await RunAsync(loop, "Vance, she's fine");

        AssertOffTheChannel(events, provider);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheNextExchangeReplacesTheOneHeld()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Hey.");
        var (loop, line, world) = Build(TestSurface.For(install), provider);
        Hail(line, world, exchangeIndex: 7);
        Hail(line, world, exchangeIndex: 8, name: "Ilo");

        var events = await RunAsync(loop, "Vance, she's fine");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.Contains(ShipAiBrief, provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheFifthAddressIsRefused()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Sure thing.");
        var (loop, line, world) = Build(TestSurface.For(install), provider);
        Hail(line, world);

        for (var reply = 1; reply <= ChatterLine.MostReplies; reply++)
        {
            var events = await RunAsync(loop, "Vance, tell me more");

            Assert.Single(events.OfType<TurnEvent.Addressed>());

            if (reply == ChatterLine.MostReplies)
            {
                Assert.Contains("last reply", provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
            }
        }

        var calls = provider.CallCount;
        var fifth = await RunAsync(loop, "Vance, tell me more");

        Assert.Equal("Vance is off the channel.", Result(fifth).Text);
        Assert.Equal(TurnRoute.ChatterClosed, Result(fifth).Route);
        Assert.Equal(calls, provider.CallCount);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AReplyThatEscalatesIsReplacedByTheSignOffAndShutsTheExchange()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Keep talking and I'll interdict you myself.");
        var (loop, line, world) = Build(TestSurface.For(install), provider);
        Hail(line, world);

        var events = await RunAsync(loop, "Vance, she's fine");

        Assert.Equal("Got to go. Vance out.", Result(events).Text);
        Assert.Equal(
            "Got to go. Vance out.",
            string.Concat(events.OfType<TurnEvent.TextDelta>().Select(delta => delta.Text)));
        Assert.False(line.IsOpen);

        var calls = provider.CallCount;
        var after = await RunAsync(loop, "Vance, wait");

        Assert.Equal("Vance is off the channel.", Result(after).Text);
        Assert.Equal(calls, provider.CallCount);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AControllerExchangeIsNeverTaken()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Noted.");
        var (loop, line, world) = Build(TestSurface.For(install), provider);

        line.Heard(
            new NpcChatterLine("Vance", "Requesting pad four."),
            NpcChatter.MayNotice(NpcChatterKind.Controller, 3),
            3,
            world.System,
            world.Situation);

        var events = await RunAsync(loop, "Vance, she's fine");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task PassersbyThatMayNotNoticeTheCommanderAreNeverTaken()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Noted.");
        var (loop, line, world) = Build(TestSurface.For(install), provider);

        var unnoticing = Enumerable.Range(0, 20).First(index => !NpcChatter.MayNotice(NpcChatterKind.Passersby, index));

        line.Heard(
            new NpcChatterLine("Vance", "Cargo's late again."),
            NpcChatter.MayNotice(NpcChatterKind.Passersby, unnoticing),
            unnoticing,
            world.System,
            world.Situation);

        var events = await RunAsync(loop, "Vance, she's fine");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
    }

    [Fact]
    public void SomePassersbyMayNoticeTheCommanderAndEveryHailDoes()
    {
        Assert.Contains(Enumerable.Range(0, 20), index => NpcChatter.MayNotice(NpcChatterKind.Passersby, index));
        Assert.All(Enumerable.Range(0, 20), index => Assert.True(NpcChatter.MayNotice(NpcChatterKind.Hail, index)));
    }

    private static void AssertOffTheChannel(List<TurnEvent> events, FakeLlmProvider provider)
    {
        var result = Result(events);

        Assert.Equal("Vance is off the channel.", result.Text);
        Assert.Equal(TurnRoute.ChatterClosed, result.Route);
        Assert.Equal(0, provider.CallCount);
    }
}

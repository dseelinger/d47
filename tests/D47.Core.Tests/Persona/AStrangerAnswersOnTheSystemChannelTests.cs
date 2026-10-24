using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>An invented stranger on the roster answers every comms turn while the SYSTEM channel selects them, for as long as they stay.</summary>
public class AStrangerAnswersOnTheSystemChannelTests
{
    private const string ShipAi = "Warden";

    private const string ShipAiBrief = "You are the ship's AI aboard this vessel.";

    private const string Background = "claude-haiku-background";

    private static readonly DateTimeOffset At = new(3312, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private sealed class World
    {
        public string System { get; set; } = "Shinrarta Dezhra";

        public NpcsHeardHere Roster { get; } = new();

        public ManualTurnClock Clock { get; } = new();
    }

    private static (TurnLoop Loop, ChatterLine Line, World World) Build(ILlmProvider provider, string selected = "Vance")
    {
        var world = new World();
        var surface = TestSurface.For(new MemoryInstall());

        var line = new ChatterLine(
            () => world.System,
            model: () => Background,
            accentOf: voice => voice == "bf_emma" ? "British" : null,
            roster: () => world.Roster);

        var loop = new TurnLoop(
            surface.Registry,
            surface.Router,
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider,
            clock: world.Clock)
        {
            Persona = ShipAiBrief,
            ShipAiNames = () => [ShipAi],
            CommsSelected = () => OnComms.System(selected),
        };

        loop.Lines.Add(line);

        return (loop, line, world);
    }

    /// <summary>A line spoken by an invented speaker, as the Announcer hands it to the roster and the line.</summary>
    private static void Heard(ChatterLine line, World world, string name, string text, int exchangeIndex = 7, string? voice = "bf_emma")
    {
        var said = new NpcChatterLine(name, text, VoiceId: voice);

        world.Roster.Heard(
            new Announcement(NpcChatter.LineKey, text) { Speaker = name, Invented = new NpcChatterHeard(said, exchangeIndex) },
            voice,
            At);
        line.Heard(said, exchangeIndex);
    }

    private static void Hail(ChatterLine line, World world) =>
        Heard(line, world, "Vance", "Nice ship, Commander. She fly as good as she looks?");

    private static TurnResult Result(List<TurnEvent> events) =>
        Assert.Single(events.OfType<TurnEvent.Completed>()).Result;

    [Fact]
    public async Task VanceAnswersEveryCommsTurnWhileSelected()
    {
        var provider = FakeLlmProvider.Answering("Like a dream, friend.");
        var (loop, line, world) = Build(provider);
        Hail(line, world);

        var events = await OnComms.SayAsync(loop, "she's fine");

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

        var next = await OnComms.SayAsync(loop, "where are you headed");

        Assert.Equal("Vance", Assert.Single(next.OfType<TurnEvent.Addressed>()).Name);
        Assert.Contains("Vance: Like a dream, friend.", provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APrimaryTurnNamingVanceReachesTheShipAi()
    {
        var provider = FakeLlmProvider.Answering("Noted.");
        var (loop, line, world) = Build(provider);
        Hail(line, world);

        var events = await OnComms.AskAsync(loop, "Vance, she's fine");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.Contains(ShipAiBrief, provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AReplyTwoMinutesAfterTheLastLineIsAnswered()
    {
        var provider = FakeLlmProvider.Answering("Still here.");
        var (loop, line, world) = Build(provider);
        Hail(line, world);

        world.Clock.Advance(TimeSpan.FromMinutes(2));
        var events = await OnComms.SayAsync(loop, "she's fine");

        Assert.Equal("Vance", Assert.Single(events.OfType<TurnEvent.Addressed>()).Name);
    }

    [Fact]
    public async Task AFifthReplyIsAnswered()
    {
        var provider = FakeLlmProvider.Answering("Sure thing.");
        var (loop, line, world) = Build(provider);
        Hail(line, world);

        for (var reply = 1; reply <= 5; reply++)
        {
            Assert.Single((await OnComms.SayAsync(loop, "tell me more")).OfType<TurnEvent.Addressed>());
        }

        Assert.Equal(5, provider.CallCount);
        Assert.DoesNotContain("last reply", provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADismissalDoesNotTakeThemOffTheChannel()
    {
        var provider = FakeLlmProvider.Answering("Fair enough.");
        var (loop, line, world) = Build(provider);
        Hail(line, world);

        await OnComms.SayAsync(loop, "that's all, Vance");

        Assert.Single((await OnComms.SayAsync(loop, "one more thing")).OfType<TurnEvent.Addressed>());
    }

    [Fact]
    public async Task AStrangerKeepsTheirOwnExchangeWhenAnotherStarts()
    {
        var provider = FakeLlmProvider.Answering("Hey.");
        var (loop, line, world) = Build(provider);
        Hail(line, world);
        Heard(line, world, "Ilo", "Anyone seen my cargo?", exchangeIndex: 8);

        var events = await OnComms.SayAsync(loop, "she's fine");

        Assert.Equal("Vance", Assert.Single(events.OfType<TurnEvent.Addressed>()).Name);
        Assert.Contains("Vance: Nice ship", provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
        Assert.DoesNotContain("Ilo:", provider.LastRequest.Prompt.Persona, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APasserByWhoNeverNoticedTheCommanderAnswersWhenSelected()
    {
        var provider = FakeLlmProvider.Answering("Who's asking?");
        var (loop, line, world) = Build(provider);
        Heard(line, world, "Vance", "Cargo's late again.", exchangeIndex: 3);
        Heard(line, world, "Brannock", "It's always late.", exchangeIndex: 3);

        var events = await OnComms.SayAsync(loop, "late for whom?");

        Assert.Equal("Vance", Assert.Single(events.OfType<TurnEvent.Addressed>()).Name);
        Assert.Contains("Brannock: It's always late.", provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AfterAJumpNoOneIsThere()
    {
        var provider = FakeLlmProvider.Answering("Hey.");
        var (loop, line, world) = Build(provider);
        Hail(line, world);

        world.System = "Sol";
        world.Roster.EnteredSystem();
        var events = await OnComms.SayAsync(loop, "she's fine");

        Assert.Equal("No one on System.", Result(events).Text);
        Assert.Equal(TurnRoute.NoOneOnComms, Result(events).Route);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task AReplyThatEscalatesIsReplacedByTheSignOffAndTakesThemOffTheRoster()
    {
        var provider = FakeLlmProvider.Answering("Keep talking and I'll interdict you myself.");
        var (loop, line, world) = Build(provider);
        Hail(line, world);

        var events = await OnComms.SayAsync(loop, "she's fine");

        Assert.Equal("Got to go. Vance out.", Result(events).Text);
        Assert.Equal(
            "Got to go. Vance out.",
            string.Concat(events.OfType<TurnEvent.TextDelta>().Select(delta => delta.Text)));
        Assert.Null(world.Roster.Find("Vance"));

        var calls = provider.CallCount;
        var after = await OnComms.SayAsync(loop, "wait");

        Assert.Equal("No one on System.", Result(after).Text);
        Assert.Equal(calls, provider.CallCount);
    }

    [Fact]
    public async Task AStrangerWhoSignsOffLeavesTheRoster()
    {
        var provider = FakeLlmProvider.Answering("Fair skies, Commander. Got to go. Vance out.");
        var (loop, line, world) = Build(provider);
        Hail(line, world);

        var events = await OnComms.SayAsync(loop, "safe travels");

        Assert.Equal("Fair skies, Commander. Got to go. Vance out.", Result(events).Text);
        Assert.Contains("end your reply with exactly: Got to go. Vance out.", provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
        Assert.Null(world.Roster.Find("Vance"));
    }

    [Fact]
    public async Task ACarrierPostHeardInChatterIsNotAStranger()
    {
        var provider = FakeLlmProvider.Answering("Hey.");
        var (loop, line, world) = Build(provider, selected: NpcChatter.TowerName);
        var said = new NpcChatterLine(NpcChatter.TowerName, "Pad two is yours.", VoiceRole.TowerControl);

        world.Roster.Heard(
            new Announcement(NpcChatter.LineKey, said.Text) { Speaker = said.Name, Invented = new NpcChatterHeard(said, 7) },
            null,
            At);
        line.Heard(said, 7);

        Assert.NotNull(world.Roster.Find(NpcChatter.TowerName));
        Assert.Equal("No one on System.", Result(await OnComms.SayAsync(loop, "request pad")).Text);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task SomeoneNeitherOnTheRosterNorHeardIsNoOne()
    {
        var provider = FakeLlmProvider.Answering("Hey.");
        var (loop, line, world) = Build(provider, selected: "Nobody");
        Hail(line, world);

        Assert.Equal("No one on System.", Result(await OnComms.SayAsync(loop, "hello")).Text);
        Assert.Equal(0, provider.CallCount);
    }
}

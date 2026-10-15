using D47.Core.Adventures;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>What the Commander says within ninety seconds of a cast member's line is answered by that member.</summary>
public class TheCommanderCanAnswerACastMemberTests
{
    private const string ShipAi = "Warden";

    private const string ShipAiBrief = "You are the ship's AI aboard this vessel.";

    private const string Hidden = "The beacon belongs to the Lantern Society.";

    private const string Line = "The beacon went quiet the day my brother left.";

    private static readonly AdventureSpeaker Mara = new("mara", "Mara Quell", "A salvager who lost her brother to the beacon.");

    private sealed class World
    {
        public DateTimeOffset Now { get; set; } = new(3312, 5, 1, 12, 0, 0, TimeSpan.Zero);

        /// <summary>Whether the story is running, switched on and with Odyssey.</summary>
        public bool Running { get; set; } = true;

        public ShipFacts Facts { get; set; } = ShipFacts.Unknown;
    }

    /// <summary>A line that is always open and takes every turn, as a crew line does once addressed.</summary>
    private sealed class OpenLine : ILine
    {
        public bool IsOpen => true;

        public Task<LineDecision> RouteAsync(string input, CancellationToken cancellationToken) =>
            Task.FromResult<LineDecision>(new LineDecision.Taken(
                new Speaker(VoiceRole.Crew, "Ava Ross", "You are a hired pilot.", [], OffersTools: false, Signal: 1),
                input));

        public void Close()
        {
        }
    }

    private static (TurnLoop Loop, CastLine Line, World World) Build(ILlmProvider provider, ILine? before = null)
    {
        var install = new MemoryInstall();
        var surface = TestSurface.For(install);
        var router = surface.Router;
        var world = new World();

        var line = new CastLine(
            () => world.Now,
            input => router.MatchSetting(input) is not null
                     || router.MatchToolCommand(input) is not null
                     || router.Match(input, InputSource.Spoken) is not null
                     || router.Book.Candidates(input, InputSource.Spoken).Count > 0,
            castId => world.Running && castId == Mara.Id ? Mara : null,
            () => Hidden,
            () => world.Facts);

        var loop = new TurnLoop(
            surface.Registry,
            router,
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider)
        {
            Persona = ShipAiBrief,
            ShipAiNames = () => [ShipAi],
            HiddenStory = () => "The ship AI's own reading.",
            WebSearchEnabled = () => true,
        };

        if (before is not null)
        {
            loop.Lines.Add(before);
        }

        loop.Lines.Add(line);

        return (loop, line, world);
    }

    private static void Speaks(TurnLoop loop) => loop.LineHeard<CastLine>(line => line.Heard(Mara.Id, Line));

    private static async Task<List<TurnEvent>> RunAsync(TurnLoop loop, string input)
    {
        List<TurnEvent> events = [];

        await foreach (var turnEvent in loop.RunAsync(input, InputSource.Spoken, TestContext.Current.CancellationToken))
        {
            events.Add(turnEvent);
        }

        return events;
    }

    [Fact]
    public async Task TheMemberAnswersInTheFirstPersonWithNoTools()
    {
        const string reply = "I never asked you to find him, Commander. Only to listen.";
        var provider = FakeLlmProvider.Answering(reply);
        var (loop, _, _) = Build(provider);
        Speaks(loop);

        var events = await RunAsync(loop, "what happened to your brother?");

        var addressed = Assert.Single(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(VoiceRole.Crew, addressed.Role);
        Assert.Equal(Mara.Name, addressed.Name);
        Assert.Equal(Mara.Id, addressed.Cast);
        Assert.Equal(reply, Assert.Single(events.OfType<TurnEvent.Completed>()).Result.Text);

        var prompt = provider.LastRequest!.Prompt;
        Assert.Empty(prompt.Tools);
        Assert.False(prompt.ToolsSearchable);
        Assert.False(provider.LastRequest.WebSearch);
        Assert.DoesNotContain(ShipAiBrief, prompt.Persona, StringComparison.Ordinal);
        Assert.Contains($"You are {Mara.Name}", prompt.Persona, StringComparison.Ordinal);
        Assert.Contains(Mara.Who, prompt.Persona, StringComparison.Ordinal);
        Assert.Contains("first person", prompt.Persona, StringComparison.Ordinal);
        Assert.Contains($"{Mara.Name}: {Line}", prompt.Persona, StringComparison.Ordinal);
        Assert.Contains("Commander: what happened to your brother?", prompt.Persona, StringComparison.Ordinal);
        Assert.Equal(Hidden, prompt.HiddenStory);
    }

    [Fact]
    public async Task ATurnOpeningWithTheShipAiNameGoesToTheShip()
    {
        var provider = FakeLlmProvider.Answering("Standing by.");
        var (loop, line, _) = Build(provider);
        Speaks(loop);

        Assert.Empty((await RunAsync(loop, $"{ShipAi}, what is she talking about?")).OfType<TurnEvent.Addressed>());
        Assert.Contains(ShipAiBrief, provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
        Assert.False(line.IsOpen);
    }

    [Fact]
    public async Task ACommandWithAModelFreeRouteGoesToTheShip()
    {
        var provider = FakeLlmProvider.Answering("Standing by.");
        var (loop, line, _) = Build(provider);
        Speaks(loop);

        Assert.Empty((await RunAsync(loop, "what can you do")).OfType<TurnEvent.Addressed>());
        Assert.True(line.IsOpen);
    }

    [Fact]
    public async Task FourRepliesAndTheFifthGoesToTheShip()
    {
        var provider = FakeLlmProvider.Answering("The beacon kept its own counsel.");
        var (loop, line, _) = Build(provider);
        Speaks(loop);

        for (var reply = 1; reply <= CastLine.MostReplies; reply++)
        {
            Assert.Single((await RunAsync(loop, "and then?")).OfType<TurnEvent.Addressed>());
        }

        Assert.Contains("last reply", provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
        Assert.False(line.IsOpen);
        Assert.Empty((await RunAsync(loop, "and then?")).OfType<TurnEvent.Addressed>());
        Assert.Contains(ShipAiBrief, provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HerNextLineOpensTheWindowAgain()
    {
        var provider = FakeLlmProvider.Answering("The beacon kept its own counsel.");
        var (loop, line, _) = Build(provider);
        Speaks(loop);

        for (var reply = 1; reply <= CastLine.MostReplies; reply++)
        {
            await RunAsync(loop, "and then?");
        }

        Speaks(loop);

        Assert.True(line.IsOpen);
        Assert.Single((await RunAsync(loop, "and then?")).OfType<TurnEvent.Addressed>());
    }

    [Fact]
    public async Task EachReplyRestartsTheNinetySeconds()
    {
        var provider = FakeLlmProvider.Answering("The beacon kept its own counsel.");
        var (loop, line, world) = Build(provider);
        Speaks(loop);

        world.Now += TimeSpan.FromSeconds(80);
        await RunAsync(loop, "and then?");
        world.Now += TimeSpan.FromSeconds(80);

        Assert.True(line.IsOpen);

        world.Now += CastLine.Window;

        Assert.False(line.IsOpen);
        Assert.Empty((await RunAsync(loop, "and then?")).OfType<TurnEvent.Addressed>());
    }

    [Fact]
    public async Task AReplyThatFailsTheClaimsCheckIsNotSaidAndShutsTheWindow()
    {
        var provider = FakeLlmProvider.Answering("We'll top up on the fuel scoop.");
        var (loop, line, world) = Build(provider);
        world.Facts = new ShipFacts { ScoopFitted = false };
        Speaks(loop);

        var events = await RunAsync(loop, "where do we refuel?");

        Assert.Equal(string.Empty, Assert.Single(events.OfType<TurnEvent.Completed>()).Result.Text);
        Assert.Empty(events.OfType<TurnEvent.TextDelta>());
        Assert.False(line.IsOpen);
    }

    [Fact]
    public void NoWindowOpensWhileTheStoryIsPausedOrOff()
    {
        var (loop, line, world) = Build(FakeLlmProvider.Answering("Unheard."));
        world.Running = false;

        Speaks(loop);

        Assert.False(line.IsOpen);
    }

    [Fact]
    public async Task AStoryPausedInsideTheWindowShutsIt()
    {
        var provider = FakeLlmProvider.Answering("Standing by.");
        var (loop, line, world) = Build(provider);
        Speaks(loop);

        world.Running = false;

        Assert.False(line.IsOpen);
        Assert.Empty((await RunAsync(loop, "what happened to your brother?")).OfType<TurnEvent.Addressed>());
    }

    [Fact]
    public async Task HerLineAfterAnOpenCrewLineTakesTheReply()
    {
        var (loop, _, _) = Build(FakeLlmProvider.Answering("Only to listen."), before: new OpenLine());
        Speaks(loop);

        var addressed = Assert.Single((await RunAsync(loop, "what happened to your brother?")).OfType<TurnEvent.Addressed>());

        Assert.Equal(Mara.Name, addressed.Name);
    }

    [Fact]
    public void TheStoryLogPutsTheCommandersWordsAboveTheAnswer() =>
        Assert.Equal("You: where is he?\n\nGone where the beacon sent him.", CastLine.Logged(" where is he? ", "Gone where the beacon sent him."));
}

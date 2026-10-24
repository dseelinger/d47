using D47.Core.Adventures;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>What the Commander says on comms while the STORY channel selects a cast member is answered by that member.</summary>
public class ACastMemberAnswersOnTheStoryChannelTests
{
    private const string ShipAi = "Warden";

    private const string ShipAiBrief = "You are the ship's AI aboard this vessel.";

    private const string Hidden = "The beacon belongs to the Lantern Society.";

    private const string Line = "The beacon went quiet the day my brother left.";

    private static readonly AdventureSpeaker Mara = new("mara", "Mara Quell", "A salvager who lost her brother to the beacon.");

    private sealed class World
    {
        /// <summary>Whether the story is running, switched on and with Odyssey.</summary>
        public bool Running { get; set; } = true;

        public ShipFacts Facts { get; set; } = ShipFacts.Unknown;

        public ManualTurnClock Clock { get; } = new();
    }

    private static (TurnLoop Loop, CastLine Line, World World) Build(ILlmProvider provider)
    {
        var install = new MemoryInstall();
        var surface = TestSurface.For(install);
        var world = new World();

        var line = new CastLine(
            castId => world.Running && castId == Mara.Id ? Mara : null,
            () => Hidden,
            () => world.Facts);

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
            HiddenStory = () => "The ship AI's own reading.",
            WebSearchEnabled = () => true,
            CommsSelected = () => OnComms.Cast(Mara.Id, Mara.Name),
        };

        loop.Lines.Add(line);

        return (loop, line, world);
    }

    private static void Speaks(TurnLoop loop) => loop.LineHeard<CastLine>(line => line.Heard(Mara.Id, Line));

    [Fact]
    public async Task TheMemberAnswersInTheFirstPersonWithNoTools()
    {
        const string reply = "I never asked you to find him, Commander. Only to listen.";
        var provider = FakeLlmProvider.Answering(reply);
        var (loop, _, _) = Build(provider);
        Speaks(loop);

        var events = await OnComms.SayAsync(loop, "what happened to your brother?");

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
    public async Task ThePrimaryKeyGoesToTheShipRightAfterHerLine()
    {
        var provider = FakeLlmProvider.Answering("Standing by.");
        var (loop, _, _) = Build(provider);
        Speaks(loop);

        Assert.Empty((await OnComms.AskAsync(loop, "what is she talking about?")).OfType<TurnEvent.Addressed>());
        Assert.Contains(ShipAiBrief, provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACommandOnCommsIsSaidToHer()
    {
        var provider = FakeLlmProvider.Answering("I can't help you with that.");
        var (loop, _, _) = Build(provider);
        Speaks(loop);

        Assert.Equal(Mara.Name, Assert.Single((await OnComms.SayAsync(loop, "what can you do")).OfType<TurnEvent.Addressed>()).Name);
    }

    [Fact]
    public async Task AFifthReplyIsAnswered()
    {
        var provider = FakeLlmProvider.Answering("The beacon kept its own counsel.");
        var (loop, _, _) = Build(provider);
        Speaks(loop);

        for (var reply = 1; reply <= 5; reply++)
        {
            Assert.Single((await OnComms.SayAsync(loop, "and then?")).OfType<TurnEvent.Addressed>());
        }

        Assert.DoesNotContain("last reply", provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AReplyTwoMinutesAfterHerLineIsAnswered()
    {
        var provider = FakeLlmProvider.Answering("The beacon kept its own counsel.");
        var (loop, _, world) = Build(provider);
        Speaks(loop);

        world.Clock.Advance(TimeSpan.FromMinutes(2));

        Assert.Single((await OnComms.SayAsync(loop, "and then?")).OfType<TurnEvent.Addressed>());
    }

    [Fact]
    public async Task HerNextLineStartsTheExchangeAfresh()
    {
        var provider = FakeLlmProvider.Answering("The beacon kept its own counsel.");
        var (loop, _, _) = Build(provider);
        Speaks(loop);

        await OnComms.SayAsync(loop, "and before that?");

        Speaks(loop);
        await OnComms.SayAsync(loop, "and then?");

        Assert.DoesNotContain("and before that?", provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
        Assert.Contains("Commander: and then?", provider.LastRequest.Prompt.Persona, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AReplyThatFailsTheClaimsCheckIsNotSaid()
    {
        var provider = FakeLlmProvider.Answering("We'll top up on the fuel scoop.");
        var (loop, _, world) = Build(provider);
        world.Facts = new ShipFacts { ScoopFitted = false };
        Speaks(loop);

        var events = await OnComms.SayAsync(loop, "where do we refuel?");

        Assert.Equal(string.Empty, Assert.Single(events.OfType<TurnEvent.Completed>()).Result.Text);
        Assert.Empty(events.OfType<TurnEvent.TextDelta>());
    }

    [Fact]
    public async Task WhileTheStoryIsPausedOrOffNoOneIsOnTheChannel()
    {
        var provider = FakeLlmProvider.Answering("Standing by.");
        var (loop, _, world) = Build(provider);
        Speaks(loop);

        world.Running = false;

        var events = await OnComms.SayAsync(loop, "what happened to your brother?");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.Equal("No one on Story.", Assert.Single(events.OfType<TurnEvent.Completed>()).Result.Text);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public void TheStoryLogPutsTheCommandersWordsAboveTheAnswer() =>
        Assert.Equal("You: where is he?\n\nGone where the beacon sent him.", CastLine.Logged(" where is he? ", "Gone where the beacon sent him."));
}

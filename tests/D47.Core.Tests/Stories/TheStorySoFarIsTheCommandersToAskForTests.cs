using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>
/// "where am I up to" and its two siblings reach the recap, which the Narrator answers; the model is refused it, and with no
/// story under way the answer is that no story is.
/// </summary>
public sealed class TheStorySoFarIsTheCommandersToAskForTests
{
    private const string Recap = "Commander Test followed a song to a Guardian beacon, and the trail now leads on.";

    private static readonly string[] Phrases =
    [
        "where am I up to",
        "Where am I up to?",
        "recap the story",
        "what's happened in the story so far",
        "what’s happened in the story so far",
    ];

    private static CapabilityRegistry Registry(List<string> asked, string? recap = Recap) =>
        CapabilityRegistry.Build([AdventureCapability.Create(recapper: new AdventureCapability.StoryRecapper
        {
            Write = _ =>
            {
                asked.Add("recap");
                return Task.FromResult(recap);
            },
        })]);

    private static async Task<List<TurnEvent>> Turn(CapabilityRegistry registry, KeywordRouter router, string said)
    {
        var loop = new TurnLoop(
            registry,
            router,
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            FakeLlmProvider.Answering("The model answered."));

        List<TurnEvent> events = [];

        await foreach (var turnEvent in loop.RunAsync(said, cancellationToken: TestContext.Current.CancellationToken))
        {
            events.Add(turnEvent);
        }

        return events;
    }

    [Fact]
    public void EachPhraseReachesTheRecapAheadOfTheLocation()
    {
        var surface = TestSurface.For(new MemoryInstall());

        foreach (var said in Phrases)
        {
            Assert.Equal(AdventureCapability.RecapTool, surface.Router.MatchToolCommand(said)?.ToolName);
        }
    }

    [Fact]
    public async Task TheNarratorAnswersWithTheRecap()
    {
        List<string> asked = [];
        var registry = Registry(asked);

        var events = await Turn(registry, new KeywordRouter(registry), "where am I up to");

        var addressed = Assert.Single(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(VoiceRole.Narrator, addressed.Role);
        Assert.Equal(Recap, string.Concat(events.OfType<TurnEvent.TextDelta>().Select(delta => delta.Text)));
        Assert.Single(asked);
    }

    [Fact]
    public async Task WithNoStoryUnderWayTheShipSaysSoAndNothingElse()
    {
        var surface = TestSurface.For(new MemoryInstall());

        var events = await Turn(surface.Registry, surface.Router, "where am I up to");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(StoryRecap.NoStory, string.Concat(events.OfType<TurnEvent.TextDelta>().Select(delta => delta.Text)));
    }

    [Fact]
    public async Task TheModelIsRefusedTheRecap()
    {
        List<string> asked = [];

        var result = await Registry(asked).InvokeAsync(
            AdventureCapability.RecapTool, ToolArguments.Empty, caller: ToolCaller.Model, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Empty(asked);
    }

    [Fact]
    public async Task TheCommanderHearsItFromTheNarrator()
    {
        List<string> asked = [];

        var result = await Registry(asked).InvokeAsync(
            AdventureCapability.RecapTool, ToolArguments.Empty, caller: ToolCaller.Commander, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(Recap, result.Spoken);
        Assert.Equal(VoiceRole.Narrator, result.SpokenBy?.Role);
    }

    [Fact]
    public async Task WithNoStoryTheCommanderIsToldSo()
    {
        List<string> asked = [];

        var result = await Registry(asked, recap: null).InvokeAsync(
            AdventureCapability.RecapTool, ToolArguments.Empty, caller: ToolCaller.Commander, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal(StoryRecap.NoStory, result.Spoken);
        Assert.Null(result.SpokenBy);
    }
}

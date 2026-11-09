using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Stories;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.Core.Tests.Adventures;

public sealed class StoryProgressTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private static StoryProgress Started(ManualTime time, string model, WriteStage stage)
    {
        var progress = new StoryProgress(time);
        progress.Enter(stage, StoryPace.For(model, chapter: true));
        return progress;
    }

    [Fact]
    public void TheBarNeverGoesBackwardsThroughADraftAndARewrite()
    {
        var time = new ManualTime();
        var progress = new StoryProgress(time);
        var pace = StoryPace.For("claude-sonnet-5-5", chapter: true);
        var last = 0.0;

        void Check()
        {
            Assert.True(progress.Fill >= last, $"{progress.Fill} fell below {last}");
            last = progress.Fill;
        }

        foreach (var stage in new[] { WriteStage.Places, WriteStage.Objectives, WriteStage.Checking })
        {
            progress.Enter(stage, pace);
            Check();
            time.Advance(TimeSpan.FromSeconds(7));
            progress.Received(300);
            Check();
        }

        time.Advance(TimeSpan.FromSeconds(30));
        Check();
        progress.NextDraft();
        Check();

        foreach (var stage in new[] { WriteStage.Places, WriteStage.Objectives, WriteStage.Checking })
        {
            progress.Enter(stage, pace);
            Check();
            time.Advance(TimeSpan.FromSeconds(9));
            Check();
        }
    }

    [Fact]
    public void TheBarIsNotFullUntilTheStoryIsDone()
    {
        var time = new ManualTime();
        var progress = Started(time, "claude-opus-5-5", WriteStage.Checking);

        time.Advance(TimeSpan.FromMinutes(10));
        progress.Received(1_000_000);

        Assert.InRange(progress.Fill, 0.9, 0.99);

        progress.Done();

        Assert.Equal(1, progress.Fill);
    }

    [Fact]
    public void OutputArrivingMovesTheBarWithTheClockHeldStill()
    {
        var progress = Started(new ManualTime(), "claude-opus-5-5", WriteStage.Places);

        foreach (var characters in new[] { 1, 40, 900, 5000 })
        {
            var before = progress.Fill;
            progress.Received(characters);

            Assert.True(progress.Fill > before);
        }
    }

    [Fact]
    public void ASlowerModelIsLessFullAfterTheSameTime()
    {
        var time = new ManualTime();
        var opus = Started(time, "claude-opus-5-5", WriteStage.Objectives);
        var sol = Started(time, "gpt-6.1-sol", WriteStage.Objectives);

        time.Advance(TimeSpan.FromSeconds(10));

        Assert.True(opus.Fill < sol.Fill);
    }

    [Fact]
    public void TheTotalForAModelWithACatalogStoryEffortIsAMeasuredChapterTime()
    {
        Assert.Equal(83, StoryPace.For("claude-sonnet-5-5", chapter: true).Total);
        Assert.Equal(40, StoryPace.For("gpt-6.1-sol", chapter: true).Total);
        Assert.Equal(136, StoryPace.For("claude-opus-5-5", chapter: true).Total);
        Assert.Equal(198, StoryPace.For("gpt-5.6-terra", chapter: true).Total);
    }

    [Fact]
    public void TheTotalForAModelWithACatalogStoryEffortIsAMeasuredShortStoryTime()
    {
        Assert.Equal(79, StoryPace.For("claude-sonnet-5-5").Total);
        Assert.Equal(76, StoryPace.For("gpt-6.1-sol").Total);
        Assert.Equal(64, StoryPace.For("claude-opus-5-5").Total);
        Assert.Equal(162, StoryPace.For("gpt-5.6-terra").Total);
    }

    [Theory]
    [InlineData("gpt-6-luna")]
    [InlineData("claude-haiku-5-5")]
    public void AModelThatCannotWriteStoriesGetsTheOtherRow(string model)
    {
        Assert.Equal(StoryPace.For("some-new-model", chapter: true), StoryPace.For(model, chapter: true));
    }

    [Fact]
    public void EnteringObjectivesStraightAfterPlacesLeavesNoJump()
    {
        var progress = Started(new ManualTime(), "claude-sonnet-5-5", WriteStage.Places);
        var before = progress.Fill;

        progress.Enter(WriteStage.Objectives, StoryPace.For("claude-sonnet-5-5", chapter: true));

        Assert.InRange(progress.Fill - before, 0, 0.05);
    }

    [Fact]
    public void AModelNotInTheTableUsesTheLastRow()
    {
        Assert.Equal(new StoryPace(5, 49, 52), StoryPace.For("some-new-model", chapter: true));
        Assert.Equal(new StoryPace(12, 38, 20), StoryPace.For("some-new-model"));
        Assert.Equal(StoryPace.For("some-new-model", chapter: true), StoryPace.For(null, chapter: true));
    }

    [Fact]
    public void AModelNotInTheTableGetsTheLocalRowOnThisMachine()
    {
        Assert.Equal(new StoryPace(5, 46, 24), StoryPace.For("some-new-model", local: true));
        Assert.Equal(new StoryPace(5, 82, 43), StoryPace.For("some-new-model", local: true, chapter: true));
        Assert.Equal(new StoryPace(5, 49, 52), StoryPace.For("some-new-model", chapter: true));
        Assert.Equal(StoryPace.For("claude-sonnet-5-5"), StoryPace.For("claude-sonnet-5-5", local: true));
        Assert.Equal(StoryPace.For("claude-sonnet-5-5", chapter: true), StoryPace.For("claude-sonnet-5-5", local: true, chapter: true));
    }

    [Theory]
    [InlineData(false, 75, "1:15")]
    [InlineData(true, 130, "2:10")]
    public void ALocalHintNamesTheLocalTimeThenSaysItIsTakingLonger(bool chapter, int seconds, string clock)
    {
        var time = new ManualTime();
        var progress = new StoryProgress(time);
        progress.Enter(WriteStage.Places, StoryPace.For("gemma-4-e4b", local: true, chapter: chapter));

        Assert.Equal($"Usually ready in about {clock}.", progress.Hint);

        time.Advance(TimeSpan.FromSeconds(seconds * 2));
        Assert.Equal($"Usually ready in about {clock}.", progress.Hint);

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("Taking longer than usual.", progress.Hint);
    }

    [Fact]
    public void TheHintNamesTheUsualTimeThenSaysItIsTakingLonger()
    {
        var time = new ManualTime();
        var progress = Started(time, "claude-sonnet-5-5", WriteStage.Places);

        Assert.Equal("Usually ready in about 1:25.", progress.Hint);

        time.Advance(TimeSpan.FromSeconds(166));
        Assert.Equal("Usually ready in about 1:25.", progress.Hint);

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("Taking longer than usual.", progress.Hint);
    }

    [Fact]
    public void AStoppedCallSaysSoUntilTheNextStage()
    {
        var progress = Started(new ManualTime(), "claude-sonnet-5-5", WriteStage.Places);

        progress.CallStopped();
        Assert.Equal("That took too long. Trying again…", progress.Hint);

        progress.Enter(WriteStage.Places, StoryPace.For("claude-sonnet-5-5", chapter: true));
        Assert.Equal("Usually ready in about 1:25.", progress.Hint);
    }

    [Fact]
    public async Task AShortStoryReportsItsStagesThenIsDone()
    {
        var progress = new StoryProgress(TimeProvider.System);
        var stages = new List<WriteStage>();
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(PlannedChapterWorld.ShortLines()), RoundScriptedLlmProvider.Saying(PlannedChapterWorld.NoObjections))
        {
            OnRequest = _ => stages.Add(progress.Stage),
        };

        var outcome = await PlannedChapterWorld.Generator(provider, PlannedChapterWorld.Roomy())
            .DraftAsync(new AdventureAsk(AdventureReach.Session, AdventureLength.Evening, Progress: progress), Now, null, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal([WriteStage.Objectives, WriteStage.Checking], stages.Distinct());
        Assert.Equal(1, progress.Fill);
    }

    [Fact]
    public async Task ACancelledShortStoryIsStoppedWithNoRefusalOrWarning()
    {
        using var cancel = new CancellationTokenSource();
        var log = new RecordingLogger();
        var state = PlannedChapterWorld.State();
        var galaxy = PlannedChapterWorld.Roomy();
        var generator = new AdventureGenerator(() => new CancellingLlmProvider(cancel), () => null, () => "You are the ship.", () => "core", () => null, () => state, () => galaxy, () => null, null, null, log);

        var outcome = await generator.DraftAsync(new AdventureAsk(AdventureReach.Session, AdventureLength.Evening), Now, null, cancel.Token);

        Assert.Same(AdventureOutcome.Stopped, outcome);
        Assert.Null(outcome.Refusal);
        Assert.DoesNotContain(
            log.Entries,
            entry => entry.Level >= LogLevel.Warning && !entry.Message.StartsWith("The galaxy search could not list the stations within reach of a story for the mission family", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EveryTextAndThinkingDeltaIsReportedWithItsLength()
    {
        var lengths = new List<int>();
        var provider = new FakeLlmProvider(
            new LlmStreamEvent.ThinkingDelta("Hmm."),
            new LlmStreamEvent.TextDelta("Hello there"),
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed));

        await FlavourTurn.AskForAsync(provider, null, null, null, "Say hi.", null, null, null, null, TestContext.Current.CancellationToken, received: lengths.Add);

        Assert.Equal([4, 11], lengths);
    }

    private sealed class CancellingLlmProvider(CancellationTokenSource cancel) : ILlmProvider
    {
        public string Id => "anthropic";

        public string DefaultModel => "claude-opus-5";

        public LlmProviderCapabilities CapabilitiesFor(string model) => new()
        {
            SupportsPromptCaching = false,
            SupportsThinkingEffort = true,
            SupportsOperatorSystemMessages = true,
            MinimumCacheablePrefixTokens = 0,
        };

        public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(
            LlmRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await cancel.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
            yield break;
        }
    }
}

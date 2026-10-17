using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.Core.Tests.Adventures;

public sealed class StoryProgressTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private static StoryProgress Started(ManualTime time, string model, WriteStage stage)
    {
        var progress = new StoryProgress(time);
        progress.Enter(stage, StoryPace.For(model));
        return progress;
    }

    [Fact]
    public void TheBarNeverGoesBackwardsThroughADraftAndARewrite()
    {
        var time = new ManualTime();
        var progress = new StoryProgress(time);
        var pace = StoryPace.For("claude-sonnet-5-5");
        var last = 0.0;

        void Check()
        {
            Assert.True(progress.Fill >= last, $"{progress.Fill} fell below {last}");
            last = progress.Fill;
        }

        foreach (var stage in new[] { WriteStage.Places, WriteStage.Outline, WriteStage.Objectives, WriteStage.Checking })
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

        foreach (var stage in new[] { WriteStage.Places, WriteStage.Outline, WriteStage.Objectives, WriteStage.Checking })
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
        var progress = Started(time, "claude-opus-5-5", WriteStage.Objectives);

        time.Advance(TimeSpan.FromMinutes(10));
        progress.Received(1_000_000);

        Assert.InRange(progress.Fill, 0.9, 0.99);

        progress.Done();

        Assert.Equal(1, progress.Fill);
    }

    [Fact]
    public void OutputArrivingMovesTheBarWithTheClockHeldStill()
    {
        var progress = Started(new ManualTime(), "claude-opus-5-5", WriteStage.Outline);

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
        var haiku = Started(time, "claude-haiku-5-5", WriteStage.Objectives);
        var terra = Started(time, "gpt-5.6-terra", WriteStage.Objectives);

        time.Advance(TimeSpan.FromSeconds(10));

        Assert.True(haiku.Fill < terra.Fill);
    }

    [Fact]
    public void AModelNotInTheTableUsesTheLastRow()
    {
        Assert.Equal(new StoryPace(5, 13, 22), StoryPace.For("some-new-model"));
        Assert.Equal(StoryPace.For("some-new-model"), StoryPace.For(null));
    }

    [Fact]
    public void TheHintNamesTheUsualTimeThenSaysItIsTakingLonger()
    {
        var time = new ManualTime();
        var progress = Started(time, "claude-haiku-5-5", WriteStage.Places);

        Assert.Equal("Usually ready in about 40 seconds.", progress.Hint);

        time.Advance(TimeSpan.FromSeconds(80));
        Assert.Equal("Usually ready in about 40 seconds.", progress.Hint);

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("Taking longer than usual.", progress.Hint);
    }

    [Fact]
    public void AStoppedCallSaysSoUntilTheNextStage()
    {
        var progress = Started(new ManualTime(), "claude-sonnet-5-5", WriteStage.Outline);

        progress.CallStopped();
        Assert.Equal("That took too long. Trying again…", progress.Hint);

        progress.Enter(WriteStage.Outline, StoryPace.For("claude-sonnet-5-5"));
        Assert.Equal("Usually ready in about 25 seconds.", progress.Hint);
    }

    [Fact]
    public async Task AShortStoryReportsItsFourStagesThenIsDone()
    {
        var progress = new StoryProgress(TimeProvider.System);
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(AdventureGeneratorTests.Spine), RoundScriptedLlmProvider.Saying(AdventureGeneratorTests.GoodBeats));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .DraftAsync(new AdventureAsk(Length: AdventureLength.Short, Progress: progress), Now, null, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal([WriteStage.Places, WriteStage.Outline, WriteStage.Objectives, WriteStage.Checking], progress.Visited);
        Assert.Equal(1, progress.Fill);
    }

    [Fact]
    public async Task ACancelledShortStoryIsStoppedWithNoRefusalOrWarning()
    {
        using var cancel = new CancellationTokenSource();
        var log = new RecordingLogger();

        var outcome = await AdventureGeneratorTests.Generator(new CancellingLlmProvider(cancel), new AdventureGeneratorTests.Galaxy(), log)
            .DraftAsync(new AdventureAsk(Length: AdventureLength.Short), Now, null, cancel.Token);

        Assert.Same(AdventureOutcome.Stopped, outcome);
        Assert.Null(outcome.Refusal);
        Assert.DoesNotContain(log.Entries, entry => entry.Level >= LogLevel.Warning);
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

        public string DisplayName => "Cancelling";

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

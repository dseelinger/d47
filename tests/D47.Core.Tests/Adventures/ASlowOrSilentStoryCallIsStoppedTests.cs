using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Stories;
using D47.Core.Tests.Stories;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>
/// A story's model call on a cloud provider is stopped after <see cref="AdventureGenerator.CallLimitFor"/> its output budget, or after
/// <see cref="AdventureGenerator.SilenceLimit"/> with no text or thinking, and counts as a failed draft. A call on this
/// machine is not stopped.
/// </summary>
public sealed class ASlowOrSilentStoryCallIsStoppedTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ACloudCallWithNothingArrivingIsStoppedAtNinetySeconds()
    {
        var time = new ManualTime();
        var provider = new ClockedLlmProvider(time, local: false, Stalls);
        var log = new RecordingLogger();

        var outcome = await Generate(provider, log, time);

        Assert.True(outcome.Redraftable);
        Assert.True(outcome.Unanswered);
        var warning = SingleWarning(log);
        Assert.Contains("lines step", warning);
        Assert.Contains("TimedOut: no first text or thinking arrived for 90 s; stopped after 90.0 s", warning);
    }

    [Fact]
    public async Task ACloudCallWhoseFirstTextArrivesAtSixtySecondsIsNotStopped()
    {
        var time = new ManualTime();
        var provider = new ClockedLlmProvider(
            time,
            local: false,
            [new(TimeSpan.FromSeconds(60), new LlmStreamEvent.TextDelta(PlannedChapterWorld.Lines(ChapterPlanner.MostObjectives))), new(TimeSpan.Zero, Completed)],
            Says(PlannedChapterWorld.NoObjections));

        var outcome = await Generate(provider, new RecordingLogger(), time);

        Assert.True(outcome.Succeeded, outcome.Refusal);
    }

    [Fact]
    public async Task ACloudCallSilentForSixtyOneSecondsAfterItsFirstTextIsStopped()
    {
        var time = new ManualTime();
        var provider = new ClockedLlmProvider(
            time,
            local: false,
            (IReadOnlyList<Step>)[
                new(TimeSpan.FromSeconds(30), new LlmStreamEvent.TextDelta("{")),
                new(TimeSpan.FromSeconds(61), new LlmStreamEvent.TextDelta("}")),
                new(TimeSpan.Zero, Completed),
            ]);
        var log = new RecordingLogger();

        var outcome = await Generate(provider, log, time);

        Assert.True(outcome.Redraftable);
        Assert.Contains("TimedOut: no text or thinking arrived for 60 s; stopped after 90.0 s", SingleWarning(log));
    }

    [Fact]
    public async Task ACloudBriefCallStillThinkingAtTwoMinutesTenIsStoppedAtOneTwenty()
    {
        var time = new ManualTime();
        var provider = new ClockedLlmProvider(time, local: false, ThinksThenSays(TimeSpan.FromSeconds(10), 13, NoBrief));
        var log = new RecordingLogger();

        await GenerateShort(provider, log, time);

        Assert.Contains(
            log.Lines,
            line => line.Contains("the brief step was not read", StringComparison.Ordinal) && line.Contains("TimedOut: a cloud call may run for 120 s; stopped after 120.0 s", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ACloudLinesCallStillThinkingAtTwoMinutesTenIsNotStopped()
    {
        var time = new ManualTime();
        var provider = new ClockedLlmProvider(
            time,
            local: false,
            ThinksThenSays(TimeSpan.FromSeconds(10), 13, PlannedChapterWorld.Lines(ChapterPlanner.MostObjectives)),
            Says(PlannedChapterWorld.NoObjections));

        var outcome = await Generate(provider, new RecordingLogger(), time);

        Assert.True(outcome.Succeeded, outcome.Refusal);
    }

    [Fact]
    public async Task ACloudLinesCallStillThinkingAtSixHundredAndSixtySecondsIsStoppedAtFourHundred()
    {
        var time = new ManualTime();
        var provider = new ClockedLlmProvider(
            time,
            local: false,
            ThinksThenSays(TimeSpan.FromSeconds(10), 41, PlannedChapterWorld.Lines(ChapterPlanner.MostObjectives)));
        var log = new RecordingLogger();

        await Generate(provider, log, time);

        Assert.Contains(
            log.Entries,
            entry => entry.Level == LogLevel.Warning && entry.Message.Contains("TimedOut: a cloud call may run for 400 s; stopped after 400.0 s", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ALocalCallSilentForTenMinutesIsNotStopped()
    {
        var time = new ManualTime();
        var provider = new ClockedLlmProvider(
            time,
            local: true,
            [new(TimeSpan.FromMinutes(10), new LlmStreamEvent.TextDelta(PlannedChapterWorld.Lines(ChapterPlanner.MostObjectives))), new(TimeSpan.Zero, Completed)],
            Says(PlannedChapterWorld.NoObjections));

        var outcome = await Generate(provider, new RecordingLogger(), time);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(TimeSpan.FromMinutes(10), time.GetUtcNow() - DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public async Task ALocalCallThatKeepsThinkingIsNotStoppedAtTwoMinutes()
    {
        var time = new ManualTime();
        var provider = new ClockedLlmProvider(
            time,
            local: true,
            ThinksThenSays(TimeSpan.FromSeconds(25), 8, PlannedChapterWorld.Lines(ChapterPlanner.MostObjectives)),
            Says(PlannedChapterWorld.NoObjections));
        var log = new RecordingLogger();

        var outcome = await Generate(provider, log, time);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(TimeSpan.FromSeconds(200), time.GetUtcNow() - DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public async Task ACloudCallThatFinishesInsideBothLimitsStands()
    {
        var time = new ManualTime();
        var provider = new ClockedLlmProvider(
            time,
            local: false,
            ThinksThenSays(TimeSpan.FromSeconds(29), 4, PlannedChapterWorld.Lines(ChapterPlanner.MostObjectives)),
            Says(PlannedChapterWorld.NoObjections));

        var outcome = await Generate(provider, new RecordingLogger(), time);

        Assert.True(outcome.Succeeded, outcome.Refusal);
    }

    [Fact]
    public async Task ATimedOutFirstDraftIsWrittenAgain()
    {
        var time = new ManualTime();
        var provider = new ClockedLlmProvider(time, local: false, Stalls, Says(PlannedChapterWorld.Lines(ChapterPlanner.MostObjectives)), Says(PlannedChapterWorld.NoObjections));
        var again = new List<string>();

        var outcome = await Generator(provider, new RecordingLogger(), time)
            .DraftAsync(PlannedChapterWorld.Ask(), Now, again.Add, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(4, provider.Calls);
        Assert.Equal([StoryDirector.WritingAgainLine(2, told: false)], again);
    }

    [Fact]
    public async Task ACallerCancellingIsNotATimeLimit()
    {
        var time = new ManualTime();
        var provider = new ClockedLlmProvider(time, local: false, Stalls);
        using var caller = new CancellationTokenSource(TimeSpan.FromSeconds(10), time);

        var reply = await FlavourTurn.AskForAsync(
            provider, null, null, null, "Write.", null, null, null, null, caller.Token, limits: new FlavourLimits(TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(30)) { Time = time });

        Assert.Equal(FlavourMiss.Cancelled, reply.Miss);
        Assert.Null(reply.Message);
    }

    private static readonly LlmStreamEvent Completed = new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed);

    private static readonly IReadOnlyList<Step> Stalls =
        [new(TimeSpan.FromMinutes(10), new LlmStreamEvent.TextDelta("{}")), new(TimeSpan.Zero, Completed)];

    private static IReadOnlyList<Step> Says(string text) =>
        [new(TimeSpan.Zero, new LlmStreamEvent.TextDelta(text)), new(TimeSpan.Zero, Completed)];

    /// <summary>A thinking delta after each <paramref name="gap"/>, <paramref name="count"/> times, then the answer.</summary>
    private static IReadOnlyList<Step> ThinksThenSays(TimeSpan gap, int count, string text) =>
        [.. Enumerable.Repeat(new Step(gap, new LlmStreamEvent.ThinkingDelta("Hmm.")), count), .. Says(text)];

    private const string NoBrief = """{"home": false, "system": null, "activities": [], "ship": null, "changes": false}""";

    private static AdventureGenerator Generator(ILlmProvider provider, ILogger log, TimeProvider time, PlannedChapterWorld.Galaxy? galaxy = null)
    {
        var state = PlannedChapterWorld.State();
        galaxy ??= new PlannedChapterWorld.Galaxy();

        return new AdventureGenerator(() => provider, () => null, () => "You are the ship.", () => "core", () => null, () => state, () => galaxy, () => null, null, null, log)
        {
            Time = time,
        };
    }

    private static Task<AdventureOutcome> Generate(ILlmProvider provider, ILogger log, TimeProvider time) =>
        Generator(provider, log, time).GenerateAsync(PlannedChapterWorld.Ask(), Now, CancellationToken.None);

    private static Task<AdventureOutcome> GenerateShort(ILlmProvider provider, ILogger log, TimeProvider time) =>
        Generator(provider, log, time, PlannedChapterWorld.Roomy())
            .GenerateAsync(new AdventureAsk(AdventureReach.Session, AdventureLength.Evening, Brief: "A grim bounty hunt."), Now, CancellationToken.None);

    private static string SingleWarning(RecordingLogger log) =>
        Assert.Single(log.Entries, entry => entry.Level == LogLevel.Warning).Message;

    /// <summary>A wait on the clock, then an event.</summary>
    private sealed record Step(TimeSpan Wait, LlmStreamEvent Event);

    /// <summary>Plays one round per call, answering a plain-wording check with no objections and no round, moving <see cref="ManualTime"/> a second at a time through each step's wait.</summary>
    private sealed class ClockedLlmProvider(ManualTime time, bool local, params IReadOnlyList<Step>[] rounds) : ILlmProvider
    {
        private int _scripted;

        /// <summary>Every call, the plain-wording checks it answers without a scripted round included.</summary>
        public int Calls { get; private set; }

        public string Id => "anthropic";

        public string DisplayName => "Clocked";

        public string DefaultModel => "claude-opus-5";

        public bool RunsOnThisMachine => local;

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
            Calls++;

            if (request.Prompt.History[0].Text.Contains("You are checking the wording of a draft", StringComparison.Ordinal))
            {
                yield return new LlmStreamEvent.TextDelta("""{"objections": []}""");
                yield return Completed;
                yield break;
            }

            var round = rounds[_scripted++];

            foreach (var step in round)
            {
                for (var waited = TimeSpan.Zero; waited < step.Wait; waited += TimeSpan.FromSeconds(1))
                {
                    time.Advance(TimeSpan.FromSeconds(1));
                    cancellationToken.ThrowIfCancellationRequested();
                }

                yield return step.Event;
            }

            await Task.CompletedTask;
        }
    }
}

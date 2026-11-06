using System.Runtime.CompilerServices;
using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Persona;
using D47.Core.Tests.Knowledge;
using Xunit;

namespace D47.Scenarios.Tests.ModelComparison;

public sealed class AChapterEndingOnAProviderFailureIsWrittenAgainTests
{
    private const string Model = "claude-sonnet-5-5";

    private static readonly LlmStreamEvent[] Overloaded = [new LlmStreamEvent.Failed("the provider reported a server error", true)];

    private static readonly LlmStreamEvent[] BadRequest = [new LlmStreamEvent.Failed("the provider rejected the request", false)];

    private static readonly LlmStreamEvent[] Answered = [new LlmStreamEvent.TextDelta("Scripted reply."), new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed)];

    [Fact]
    public async Task AChapterThatFailsTransientlyThenAnswersIsWrittenAndNotesTheRetry()
    {
        var provider = new ScriptedProvider(Overloaded, Answered);
        var meter = new MeteredLlmProvider(provider, null, 100m).View();
        var time = new RecordedWaits();

        var record = await WriteAsync(meter, time, () => ReadOneAsync(meter));

        Assert.Equal("written", record.Outcome);
        Assert.Equal(1, record.Retries);
        Assert.Equal([StoryComparisonTests.RetryWait], time.Waits);
        Assert.Equal(2, record.Calls.Count);
        Assert.Equal("the provider reported a server error", record.Calls[0].Failure);
        Assert.Null(record.Calls[1].Failure);
    }

    [Fact]
    public async Task AChapterThatFailsTransientlyOnEveryAttemptIsProviderFailedAfterThree()
    {
        var provider = new ScriptedProvider(Overloaded, Overloaded, Overloaded, Overloaded);
        var meter = new MeteredLlmProvider(provider, null, 100m).View();
        var time = new RecordedWaits();

        var record = await WriteWithTheGeneratorAsync(meter, time);

        Assert.Equal("provider failed", record.Outcome);
        Assert.Equal(2, record.Retries);
        Assert.Equal([StoryComparisonTests.RetryWait, StoryComparisonTests.RetryWait], time.Waits);
        Assert.Equal(provider.Calls, record.Calls.Count);
        Assert.All(record.Calls, call => Assert.Equal("the provider reported a server error", call.Failure));
        Assert.Equal(TimeSpan.FromSeconds(30), StoryComparisonTests.RetryWait);
    }

    [Fact]
    public async Task AFailureThatIsNotTransientIsNotWrittenAndIsNotRetried()
    {
        var provider = new ScriptedProvider(BadRequest, BadRequest);
        var meter = new MeteredLlmProvider(provider, null, 100m).View();
        var time = new RecordedWaits();

        var record = await WriteWithTheGeneratorAsync(meter, time);

        Assert.Equal("not written", record.Outcome);
        Assert.Equal(0, record.Retries);
        Assert.Empty(time.Waits);
        Assert.Equal(provider.Calls, record.Calls.Count);
        Assert.Equal("the provider rejected the request", record.Calls[0].Failure);
    }

    [Fact]
    public async Task TheCostCapStopsARetryWhenAnotherChapterSpendsItDuringTheWait()
    {
        var costly = new LlmStreamEvent[] { new LlmStreamEvent.Completed(new LlmUsage(10_000, 0, 0, 0), LlmStopReason.Completed) };
        var provider = new ScriptedProvider(Overloaded, costly, Answered);
        var shared = new MeteredLlmProvider(provider, null, 0.01m);
        var meter = shared.View();
        var time = new RecordedWaits { OnWait = () => DrainAsync(shared.View()).GetAwaiter().GetResult() };

        var record = await WriteAsync(meter, time, () => ReadOneAsync(meter));

        Assert.Equal("not written", record.Outcome);
        Assert.Equal(1, record.Retries);
        Assert.Equal(2, provider.Calls);
        Assert.Contains(" cap.", record.Exception);
    }

    [Fact]
    public async Task AFailedCallIsRecordedWithItsMessageAndWhetherItWasTransient()
    {
        var provider = new ScriptedProvider(Overloaded, BadRequest);
        var meter = new MeteredLlmProvider(provider, null, 100m);

        await DrainAsync(meter);
        await DrainAsync(meter);

        var requests = meter.Drain();
        Assert.Equal(2, requests.Count);
        Assert.Equal("the provider reported a server error", requests[0].Failure);
        Assert.True(requests[0].Transient);
        Assert.Equal("the provider rejected the request", requests[1].Failure);
        Assert.False(requests[1].Transient);
    }

    private static LlmRequest Request => new()
    {
        Model = Model,
        Prompt = new PromptAssembly(),
        Effort = ThinkingEffort.Medium,
        Sampling = LlmSampling.Adventure,
    };

    private static async Task DrainAsync(ILlmProvider provider)
    {
        await foreach (var _ in provider.StreamAsync(Request, TestContext.Current.CancellationToken))
        {
        }
    }

    /// <summary>One request through the meter; a failed one gives a refusal and an answered one a draft.</summary>
    private static async Task<AdventureOutcome> ReadOneAsync(MeteredLlmProvider.MeteredView meter)
    {
        var failed = false;

        await foreach (var streamEvent in meter.StreamAsync(Request, TestContext.Current.CancellationToken))
        {
            failed |= streamEvent is LlmStreamEvent.Failed;
        }

        return failed
            ? new AdventureOutcome(null, null, "The provider failed.", [])
            : new AdventureOutcome(new Adventure { Key = "k", Name = "n", Source = AdventureSource.Generated }, null, null, []);
    }

    private static Task<StoryRecord> WriteAsync(MeteredLlmProvider.MeteredView meter, TimeProvider time, Func<Task<AdventureOutcome>> generate) =>
        StoryComparisonTests.WriteOneAsync(
            meter, Model, ThinkingEffort.Medium, 1, PersonaCatalog.Resolve("warden"), new ScenarioWorld(), new TimedGalaxy(new UnusedGalaxy()), null,
            new AdventureAsk(AdventureReach.NearHere, AdventureLength.Short), "chapter", "", null, time: time, generate: generate);

    /// <summary>A short story with a brief, so the brief step is the call the provider fails before anything is planned.</summary>
    private static Task<StoryRecord> WriteWithTheGeneratorAsync(MeteredLlmProvider.MeteredView meter, TimeProvider time) =>
        StoryComparisonTests.WriteOneAsync(
            meter, Model, ThinkingEffort.Medium, 1, PersonaCatalog.Resolve("warden"), new ScenarioWorld(), new TimedGalaxy(new UnusedGalaxy()), null,
            new AdventureAsk(AdventureReach.NearHere, AdventureLength.Short, Brief: "A quiet run."), "chapter", "", null, time: time);

    private sealed class UnusedGalaxy : StubGalaxyService, IGalaxyService
    {
        public Task<StarPosition?> PositionAsync(string system, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    /// <summary>Answers each request with the next script; the last one repeats.</summary>
    private sealed class ScriptedProvider(params LlmStreamEvent[][] script) : ILlmProvider
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public string Id => "anthropic";

        public string DisplayName => "Scripted";

        public string DefaultModel => Model;

        public LlmProviderCapabilities CapabilitiesFor(string model) => new()
        {
            SupportsPromptCaching = true,
            SupportsThinkingEffort = true,
            SupportsOperatorSystemMessages = true,
            MinimumCacheablePrefixTokens = 512,
        };

        public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(LlmRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var at = Interlocked.Increment(ref _calls) - 1;
            await Task.Yield();

            foreach (var streamEvent in script[Math.Min(at, script.Length - 1)])
            {
                yield return streamEvent;
            }
        }
    }

    /// <summary>A clock whose timers fire at once, keeping the waits asked of it.</summary>
    private sealed class RecordedWaits : TimeProvider
    {
        public List<TimeSpan> Waits { get; } = [];

        public Action? OnWait { get; init; }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Waits.Add(dueTime);
            OnWait?.Invoke();
            callback(state);
            return new Fired();
        }

        private sealed class Fired : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}

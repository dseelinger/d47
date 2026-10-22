using System.Diagnostics;
using System.Runtime.CompilerServices;
using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Persona;
using Xunit;

namespace D47.Scenarios.Tests.ModelComparison;

public sealed class ChaptersWrittenTogetherKeepTheirOwnRecordsTests
{
    private static readonly TimeSpan Reply = TimeSpan.FromMilliseconds(300);

    [Fact]
    public async Task SixCombinationsAtOnceEachRecordOnlyTheirOwnRequestsInAboutOneChaptersTime()
    {
        string[] models = ["claude-sonnet-5-5", "claude-haiku-5-5", "claude-opus-5-5", "gpt-6.1-sol", "gpt-6-luna", "gpt-5.6-terra"];
        var stub = new DelayedProvider();
        var meter = new MeteredLlmProvider(stub, null, 100m);
        var world = new ScenarioWorld();
        var persona = PersonaCatalog.Resolve("warden");
        var ask = new AdventureAsk(AdventureReach.NearHere, AdventureLength.Short);

        var one = Stopwatch.StartNew();
        _ = await Write(meter, models[0], persona, world, ask);
        var single = one.Elapsed;
        stub.Reset();

        var together = Stopwatch.StartNew();
        var records = await Task.WhenAll(models.Select(model => Write(meter, model, persona, world, ask)));
        var elapsed = together.Elapsed;

        foreach (var record in records)
        {
            Assert.NotEmpty(record.Calls);
            Assert.Equal(stub.CallsFor(record.Model), record.Calls.Count);
        }

        Assert.Equal(stub.Total, records.Sum(record => record.Calls.Count));
        Assert.True(elapsed < single * 3, $"six at once took {elapsed}, one alone took {single}");
    }

    [Fact]
    public async Task AChapterThatWouldPassTheCapWaitsForAReservationToBeReleased()
    {
        decimal spent = 0m;
        var reservations = new CapReservations(() => spent, 1.00m);

        var first = await reservations.ReserveAsync(0.60m, TestContext.Current.CancellationToken);
        Assert.NotNull(first);

        var second = reservations.ReserveAsync(0.60m, TestContext.Current.CancellationToken);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.False(second.IsCompleted);

        first.Dispose();
        Assert.NotNull(await second);
    }

    [Fact]
    public async Task AChapterThatCannotFitEvenAloneIsRefused()
    {
        var reservations = new CapReservations(() => 0.90m, 1.00m);

        Assert.Null(await reservations.ReserveAsync(0.60m, TestContext.Current.CancellationToken));
    }

    private static Task<StoryRecord> Write(MeteredLlmProvider meter, string model, Persona persona, ScenarioWorld world, AdventureAsk ask) =>
        StoryComparisonTests.WriteOneAsync(
            meter.View(), model, ThinkingEffort.Medium, 1, persona, world, new TimedGalaxy(new UnusedGalaxy()), null, ask, "short story", "", null);

    /// <summary>Present so the generator starts; with no Commander location it is never asked anything.</summary>
    private sealed class UnusedGalaxy : IGalaxyService
    {
        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<StarPosition?> PositionAsync(string system, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class DelayedProvider : ILlmProvider
    {
        private readonly Dictionary<string, int> _calls = new(StringComparer.Ordinal);

        public string Id => "anthropic";

        public string DisplayName => "Delayed";

        public string DefaultModel => "claude-sonnet-5-5";

        public int Total
        {
            get
            {
                lock (_calls)
                {
                    return _calls.Values.Sum();
                }
            }
        }

        public int CallsFor(string model)
        {
            lock (_calls)
            {
                return _calls.GetValueOrDefault(model);
            }
        }

        public void Reset()
        {
            lock (_calls)
            {
                _calls.Clear();
            }
        }

        public LlmProviderCapabilities CapabilitiesFor(string model) => new()
        {
            SupportsPromptCaching = true,
            SupportsThinkingEffort = true,
            SupportsOperatorSystemMessages = true,
            MinimumCacheablePrefixTokens = 512,
        };

        public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(
            LlmRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            lock (_calls)
            {
                _calls[request.Model] = _calls.GetValueOrDefault(request.Model) + 1;
            }

            await Task.Delay(Reply, cancellationToken).ConfigureAwait(false);
            yield return new LlmStreamEvent.TextDelta("Scripted reply.");
            yield return new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed);
        }
    }
}

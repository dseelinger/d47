using System.Diagnostics;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Scenarios.Tests.ModelComparison;

/// <summary>One model call in a story run.</summary>
public sealed record StoryCall(
    string Step,
    double Seconds,
    int InputTokens,
    int CacheWriteTokens,
    int CacheReadTokens,
    int OutputTokens,
    string? StopReason,
    string? Failure,
    decimal Dollars,
    double? FirstOutputSeconds = null,
    double LongestSilenceSeconds = 0);

/// <summary>One short story or story chapter written by one model at one effort, as the results file holds it.</summary>
public sealed record StoryRecord
{
    /// <summary>"short story" or "chapter".</summary>
    public string Kind { get; init; } = "short story";

    /// <summary><c>D47_COMPARE_LABEL</c>: the context and variant the run was made under.</summary>
    public string Label { get; init; } = "";

    public required string Model { get; init; }

    public required string Effort { get; init; }

    public required int Run { get; init; }

    /// <summary>"written", "not written", "over limit" when a call ran past its <c>CallLimitFor</c>, or "cap" when the budget stopped a request.</summary>
    public required string Outcome { get; init; }

    /// <summary>The step the #1239 warning names, or null when it names none.</summary>
    public string? FailingStep { get; init; }

    /// <summary>The #1239 warning, as logged.</summary>
    public string? Reason { get; init; }

    public string? Refusal { get; init; }

    /// <summary>The Commander's message when a reply stopped at the endpoint's context.</summary>
    public string? ContextMessage { get; init; }

    /// <summary>The file under <c>drafts</c> holding the whole draft, or the refusal.</summary>
    public string? DraftFile { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public string? Name { get; init; }

    public int Beats { get; init; }

    public double GalaxySeconds { get; init; }

    public int GalaxyCalls { get; init; }

    public double NotablePlacesSeconds { get; init; }

    public IReadOnlyList<StoryCall> Calls { get; init; } = [];

    public double TotalSeconds { get; init; }

    public int InputTokens { get; init; }

    public int OutputTokens { get; init; }

    public decimal Dollars { get; init; }

    public string? Exception { get; init; }
}

/// <summary>Sends each request to the provider that serves its model.</summary>
public sealed class ModelRoutedProvider(Func<string, ILlmProvider> forModel, string defaultModel) : ILlmProvider
{
    public string Id => "routed";

    public string DisplayName => "Routed by model";

    public string DefaultModel => defaultModel;

    public LlmProviderCapabilities CapabilitiesFor(string model) => forModel(model).CapabilitiesFor(model);

    public IAsyncEnumerable<LlmStreamEvent> StreamAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return forModel(request.Model).StreamAsync(request, cancellationToken);
    }
}

/// <summary>Seconds spent waiting on the wrapped service, summed since the last reset.</summary>
public abstract class Timed
{
    private long _ticks;

    private int _calls;

    public double Seconds => TimeSpan.FromTicks(Interlocked.Read(ref _ticks)).TotalSeconds;

    public int Calls => _calls;

    public void Reset()
    {
        Interlocked.Exchange(ref _ticks, 0);
        Interlocked.Exchange(ref _calls, 0);
    }

    protected async Task<T> TimeAsync<T>(Func<Task<T>> call)
    {
        var started = Stopwatch.GetTimestamp();

        try
        {
            return await call().ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Add(ref _ticks, Stopwatch.GetElapsedTime(started).Ticks);
            Interlocked.Increment(ref _calls);
        }
    }
}

public sealed class TimedGalaxy(IGalaxyService inner) : Timed, IGalaxyService
{
    public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
        TimeAsync(() => inner.SearchAsync(query, cancellationToken));

    public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
        TimeAsync(() => inner.DistanceAsync(from, to, cancellationToken));

    public Task<StarPosition?> PositionAsync(string system, CancellationToken cancellationToken) =>
        TimeAsync(() => inner.PositionAsync(system, cancellationToken));

    public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
        TimeAsync(() => inner.FindStationsAsync(query, cancellationToken));

    public Task<IReadOnlyList<OutfitterStation>> FindOutfittersAsync(
        IReadOnlyList<OutfittingItem> items,
        string referenceSystem,
        double maxDistance,
        PadSize? minPad,
        int? maxReportAgeHours,
        CancellationToken cancellationToken) =>
        TimeAsync(() => inner.FindOutfittersAsync(items, referenceSystem, maxDistance, minPad, maxReportAgeHours, cancellationToken));

    public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
        TimeAsync(() => inner.FindBodiesAsync(query, cancellationToken));

    public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) =>
        TimeAsync(() => inner.ScanForColonisationAsync(query, cancellationToken));

    public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) =>
        TimeAsync(() => inner.SystemBodiesAsync(systemAddress, cancellationToken));

    public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
        TimeAsync(() => inner.SystemBiologyAsync(systemAddress, cancellationToken));

    public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) =>
        TimeAsync(() => inner.FindMissionStationsAsync(query, cancellationToken));
}

public sealed class TimedNotablePlaces(INotablePlacesService inner) : Timed, INotablePlacesService
{
    public Task<IReadOnlyList<NotablePlace>> NearAsync(StarPosition from, double radiusLightYears, int limit, CancellationToken cancellationToken) =>
        TimeAsync(() => inner.NearAsync(from, radiusLightYears, limit, cancellationToken));
}

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using D47.Core.Conversation;
using Xunit;

namespace D47.Scenarios.Tests.ModelComparison;

/// <summary>One model's published prices, in dollars per million tokens.</summary>
public sealed record PublishedPrice(decimal Input, decimal Output, decimal CacheWrite, decimal CacheRead)
{
    public decimal DollarsFor(LlmUsage usage) =>
        (usage.InputTokens * Input
         + usage.OutputTokens * Output
         + usage.CacheCreationInputTokens * CacheWrite
         + usage.CacheReadInputTokens * CacheRead) / 1_000_000m
        + usage.WebSearchRequests * 0.01m;
}

/// <summary>
/// Sonnet 5.5 and Haiku 5.5 at the prices on Anthropic's pricing page on 2026-10-08, not d47's catalog, whose
/// cache prices disagree with it; the five-minute cache write is the one d47 requests. The rest from the catalog.
/// </summary>
public static class PublishedPrices
{
    private static readonly PublishedPrice Sonnet55 = new(2m, 10m, 2.5m, 0.10m);

    private static readonly PublishedPrice Haiku55 = new(0.10m, 0.50m, 0.125m, 0.01m);

    private static readonly PublishedPrice Haiku55Long = new(0.50m, 2.50m, 0.625m, 0.05m);

    /// <summary>Input and output from the model catalog published 2026-10-15; cache write and read at 1.25 and 0.1 of input.</summary>
    private static PublishedPrice FromCatalog(decimal input, decimal output) => new(input, output, input * 1.25m, input * 0.1m);

    private static readonly PublishedPrice OnThisMachine = new(0m, 0m, 0m, 0m);

    /// <summary>The price of one request, or null for a model this table does not know.</summary>
    public static PublishedPrice? For(string model, LlmUsage usage) => model switch
    {
        "claude-sonnet-5-5" => Sonnet55,
        "claude-haiku-5-5" => usage.TotalInputTokens > 100_000 ? Haiku55Long : Haiku55,
        "claude-opus-5-5" => FromCatalog(4m, 20m),
        "gpt-6.1-sol" => FromCatalog(2m, 10m),
        "gpt-6-luna" => FromCatalog(0.10m, 0.50m),
        "gpt-5.6-terra" => FromCatalog(2m, 12m),
        _ when ModelComparisonTests.LocalModels.Contains(model, StringComparer.Ordinal) => OnThisMachine,
        _ => null,
    };
}

/// <summary>Thrown before a request that would start once the budget is spent.</summary>
public sealed class BudgetSpentException(decimal spent, decimal cap)
    : Exception(string.Create(CultureInfo.InvariantCulture, $"Spent ${spent:0.0000} of the ${cap:0.00} cap."));

/// <summary>One request, as metered.</summary>
/// <param name="FirstOutputMilliseconds">From the request to its first text or thinking; null when none arrived.</param>
/// <param name="LongestSilenceMilliseconds">The longest stretch with no text or thinking arriving, from the request to its end.</param>
public sealed record MeteredRequest(
    string Model,
    LlmUsage Usage,
    decimal Dollars,
    long Milliseconds,
    string? Failure,
    LlmStopReason? StopReason = null,
    long? FirstOutputMilliseconds = null,
    long LongestSilenceMilliseconds = 0);

/// <summary>
/// A provider that prices every request at <see cref="PublishedPrices"/>, appends it to a ledger that
/// outlives the run, and refuses to start a request once the ledger reaches the cap.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MeteredLlmProvider : ILlmProvider
{
    private readonly ILlmProvider _inner;

    private readonly string? _ledger;

    private readonly decimal _cap;

    private readonly Lock _gate = new();

    private readonly List<MeteredRequest> _requests = [];

    private decimal _spent;

    /// <param name="ledger">The file every request is appended to, or null to keep none.</param>
    public MeteredLlmProvider(ILlmProvider inner, string? ledger, decimal cap)
    {
        _inner = inner;
        _ledger = ledger;
        _cap = cap;
        _spent = ledger is null ? 0m : Banked(ledger);
    }

    /// <summary>Dollars in the ledger, this run and every earlier one.</summary>
    public decimal Spent
    {
        get
        {
            lock (_gate)
            {
                return _spent;
            }
        }
    }

    /// <summary>A view of this meter that shares its spend and cap but holds only the requests made through it.</summary>
    public MeteredView View() => new(this);

    public string Id => _inner.Id;

    public string DisplayName => _inner.DisplayName;

    public string DefaultModel => _inner.DefaultModel;

    public LlmProviderCapabilities CapabilitiesFor(string model) => _inner.CapabilitiesFor(model);

    /// <summary>Takes the requests metered since the last call.</summary>
    public IReadOnlyList<MeteredRequest> Drain() => Drain(_requests);

    private List<MeteredRequest> Drain(List<MeteredRequest> sink)
    {
        lock (_gate)
        {
            var drained = sink.ToList();
            sink.Clear();
            return drained;
        }
    }

    public IAsyncEnumerable<LlmStreamEvent> StreamAsync(LlmRequest request, CancellationToken cancellationToken) =>
        StreamAsync(request, _requests, cancellationToken);

    private async IAsyncEnumerable<LlmStreamEvent> StreamAsync(
        LlmRequest request,
        List<MeteredRequest> sink,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (Spent >= _cap)
        {
            throw new BudgetSpentException(Spent, _cap);
        }

        var clock = Stopwatch.StartNew();
        LlmUsage? usage = null;
        string? failure = null;
        LlmStopReason? stopReason = null;
        long? firstOutput = null;
        long lastOutput = 0;
        long longestSilence = 0;

        await foreach (var streamEvent in _inner.StreamAsync(request, cancellationToken).ConfigureAwait(false))
        {
            switch (streamEvent)
            {
                case LlmStreamEvent.Completed completed:
                    usage = completed.Usage;
                    stopReason = completed.StopReason;
                    break;
                case LlmStreamEvent.Failed failed:
                    failure = failed.Message;
                    break;
                case LlmStreamEvent.TextDelta or LlmStreamEvent.ThinkingDelta:
                    var at = clock.ElapsedMilliseconds;
                    firstOutput ??= at;
                    longestSilence = Math.Max(longestSilence, at - lastOutput);
                    lastOutput = at;
                    break;
            }

            yield return streamEvent;
        }

        var elapsed = clock.ElapsedMilliseconds;
        Record(sink, request.Model, usage ?? LlmUsage.None, elapsed, failure, stopReason, firstOutput, Math.Max(longestSilence, elapsed - lastOutput));
    }

    private void Record(
        List<MeteredRequest> sink,
        string model, LlmUsage usage, long milliseconds, string? failure, LlmStopReason? stopReason, long? firstOutput, long longestSilence)
    {
        var price = PublishedPrices.For(model, usage)
                    ?? throw new InvalidOperationException($"No published price for {model}; refusing to run unpriced.");

        var dollars = price.DollarsFor(usage);

        lock (_gate)
        {
            _spent += dollars;
            sink.Add(new MeteredRequest(model, usage, dollars, milliseconds, failure, stopReason, firstOutput, longestSilence));

            if (_ledger is not null)
            {
                File.AppendAllText(
                    _ledger,
                    JsonSerializer.Serialize(new
                    {
                        at = DateTimeOffset.UtcNow,
                        model,
                        input = usage.InputTokens,
                        cacheWrite = usage.CacheCreationInputTokens,
                        cacheRead = usage.CacheReadInputTokens,
                        output = usage.OutputTokens,
                        webSearches = usage.WebSearchRequests,
                        dollars,
                    }) + Environment.NewLine);
            }
        }
    }

    /// <summary>One chapter's view of a <see cref="MeteredLlmProvider"/>: its requests alone, against the shared spend.</summary>
    public sealed class MeteredView : ILlmProvider
    {
        private readonly MeteredLlmProvider _meter;

        private readonly List<MeteredRequest> _own = [];

        internal MeteredView(MeteredLlmProvider meter) => _meter = meter;

        public string Id => _meter.Id;

        public string DisplayName => _meter.DisplayName;

        public string DefaultModel => _meter.DefaultModel;

        public LlmProviderCapabilities CapabilitiesFor(string model) => _meter.CapabilitiesFor(model);

        /// <summary>Takes the requests made through this view since the last call.</summary>
        public IReadOnlyList<MeteredRequest> Drain() => _meter.Drain(_own);

        public IAsyncEnumerable<LlmStreamEvent> StreamAsync(LlmRequest request, CancellationToken cancellationToken) =>
            _meter.StreamAsync(request, _own, cancellationToken);
    }

    private static decimal Banked(string ledger)
    {
        if (!File.Exists(ledger))
        {
            return 0m;
        }

        return File.ReadLines(ledger)
            .Where(line => line.Length > 0)
            .Sum(line => JsonDocument.Parse(line).RootElement.GetProperty("dollars").GetDecimal());
    }
}

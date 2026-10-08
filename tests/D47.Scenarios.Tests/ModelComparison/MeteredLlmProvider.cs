using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using D47.Core.Conversation;

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
/// The prices on the providers' own pricing pages on 2026-10-08, not d47's catalog, whose cache prices
/// disagree with them. The five-minute cache write is the one d47 requests.
/// </summary>
public static class PublishedPrices
{
    private static readonly PublishedPrice Sonnet55 = new(2m, 10m, 2.5m, 0.10m);

    private static readonly PublishedPrice Haiku55 = new(0.10m, 0.50m, 0.125m, 0.01m);

    private static readonly PublishedPrice Haiku55Long = new(0.50m, 2.50m, 0.625m, 0.05m);

    /// <summary>The price of one request, or null for a model this table does not know.</summary>
    public static PublishedPrice? For(string model, LlmUsage usage) => model switch
    {
        "claude-sonnet-5-5" => Sonnet55,
        "claude-haiku-5-5" => usage.TotalInputTokens > 100_000 ? Haiku55Long : Haiku55,
        _ => null,
    };
}

/// <summary>Thrown before a request that would start once the budget is spent.</summary>
public sealed class BudgetSpentException(decimal spent, decimal cap)
    : Exception(string.Create(CultureInfo.InvariantCulture, $"Spent ${spent:0.0000} of the ${cap:0.00} cap."));

/// <summary>One request, as metered.</summary>
public sealed record MeteredRequest(string Model, LlmUsage Usage, decimal Dollars, long Milliseconds, string? Failure);

/// <summary>
/// A provider that prices every request at <see cref="PublishedPrices"/>, appends it to a ledger that
/// outlives the run, and refuses to start a request once the ledger reaches the cap.
/// </summary>
public sealed class MeteredLlmProvider : ILlmProvider
{
    private readonly ILlmProvider _inner;

    private readonly string _ledger;

    private readonly decimal _cap;

    private readonly Lock _gate = new();

    private readonly List<MeteredRequest> _requests = [];

    public MeteredLlmProvider(ILlmProvider inner, string ledger, decimal cap)
    {
        _inner = inner;
        _ledger = ledger;
        _cap = cap;
        Spent = Banked(ledger);
    }

    /// <summary>Dollars in the ledger, this run and every earlier one.</summary>
    public decimal Spent { get; private set; }

    public string Id => _inner.Id;

    public string DisplayName => _inner.DisplayName;

    public string DefaultModel => _inner.DefaultModel;

    public LlmProviderCapabilities CapabilitiesFor(string model) => _inner.CapabilitiesFor(model);

    /// <summary>Takes the requests metered since the last call.</summary>
    public IReadOnlyList<MeteredRequest> Drain()
    {
        lock (_gate)
        {
            var drained = _requests.ToList();
            _requests.Clear();
            return drained;
        }
    }

    public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (Spent >= _cap)
        {
            throw new BudgetSpentException(Spent, _cap);
        }

        var clock = Stopwatch.StartNew();
        LlmUsage? usage = null;
        string? failure = null;

        await foreach (var streamEvent in _inner.StreamAsync(request, cancellationToken).ConfigureAwait(false))
        {
            switch (streamEvent)
            {
                case LlmStreamEvent.Completed completed:
                    usage = completed.Usage;
                    break;
                case LlmStreamEvent.Failed failed:
                    failure = failed.Message;
                    break;
            }

            yield return streamEvent;
        }

        Record(request.Model, usage ?? LlmUsage.None, clock.ElapsedMilliseconds, failure);
    }

    private void Record(string model, LlmUsage usage, long milliseconds, string? failure)
    {
        var price = PublishedPrices.For(model, usage)
                    ?? throw new InvalidOperationException($"No published price for {model}; refusing to run unpriced.");

        var dollars = price.DollarsFor(usage);

        lock (_gate)
        {
            Spent += dollars;
            _requests.Add(new MeteredRequest(model, usage, dollars, milliseconds, failure));

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

using D47.Core.Catalog;

namespace D47.Core.Conversation;

/// <summary>Per-million-token rates for one model.</summary>
public sealed record ModelPrice(decimal InputPerMillion, decimal OutputPerMillion)
{
    /// <summary>What writing a cache entry costs, as a multiple of the input rate.</summary>
    public decimal CacheWriteFactor { get; init; } = 1.25m;

    /// <summary>What reading one costs, as a multiple of the input rate.</summary>
    public decimal CacheReadFactor { get; init; } = 0.1m;

    public decimal CacheWritePerMillion => InputPerMillion * CacheWriteFactor;

    public decimal CacheReadPerMillion => InputPerMillion * CacheReadFactor;

    /// <summary>What one server-side web search costs, on top of the tokens its results become.</summary>
    public const decimal DollarsPerWebSearch = 0.01m;

    public decimal DollarsFor(LlmUsage usage) =>
        ((usage.InputTokens * InputPerMillion
          + usage.OutputTokens * OutputPerMillion
          + usage.CacheCreationInputTokens * CacheWritePerMillion
          + usage.CacheReadInputTokens * CacheReadPerMillion) / 1_000_000m)
        + (usage.WebSearchRequests * DollarsPerWebSearch);
}

/// <summary>
/// Prices per provider and per model, so a running total survives an endpoint switch.
/// </summary>
public sealed class PriceTable(ModelCatalogSource source)
{
    public static PriceTable Default { get; } = new(ModelCatalogSource.Shared);

    /// <summary>Every turn free, which is what a model running on the Commander's own machine costs.</summary>
    public static ModelPrice Free { get; } = new(0m, 0m) { CacheReadFactor = 0m, CacheWriteFactor = 0m };

    /// <summary>Null when the model is priced as unknown.</summary>
    public ModelPrice? For(string providerId, string model) => source.Current.PriceFor(providerId, model);
}

/// <summary>What one turn cost.</summary>
public sealed record TurnCost(LlmUsage Usage, decimal Dollars, bool Priced)
{
    public static TurnCost Unpriced(LlmUsage usage) => new(usage, 0m, Priced: false);
}

/// <summary>
/// What the provider said about caching for one turn, in terms that mean the same thing whoever
/// answered it (Phase 29, seam 3).
/// </summary>
public enum PrefixWarmth
{
    /// <summary>
    /// The turn cannot be read either way — no usage came back, or the endpoint does not cache at all.
    /// </summary>
    Unknown,

    /// <summary>
    /// The prefix was not there: cache was written, or nothing was read when a cacheable prefix should
    /// have been waiting.
    /// </summary>
    Cold,

    /// <summary>The prefix was found and read.</summary>
    Warm,
}

/// <summary>Per-turn usage and a running total (Phase 3, "LLM Turn Price").</summary>
/// <param name="ledger">
/// Where charges are kept between runs, or null for a tracker that only knows this session — the replay
/// harness, and every test that is not about history.
/// </param>
public sealed class SpendTracker(SpendLedger? ledger = null)
{
    private readonly List<TurnCost> _turns = [];

    public decimal RunningTotalDollars { get; private set; }

    public int TurnCount => _turns.Count;

    /// <summary>Cold prefixes with no sanctioned cause.</summary>
    public int UnexplainedColdPrefixes { get; private set; }

    /// <summary>
    /// Turns whose caching could not be read at all — no usage block, or an endpoint that does not
    /// cache.
    /// </summary>
    public int UnmeasuredPrefixes { get; private set; }

    public TurnCost? Last => _turns.Count > 0 ? _turns[^1] : null;

    /// <summary>Empties the session's figures, for a reset performed in the Details dialog (#197).</summary>
    public void Forget()
    {
        _turns.Clear();
        RunningTotalDollars = 0m;
        UnexplainedColdPrefixes = 0;
        UnmeasuredPrefixes = 0;
    }

    /// <summary>
    /// <paramref name="coldPrefixExpected"/> is true for the first turn of a session and for the turn
    /// after a model or provider change — the cases where writing cache is correct.
    /// </summary>
    /// <paramref name="coldPrefixExpected"/>
    /// is true for the first turn of a session and for the turn after a model or provider change — the
    /// cases where writing cache is correct.
    /// </paramref>
    public void Record(
        TurnCost cost,
        bool coldPrefixExpected,
        string providerId = "",
        string model = "",
        PrefixWarmth warmth = PrefixWarmth.Unknown)
    {
        _turns.Add(cost);
        RunningTotalDollars += cost.Dollars;

        // An uninformed caller gets the original rule — a cache write with no sanctioned cause — which is
        // still correct for the provider it was written against.
        var observed = warmth is not PrefixWarmth.Unknown ? warmth
            : !cost.Usage.Reported ? PrefixWarmth.Unknown
            : cost.Usage.CacheCreationInputTokens > 0 ? PrefixWarmth.Cold
            : PrefixWarmth.Warm;

        if (observed is PrefixWarmth.Unknown)
        {
            UnmeasuredPrefixes++;
        }
        else if (observed is PrefixWarmth.Cold && !coldPrefixExpected)
        {
            UnexplainedColdPrefixes++;
        }

        ledger?.Append(new SpendEntry
        {
            Kind = SpendKind.Model,
            ProviderId = providerId,
            Model = model,
            Dollars = cost.Dollars,
            Priced = cost.Priced,
            InputTokens = cost.Usage.InputTokens,
            CacheWriteTokens = cost.Usage.CacheCreationInputTokens,
            CacheReadTokens = cost.Usage.CacheReadInputTokens,
            OutputTokens = cost.Usage.OutputTokens,
            WebSearchRequests = cost.Usage.WebSearchRequests,
        });
    }
}

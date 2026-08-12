namespace D47.Core.Conversation;

/// <summary>
/// Per-million-token rates for one model. Cache rates are derived rather than listed: writing
/// costs 1.25x input at the default 5-minute TTL and reading costs 0.1x, so quoting them
/// separately would be two more numbers to keep in step with the first.
/// </summary>
public sealed record ModelPrice(decimal InputPerMillion, decimal OutputPerMillion)
{
    public decimal CacheWritePerMillion => InputPerMillion * 1.25m;

    public decimal CacheReadPerMillion => InputPerMillion * 0.1m;

    public decimal DollarsFor(LlmUsage usage) =>
        (usage.InputTokens * InputPerMillion
         + usage.OutputTokens * OutputPerMillion
         + usage.CacheCreationInputTokens * CacheWritePerMillion
         + usage.CacheReadInputTokens * CacheReadPerMillion) / 1_000_000m;
}

/// <summary>
/// Prices per provider and per model, so a running total survives an endpoint switch
/// (architecture.md §6). List prices as published 2026-06-24; a model with no entry is priced
/// as unknown rather than as free.
/// </summary>
public sealed class PriceTable
{
    private readonly Dictionary<(string Provider, string Model), ModelPrice> _prices;

    private PriceTable(Dictionary<(string, string), ModelPrice> prices) => _prices = prices;

    public static PriceTable Default { get; } = new(new Dictionary<(string, string), ModelPrice>
    {
        // Anthropic list prices. Claude Sonnet 5 also has introductory pricing ($2/$10) running
        // to 2026-08-31, which is deliberately not modelled: honouring it needs today's date,
        // and no Core component reads the clock. The effect is that a Sonnet 5 turn is quoted
        // slightly high while the introduction lasts.
        [("anthropic", "claude-opus-5")] = new(5m, 25m),
        [("anthropic", "claude-opus-4-8")] = new(5m, 25m),
        [("anthropic", "claude-sonnet-5")] = new(3m, 15m),
        [("anthropic", "claude-haiku-4-5")] = new(1m, 5m),
        [("anthropic", "claude-fable-5")] = new(10m, 50m),
    });

    public ModelPrice? For(string providerId, string model) =>
        _prices.GetValueOrDefault((providerId, model));
}

/// <summary>
/// What one turn cost. <see cref="Priced"/> is false when the model has no price-table entry,
/// which keeps a running total honest instead of quietly treating an unknown model as free.
/// </summary>
public sealed record TurnCost(LlmUsage Usage, decimal Dollars, bool Priced)
{
    public static TurnCost Unpriced(LlmUsage usage) => new(usage, 0m, Priced: false);
}

/// <summary>
/// Per-turn usage and a running total (list.md Phase 3, "LLM Turn Price").
/// <para>
/// Also the regression detector the checklist asks for: a profile switch is the only sanctioned
/// cause of a cold prefix, so a turn that writes cache without one is counted as unexplained
/// rather than absorbed into the total unnoticed.
/// </para>
/// </summary>
public sealed class SpendTracker
{
    private readonly List<TurnCost> _turns = [];

    public decimal RunningTotalDollars { get; private set; }

    public int TurnCount => _turns.Count;

    /// <summary>
    /// Cold prefixes with no sanctioned cause. Non-zero means caching is being defeated by
    /// something — non-deterministic tool schemas, a mutated descriptor, a prompt whose bytes
    /// vary per turn.
    /// </summary>
    public int UnexplainedColdPrefixes { get; private set; }

    public TurnCost? Last => _turns.Count > 0 ? _turns[^1] : null;

    /// <summary>
    /// <paramref name="coldPrefixExpected"/> is true for the first turn of a session and for the
    /// turn after a model or provider change — the cases where writing cache is correct.
    /// </summary>
    public void Record(TurnCost cost, bool coldPrefixExpected)
    {
        _turns.Add(cost);
        RunningTotalDollars += cost.Dollars;

        if (cost.Usage.CacheCreationInputTokens > 0 && !coldPrefixExpected)
        {
            UnexplainedColdPrefixes++;
        }
    }
}

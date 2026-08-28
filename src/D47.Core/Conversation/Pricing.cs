namespace D47.Core.Conversation;

/// <summary>
/// Per-million-token rates for one model. Cache rates are derived rather than listed: writing
/// costs 1.25x input at the default 5-minute TTL and reading costs 0.1x, so quoting them
/// separately would be two more numbers to keep in step with the first.
/// <para>
/// <b>Those two factors are Anthropic's terms, and were the defaults before anybody else was
/// reachable</b> (Phase 29). They stay the defaults, so no Anthropic row changes and no
/// existing test moves — but they are per-row now, because OpenAI charges <em>nothing</em> to
/// write a cache entry and discounts reads by its own factor. Left derived, every OpenAI turn
/// would be invoiced for a cache write that does not exist, upward and silently.
/// </para>
/// <para>
/// Factors rather than two more dollar figures, which keeps the reasoning above intact: a row
/// that changes its input rate does not then have to remember to change three numbers, and a
/// provider that bills cache writes at nothing says so as a zero rather than as a rate that has
/// to be kept at zero.
/// </para>
/// </summary>
public sealed record ModelPrice(decimal InputPerMillion, decimal OutputPerMillion)
{
    /// <summary>What writing a cache entry costs, as a multiple of the input rate.</summary>
    public decimal CacheWriteFactor { get; init; } = 1.25m;

    /// <summary>What reading one costs, as a multiple of the input rate.</summary>
    public decimal CacheReadFactor { get; init; } = 0.1m;

    public decimal CacheWritePerMillion => InputPerMillion * CacheWriteFactor;

    public decimal CacheReadPerMillion => InputPerMillion * CacheReadFactor;

    /// <summary>
    /// What one server-side web search costs, on top of the tokens its results become. Listed
    /// at $10 per 1,000 searches, so a penny each.
    /// <para>
    /// A flat rate rather than a per-model one because it is not a model rate: every model that
    /// can search reaches the same service at the same published price. It sits here because
    /// this is where a turn is priced, and it is worth stating what a penny means next to these
    /// other numbers — <b>one search costs more than a whole Haiku turn</b> of a few hundred
    /// tokens each way. That is why it is counted rather than rounded away.
    /// </para>
    /// </summary>
    public const decimal DollarsPerWebSearch = 0.01m;

    public decimal DollarsFor(LlmUsage usage) =>
        ((usage.InputTokens * InputPerMillion
          + usage.OutputTokens * OutputPerMillion
          + usage.CacheCreationInputTokens * CacheWritePerMillion
          + usage.CacheReadInputTokens * CacheReadPerMillion) / 1_000_000m)
        + (usage.WebSearchRequests * DollarsPerWebSearch);
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

        // OpenAI list prices, read from developers.openai.com/api/docs/pricing on 2026-08-18
        // rather than written from memory — the plan of record required that, and it was right
        // to: two of the things it expected to find were no longer true. Cache reads discount to
        // a tenth across this family, which happens to be Anthropic's factor; cache writes bill
        // at 1.25x, which also happens to be Anthropic's factor, but only from GPT-5.6 on.
        //
        // The write factor is stated on every row anyway, including the rows where it is zero,
        // because "the default is right here" is exactly the reasoning that made these numbers
        // Anthropic's in the first place.
        [("openai", "gpt-5.6-sol")] = new(5m, 30m) { CacheReadFactor = 0.1m },
        [("openai", "gpt-5.6-terra")] = new(2m, 12m) { CacheReadFactor = 0.1m },
        [("openai", "gpt-5.6-luna")] = new(0.20m, 1.20m) { CacheReadFactor = 0.1m },

        // Before GPT-5.6, a cache write is free — and is also not counted, so this factor never
        // multiplies anything. Zero rather than 1.25 all the same: the day one of these rows is
        // copied to make a new one, the wrong half is the half nobody looked at.
        [("openai", "gpt-5.5")] = new(5m, 30m) { CacheReadFactor = 0.1m, CacheWriteFactor = 0m },
        [("openai", "gpt-5.4")] = new(2.50m, 15m) { CacheReadFactor = 0.1m, CacheWriteFactor = 0m },
        [("openai", "gpt-5.4-mini")] = new(0.75m, 4.50m) { CacheReadFactor = 0.1m, CacheWriteFactor = 0m },
        [("openai", "gpt-5.4-nano")] = new(0.20m, 1.25m) { CacheReadFactor = 0.1m, CacheWriteFactor = 0m },
        [("openai", "gpt-5")] = new(1.25m, 10m) { CacheReadFactor = 0.1m, CacheWriteFactor = 0m },
        [("openai", "gpt-5-mini")] = new(0.25m, 2m) { CacheReadFactor = 0.1m, CacheWriteFactor = 0m },
        [("openai", "gpt-5-nano")] = new(0.05m, 0.40m) { CacheReadFactor = 0.1m, CacheWriteFactor = 0m },
    });

    /// <summary>
    /// Every turn free, which is what a model running on the Commander's own machine costs.
    /// <para>
    /// Not a row in the table above, because the table is keyed by model id and the thing being
    /// priced here is an <em>address</em> — no id is known in advance and none needs to be. The
    /// caller decides an endpoint is loopback and asks for this; see
    /// <see cref="LocalEndpoint.IsLoopback"/>, which is where that judgement lives.
    /// </para>
    /// <para>
    /// An unknown model stays unpriced, which is the honest existing behaviour. A model served
    /// from 127.0.0.1 is not unknown — it is free, and reporting "unknown" about it on every turn
    /// forever is noise pretending to be rigour. The host is evidence rather than a guess.
    /// </para>
    /// </summary>
    public static ModelPrice Free { get; } = new(0m, 0m) { CacheReadFactor = 0m, CacheWriteFactor = 0m };

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
/// What the provider said about caching for one turn, in terms that mean the same thing whoever
/// answered it (Phase 29, seam 3).
/// </summary>
public enum PrefixWarmth
{
    /// <summary>
    /// The turn cannot be read either way — no usage came back, or the endpoint does not cache
    /// at all. Not counted, because an instrument that is not measuring must not report a
    /// reading.
    /// </summary>
    Unknown,

    /// <summary>
    /// The prefix was not there: cache was written, or nothing was read when a cacheable prefix
    /// should have been waiting.
    /// </summary>
    Cold,

    /// <summary>The prefix was found and read.</summary>
    Warm,
}

/// <summary>
/// Per-turn usage and a running total (Phase 3, "LLM Turn Price").
/// <para>
/// Also the regression detector the checklist asks for: a profile switch is the only sanctioned
/// cause of a cold prefix, so a turn that writes cache without one is counted as unexplained
/// rather than absorbed into the total unnoticed.
/// </para>
/// </summary>
/// <param name="ledger">
/// Where charges are kept between runs, or null for a tracker that only knows this session —
/// the replay harness, and every test that is not about history. What this class counts is
/// still session-scoped: the running total is what has been spent since launch, and the ledger
/// answers the longer questions beside it rather than changing what these properties mean.
/// </param>
public sealed class SpendTracker(SpendLedger? ledger = null)
{
    private readonly List<TurnCost> _turns = [];

    public decimal RunningTotalDollars { get; private set; }

    public int TurnCount => _turns.Count;

    /// <summary>
    /// Cold prefixes with no sanctioned cause. Non-zero means caching is being defeated by
    /// something — non-deterministic tool schemas, a mutated descriptor, a prompt whose bytes
    /// vary per turn.
    /// <para>
    /// <b>What counts as cold had to stop being one provider's evidence for it</b> (Phase 29).
    /// This counted turns that <em>wrote</em> cache, which is Anthropic's way of
    /// showing a cold prefix and is not universal: a provider that never reports a write cannot
    /// trip it, so the counter would have sat at zero on OpenAI and read as <em>caching is
    /// perfect</em> rather than as <em>this instrument is not measuring here</em> — the more
    /// expensive of the two failures, because nobody investigates good news.
    /// </para>
    /// <para>
    /// So the caller states the warmth and this counts it. The inverse signal is the other half:
    /// a turn that read <em>nothing</em> when the prefix had not changed is a cold prefix
    /// whether or not anybody billed for a write.
    /// </para>
    /// </summary>
    public int UnexplainedColdPrefixes { get; private set; }

    /// <summary>
    /// Turns whose caching could not be read at all — no usage block, or an endpoint that does
    /// not cache. Reported beside the count above so a zero can be told apart from a zero
    /// nobody was in a position to measure.
    /// </summary>
    public int UnmeasuredPrefixes { get; private set; }

    public TurnCost? Last => _turns.Count > 0 ? _turns[^1] : null;

    /// <summary>
    /// <paramref name="coldPrefixExpected"/> is true for the first turn of a session and for the
    /// turn after a model or provider change — the cases where writing cache is correct.
    /// <para>
    /// The provider and model are taken here rather than derived later because this is the last
    /// place that knows them for certain. A row is stamped with what actually answered, not with
    /// whatever is selected by the time somebody opens the dialog.
    /// </para>
    /// </summary>
    /// <param name="warmth">
    /// What the turn showed about its prefix. Defaulted to <see cref="PrefixWarmth.Unknown"/>,
    /// which falls back to reading a cache write as cold — the rule this had before there was
    /// anyone but Anthropic to ask, and the right answer for a caller that has not been taught
    /// to work it out. A caller that passes a real value is believed instead.
    /// </param>
    public void Record(
        TurnCost cost,
        bool coldPrefixExpected,
        string providerId = "",
        string model = "",
        PrefixWarmth warmth = PrefixWarmth.Unknown)
    {
        _turns.Add(cost);
        RunningTotalDollars += cost.Dollars;

        // An uninformed caller gets the original rule — a cache write with no sanctioned cause —
        // which is still correct for the provider it was written against. An informed one has
        // already reconciled the two signals and is taken at its word.
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

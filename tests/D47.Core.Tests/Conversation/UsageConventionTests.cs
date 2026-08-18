using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// The seam between two providers that count the same turn differently (list.md Phase 29, seam 1).
/// <para>
/// Anthropic's <c>input_tokens</c> excludes what was cached; OpenAI's <c>prompt_tokens</c>
/// includes it. Every test here exists because that difference produces a <em>plausible</em>
/// number rather than an error, so nothing downstream would ever have reported it.
/// </para>
/// </summary>
public class UsageConventionTests
{
    /// <summary>
    /// One turn, counted both ways, priced once. This is the guard the plan of record asked for
    /// and it is the whole point of the conversion: 200 tokens fresh, 400 written to cache, 2,000
    /// read from it, 150 out.
    /// <para>
    /// Anthropic reports that as (200, 400, 2000) and OpenAI as a prompt of 2,600 with 2,000
    /// cached and 400 written. Left unconverted, <c>TotalInputTokens</c> reads 2,600 on one and
    /// 5,000 on the other — for the same work.
    /// </para>
    /// </summary>
    [Fact]
    public void TheSameTurnPricesIdenticallyWhoeverCountedIt()
    {
        var anthropicShaped = new LlmUsage(InputTokens: 200, OutputTokens: 150, CacheCreationInputTokens: 400, CacheReadInputTokens: 2_000);

        var openAiShaped = LlmUsage.FromInclusiveInput(
            promptTokens: 2_600,
            cachedInputTokens: 2_000,
            cacheWriteTokens: 400,
            outputTokens: 150);

        Assert.Equal(anthropicShaped, openAiShaped);
        Assert.Equal(2_600, openAiShaped.TotalInputTokens);

        // And through the thing that actually bills. Same rates on both sides, so any difference
        // left in the token split shows up here as money.
        var price = new ModelPrice(3m, 15m);
        Assert.Equal(price.DollarsFor(anthropicShaped), price.DollarsFor(openAiShaped));
    }

    /// <summary>
    /// The plan of record expected no cache-write count on this path at all. There is one from
    /// GPT-5.6 on, and dropping it does not fail — it moves 400 tokens from the write rate to the
    /// uncached rate and bills them at 1.25x nothing extra, which is a number that looks fine.
    /// </summary>
    [Fact]
    public void WrittenTokensAreNotLeftInTheUncachedCount()
    {
        var usage = LlmUsage.FromInclusiveInput(2_600, cachedInputTokens: 2_000, cacheWriteTokens: 400, outputTokens: 150);

        Assert.Equal(200, usage.InputTokens);
        Assert.Equal(400, usage.CacheCreationInputTokens);
        Assert.Equal(2_000, usage.CacheReadInputTokens);
    }

    /// <summary>
    /// An endpoint reporting no cache fields at all — every Chat Completions server, and every
    /// OpenAI model before GPT-5.6. The whole prompt is uncached, which is the truth there.
    /// </summary>
    [Fact]
    public void AnEndpointThatReportsNoCachingCountsTheWholePrompt()
    {
        var usage = LlmUsage.FromInclusiveInput(2_600, 0, 0, 150);

        Assert.Equal(2_600, usage.InputTokens);
        Assert.Equal(2_600, usage.TotalInputTokens);
        Assert.Equal(0, usage.CacheCreationInputTokens);
    }

    /// <summary>
    /// A server whose numbers do not add up cannot produce a negative charge. This phase is what
    /// makes a stranger's endpoint reachable, and nothing on the other end of it is trusted.
    /// </summary>
    [Fact]
    public void NonsenseFromTheEndpointCannotBillBelowZero()
    {
        var overCached = LlmUsage.FromInclusiveInput(100, cachedInputTokens: 900, cacheWriteTokens: 900, outputTokens: 10);

        Assert.Equal(0, overCached.InputTokens);
        Assert.Equal(100, overCached.TotalInputTokens);
        Assert.True(new ModelPrice(3m, 15m).DollarsFor(overCached) >= 0m);

        var negative = LlmUsage.FromInclusiveInput(-5, -5, -5, -5);
        Assert.Equal(LlmUsage.None, negative);
    }

    /// <summary>
    /// The default factors are Anthropic's and stay Anthropic's, so no existing row moves. A row
    /// that declares its own is the only thing that differs.
    /// </summary>
    [Fact]
    public void CacheFactorsDefaultToAnthropicsTerms()
    {
        var anthropic = new ModelPrice(3m, 15m);

        Assert.Equal(3.75m, anthropic.CacheWritePerMillion);
        Assert.Equal(0.30m, anthropic.CacheReadPerMillion);

        var noWriteCharge = new ModelPrice(2m, 12m) { CacheWriteFactor = 0m, CacheReadFactor = 0.1m };

        Assert.Equal(0m, noWriteCharge.CacheWritePerMillion);
        Assert.Equal(0.20m, noWriteCharge.CacheReadPerMillion);
    }

    /// <summary>
    /// Loopback is an address, not a model id — which is why it is a price rather than a table
    /// row. Nothing about it is unknown, so nothing about it should read as unknown.
    /// </summary>
    [Theory]
    [InlineData("http://127.0.0.1:11434", true)]
    [InlineData("http://127.0.0.2:8080/v1", true)]
    [InlineData("http://localhost:1234/v1", true)]
    [InlineData("http://ollama.localhost:11434", true)]
    [InlineData("http://[::1]:8000/v1", true)]
    [InlineData("https://api.openai.com", false)]
    [InlineData("http://192.168.1.40:11434", false)]
    [InlineData("https://localhost.example.com", false)]
    [InlineData("not a url", false)]
    [InlineData(null, false)]
    public void OnlyThisMachineCountsAsThisMachine(string? endpoint, bool expected) =>
        Assert.Equal(expected, LocalEndpoint.IsLoopback(endpoint));

    /// <summary>A turn on a machine the Commander owns costs nothing, and says so.</summary>
    [Fact]
    public void ALoopbackTurnIsFreeRatherThanUnknown()
    {
        var usage = LlmUsage.FromInclusiveInput(4_000, 3_000, 0, 200);

        Assert.Equal(0m, PriceTable.Free.DollarsFor(usage));
    }

    /// <summary>
    /// The cold-prefix detector, made to mean the same thing whoever answered (seam 3). It counted
    /// turns that <em>wrote</em> cache, which is one provider's evidence for a cold prefix and not
    /// universal — so on an endpoint that never reports a write it would have sat at zero and read
    /// as caching being perfect rather than as the instrument not measuring.
    /// </summary>
    [Fact]
    public void AnUnexplainedColdPrefixIsCountedWhicheverSignalShowedIt()
    {
        var wrote = new SpendTracker();
        var readNothing = new SpendTracker();

        // Anthropic's signal: a cache write with no sanctioned cause.
        wrote.Record(
            new TurnCost(new LlmUsage(200, 50, 4_000, 0), 0.1m, Priced: true),
            coldPrefixExpected: false,
            warmth: PrefixWarmth.Cold);

        // The inverse signal, which is the only one available on a provider that bills no write.
        readNothing.Record(
            new TurnCost(new LlmUsage(4_200, 50, 0, 0), 0.1m, Priced: true),
            coldPrefixExpected: false,
            warmth: PrefixWarmth.Cold);

        Assert.Equal(1, wrote.UnexplainedColdPrefixes);
        Assert.Equal(1, readNothing.UnexplainedColdPrefixes);
    }

    /// <summary>
    /// Not measuring is its own answer and is kept apart from measuring zero. Folding the two
    /// together is the failure the plan of record called the more expensive one, because nobody
    /// investigates good news.
    /// </summary>
    [Fact]
    public void ATurnNobodyCouldMeasureIsNotCountedAsAGoodOne()
    {
        var tracker = new SpendTracker();

        tracker.Record(
            TurnCost.Unpriced(LlmUsage.Unreported),
            coldPrefixExpected: false,
            warmth: PrefixWarmth.Unknown);

        tracker.Record(
            new TurnCost(new LlmUsage(200, 50, 0, 4_000), 0.1m, Priced: true),
            coldPrefixExpected: false,
            warmth: PrefixWarmth.Warm);

        Assert.Equal(0, tracker.UnexplainedColdPrefixes);
        Assert.Equal(1, tracker.UnmeasuredPrefixes);
    }

    /// <summary>
    /// A caller that has not been taught to work the warmth out gets the original rule, so no
    /// existing behaviour moved when the parameter arrived.
    /// </summary>
    [Fact]
    public void AnUninformedCallerStillGetsTheOriginalRule()
    {
        var tracker = new SpendTracker();

        tracker.Record(new TurnCost(new LlmUsage(200, 50, 4_000, 0), 0.1m, true), coldPrefixExpected: false);
        tracker.Record(new TurnCost(new LlmUsage(200, 50, 4_000, 0), 0.1m, true), coldPrefixExpected: true);
        tracker.Record(new TurnCost(new LlmUsage(200, 50, 0, 4_000), 0.1m, true), coldPrefixExpected: false);

        Assert.Equal(1, tracker.UnexplainedColdPrefixes);
        Assert.Equal(0, tracker.UnmeasuredPrefixes);
    }
}

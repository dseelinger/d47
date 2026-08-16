using D47.Core.Audio;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// A running total for paid speech, beside what the model costs (list.md Phase 19).
/// <para>
/// The two claims this makes are of different kinds and the tests are arranged around that:
/// the character count is measured, and the dollar figure is an assumption the Commander can
/// correct. Anything that lets the second masquerade as the first is the bug.
/// </para>
/// </summary>
public class WhatTheVoicesCostTests
{
    private const string Edge = TtsProviderCatalog.EdgeId;
    private const string Eleven = TtsProviderCatalog.ElevenLabsId;

    private static D47Settings On(string provider, double? price = null) => new()
    {
        Speech = new SpeechSettings
        {
            Provider = provider,
            CharacterPrices = price is { } rate
                ? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { [provider] = rate }
                : new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
        },
    };

    [Fact]
    public void NothingSpokenSaysNothing()
    {
        // A line about a subsystem that has done nothing is a line nobody needs on a panel meant
        // to sit beside a running game.
        Assert.Null(new SpeechSpend().Describe(On(Eleven)));
    }

    [Fact]
    public void TheUnitIsCharactersAndNotTokens()
    {
        var spend = new SpeechSpend();
        spend.Record(Eleven, "Frame shift charged.".Length);

        Assert.Contains("20 characters", spend.Describe(On(Eleven))!, StringComparison.Ordinal);
        Assert.Equal(20, spend.TotalCharacters);
    }

    /// <summary>
    /// $0.10 per thousand is the published list price for the model d47 pins, so 20,000
    /// characters is $2.00 — arithmetic anybody can check, which is the point of quoting the
    /// rate on the row.
    /// </summary>
    [Fact]
    public void DollarsAreCharactersTimesTheRate()
    {
        var spend = new SpeechSpend();
        spend.Record(Eleven, 20_000);

        Assert.Contains("$2.00", spend.Describe(On(Eleven))!, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCommandersOwnRateBeatsTheListPrice()
    {
        var spend = new SpeechSpend();
        spend.Record(Eleven, 20_000);

        // A subscription bundles credits at a different effective rate and the API reports
        // neither the tier nor how much of the bundle is left, so this row is the only way the
        // figure can be true for a particular account.
        Assert.Contains("$3.60", spend.Describe(On(Eleven, price: 0.18))!, StringComparison.Ordinal);
    }

    /// <summary>
    /// The distinction the item is built around. "$0.00" from Edge and "$0.00" from an
    /// ElevenLabs run nobody has priced are the same string for opposite reasons.
    /// </summary>
    [Fact]
    public void AProviderThatCostsNothingReadsAsFreeRatherThanAsZero()
    {
        var spend = new SpeechSpend();
        spend.Record(Edge, 5_000);

        var said = spend.Describe(On(Edge))!;

        Assert.Contains("free", said, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("$0.00", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every provider that charges publishes a rate, today — so the "count with no price"
    /// wording is unreachable and is kept for the next one that does not.
    /// <para>
    /// Asserted rather than assumed, because the alternative is a branch nobody notices has gone
    /// live: a provider added with <c>Billed = true</c> and no list price would start quoting a
    /// character count with no explanation of why there is no money beside it, and this is the
    /// test that says so at the moment it is added.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryProviderThatChargesPublishesARateOrTheWordingSaysSo()
    {
        Assert.All(
            TtsProviderCatalog.All.Where(provider => provider.Billed),
            provider => Assert.NotNull(provider.ListDollarsPerThousandCharacters));
    }

    [Fact]
    public void AnUnknownProviderIdIsPricedAsTheOneItResolvesTo()
    {
        // TtsProviderCatalog resolves an id d47 does not ship to Edge, everywhere. A rate lookup
        // that did otherwise would quote money for a provider the rest of the app treats as free.
        Assert.Null(SpeechSpend.RateFor(On(Edge), "festival"));
    }

    [Fact]
    public void ASessionThatCrossedASwitchIsReportedPerProvider()
    {
        var spend = new SpeechSpend();
        spend.Record(Edge, 1_806);
        spend.Record(Eleven, 20_000);

        var said = spend.Describe(On(Eleven))!;

        // One total that mixed a free provider's characters with a paid one's is a figure that
        // means nothing. Both are named, and both carry their own answer about money.
        Assert.Contains("21,806 characters", said, StringComparison.Ordinal);
        Assert.Contains("Edge Neural 1,806 (Edge Neural is free)", said, StringComparison.Ordinal);
        Assert.Contains("ElevenLabs 20,000 ($2.00", said, StringComparison.Ordinal);
    }

    [Fact]
    public void UtterancesAreCountedBecauseThereIsNoCaching()
    {
        var spend = new SpeechSpend();

        // The same sentence twice is billed twice, which is worth surfacing if a callout ever
        // repeats itself in a loop.
        spend.Record(Eleven, 20);
        spend.Record(Eleven, 20);

        Assert.Equal(2, spend.Utterances);
        Assert.Equal(40, spend.TotalCharacters);
    }

    [Fact]
    public void ARecordOfNothingIsNotAnUtterance()
    {
        var spend = new SpeechSpend();
        spend.Record(Eleven, 0);

        Assert.Equal(0, spend.Utterances);
        Assert.Null(spend.Describe(On(Eleven)));
    }

    [Fact]
    public void TheListPriceIsWhatTheProviderPublishes()
    {
        // Read from elevenlabs.io/pricing/api on 2026-08-16 for eleven_multilingual_v2, which is
        // the model ElevenLabsTtsProvider pins. If that pin ever moves, this figure moves with it.
        Assert.Equal(0.10m, TtsProviderCatalog.ElevenLabs.ListDollarsPerThousandCharacters);
        Assert.True(TtsProviderCatalog.ElevenLabs.Billed);

        Assert.Null(TtsProviderCatalog.Edge.ListDollarsPerThousandCharacters);
        Assert.False(TtsProviderCatalog.Edge.Billed);
        Assert.False(TtsProviderCatalog.None.Billed);
    }
}

/// <summary>
/// Counting happens at the seam and only on synthesis that succeeded (list.md Phase 19).
/// </summary>
public class SpeechIsCountedAtTheSeamTests
{
    private static ITtsProvider Metered(SpeechSpend spend, FakeTtsProvider? inner = null) =>
        new MeteredTtsProvider(inner ?? new FakeTtsProvider(), spend);

    [Fact]
    public async Task WhatWasHandedOverIsWhatIsCounted()
    {
        var spend = new SpeechSpend();

        await Metered(spend).SynthesizeAsync(
            "Frame shift charged.", VoiceSelection.Default, TestContext.Current.CancellationToken);

        Assert.Equal(20, spend.TotalCharacters);
    }

    [Fact]
    public async Task ARefusedRequestCostsNothing()
    {
        var spend = new SpeechSpend();
        var provider = Metered(spend, new FakeTtsProvider { FailOn = "boom" });

        await Assert.ThrowsAsync<TtsException>(() => provider.SynthesizeAsync(
            "boom goes the module", VoiceSelection.Default, TestContext.Current.CancellationToken));

        Assert.Equal(0, spend.TotalCharacters);
    }

    /// <summary>
    /// A voice the provider will not accept is the same case and worth its own line: it is a
    /// failure d47 recovers from by itself, and a recovery that quietly billed for the attempt
    /// would put a charge on the Commander's account for a sentence they never heard.
    /// </summary>
    [Fact]
    public async Task ARefusedVoiceCostsNothing()
    {
        var spend = new SpeechSpend();
        var provider = Metered(spend, new FakeTtsProvider { Refuses = "en-US-RogerNeural" });

        await Assert.ThrowsAsync<TtsException>(() => provider.SynthesizeAsync(
            "anything at all",
            new VoiceSelection("en-US-RogerNeural"),
            TestContext.Current.CancellationToken));

        Assert.Equal(0, spend.TotalCharacters);
    }

    /// <summary>
    /// A turn cut off by the shut-up hotkey has already paid for the sentences that were
    /// synthesised before it — which is the case that makes "count what was sent" different from
    /// "count what was said", and the one worth a test.
    /// </summary>
    [Fact]
    public async Task ATurnCutOffStillPaysForWhatWasAlreadySent()
    {
        var spend = new SpeechSpend();
        var inner = new FakeTtsProvider();
        var provider = Metered(spend, inner);

        await provider.SynthesizeAsync("First sentence.", VoiceSelection.Default, TestContext.Current.CancellationToken);
        await provider.SynthesizeAsync("Second sentence.", VoiceSelection.Default, TestContext.Current.CancellationToken);

        // And then the Commander presses the key: the third never leaves.
        using var stopped = new CancellationTokenSource();
        await stopped.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.SynthesizeAsync(
            "Third sentence, which nobody hears.", VoiceSelection.Default, stopped.Token));

        Assert.Equal("First sentence.".Length + "Second sentence.".Length, spend.TotalCharacters);
        Assert.Equal(2, spend.Utterances);
    }

    [Fact]
    public async Task ListingVoicesIsNotSpeakingAndIsNotBilled()
    {
        var spend = new SpeechSpend();

        await Metered(spend).ListVoicesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, spend.TotalCharacters);
    }

    [Fact]
    public void TheIdIsTheWrappedProvidersOwn()
    {
        // What is counted has to be filed under the provider that will bill for it, not under a
        // decorator — the whole report is per provider.
        Assert.Equal("fake", Metered(new SpeechSpend()).Id);
    }
}

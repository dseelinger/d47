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

    private static D47Settings PerMinute(string provider, double? price = null) => new()
    {
        Speech = new SpeechSettings
        {
            Provider = provider,
            MinutePrices = price is { } rate
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

        Assert.Contains("$1.00", spend.Describe(On(Eleven))!, StringComparison.Ordinal);
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
    /// Every provider that charges now has a rate, in whichever unit it bills in
    /// (<a href="https://github.com/dseelinger/d47/issues/63">#63</a>).
    /// <para>
    /// <b>This test has now been three things, and the sequence is the point.</b> It first
    /// asserted every billed provider published a rate, and named its own trigger: a provider
    /// added with <c>Billed = true</c> and no list price. OpenAI was that provider (list.md Phase
    /// 58) and it fired exactly as written. It then asserted the opposite — that such a provider
    /// says "no rate set" rather than quoting zero — because OpenAI publishes per minute and d47
    /// counted characters, and the conversion between them moves about 40% with the content of
    /// the line.
    /// </para>
    /// <para>
    /// <b>That second reading was true and was answering the wrong question.</b> The 40% spread
    /// only mattered while d47 had to turn characters into minutes, and it never had to: it holds
    /// the audio, so it knows each clip's length to the sample. The gap was a measurement being
    /// computed and discarded, not a conversion that could not be made.
    /// </para>
    /// <para>
    /// So the assertion returns to its first form, with the unit no longer assumed. The wording it
    /// used to guard is still live and still reachable — a provider added tomorrow with neither
    /// rate set trips this, which is what it is for.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryBilledProviderHasARateInTheUnitItBillsIn()
    {
        var unpriced = TtsProviderCatalog.All
            .Where(provider => provider.Billed)
            .Where(provider => provider.BilledByMinute
                ? provider.ListDollarsPerMinute is null
                : provider.ListDollarsPerThousandCharacters is null)
            .Select(provider => provider.Name)
            .ToList();

        Assert.True(
            unpriced.Count == 0,
            $"Billed with no rate in the unit they bill in: {string.Join(", ", unpriced)}. Set "
            + "ListDollarsPerThousandCharacters or ListDollarsPerMinute, or say in a comment why "
            + "neither can be known.");
    }

    /// <summary>
    /// The measurement that was already being computed and thrown away. A minute-billed provider
    /// is priced from the length of the audio, which d47 has to the sample, rather than from the
    /// characters it handed over.
    /// </summary>
    [Fact]
    public void AMinuteBilledProviderIsPricedFromTheAudioItProduced()
    {
        var spend = new SpeechSpend();

        // Two minutes of audio, from a line whose character count is nothing like proportional
        // to it — which is the whole reason characters were the wrong measure.
        spend.Record(TtsProviderCatalog.OpenAiId, 900, group: null, audio: TimeSpan.FromMinutes(2));

        var said = spend.Describe(PerMinute(TtsProviderCatalog.OpenAiId, price: 0.015))!;

        Assert.DoesNotContain("no rate set", said, StringComparison.Ordinal);

        // 2 minutes at $0.015 is $0.03, and nothing about the 900 characters enters into it.
        Assert.Contains("0.03", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// The two units never cross. A per-minute rate multiplied by a character count, or the
    /// reverse, is the failure that forcing one unit on both providers would produce.
    /// </summary>
    [Fact]
    public void ACharacterRateIsNeverAppliedToAMinuteBilledProvider()
    {
        var spend = new SpeechSpend();
        spend.Record(TtsProviderCatalog.OpenAiId, 10_000, group: null, audio: TimeSpan.FromMinutes(1));

        // A character price set for a provider that does not bill by the character is ignored
        // rather than multiplied by ten thousand.
        var said = spend.Describe(On(TtsProviderCatalog.OpenAiId, price: 0.05))!;

        // One minute at the published $0.015, not $0.50.
        Assert.Contains("0.015", said, StringComparison.Ordinal);
        Assert.DoesNotContain("0.50", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// And the Commander can still correct it, which is what makes a published figure a starting
    /// point rather than a claim. It matters more here than for ElevenLabs: OpenAI publishes no
    /// per-minute rate at all, so the default is a proxy derived from the token rate they do
    /// publish.
    /// </summary>
    [Fact]
    public void AndTheCommanderCanSetOneThemselves()
    {
        var spend = new SpeechSpend();
        spend.Record(TtsProviderCatalog.OpenAiId, 10_000, group: null, audio: TimeSpan.FromMinutes(10));

        var said = spend.Describe(PerMinute(TtsProviderCatalog.OpenAiId, price: 0.02))!;

        Assert.DoesNotContain("no rate set", said, StringComparison.Ordinal);
        Assert.Contains("0.20", said, StringComparison.Ordinal);
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
        Assert.Contains("ElevenLabs 20,000 ($1.00", said, StringComparison.Ordinal);
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
        // Read from elevenlabs.io/pricing/api for eleven_flash_v2_5, which is the model
        // ElevenLabsTtsProvider pins. If that pin ever moves, this figure moves with it — and it
        // has moved twice: to Turbo 2.5 on 2026-08-16 for language enforcement, which halved the
        // rate, and to Flash 2.5 on 2026-08-25 because ElevenLabs deprecated Turbo. Flash is
        // billed at the same $0.05, so the figure survived the second move unchanged.
        Assert.Equal(0.05m, TtsProviderCatalog.ElevenLabs.ListDollarsPerThousandCharacters);
        Assert.True(TtsProviderCatalog.ElevenLabs.Billed);

        Assert.Null(TtsProviderCatalog.Edge.ListDollarsPerThousandCharacters);
        Assert.False(TtsProviderCatalog.Edge.Billed);
        Assert.False(TtsProviderCatalog.None.Billed);
    }
    /// <summary>
    /// Which slot is costing money, which is a question nobody could ask before six of them could
    /// name six providers (list.md Phase 57).
    /// </summary>
    [Fact]
    public void TheCharactersBreakDownBySlot()
    {
        var spend = new SpeechSpend();

        spend.Record(Eleven, 1_000, VoiceGroup.Aboard);
        spend.Record(Edge, 4_000, VoiceGroup.AnyoneInRange);
        spend.Record(Edge, 250, VoiceGroup.Npcs);

        var slots = spend.BySlot;

        Assert.Equal(3, slots.Count);
        Assert.Equal(VoiceGroup.AnyoneInRange, slots[0].Group);
        Assert.Equal(4_000, slots[0].Characters);
    }

    /// <summary>
    /// The two views sum the same rows rather than keeping two tallies, so they cannot drift. Two
    /// counters for one fact are two counters that eventually disagree about a bill.
    /// </summary>
    [Fact]
    public void AndTheProviderTotalStillAgreesWithItself()
    {
        var spend = new SpeechSpend();

        spend.Record(Edge, 4_000, VoiceGroup.AnyoneInRange);
        spend.Record(Edge, 250, VoiceGroup.Npcs);
        spend.Record(Edge, 100, VoiceGroup.Carrier);

        var edge = Assert.Single(spend.Charges);

        Assert.Equal(4_350, edge.Characters);
        Assert.Equal(3, edge.Utterances);
        Assert.Equal(spend.BySlot.Sum(charge => charge.Characters), edge.Characters);
    }

    [Fact]
    public void OneSlotOnAPaidProviderIsPricedAndTheFreeOnesAreNamedAsFree()
    {
        var spend = new SpeechSpend();

        spend.Record(Eleven, 1_000, VoiceGroup.Aboard);
        spend.Record(Edge, 20_000, VoiceGroup.AnyoneInRange);

        var said = spend.DescribeSlots(On(Eleven));

        Assert.NotNull(said);
        Assert.Contains("Anyone in range", said, StringComparison.Ordinal);
        Assert.Contains("Edge Neural is free", said, StringComparison.Ordinal);
        Assert.Contains("Aboard", said, StringComparison.Ordinal);
    }

    [Fact]
    public void NothingSpokenSaysNothingPerSlotEither() =>
        Assert.Null(new SpeechSpend().DescribeSlots(On(Eleven)));

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

using D47.Core.Audio;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>A running total for paid speech, beside what the model costs.</summary>
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
        // A line about a subsystem that has done nothing is a line nobody needs on a panel meant to sit
        // beside a running game.
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
    /// $0.10 per thousand is the published list price for the model d47 pins, so 20,000 characters is
    /// $2.00 — arithmetic anybody can check, which is the point of quoting the rate on the row.
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

        // A subscription bundles credits at a different effective rate and the API reports neither the tier
        // nor how much of the bundle is left, so this row is the only way the figure can be true for a
        // particular account.
        Assert.Contains("$3.60", spend.Describe(On(Eleven, price: 0.18))!, StringComparison.Ordinal);
    }

    /// <summary>
    /// The distinction the item is built around. "$0.00" from Edge and "$0.00" from an ElevenLabs run
    /// nobody has priced are the same string for opposite reasons.
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

    [Fact]
    public void EveryBilledProviderHasARateInTheUnitItBillsIn()
    {
        var unpriced = TtsProviderCatalog.All
            .Where(provider => provider.Billed)
            .Where(provider => provider.NoRateBecause is null)
            .Where(provider => provider.BilledByMinute
                ? provider.ListDollarsPerMinute is null
                : provider.ListDollarsPerThousandCharacters is null)
            .Select(provider => provider.Name)
            .ToList();

        Assert.True(
            unpriced.Count == 0,
            $"Billed with no rate in the unit they bill in: {string.Join(", ", unpriced)}. Set "
            + "ListDollarsPerThousandCharacters or ListDollarsPerMinute, or say in NoRateBecause "
            + "why neither can be known.");
    }

    /// <summary>The wording the escape above depends on.</summary>
    [Fact]
    public void AProviderThatCannotBePricedSaysSoRatherThanQuotingZero()
    {
        var unpriced = TtsProviderCatalog.All
            .Where(provider => provider is { Billed: true, NoRateBecause: not null })
            .ToList();

        Assert.NotEmpty(unpriced);

        foreach (var provider in unpriced)
        {
            var spend = new SpeechSpend();
            spend.Record(provider.Id, 4_000, group: null, audio: TimeSpan.FromMinutes(1));

            var said = spend.Describe(new D47Settings())!;

            Assert.Contains("no rate set", said, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("$0.00", said, StringComparison.Ordinal);
        }
    }

    /// <summary>The measurement that was already being computed and thrown away.</summary>
    [Fact]
    public void AMinuteBilledProviderIsPricedFromTheAudioItProduced()
    {
        var spend = new SpeechSpend();

        // Two minutes of audio, from a line whose character count is nothing like proportional to it — which
        // is the whole reason characters were the wrong measure.
        spend.Record(TtsProviderCatalog.OpenAiId, 900, group: null, audio: TimeSpan.FromMinutes(2));

        var said = spend.Describe(PerMinute(TtsProviderCatalog.OpenAiId, price: 0.015))!;

        Assert.DoesNotContain("no rate set", said, StringComparison.Ordinal);

        // 2 minutes at $0.015 is $0.03, and nothing about the 900 characters enters into it.
        Assert.Contains("0.03", said, StringComparison.Ordinal);
    }

    /// <summary>The two units never cross.</summary>
    [Fact]
    public void ACharacterRateIsNeverAppliedToAMinuteBilledProvider()
    {
        var spend = new SpeechSpend();
        spend.Record(TtsProviderCatalog.OpenAiId, 10_000, group: null, audio: TimeSpan.FromMinutes(1));

        // A character price set for a provider that does not bill by the character is ignored rather than
        // multiplied by ten thousand.
        var said = spend.Describe(On(TtsProviderCatalog.OpenAiId, price: 0.05))!;

        // One minute at the published $0.015, not $0.50.
        Assert.Contains("0.015", said, StringComparison.Ordinal);
        Assert.DoesNotContain("0.50", said, StringComparison.Ordinal);
    }

    /// <summary>
    /// And the Commander can still correct it, which is what makes a published figure a starting point
    /// rather than a claim.
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
        // TtsProviderCatalog resolves an id d47 does not ship to Edge, everywhere.
        Assert.Null(SpeechSpend.RateFor(On(Edge), "festival"));
    }

    [Fact]
    public void ASessionThatCrossedASwitchIsReportedPerProvider()
    {
        var spend = new SpeechSpend();
        spend.Record(Edge, 1_806);
        spend.Record(Eleven, 20_000);

        var said = spend.Describe(On(Eleven))!;

        // One total that mixed a free provider's characters with a paid one's is a figure that means nothing.
        Assert.Contains("21,806 characters", said, StringComparison.Ordinal);
        Assert.Contains("Edge Neural 1,806 (Edge Neural is free)", said, StringComparison.Ordinal);
        Assert.Contains("ElevenLabs 20,000 ($1.00", said, StringComparison.Ordinal);
    }

    [Fact]
    public void UtterancesAreCountedBecauseThereIsNoCaching()
    {
        var spend = new SpeechSpend();

        // The same sentence twice is billed twice, which is worth surfacing if a callout ever repeats itself
        // in a loop.
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
        // Read from elevenlabs.io/pricing/api for eleven_flash_v2_5, which is the model ElevenLabsTtsProvider
        // pins.
        Assert.Equal(0.05m, TtsProviderCatalog.ElevenLabs.ListDollarsPerThousandCharacters);
        Assert.True(TtsProviderCatalog.ElevenLabs.Billed);

        Assert.Null(TtsProviderCatalog.Edge.ListDollarsPerThousandCharacters);
        Assert.False(TtsProviderCatalog.Edge.Billed);
        Assert.False(TtsProviderCatalog.None.Billed);
    }
    /// <summary>
    /// Which slot is costing money, which is a question nobody could ask before six of them could name
 /// six providers.
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

    /// <summary>The two views sum the same rows rather than keeping two tallies, so they cannot drift.</summary>
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

/// <summary>Counting happens at the seam and only on synthesis that succeeded.</summary>
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
    /// A voice the provider will not accept is the same case and worth its own line: it is a failure
    /// d47 recovers from by itself, and a recovery that quietly billed for the attempt would put a
    /// charge on the Commander's account for a sentence they never heard.
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
    /// A turn cut off by the shut-up hotkey has already paid for the sentences that were synthesised
    /// before it — which is the case that makes "count what was sent" different from "count what was
    /// said", and the one worth a test.
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
        // What is counted has to be filed under the provider that will bill for it, not under a decorator —
        // the whole report is per provider.
        Assert.Equal("fake", Metered(new SpeechSpend()).Id);
    }
}

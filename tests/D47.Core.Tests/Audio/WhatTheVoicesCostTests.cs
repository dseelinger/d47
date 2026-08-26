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
    /// A provider that charges and publishes no rate says so, in words, and never quotes a figure.
    /// <para>
    /// <b>This test used to assert that every billed provider published a rate</b>, on the
    /// reasoning that the "count with no price" wording was therefore unreachable and kept for
    /// the next provider that did not — and it named its own trigger: <i>"a provider added with
    /// <c>Billed = true</c> and no list price … this is the test that says so at the moment it is
    /// added"</i>. OpenAI is that provider (list.md Phase 58), and it fired exactly as written.
    /// </para>
    /// <para>
    /// So the guard moves rather than goes. What it was really protecting is that a character
    /// count never acquires money d47 made up: OpenAI publishes per minute of audio and d47
    /// counts characters, and the conversion between them moves about 40% with the *content* of
    /// the line — 951 characters a minute for plain prose against 671 for a line of system names
    /// (docs/spikes/openai-tts-language-and-speed.md). There is no rate to state, and the honest
    /// answer is the one the wording already had.
    /// </para>
    /// </summary>
    [Fact]
    public void AProviderThatChargesWithNoPublishedRateSaysSoRatherThanQuotingZero()
    {
        var unpriced = TtsProviderCatalog.All
            .Where(provider => provider.Billed && provider.ListDollarsPerThousandCharacters is null)
            .ToList();

        Assert.NotEmpty(unpriced);

        foreach (var provider in unpriced)
        {
            var spend = new SpeechSpend();
            spend.Record(provider.Id, 5_000);

            var said = spend.Describe(On(provider.Id));

            Assert.NotNull(said);

            // The three readings that must stay apart: free, a figure, and a count with no rate
            // behind it. "$0.00" here would be the first of those said about the third.
            Assert.Contains($"no rate set for {provider.Name}", said, StringComparison.Ordinal);
            Assert.DoesNotContain("$0.00", said, StringComparison.Ordinal);
            Assert.DoesNotContain("is free", said, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// And the Commander can still put one in, which is what makes the absence a starting point
    /// rather than a dead end.
    /// </summary>
    [Fact]
    public void AndTheCommanderCanSetOneThemselves()
    {
        var spend = new SpeechSpend();
        spend.Record(TtsProviderCatalog.OpenAiId, 10_000);

        var said = spend.Describe(On(TtsProviderCatalog.OpenAiId, price: 0.015));

        Assert.NotNull(said);
        Assert.DoesNotContain("no rate set", said, StringComparison.Ordinal);
        Assert.Contains("0.15", said, StringComparison.Ordinal);
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

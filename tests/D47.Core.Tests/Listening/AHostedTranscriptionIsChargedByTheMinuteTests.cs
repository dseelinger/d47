using D47.Core.Catalog;
using D47.Core.Conversation;
using D47.Core.Listening;
using D47.Core.Storage;
using D47.Core.Tests.Catalog;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>Each hosted transcription that succeeds is one priced row in the spend ledger (#2554).</summary>
public class AHostedTranscriptionIsChargedByTheMinuteTests
{
    private const string Path = @"C:\d47\data\spend.jsonl";

    private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    private static readonly SpendPeriod Today = new("today", Now.AddHours(-12), Now.AddHours(12));

    private const int Rate = 16_000;

    private sealed class StoppedClock : IWallClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    /// <summary>Answers, throws or waits to be cancelled, and counts its disposals.</summary>
    private sealed class FakeTranscriber(Func<CancellationToken, Task<Transcription>> answer) : ISpeechTranscriber
    {
        public int Disposed { get; private set; }

        public bool IsReady => true;

        public Task<Transcription> TranscribeAsync(
            Utterance utterance,
            IReadOnlyList<string> properNouns,
            CancellationToken cancellationToken = default) => answer(cancellationToken);

        public void Dispose() => Disposed++;
    }

    private readonly MemoryFileSystem _files = new();

    private SpendLedger Ledger() => new(Path, _files, new StoppedClock(), NullLogger.Instance);

    private static Utterance Seconds(double seconds) => new(new float[(int)(seconds * Rate)], Rate);

    private static FakeTranscriber Answering() => new(_ => Task.FromResult(new Transcription("where am I")));

    private static TranscriptionSpend Spend(SpendLedger ledger, ModelCatalog? catalog = null) =>
        new(ledger, new ModelCatalogSource(catalog ?? ModelCatalog.Embedded));

    [Fact]
    public async Task ASuccessWritesARowWithTheAudioAndItsPrice()
    {
        var ledger = Ledger();
        var spend = Spend(ledger);

        using var metered = new MeteredTranscriber(Answering(), SttProviderCatalog.OpenAi, spend);
        var heard = await metered.TranscribeAsync(Seconds(30), [], TestContext.Current.CancellationToken);

        Assert.Equal("where am I", heard.Text);

        var row = Assert.Single(SpendFile.Entries(_files, Path));
        var price = ModelCatalog.Embedded.TranscriptionPriceFor(SttProviderCatalog.OpenAiId, "gpt-4o-mini-transcribe")!;

        Assert.Equal(SpendKind.Transcription, row.Kind);
        Assert.Equal(SttProviderCatalog.OpenAiId, row.ProviderId);
        Assert.Equal("gpt-4o-mini-transcribe", row.Model);
        Assert.Equal(30d, row.AudioSeconds);
        Assert.Equal(price.DollarsPerMinute / 2m, row.Dollars);
        Assert.True(row.Priced);
        Assert.Equal(SpendPurposes.Transcription, row.Purpose);
        Assert.Equal(row.Dollars, spend.Dollars);
    }

    [Fact]
    public async Task AShortRequestIsPricedAtItsProvidersMinimumAndRecordsItsRealLength()
    {
        var spend = Spend(Ledger());

        using var metered = new MeteredTranscriber(Answering(), SttProviderCatalog.Groq, spend);
        await metered.TranscribeAsync(Seconds(3), [], TestContext.Current.CancellationToken);

        var row = Assert.Single(SpendFile.Entries(_files, Path));
        var price = ModelCatalog.Embedded.TranscriptionPriceFor(SttProviderCatalog.GroqId, "whisper-large-v3-turbo")!;

        Assert.Equal(10d, price.MinimumSeconds);
        Assert.Equal(3d, row.AudioSeconds);
        Assert.Equal(price.DollarsPerMinute * 10m / 60m, row.Dollars);
    }

    [Fact]
    public async Task AFailedRequestWritesNothing()
    {
        var spend = Spend(Ledger());

        using var metered = new MeteredTranscriber(
            new FakeTranscriber(_ => throw new TranscriptionUnavailableException("Groq", TranscriptionFailure.Unreachable, "No answer.")),
            SttProviderCatalog.Groq,
            spend);

        await Assert.ThrowsAsync<TranscriptionUnavailableException>(
            () => metered.TranscribeAsync(Seconds(4), [], TestContext.Current.CancellationToken));

        Assert.Empty(SpendFile.Entries(_files, Path));
        Assert.Equal(0m, spend.Dollars);
    }

    [Fact]
    public async Task ACancelledRequestWritesNothing()
    {
        var spend = Spend(Ledger());
        using var cancel = new CancellationTokenSource();

        using var metered = new MeteredTranscriber(
            new FakeTranscriber(async token =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return new Transcription("never");
            }),
            SttProviderCatalog.Deepgram,
            spend);

        var pending = metered.TranscribeAsync(Seconds(4), [], cancel.Token);
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

        Assert.Empty(SpendFile.Entries(_files, Path));
    }

    [Fact]
    public async Task AModelWithNoPriceWritesAnUnpricedRowAndTheTotalsSaySo()
    {
        var ledger = Ledger();
        var spend = Spend(ledger, ModelCatalog.Parse(CatalogWithoutTranscription()));

        using var metered = new MeteredTranscriber(Answering(), SttProviderCatalog.ElevenLabs, spend);
        await metered.TranscribeAsync(Seconds(6), [], TestContext.Current.CancellationToken);

        var row = Assert.Single(SpendFile.Entries(_files, Path));

        Assert.False(row.Priced);
        Assert.Equal(0m, row.Dollars);
        Assert.Equal(6d, row.AudioSeconds);
        Assert.False(ledger.Total(Today).Complete);
    }

    [Fact]
    public async Task TheTotalsCountTranscriptionAsItsOwnProvider()
    {
        var ledger = Ledger();
        var spend = Spend(ledger);

        using var metered = new MeteredTranscriber(Answering(), SttProviderCatalog.Deepgram, spend);
        await metered.TranscribeAsync(Seconds(60), [], TestContext.Current.CancellationToken);
        await metered.TranscribeAsync(Seconds(30), [], TestContext.Current.CancellationToken);

        var today = ledger.Total(Today);
        var share = Assert.Single(today.Shares);

        Assert.True(today.Any);
        Assert.True(today.Complete);
        Assert.Equal(0, today.Turns);
        Assert.Equal(2, today.Transcriptions);
        Assert.Equal(spend.Dollars, today.TranscriptionDollars);
        Assert.Equal(today.TranscriptionDollars, today.Dollars);
        Assert.Equal(SpendKind.Transcription, share.Kind);
        Assert.Equal(SttProviderCatalog.DeepgramId, share.Provider);
        Assert.Equal(90d, share.AudioSeconds);
    }

    [Fact]
    public void EveryHostedProviderHasAPriceInTheCatalog()
    {
        Assert.All(
            SttProviderCatalog.All.Where(provider => provider.Hosted),
            provider => Assert.True(
                ModelCatalog.Embedded.TranscriptionPriceFor(provider.Id, provider.Model!) is { DollarsPerMinute: > 0m },
                $"The model catalog has no transcription price for {provider.Id}/{provider.Model}."));
    }

    [Fact]
    public void ACatalogWithNoTranscriptionSectionIsReadWithEveryModelUnpriced()
    {
        var catalog = ModelCatalog.Parse(CatalogWithoutTranscription());

        Assert.Null(catalog.TranscriptionPriceFor(SttProviderCatalog.GroqId, "whisper-large-v3-turbo"));
    }

    [Fact]
    public void TheMeterDisposesTheTranscriberItWraps()
    {
        var inner = Answering();

        new MeteredTranscriber(inner, SttProviderCatalog.Groq, new TranscriptionSpend()).Dispose();

        Assert.Equal(1, inner.Disposed);
    }

    private static string CatalogWithoutTranscription() => $$"""
        {
          "schema": 1,
          "published": "2026-09-28",
          "providers": {},
          {{SpeechSection.Json()}}
        }
        """;
}

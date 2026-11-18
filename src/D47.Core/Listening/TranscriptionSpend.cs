using D47.Core.Catalog;
using D47.Core.Conversation;

namespace D47.Core.Listening;

/// <summary>What hosted transcription has cost this session, with one ledger row per charge.</summary>
/// <param name="ledger">Where charges are kept between runs, or null for a session-only count.</param>
/// <param name="catalog">Where the per-minute prices are read; the app's shared catalog when null.</param>
public sealed class TranscriptionSpend(SpendLedger? ledger = null, ModelCatalogSource? catalog = null)
{
    private readonly ModelCatalogSource _catalog = catalog ?? ModelCatalogSource.Shared;
    private readonly Lock _lock = new();
    private decimal _dollars;

    /// <summary>Raised after each charge is recorded, on the thread that transcribed.</summary>
    public event Action? Recorded;

    /// <summary>What this session's priced transcriptions came to; an unpriced one adds nothing.</summary>
    public decimal Dollars
    {
        get
        {
            lock (_lock)
            {
                return _dollars;
            }
        }
    }

    /// <summary>One hosted transcription that succeeded.</summary>
    public void Record(SttProviderInfo provider, TimeSpan audio)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var model = provider.Model ?? string.Empty;
        var price = _catalog.Current.TranscriptionPriceFor(provider.Id, model);
        var dollars = price?.DollarsFor(audio) ?? 0m;

        lock (_lock)
        {
            _dollars += dollars;
        }

        ledger?.Append(new SpendEntry
        {
            Kind = SpendKind.Transcription,
            ProviderId = provider.Id,
            Model = model,
            Dollars = dollars,
            Priced = price is not null,
            Purpose = SpendPurposes.Transcription,
            AudioSeconds = audio.TotalSeconds,
        });

        Recorded?.Invoke();
    }

    /// <summary>Empties the session's figures, for a reset performed in the SPEND dialog.</summary>
    public void Forget()
    {
        lock (_lock)
        {
            _dollars = 0m;
        }
    }
}

/// <summary>Records each hosted transcription that succeeds, and hands the words on unchanged.</summary>
public sealed class MeteredTranscriber(ISpeechTranscriber inner, SttProviderInfo provider, TranscriptionSpend spend)
    : ISpeechTranscriber
{
    public bool IsReady => inner.IsReady;

    public async Task<Transcription> TranscribeAsync(
        Utterance utterance,
        IReadOnlyList<string> properNouns,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(utterance);

        var transcription = await inner.TranscribeAsync(utterance, properNouns, cancellationToken).ConfigureAwait(false);

        // After the await and outside any catch: a failed or cancelled request leaves through the exception
        // and records nothing.
        spend.Record(provider, utterance.Duration);

        return transcription;
    }

    /// <summary>Disposes the wrapped transcriber, which this owns.</summary>
    public void Dispose() => inner.Dispose();
}

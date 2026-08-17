using System.Globalization;
using D47.Core.Configuration;

namespace D47.Core.Audio;

/// <summary>What one provider has been asked to say this session.</summary>
public sealed record SpeechCharge(string ProviderId, long Characters, int Utterances);

/// <summary>
/// What the voices have cost, beside what the model costs (list.md Phase 19).
/// <para>
/// <b>The unit is characters, not tokens.</b> ElevenLabs bills per character, Edge bills
/// nothing, and reusing <see cref="Conversation.SpendTracker"/>'s shape would quote a number
/// whose basis is wrong.
/// </para>
/// <para>
/// <b>Characters are a fact and dollars are an assumption</b>, and the two are kept apart all
/// the way to the sentence. The count is measured — it is the string that was handed to the
/// provider. The price is a list price the Commander can correct, because the rate actually
/// being paid is a property of a subscription tier and the API reports neither.
/// </para>
/// <para>
/// Per provider rather than one total, because a session can cross a switch: an hour on Edge
/// and ten minutes on ElevenLabs is one figure that means nothing and two that mean something.
/// </para>
/// <para>
/// Nothing to model for caching, because there is none — the same sentence spoken twice is
/// billed twice. That is itself worth surfacing if a callout ever repeats itself in a loop,
/// which is why <see cref="Utterances"/> is counted alongside the characters.
/// </para>
/// </summary>
public sealed class SpeechSpend
{
    private readonly Dictionary<string, SpeechCharge> _byProvider = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();

    /// <summary>
    /// Where charges are kept between runs, and the rate to price them at. Null for a session-only
    /// tracker — the replay harness and every test that is not about history.
    /// <para>
    /// <b>Speech has to reach the ledger or the ledger is wrong.</b> Ledgering the model alone
    /// would leave a month figure that looks authoritative while covering half of what was spent,
    /// which is worse than not reporting one.
    /// </para>
    /// </summary>
    private Conversation.SpendLedger? _ledger;

    private Func<D47Settings>? _settings;

    /// <summary>
    /// Handed the ledger and a way to read the current rates, after construction because the
    /// composition root builds the settings service and this on either side of each other.
    /// <para>
    /// The rate is read at the moment of the charge rather than at the moment of the question.
    /// A Commander who corrects their rate should not silently restate what last month cost —
    /// the row records what the figure was believed to be when it was spent.
    /// </para>
    /// </summary>
    public void LedgerTo(Conversation.SpendLedger ledger, Func<D47Settings> settings)
    {
        _ledger = ledger;
        _settings = settings;
    }

    /// <summary>
    /// One synthesis that <b>succeeded</b>. A refused voice or a failed request costs nothing,
    /// and a turn cut off by the shut-up hotkey has already paid for the sentences that were
    /// synthesised before it — which is the case that makes "count what was sent" different from
    /// "count what was said".
    /// </summary>
    public void Record(string providerId, int characters)
    {
        if (characters <= 0)
        {
            return;
        }

        lock (_lock)
        {
            var held = _byProvider.GetValueOrDefault(providerId) ?? new SpeechCharge(providerId, 0, 0);

            _byProvider[providerId] = held with
            {
                Characters = held.Characters + characters,
                Utterances = held.Utterances + 1,
            };
        }

        if (_ledger is null || _settings is null)
        {
            return;
        }

        // Priced from this one utterance rather than from the running total, because a ledger row
        // is one charge. Characters stay a fact and dollars an assumption all the way into the
        // file: an unpriced provider records the count with Priced false, so a window containing
        // it reports a floor rather than pretending to a total.
        var settings = _settings();
        var one = new SpeechCharge(providerId, characters, 1);

        _ledger.Append(new Conversation.SpendEntry
        {
            Kind = Conversation.SpendKind.Voice,
            ProviderId = providerId,
            Model = TtsProviderCatalog.Selected(providerId).Name,
            Dollars = DollarsFor(settings, one) ?? 0m,
            Priced = Priced(settings, providerId),
            Characters = characters,
        });
    }

    /// <summary>Every provider that has spoken this session, most characters first.</summary>
    public IReadOnlyList<SpeechCharge> Charges
    {
        get
        {
            lock (_lock)
            {
                return [.. _byProvider.Values.OrderByDescending(charge => charge.Characters)];
            }
        }
    }

    public long TotalCharacters => Charges.Sum(charge => charge.Characters);

    public int Utterances => Charges.Sum(charge => charge.Utterances);

    /// <summary>
    /// What the session's speech has cost, in one line, or null when nothing has been spoken.
    /// <para>
    /// Read by the panel's turn line and by <c>get_model_status</c>, so "what has this cost" has
    /// one answer rather than one per subsystem — which is the whole point of the item.
    /// </para>
    /// </summary>
    public string? Describe(D47Settings settings)
    {
        var charges = Charges;

        if (charges.Count == 0)
        {
            return null;
        }

        var priced = charges.Sum(charge => DollarsFor(settings, charge) ?? 0m);
        var everythingPriced = charges.All(charge => Priced(settings, charge.ProviderId));

        var line = new System.Text.StringBuilder(
            $"{TotalCharacters.ToString("N0", CultureInfo.CurrentCulture)} characters spoken");

        // A provider that costs nothing reads as free, never as an untracked zero: "$0.00" from
        // Edge and "$0.00" from an ElevenLabs run nobody has priced are the same string for
        // opposite reasons.
        if (charges.Count == 1)
        {
            line.Append($", {Cost(settings, charges[0])}");
            return line.ToString();
        }

        line.Append(everythingPriced ? $", {priced:C4}" : ", part of it unpriced");
        line.Append(" — ");
        line.AppendJoin(", ", charges.Select(charge =>
            $"{Name(charge.ProviderId)} {charge.Characters.ToString("N0", CultureInfo.CurrentCulture)} "
            + $"({Cost(settings, charge)})"));

        return line.ToString();
    }

    /// <summary>
    /// What one provider's characters came to, in words. Three answers, and they are three
    /// different things: free, a figure, and a count with no rate behind it.
    /// </summary>
    private static string Cost(D47Settings settings, SpeechCharge charge)
    {
        var provider = TtsProviderCatalog.Selected(charge.ProviderId);

        if (!provider.Billed)
        {
            return $"{provider.Name} is free";
        }

        return DollarsFor(settings, charge) is { } dollars
            ? dollars.ToString("C4", CultureInfo.CurrentCulture)
            : $"no rate set for {provider.Name}";
    }

    private static string Name(string providerId) => TtsProviderCatalog.Selected(providerId).Name;

    private static bool Priced(D47Settings settings, string providerId) =>
        !TtsProviderCatalog.Selected(providerId).Billed || RateFor(settings, providerId) is not null;

    private static decimal? DollarsFor(D47Settings settings, SpeechCharge charge)
    {
        var provider = TtsProviderCatalog.Selected(charge.ProviderId);

        if (!provider.Billed)
        {
            return 0m;
        }

        return RateFor(settings, charge.ProviderId) is { } rate
            ? rate * charge.Characters / 1000m
            : null;
    }

    /// <summary>
    /// The dollars-per-thousand-characters in force for a provider: the Commander's own if they
    /// have corrected it, then the published list price, then nothing — which is a real answer
    /// and means the character count is quoted on its own.
    /// </summary>
    public static decimal? RateFor(D47Settings settings, string providerId)
    {
        var provider = TtsProviderCatalog.Selected(providerId);

        return settings.Speech.CharacterPrices.TryGetValue(provider.Id, out var own)
            ? (decimal)own
            : provider.ListDollarsPerThousandCharacters;
    }
}

/// <summary>
/// Counts what was handed over, and hands it over unchanged.
/// <para>
/// <b>At the <see cref="ITtsProvider"/> seam because that is the only place every caller
/// passes</b> — the ship's AI, a callout, a re-voiced in-game sender and a core's own
/// introduction all converge here, and counting at any caller means missing the next one.
/// </para>
/// <para>
/// A decorator rather than a line inside each provider, so a provider added later is counted
/// without anybody remembering to count it, and so the two that exist cannot disagree about
/// what a character is.
/// </para>
/// </summary>
public sealed class MeteredTtsProvider(ITtsProvider inner, SpeechSpend spend) : ITtsProvider, IDisposable
{
    public string Id => inner.Id;

    public string Name => inner.Name;

    public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
        inner.ListVoicesAsync(cancellationToken);

    public async Task<AudioClip> SynthesizeAsync(
        string text,
        VoiceSelection voice,
        CancellationToken cancellationToken = default)
    {
        var clip = await inner.SynthesizeAsync(text, voice, cancellationToken).ConfigureAwait(false);

        // After the await and outside any catch, which is the whole rule: a refused voice, a
        // rejected key and a cancelled turn all leave through the exception and none of them is
        // billed. What was synthesised before a shut-up was, and is counted, because it was sent.
        //
        // Asked of the provider rather than measured here, because a provider may rewrite a line
        // on its way out — ElevenLabs spells numerals — and it is the rewritten length that
        // arrives on the bill.
        spend.Record(inner.Id, inner.Billable(text).Length);

        return clip;
    }

    /// <summary>
    /// Forwarded, because the composition root disposes what it built through this interface and
    /// a decorator that swallowed it would leak an HTTP handle on every provider switch.
    /// </summary>
    public void Dispose() => (inner as IDisposable)?.Dispose();
}

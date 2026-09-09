using System.Globalization;
using D47.Core.Configuration;

namespace D47.Core.Audio;

/// <summary>What one provider has been asked to say this session.</summary>
public sealed record SpeechCharge(string ProviderId, long Characters, int Utterances)
{
    /// <summary>
    /// Which slot it was speaking for, or null for the per-provider total that covers all of them
    /// (Phase 57).
    /// </summary>
    public VoiceGroup? Group { get; init; }

    /// <summary>
    /// How much audio came back, for a provider whose bill is a function of that rather than of the
    /// characters handed over (#63).
    /// </summary>
    public TimeSpan Audio { get; init; }
}

/// <summary>What the voices have cost, beside what the model costs (Phase 19).</summary>
public sealed class SpeechSpend
{
    /// <summary>
    /// Counted against the provider and the slot, because the two answer different questions and the
    /// bill only knows the first.
    /// </summary>
    private readonly Dictionary<(string Provider, VoiceGroup? Group), SpeechCharge> _charges = new();
    private readonly Lock _lock = new();

    /// <summary>Where charges are kept between runs, and the rate to price them at.</summary>
    private Conversation.SpendLedger? _ledger;

    private Func<D47Settings>? _settings;

    /// <summary>
    /// Handed the ledger and a way to read the current rates, after construction because the
    /// composition root builds the settings service and this on either side of each other.
    /// </summary>
    public void LedgerTo(Conversation.SpendLedger ledger, Func<D47Settings> settings)
    {
        _ledger = ledger;
        _settings = settings;
    }

    /// <summary>One synthesis that succeeded.</summary>
    /// <param name="group">
    /// Which slot was speaking, or null where the caller has no slot to name — the audition path and
    /// every test that is not about the breakdown.
    /// </param>
    public void Record(string providerId, int characters, VoiceGroup? group = null, TimeSpan audio = default)
    {
        if (characters <= 0)
        {
            return;
        }

        lock (_lock)
        {
            var key = (providerId, group);
            var held = _charges.GetValueOrDefault(key) ?? new SpeechCharge(providerId, 0, 0) { Group = group };

            _charges[key] = held with
            {
                Characters = held.Characters + characters,
                Utterances = held.Utterances + 1,
                Audio = held.Audio + audio,
            };
        }

        if (_ledger is null || _settings is null)
        {
            return;
        }

        // Priced from this one utterance rather than from the running total, because a ledger row is one
        // charge.
        var settings = _settings();
        var one = new SpeechCharge(providerId, characters, 1) { Audio = audio };

        _ledger.Append(new Conversation.SpendEntry
        {
            Kind = Conversation.SpendKind.Voice,
            ProviderId = providerId,
            Model = TtsProviderCatalog.Selected(providerId).Name,
            Dollars = DollarsFor(settings, one) ?? 0m,
            Priced = Priced(settings, providerId),
            Characters = characters,
            AudioSeconds = audio > TimeSpan.Zero ? audio.TotalSeconds : null,
        });
    }

    /// <summary>Every provider that has spoken this session, most characters first.</summary>
    public IReadOnlyList<SpeechCharge> Charges
    {
        get
        {
            lock (_lock)
            {
                return
                [
                    .. _charges.Values
                        .GroupBy(charge => charge.ProviderId, StringComparer.OrdinalIgnoreCase)
                        .Select(perProvider => new SpeechCharge(
                            perProvider.Key,
                            perProvider.Sum(charge => charge.Characters),
                            perProvider.Sum(charge => charge.Utterances))
                        {
                            // Summed with the rest, or a provider billed by the minute would be priced from a
                            // total that lost the measure it is billed on (#63).
                            Audio = perProvider.Aggregate(
                                TimeSpan.Zero,
                                (total, charge) => total + charge.Audio),
                        })
                        .OrderByDescending(charge => charge.Characters),
                ];
            }
        }
    }

    /// <summary>The same characters, broken down by the slot that spoke them, most first (Phase 57).</summary>
    public IReadOnlyList<SpeechCharge> BySlot
    {
        get
        {
            lock (_lock)
            {
                return [.. _charges.Values.OrderByDescending(charge => charge.Characters)];
            }
        }
    }

    /// <summary>
    /// Empties the session's speech figures, for a reset performed in the Details dialog (#197).
    /// </summary>
    public void Forget()
    {
        lock (_lock)
        {
            _charges.Clear();
        }
    }

    public long TotalCharacters => Charges.Sum(charge => charge.Characters);

    public int Utterances => Charges.Sum(charge => charge.Utterances);

    /// <summary>What the session's speech has cost, in one line, or null when nothing has been spoken.</summary>
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

        // A provider that costs nothing reads as free, never as an untracked zero: "$0.00" from Edge and
        // "$0.00" from an ElevenLabs run nobody has priced are the same string for opposite reasons.
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
    /// What each slot has cost, one line per slot that has spoken, or null when nothing has (Phase 57).
    /// </summary>
    public string? DescribeSlots(D47Settings settings)
    {
        var charges = BySlot;

        if (charges.Count == 0)
        {
            return null;
        }

        return string.Join(
            "; ",
            charges.Select(charge =>
                $"{SlotName(charge.Group)} {charge.Characters.ToString("N0", CultureInfo.CurrentCulture)} "
                + $"through {Name(charge.ProviderId)} ({Cost(settings, charge)})"));
    }

    /// <summary>What a slot is called.</summary>
    private static string SlotName(VoiceGroup? group) =>
        group is { } slot ? VoiceGroups.Info(slot).Name : "Not attributed";

    /// <summary>What one provider's characters came to, in words.</summary>
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

    private static bool Priced(D47Settings settings, string providerId)
    {
        var provider = TtsProviderCatalog.Selected(providerId);

        if (!provider.Billed)
        {
            return true;
        }

        return provider.BilledByMinute
            ? MinuteRateFor(settings, providerId) is not null
            : RateFor(settings, providerId) is not null;
    }

    private static decimal? DollarsFor(D47Settings settings, SpeechCharge charge)
    {
        var provider = TtsProviderCatalog.Selected(charge.ProviderId);

        if (!provider.Billed)
        {
            return 0m;
        }

        // The rate and the measure are chosen together, from the same fact about the provider, so a
        // per-minute rate can never be multiplied by a character count (#63).
        if (provider.BilledByMinute)
        {
            return MinuteRateFor(settings, charge.ProviderId) is { } perMinute
                ? perMinute * (decimal)charge.Audio.TotalMinutes
                : null;
        }

        return RateFor(settings, charge.ProviderId) is { } rate
            ? rate * charge.Characters / 1000m
            : null;
    }

    /// <summary>
    /// The dollars-per-thousand-characters in force for a provider: the Commander's own if they have
    /// corrected it, then the published list price, then nothing — which is a real answer and means the
    /// character count is quoted on its own.
    /// </summary>
    public static decimal? RateFor(D47Settings settings, string providerId)
    {
        var provider = TtsProviderCatalog.Selected(providerId);

        return settings.Speech.CharacterPrices.TryGetValue(provider.Id, out var own)
            ? (decimal)own
            : provider.ListDollarsPerThousandCharacters;
    }

    /// <summary>
    /// The dollars-per-minute-of-audio in force for a provider, on the same three-step ladder as <see
    /// cref="RateFor"/>: the Commander's own, then the published figure, then nothing.
    /// </summary>
    public static decimal? MinuteRateFor(D47Settings settings, string providerId)
    {
        var provider = TtsProviderCatalog.Selected(providerId);

        return settings.Speech.MinutePrices.TryGetValue(provider.Id, out var own)
            ? (decimal)own
            : provider.ListDollarsPerMinute;
    }
}

/// <summary>Counts what was handed over, and hands it over unchanged.</summary>
/// <param name="group">
/// Which slot this decorator counts for, or null where there is no slot to name.
/// </param>
public sealed class MeteredTtsProvider(ITtsProvider inner, SpeechSpend spend, VoiceGroup? group = null)
    : ITtsProvider, IDisposable
{
    public string Id => inner.Id;

    public string Name => inner.Name;

    public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) =>
        inner.ListVoicesAsync(cancellationToken);

    /// <summary>Everything below here forwards, and forgetting one is invisible.</summary>
    public string Billable(string text) => inner.Billable(text);

    public string? Phonemes(string text, VoiceSelection voice) => inner.Phonemes(text, voice);

    public bool ReadsAudioTags => inner.ReadsAudioTags;

    public int GroupsSentencesUpTo => inner.GroupsSentencesUpTo;

    public async Task<AudioClip> SynthesizeAsync(
        string text,
        VoiceSelection voice,
        CancellationToken cancellationToken = default)
    {
        var clip = await inner.SynthesizeAsync(text, voice, cancellationToken).ConfigureAwait(false);

        // After the await and outside any catch, which is the whole rule: a refused voice, a rejected key and
        // a cancelled turn all leave through the exception and none of them is billed.
        spend.Record(inner.Id, inner.Billable(text).Length, group, clip.Duration);

        return clip;
    }

    /// <summary>Deliberately does not forward.</summary>
    public void Dispose()
    {
    }
}

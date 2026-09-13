using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Audio;

namespace D47.Core.Configuration;

/// <summary>
/// Every voice chosen while one provider was selected: the ship's AI's, the two named roles', and one
/// per core, plus whether the pairing pass has run against that provider's list.
/// </summary>
public sealed record VoiceChoices
{
    /// <inheritdoc cref="D47Settings.Extra"/>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    public string? Ship { get; init; }

    public string? CarrierCaptain { get; init; }

    public string? Tower { get; init; }

    /// <summary>The voice paired to each core, keyed by persona id.</summary>
    public IReadOnlyDictionary<string, string> Cores { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// The voice the pairing pass chose for each core on this provider, kept even after a hand-picked
    /// choice overwrites <see cref="Cores"/> (#85).
    /// </summary>
    public IReadOnlyDictionary<string, string> PairedCores { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Whether the background pairing has run against this provider's voice list.</summary>
    public bool Paired { get; init; }

    /// <summary>Nothing was ever chosen here.</summary>
    public bool IsEmpty =>
        Ship is null && CarrierCaptain is null && Tower is null
        && Cores.Count == 0 && PairedCores.Count == 0 && !Paired;
}

/// <summary>
/// Which voices belong to which provider, and what happens to them when the provider changes (Phase 19,
/// "Remember which voice you chose for each provider").
/// </summary>
public static class VoiceMemory
{
    /// <summary>
    /// Makes the stored voices and the selected provider agree, and answers the settings that result —
    /// the same instance when they already agreed, so a caller can compare by reference and skip a
    /// write.
    /// </summary>
    public static D47Settings Reconciled(D47Settings settings)
    {
        // Before anything is compared, because the comparison is against the slot's provider and a file from
        // before Phase 57 has not got one yet.
        settings = VoiceGroups.Migrated(settings);

        settings = Reconciled(settings, VoiceGroup.Aboard);
        settings = Reconciled(settings, VoiceGroup.Carrier);

        return settings;
    }

    /// <summary>The same question asked of one slot.</summary>
    private static D47Settings Reconciled(D47Settings settings, VoiceGroup group)
    {
        var selected = VoiceGroups.ProviderFor(settings.Speech, group);
        var chosenFor = ChosenFor(settings.Speech, group);

        if (string.Equals(chosenFor, selected, StringComparison.Ordinal))
        {
            return settings;
        }

        return chosenFor is null
            ? Stamped(settings, group, selected)
            : Switched(settings, group, chosenFor, selected);
    }

    /// <summary>Which provider this slot's live voices were chosen from, or null if unrecorded.</summary>
    private static string? ChosenFor(SpeechSettings speech, VoiceGroup group) => group switch
    {
        VoiceGroup.Carrier => speech.CarrierVoicesProvider,
        _ => speech.VoicesProvider,
    };

    /// <summary>Records which provider this slot's voices belong to without touching them.</summary>
    private static D47Settings Stamped(D47Settings settings, VoiceGroup group, string provider) => group switch
    {
        VoiceGroup.Carrier => settings with
        {
            Speech = settings.Speech with { CarrierVoicesProvider = provider },
        },
        _ => settings with { Speech = settings.Speech with { VoicesProvider = provider } },
    };

    /// <summary>
    /// The same settings with the live voice slots emptied of <paramref name="from"/>'s choices, those
    /// choices filed under <paramref name="from"/>, and whatever was filed under <paramref name="to"/>
    /// put back.
    /// </summary>
    /// <paramref name="from"/>
    /// 's choices, those choices filed under <paramref name="from"/>, and whatever was filed under
    /// <paramref name="to"/> put back.
    /// </paramref>
    public static D47Settings Switched(D47Settings settings, string? from, string to) =>
        Switched(Switched(settings, VoiceGroup.Aboard, from, to), VoiceGroup.Carrier, from, to);

    /// <summary>The same move for one slot's voices only (Phase 57).</summary>
    public static D47Settings Switched(D47Settings settings, VoiceGroup group, string? from, string to)
    {
        var mine = Remembered(settings);
        var restoring = settings.Speech.ProviderVoices.GetValueOrDefault(to) ?? new VoiceChoices();

        var remembered = new Dictionary<string, VoiceChoices>(
            settings.Speech.ProviderVoices,
            StringComparer.OrdinalIgnoreCase);

        if (from is not null)
        {
            var filed = Merged(remembered.GetValueOrDefault(from) ?? new VoiceChoices(), mine, group);

            if (filed.IsEmpty)
            {
                // Nothing was chosen while that provider was selected, so there is nothing to come back to.
                remembered.Remove(from);
            }
            else
            {
                remembered[from] = filed;
            }
        }

        // The provider being switched to no longer has *this slot's* choices owed to it — they are now live.
        var owed = Merged(restoring, new VoiceChoices(), group);

        if (owed.IsEmpty)
        {
            remembered.Remove(to);
        }
        else
        {
            remembered[to] = owed;
        }

        var restored = Stamped(Restored(settings, restoring, group), group, to);

        return restored with { Speech = restored.Speech with { ProviderVoices = remembered } };
    }

    /// <summary>One slot's fields taken from <paramref name="taking"/>, the rest left as they are.</summary>
    private static VoiceChoices Merged(VoiceChoices held, VoiceChoices taking, VoiceGroup group) => group switch
    {
        VoiceGroup.Carrier => held with
        {
            CarrierCaptain = taking.CarrierCaptain,
            Tower = taking.Tower,
        },
        _ => held with
        {
            Ship = taking.Ship,
            Cores = taking.Cores,
            PairedCores = taking.PairedCores,
            Paired = taking.Paired,
        },
    };

    /// <summary>The live slots filled from what was filed under the provider being switched to.</summary>
    private static D47Settings Restored(D47Settings settings, VoiceChoices restoring, VoiceGroup group) =>
        group switch
        {
            VoiceGroup.Carrier => settings with
            {
                Speech = settings.Speech with
                {
                    CarrierCaptainVoice = restoring.CarrierCaptain,
                    TowerVoice = restoring.Tower,
                },
            },
            _ => settings with
            {
                Speech = settings.Speech with { Voice = restoring.Ship },
                Persona = settings.Persona with
                {
                    Voices = new Dictionary<string, string>(restoring.Cores, StringComparer.Ordinal),
                    PairedVoices = new Dictionary<string, string>(restoring.PairedCores, StringComparer.Ordinal),

                    // Restored with the pairings it describes.
                    VoicesPaired = restoring.Paired,
                },
            },
        };

    /// <summary>The choices currently live, as one value.</summary>
    public static VoiceChoices Remembered(D47Settings settings) => new()
    {
        Ship = settings.Speech.Voice,
        CarrierCaptain = settings.Speech.CarrierCaptainVoice,
        Tower = settings.Speech.TowerVoice,
        Cores = new Dictionary<string, string>(settings.Persona.Voices, StringComparer.Ordinal),
        PairedCores = new Dictionary<string, string>(settings.Persona.PairedVoices, StringComparer.Ordinal),
        Paired = settings.Persona.VoicesPaired,
    };

    /// <summary>What a reset to the pairing's own choices did (#85).</summary>
    public sealed record VoiceResetOutcome(
        int Restored,
        int PairingPending,
        int ProvidersTouched,
        bool ClearedOtherVoices)
    {
        /// <summary>What the reset row says afterwards.</summary>
        public string Said
        {
            get
            {
                var parts = new List<string>
                {
                    Restored switch
                    {
                        0 => "No core had a recorded pairing.",
                        1 => "One core now has the voice d47 paired it with.",
                        _ => $"{Restored} cores now have the voice d47 paired them with.",
                    },
                };

                if (PairingPending > 0)
                {
                    parts.Add(PairingPending == 1
                        ? "One core had no recorded pairing, so it will be paired again the next chance d47 gets."
                        : $"{PairingPending} cores had no recorded pairing, so they will be paired again the "
                          + "next chance d47 gets.");
                }

                if (ClearedOtherVoices)
                {
                    parts.Add("The carrier captain and tower are back to speaking in the ship AI's voice.");
                }

                parts.Add(ProvidersTouched == 1
                    ? "Covered the voice provider in use."
                    : $"Covered all {ProvidersTouched} voice providers you have used, so switching providers "
                      + "will not bring a hand-picked voice back.");

                return string.Join(" ", parts);
            }
        }
    }

    /// <summary>
    /// Puts every core, on every voice provider the Commander has used, back to the voice the pairing
    /// pass chose for it, and clears the carrier's two voices back to following the ship AI's. A core
    /// with no recorded pairing is dropped instead, so it is paired again the next chance d47 gets
    /// (#85).
    /// </summary>
    public static (D47Settings Settings, VoiceResetOutcome Outcome) ResetToPairing(D47Settings settings)
    {
        var (persona, restored, pending) = ResetPersona(settings.Persona);

        var clearedAny = settings.Speech.Voice is not null
            || settings.Speech.CarrierCaptainVoice is not null
            || settings.Speech.TowerVoice is not null;

        var providerVoices = new Dictionary<string, VoiceChoices>(
            settings.Speech.ProviderVoices, StringComparer.OrdinalIgnoreCase);

        foreach (var (provider, choices) in settings.Speech.ProviderVoices)
        {
            var (updated, stashRestored, stashPending) = ResetStashed(choices);

            providerVoices[provider] = updated;
            restored += stashRestored;
            pending += stashPending;
            clearedAny = clearedAny
                || choices.Ship is not null || choices.CarrierCaptain is not null || choices.Tower is not null;
        }

        var updatedSettings = settings with
        {
            Persona = persona,
            Speech = settings.Speech with
            {
                Voice = null,
                CarrierCaptainVoice = null,
                TowerVoice = null,
                ProviderVoices = providerVoices,
            },
        };

        var outcome = new VoiceResetOutcome(
            restored,
            pending,
            ProvidersTouched: 1 + settings.Speech.ProviderVoices.Count,
            ClearedOtherVoices: clearedAny);

        return (updatedSettings, outcome);
    }

    /// <summary>The live slot's part of a reset.</summary>
    private static (PersonaSettings Persona, int Restored, int Pending) ResetPersona(PersonaSettings persona)
    {
        var (voices, restored, pending) = WithPairingsRestored(persona.Voices, persona.PairedVoices);

        return (persona with { Voices = voices, VoicesPaired = persona.VoicesPaired && pending == 0 }, restored, pending);
    }

    /// <summary>The same reset for one provider's stashed choices.</summary>
    private static (VoiceChoices Choices, int Restored, int Pending) ResetStashed(VoiceChoices choices)
    {
        var (cores, restored, pending) = WithPairingsRestored(choices.Cores, choices.PairedCores);

        return (
            choices with
            {
                Cores = cores,
                Paired = choices.Paired && pending == 0,
                Ship = null,
                CarrierCaptain = null,
                Tower = null,
            },
            restored,
            pending);
    }

    /// <summary>
    /// Every entry a pairing was recorded for, put back to it; every entry with none dropped rather
    /// than left as it was hand-picked (#85).
    /// </summary>
    private static (IReadOnlyDictionary<string, string> Voices, int Restored, int Pending) WithPairingsRestored(
        IReadOnlyDictionary<string, string> live,
        IReadOnlyDictionary<string, string> recorded)
    {
        var restored = new Dictionary<string, string>(StringComparer.Ordinal);
        var pending = 0;

        foreach (var id in live.Keys.Union(recorded.Keys, StringComparer.Ordinal))
        {
            if (recorded.TryGetValue(id, out var paired))
            {
                restored[id] = paired;
            }
            else
            {
                pending++;
            }
        }

        return (restored, restored.Count, pending);
    }
}

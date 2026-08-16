using D47.Core.Audio;

namespace D47.Core.Configuration;

/// <summary>
/// Every voice chosen while one provider was selected: the ship's AI's, the two named roles',
/// and one per core, plus whether the pairing pass has run against that provider's list.
/// <para>
/// A record rather than a flat set of extra keys on <see cref="SpeechSettings"/>, because this
/// is the whole reason the choices were being dropped instead of kept.
/// <see cref="SpeechSettings.ProviderRates"/> keeps one number per provider and that shape does
/// not stretch: a voice choice is four kinds of thing at once, and the fix that stopped the
/// silence dropped them rather than waiting on a schema that could hold all of it twice
/// (list.md Phase 19).
/// </para>
/// <para>
/// <see cref="Paired"/> is the field that makes this worth having. Without it, switching to
/// ElevenLabs and back would restore eleven pairings and then immediately re-derive them,
/// because the flag saying "this pass has run" is what stops the pass running again.
/// </para>
/// </summary>
public sealed record VoiceChoices
{
    public string? Ship { get; init; }

    public string? CarrierCaptain { get; init; }

    public string? Tower { get; init; }

    /// <summary>The voice paired to each core, keyed by persona id.</summary>
    public IReadOnlyDictionary<string, string> Cores { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Whether the background pairing has run against this provider's voice list.</summary>
    public bool Paired { get; init; }

    /// <summary>
    /// Nothing was ever chosen here. Worth asking before storing one: an entry recording that a
    /// provider was selected once and nothing was picked is a row in the settings file that
    /// restores nothing, and the file is something a Commander reads.
    /// </summary>
    public bool IsEmpty =>
        Ship is null && CarrierCaptain is null && Tower is null && Cores.Count == 0 && !Paired;
}

/// <summary>
/// Which voices belong to which provider, and what happens to them when the provider changes
/// (list.md Phase 19, "Remember which voice you chose for each provider").
/// <para>
/// The behaviour this replaces was correct and lossy: a voice id means nothing to a provider
/// that did not issue it, so carrying one across a switch fails every sentence — and the fix
/// that stopped the silence dropped them. Dropping is right about the <em>live</em> slots and
/// wrong about the file: switching to ElevenLabs to hear what it sounds like, and back, cost
/// the Commander eleven paired cores and the ship's own voice, twice.
/// </para>
/// <para>
/// So a switch is now a stash and a restore rather than a clear. The live slots are emptied
/// exactly as before — that part was never the bug — and what came out of them is written under
/// the provider it belonged to, where switching back finds it.
/// </para>
/// <para>
/// Pure, and deliberately in Core: this is one of the two decisions
/// <see cref="Audio.SpeechWiring"/>'s summary is about, and it is the one whose failure mode is
/// silent data loss rather than silence.
/// </para>
/// </summary>
public static class VoiceMemory
{
    /// <summary>
    /// Makes the stored voices and the selected provider agree, and answers the settings that
    /// result — the same instance when they already agreed, so a caller can compare by reference
    /// and skip a write.
    /// <para>
    /// Asked on every apply rather than only when the provider is seen to change, which is the
    /// difference that matters: watching the process meant a settings file that arrived
    /// <em>already</em> mismatched — written by an older build, or by a switch that never reached
    /// the check — was trusted on every launch and failed every sentence forever. The file says
    /// which provider its voices came from, so the question is asked of the file.
    /// </para>
    /// <para>
    /// A file with nothing recorded is stamped rather than cleared. It was written before d47
    /// recorded this, and its voices are as likely to be right as wrong — throwing them away on a
    /// guess would cost every Commander whose file was fine, while the one whose file is not is
    /// repaired at the seam the moment a voice is actually refused.
    /// </para>
    /// </summary>
    public static D47Settings Reconciled(D47Settings settings)
    {
        var selected = TtsProviderCatalog.Selected(settings.Speech.Provider).Id;

        if (string.Equals(settings.Speech.VoicesProvider, selected, StringComparison.Ordinal))
        {
            return settings;
        }

        if (settings.Speech.VoicesProvider is { } chosenFor)
        {
            return Switched(settings, chosenFor, selected);
        }

        return settings with { Speech = settings.Speech with { VoicesProvider = selected } };
    }

    /// <summary>
    /// The same settings with the live voice slots emptied of <paramref name="from"/>'s choices,
    /// those choices filed under <paramref name="from"/>, and whatever was filed under
    /// <paramref name="to"/> put back.
    /// </summary>
    /// <param name="from">
    /// Whose the live choices are. <b>Null when the file does not say</b> — written before d47
    /// recorded it — and then they are dropped rather than filed, exactly as they were before
    /// any of this was remembered. Filing them under a guess would offer one provider's ids back
    /// as another's, which is the failure this whole mechanism exists to stop.
    /// </param>
    public static D47Settings Switched(D47Settings settings, string? from, string to)
    {
        var stashed = Remembered(settings);
        var restoring = settings.Speech.ProviderVoices.GetValueOrDefault(to) ?? new VoiceChoices();

        var remembered = new Dictionary<string, VoiceChoices>(
            settings.Speech.ProviderVoices,
            StringComparer.OrdinalIgnoreCase);

        if (from is not null)
        {
            if (stashed.IsEmpty)
            {
                // Nothing was chosen while that provider was selected, so there is nothing to
                // come back to. Removed rather than written empty: an entry that restores
                // nothing is a row in a file the Commander reads.
                remembered.Remove(from);
            }
            else
            {
                remembered[from] = stashed;
            }
        }

        // The provider being switched to no longer has anything owed to it — what it had is now
        // live. Left in place it would be restored a second time over choices made since.
        remembered.Remove(to);

        return settings with
        {
            Speech = settings.Speech with
            {
                Voice = restoring.Ship,
                CarrierCaptainVoice = restoring.CarrierCaptain,
                TowerVoice = restoring.Tower,
                VoicesProvider = to,
                ProviderVoices = remembered,
            },
            Persona = settings.Persona with
            {
                Voices = new Dictionary<string, string>(restoring.Cores, StringComparer.Ordinal),

                // Restored with the pairings it describes. Cleared when there are none, which is
                // what lets the pairing run against a provider's list for the first time —
                // leaving it set was how eleven cores kept pointing at voices that had stopped
                // existing.
                VoicesPaired = restoring.Paired,
            },
        };
    }

    /// <summary>The choices currently live, as one value.</summary>
    public static VoiceChoices Remembered(D47Settings settings) => new()
    {
        Ship = settings.Speech.Voice,
        CarrierCaptain = settings.Speech.CarrierCaptainVoice,
        Tower = settings.Speech.TowerVoice,
        Cores = new Dictionary<string, string>(settings.Persona.Voices, StringComparer.Ordinal),
        Paired = settings.Persona.VoicesPaired,
    };
}

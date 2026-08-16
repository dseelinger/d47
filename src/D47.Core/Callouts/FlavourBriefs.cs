using D47.Core.Audio;

namespace D47.Core.Callouts;

/// <summary>
/// What to ask a model for when a callout is being said in character, and what context that
/// question is allowed to carry.
/// </summary>
public sealed record FlavourBrief
{
    /// <summary>The instruction the model is given.</summary>
    public required string Instruction { get; init; }

    /// <summary>
    /// Whether the core aboard's persona block goes with it.
    /// <para>
    /// False for the carrier's two roles, and that is not a detail: handing a Guardian core the
    /// captain's lines would put one of them in two places at once, which is the one thing the
    /// isolation model cannot survive (guardian-personas.md). The brief below supplies its own
    /// description of who is speaking instead.
    /// </para>
    /// </summary>
    public required bool NeedsPersona { get; init; }

    /// <summary>
    /// Whether the live game state goes with it. An ambient remark is about where the Commander
    /// actually is; the carrier's lines are about nothing that has happened.
    /// </summary>
    public required bool NeedsGameState { get; init; }

    /// <summary>
    /// The character brief for a speaker who is not the ship's AI, or null when
    /// <see cref="NeedsPersona"/> supplies one instead. Occupies the same slot in the prompt.
    /// </summary>
    public string? Speaker { get; init; }
}

/// <summary>
/// Which callouts get said in character, and what the model is asked (list.md Phase 11, "with
/// varied LLM arrival and departure responses").
/// <para>
/// Only the carrier's own lines and the ambient remarks. <b>A danger callout is never rewritten
/// by a model</b>: those fire on the event and say exactly what happened, and "shields are down"
/// is not a line that benefits from personality (list.md Phase 8). That exclusion is the whole
/// safety property here, and it was expressed as the default arm of a switch inside
/// <c>AppHost.VaryAsync</c> — where nothing could assert it, and where adding a case above it
/// would have been the entire cost of losing it.
/// </para>
/// <para>
/// Pure. The root still resolves the persona block, reads the live game state and makes the call;
/// what was lifted is which line is eligible and what it is asked (list.md Phase 19).
/// </para>
/// </summary>
public static class FlavourBriefs
{
    /// <summary>
    /// The brief for one announcement, or null when it is to be said exactly as written.
    /// </summary>
    /// <param name="personalityEnabled">
    /// Personality off silences all of it. The checklist puts "no ambient remarks" in that item's
    /// own acceptance criteria, and a carrier captain improvising is personality by any reading.
    /// </param>
    public static FlavourBrief? For(Announcement announcement, bool personalityEnabled)
    {
        ArgumentNullException.ThrowIfNull(announcement);

        if (!personalityEnabled)
        {
            return null;
        }

        if (announcement.Key.StartsWith(AmbientCallout.KeyPrefix, StringComparison.Ordinal))
        {
            return new FlavourBrief
            {
                Instruction =
                    "Make one short unprompted remark about where the Commander is right now — you are "
                    + $"{AmbientLines.Describe(SituationOf(announcement.Key))}. Nothing has happened; this is you "
                    + "filling a quiet moment in character. One or two sentences. Do not ask a question, "
                    + "do not offer help, and do not comment on the Commander's decisions.",
                NeedsPersona = true,
                NeedsGameState = true,
            };
        }

        if (announcement.Voice is VoiceRole.CarrierCaptain or VoiceRole.TowerControl)
        {
            return new FlavourBrief
            {
                Speaker =
                    $"You are {(announcement.Voice == VoiceRole.CarrierCaptain
                        ? "the captain of the Commander's fleet carrier"
                        : "the tower controller aboard the Commander's fleet carrier")}. You are a "
                    + "professional, not a character — brief, competent and human. One short sentence. "
                    + "Never mention being an AI.",
                Instruction = $"Say this in your own words, once: \"{announcement.Text}\"",
                NeedsPersona = false,
                NeedsGameState = false,
            };
        }

        return null;
    }

    /// <summary>
    /// Which situation an ambient announcement was about, from its key. Carried on the key rather
    /// than read back off the callout, because a batch may hold more than one and the callout
    /// only remembers the last.
    /// </summary>
    public static AmbientSituation SituationOf(string key) =>
        Enum.TryParse<AmbientSituation>(
            key[AmbientCallout.KeyPrefix.Length..], ignoreCase: true, out var situation)
            ? situation
            : AmbientSituation.None;
}

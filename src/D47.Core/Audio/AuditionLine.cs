using D47.Core.Persona;

namespace D47.Core.Audio;

/// <summary>
/// What a voice says when it is auditioned (Phase 19, "Hear a voice before you choose
/// it").
/// <para>
/// <b>The core's own line, not a generic sample.</b> The picker lists several hundred names and
/// the Commander is casting a character from them — "Bill - Wise, Mature, Balanced" and "George
/// - Warm, Captivating Storyteller" are both true and neither tells you which one is Warden.
/// A neutral sentence answers a different question than the one being asked.
/// </para>
/// <para>
/// The opening of it rather than the whole introduction, because Warden's runs to five sentences
/// and every press of a play glyph is a synthesis request billed by the character. The opening is
/// where a core's voice is most itself anyway — it is written as the opening.
/// </para>
/// <para>
/// <b>Verbatim, and it is the exception.</b> A core's introduction goes through a model everywhere
/// else it is spoken, because <c>guardian-personas.md</c> files those as sample lines rather than
/// as a script — see <see cref="Callouts.FlavourBriefs.Introducing"/>. Here it must not: this is a
/// sample being synthesised so two voices can be compared, and a line that came out differently on
/// each press would compare them on different words. It is also a model turn per press of a play
/// glyph, to answer a question about the voice rather than about the writing.
/// </para>
/// </summary>
public static class AuditionLine
{
    /// <summary>
    /// Long enough to judge a voice by and short enough to press four times. A first sentence
    /// longer than this is cut at the last word that fits rather than mid-syllable.
    /// </summary>
    private const int LongestLine = 180;

    /// <summary>
    /// Below this, one more sentence is taken. Six of the eleven cores open by addressing the
    /// Commander, so "the first sentence" is often <c>"Commander."</c> and nobody can cast a
    /// character from one word.
    /// </summary>
    private const int ShortestLine = 60;

    /// <summary>
    /// Roughly how long an audition line is, for pricing an audition before it is played. The
    /// eleven cores produce 71 to 171 characters with a median of 134, measured rather than
    /// guessed — see HearAVoiceBeforeYouChooseItTests, which fails if this drifts outside the
    /// range the catalogue actually produces.
    /// <para>
    /// Deliberately an estimate and said as one in the line above the picker's list, because the
    /// exact figure is not known until the core aboard is, and the Commander has to see a number
    /// before anybody presses anything. Rounded down from the median, since a price quoted low
    /// and charged high is the wrong way round.
    /// </para>
    /// </summary>
    public const int TypicalCharacters = 130;

    /// <summary>The line this core auditions with, and the role it is being cast in.</summary>
    public static string For(D47.Core.Persona.Persona persona) => Trimmed(Opening(persona.Intro));

    /// <summary>
    /// The line for a role that is nobody's core — the carrier's captain, its tower. They are
    /// different people from the ship's AI and have no written introduction, so they say what
    /// they are actually for, in the register they will actually be heard in.
    /// </summary>
    public static string For(VoiceRole role) => role switch
    {
        VoiceRole.CarrierCaptain =>
            "Carrier bridge. We're holding at the beacon, Commander — jump plotted whenever you say.",

        VoiceRole.TowerControl =>
            "Tower to inbound. You're cleared for landing pad seven, Commander. Mind the traffic.",

        _ => "Systems nominal, Commander. Standing by.",
    };

    /// <summary>
    /// Enough of a voice to judge it by — one sentence where one is enough and two where it is
    /// not.
    /// <para>
    /// Not simply the first sentence, which is what this was and what the item asks for in the
    /// easy case. Six of the eleven cores open by addressing the Commander, so the first
    /// sentence of Warden's introduction is the single word <c>"Commander."</c> — a sample too
    /// short to tell one voice from another, which is the entire point of auditioning. So whole
    /// sentences are taken until there is enough to hear, and the boundary is still a sentence
    /// boundary: a sample that stops mid-clause tells you about d47's string handling rather
    /// than about the voice.
    /// </para>
    /// </summary>
    private static string Opening(string intro)
    {
        var taken = 0;

        while (taken < intro.Length)
        {
            var end = intro.AsSpan(taken).IndexOfAny('.', '!', '?');

            if (end < 0)
            {
                return intro.Trim();
            }

            taken += end + 1;

            if (taken >= ShortestLine)
            {
                break;
            }
        }

        return intro[..taken].Trim();
    }

    private static string Trimmed(string line)
    {
        if (line.Length <= LongestLine)
        {
            return line;
        }

        var cut = line.LastIndexOf(' ', LongestLine);

        return (cut < 0 ? line[..LongestLine] : line[..cut]).TrimEnd(',', ';', ' ') + "…";
    }
}

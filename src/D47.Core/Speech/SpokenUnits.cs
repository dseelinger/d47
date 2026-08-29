using System.Text.RegularExpressions;

namespace D47.Core.Speech;

/// <summary>
/// Unit abbreviations written out as words, for the provider only
/// (<a href="https://github.com/dseelinger/d47/issues/155">#155</a>).
/// <para>
/// <b>Reported against ElevenLabs, and it is not ElevenLabs' fault.</b> The Commander heard
/// <em>"Perez Ring, LHS 2637 — 5.79 lee, 395 lez out"</em>: <c>ly</c> is a word to a
/// text-to-speech service and <em>lee</em> is a perfectly reasonable guess at it. Kokoro's
/// dictionary would say <em>lie</em>, just as wrongly, and no provider has a lexicon parameter
/// all four of them honour — so the abbreviation is expanded before any of them sees it.
/// </para>
/// <para>
/// <b>Here rather than at the seventeen call sites that write <c>ly</c> into prose.</b> Those are
/// correct as text, and chasing them individually is how three get missed and the fourth
/// regresses. This runs between the sentence splitter and the provider, so it covers every
/// provider at once and never touches what the caption, the transcript or the panel shows.
/// </para>
/// <para>
/// <b>Number-anchored, whole-token, and that is the whole of what keeps it safe.</b> The unit has
/// to follow a number with nothing but spaces between them, and has to be a token in its own
/// right — so <c>5.79 ly</c> is rewritten and <c>LHS</c>, <c>Lys</c>, <c>lyrics</c> and a system
/// name ending in <em>ly</em> are not. This is the male/female lesson
/// (<a href="https://github.com/dseelinger/d47/issues/146">#146</a>) applied in advance: whole
/// tokens, anchored, never substrings.
/// </para>
/// <para>
/// <b>Case is not required to match</b>, because the game and d47's own prose write <c>Cr</c>,
/// <c>Ls</c> and <c>LS</c> for the same units, and a table that only knew one spelling would say
/// half of them. The anchor is what does the work here, not the capitalisation: nothing reaches
/// this table without a number in front of it and a token boundary on both sides.
/// </para>
/// <para>
/// <b>This is not the pronunciation override file</b>
/// (<a href="https://github.com/dseelinger/d47/issues/150">#150</a>), which is per-word IPA for the
/// local voice. They compose: this turns <c>ly</c> into <em>light years</em>, and every provider
/// already says those two words right.
/// </para>
/// </summary>
public static class SpokenUnits
{
    /// <summary>
    /// Every unit that is spoken, with what to say for one of it and for any other number of it.
    /// <para>
    /// <b>The next unit is a row here, not a hunt.</b> That is the point of the table: the five
    /// were found by reading what actually reaches spoken prose — <c>ly</c> and <c>ls</c> are the
    /// volume offenders at seventeen sites, <c>cr</c> is in the colonisation, engineering and trade
    /// answers, <c>MW</c> is the power gauge's unit, and bare <c>t</c> is rare because most call
    /// sites already write <em>tonnes</em> out. The rare one is here anyway, because the
    /// carrier-routing work is about to add more of it.
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, (string One, string Many)> Units =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ly"] = ("light year", "light years"),
            ["ls"] = ("light second", "light seconds"),
            ["t"] = ("tonne", "tonnes"),
            ["MW"] = ("megawatt", "megawatts"),
            ["cr"] = ("credit", "credits"),
        };

    /// <summary>
    /// A number, the spaces after it, and a unit — with a token boundary on both ends.
    /// <para>
    /// The lookbehind is what stops the number being matched inside something larger, so the
    /// <c>2637</c> of a catalogue number cannot anchor a unit that happens to follow it. The
    /// lookahead is what makes the unit whole: <c>5 tonnes</c> does not match <c>t</c>, and
    /// <c>5 lyrics</c> does not match <c>ly</c>.
    /// </para>
    /// <para>
    /// The number itself is captured only to be read and put back unchanged. Saying it is the
    /// ladder's business, not this one's — and since #177 it can say a decimal.
    /// </para>
    /// </summary>
    private static readonly Regex Anchored = new(
        @"(?<![\p{L}\d])(?<number>\d+(?:,\d{3})*(?:\.\d+)?)(?<gap>[ \t]+)(?<unit>ly|ls|t|MW|cr)(?![\p{L}\d])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// One line as it should be spoken. The written form is returned unchanged where it holds no
    /// number-anchored unit, which is most lines.
    /// </summary>
    public static string Rewrite(string? line) =>
        string.IsNullOrEmpty(line) ? line ?? string.Empty : Anchored.Replace(line, Say);

    /// <summary>
    /// One match, said.
    /// <para>
    /// <b>Singular at exactly one, plural everywhere else — including at 1.0.</b> A decimal is
    /// plural however small it is: a person says <em>1.5 light years</em> and <em>0.5 light
    /// years</em>, and would say <em>1.0 light years</em> too. So the test is the digits as
    /// written rather than the value they parse to, which also means no number is parsed here at
    /// all.
    /// </para>
    /// </summary>
    private static string Say(Match match)
    {
        var number = match.Groups["number"].Value;
        var (one, many) = Units[match.Groups["unit"].Value];

        return number + match.Groups["gap"].Value + (number == "1" ? one : many);
    }
}

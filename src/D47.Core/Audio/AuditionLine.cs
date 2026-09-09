using D47.Core.Persona;

namespace D47.Core.Audio;

/// <summary>What a voice says when it is auditioned (Phase 19, "Hear a voice before you choose it").</summary>
public static class AuditionLine
{
    /// <summary>Long enough to judge a voice by and short enough to press four times.</summary>
    private const int LongestLine = 180;

    /// <summary>Below this, one more sentence is taken.</summary>
    private const int ShortestLine = 60;

    /// <summary>Roughly how long an audition line is, for pricing an audition before it is played.</summary>
    public const int TypicalCharacters = 130;

    /// <summary>The line this core auditions with, and the role it is being cast in.</summary>
    public static string For(D47.Core.Persona.Persona persona) => Trimmed(Opening(persona.Intro));

    /// <summary>The line for a role that is nobody's core — the carrier's captain, its tower.</summary>
    public static string For(VoiceRole role) => role switch
    {
        VoiceRole.CarrierCaptain =>
            "Carrier bridge. We're holding at the beacon, Commander — jump plotted whenever you say.",

        VoiceRole.TowerControl =>
            "Tower to inbound. You're cleared for landing pad seven, Commander. Mind the traffic.",

        _ => "Systems nominal, Commander. Standing by.",
    };

    /// <summary>
    /// Enough of a voice to judge it by — one sentence where one is enough and two where it is not.
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

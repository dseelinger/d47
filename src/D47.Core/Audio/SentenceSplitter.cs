using System.Text;

namespace D47.Core.Audio;

/// <summary>
/// Turns a stream of model text deltas into complete sentences, as soon as each one closes.
/// <para>
/// This is the largest perceived-latency win available (list.md Phase 5, architecture.md §6):
/// speech starts at the first sentence boundary instead of at end of turn. It only exists
/// because the provider streams — a completion that arrives whole has no first boundary to
/// start at.
/// </para>
/// <para>
/// A terminator is only a boundary once the character *after* it has arrived, because "12." is
/// the start of "12.5 light years" as often as it is the end of anything. That means the last
/// sentence of a reply always waits for <see cref="Flush"/>, which is correct: the stream
/// ending is what proves it was the last one.
/// </para>
/// </summary>
public sealed class SentenceSplitter
{
    /// <summary>
    /// Where a run-on gets broken anyway. A model that produces four hundred characters with
    /// no terminator would otherwise mean no speech at all until the turn ended, which is the
    /// exact failure this class exists to prevent. Broken at punctuation or a space, never
    /// mid-word.
    /// </summary>
    private const int SoftCap = 320;

    /// <summary>
    /// Trailing tokens that end in a full stop without ending a sentence. Kept short on
    /// purpose — every entry is a case where d47 stays silent a beat longer, and the cost of
    /// missing one is a small pause in the wrong place rather than a wrong reading.
    /// <para>
    /// `Cmdr` earns its place: it is in front of a name in a large share of what this app will
    /// ever say out loud.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        "cmdr", "cdr", "dr", "mr", "mrs", "ms", "st", "lt", "sgt", "capt", "prof",
        "vs", "etc", "approx", "no", "fig", "mt", "e.g", "i.e", "a.m", "p.m",
    };

    private readonly StringBuilder _buffer = new();

    /// <summary>Everything not yet emitted. Exposed for the caller to reason about, not to mutate.</summary>
    public int Pending => _buffer.Length;

    /// <summary>
    /// Adds text and returns whatever sentences that completed. Usually none; occasionally
    /// more than one, when a delta lands several boundaries at once.
    /// </summary>
    public IReadOnlyList<string> Push(string text)
    {
        _buffer.Append(text);

        List<string>? completed = null;

        while (NextBoundary() is { } end)
        {
            var sentence = _buffer.ToString(0, end).Trim();
            _buffer.Remove(0, end);

            if (sentence.Length > 0)
            {
                (completed ??= []).Add(sentence);
            }
        }

        return (IReadOnlyList<string>?)completed ?? [];
    }

    /// <summary>
    /// The remainder, once the stream has ended. This is where the final sentence of every
    /// reply comes from, since nothing else can prove a trailing terminator was terminal.
    /// </summary>
    public string? Flush()
    {
        var remainder = _buffer.ToString().Trim();
        _buffer.Clear();
        return remainder.Length > 0 ? remainder : null;
    }

    /// <summary>The index just past the end of a complete sentence, or null if there isn't one yet.</summary>
    private int? NextBoundary()
    {
        var text = _buffer;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (c == '\n')
            {
                return i + 1;
            }

            if (c is not ('.' or '!' or '?'))
            {
                continue;
            }

            // Run the terminator out — "..." and "?!" are one boundary, not three.
            var end = i;
            while (end + 1 < text.Length && text[end + 1] is '.' or '!' or '?')
            {
                end++;
            }

            // A closing quote or bracket belongs to the sentence it closes.
            while (end + 1 < text.Length && text[end + 1] is '"' or '\'' or ')' or ']' or '”' or '’')
            {
                end++;
            }

            if (end + 1 >= text.Length)
            {
                // The deciding character has not streamed in yet. Waiting is the whole point.
                break;
            }

            if (!char.IsWhiteSpace(text[end + 1]))
            {
                // "12.5", "v4.0", "Col 285 Sector AB-C d1.2" — not a boundary at all.
                continue;
            }

            if (c == '.' && EndsWithAbbreviation(text, i))
            {
                continue;
            }

            return end + 1;
        }

        return text.Length >= SoftCap ? SoftCapBoundary(text) : null;
    }

    private static bool EndsWithAbbreviation(StringBuilder text, int dotIndex)
    {
        // Walk back over the token, keeping interior dots so "e.g" is recognisable as itself.
        var start = dotIndex;
        while (start > 0 && (char.IsLetter(text[start - 1]) || text[start - 1] == '.'))
        {
            start--;
        }

        return start != dotIndex && Abbreviations.Contains(text.ToString(start, dotIndex - start));
    }

    /// <summary>The last comma or space inside the cap, so a forced break still lands between words.</summary>
    private static int? SoftCapBoundary(StringBuilder text)
    {
        for (var i = SoftCap - 1; i > SoftCap / 3; i--)
        {
            if (text[i] is ',' or ';' or ':' or '—')
            {
                return i + 1;
            }
        }

        for (var i = SoftCap - 1; i > SoftCap / 3; i--)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                return i + 1;
            }
        }

        // A single unbroken 320-character token is not language. Let it keep buffering rather
        // than cutting a word in half on the way to the synthesiser.
        return null;
    }
}

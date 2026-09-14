using System.Text.RegularExpressions;
using D47.Core.Journal;

namespace D47.Core.Speech;

/// <summary>
/// Drops a repeated "Commander" address from what is spoken: one kept every thirty seconds, shared
/// across every voice that speaks through it (#196).
/// </summary>
public sealed class SpokenAddress(IWallClock clock)
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(30);

    private DateTimeOffset? _lastKept;

    private string? _surname;
    private Regex _pattern = Pattern(null);

    /// <summary>The Commander's full name, so the surname that follows "Commander" can be recognised.</summary>
    public string? CommanderName
    {
        set
        {
            var surname = CommanderAddress.Surname(value);

            if (surname == _surname)
            {
                return;
            }

            _surname = surname;
            _pattern = Pattern(surname);
        }
    }

    /// <summary>The line as it will be spoken, with a repeated address dropped.</summary>
    public string Rewrite(string line) =>
        line.Length == 0 ? line : _pattern.Replace(line, Decide);

    private string Decide(Match match)
    {
        var now = clock.UtcNow;
        var keep = _lastKept is not { } last || now - last >= Window;

        if (keep)
        {
            _lastKept = now;
            return match.Value;
        }

        if (match.Groups["lead"].Success)
        {
            return match.Groups["after"] is { Success: true } after
                ? char.ToUpperInvariant(after.Value[0]) + after.Value[1..]
                : string.Empty;
        }

        return match.Groups["mid"].Success ? "," : string.Empty;
    }

    /// <summary>
    /// One "Commander", optionally followed by the known surname — matched only as a vocative address:
    /// leading the sentence before a dash or comma, flanked by commas mid-sentence, trailing before the
    /// close of the sentence, or the whole sentence on its own. Anything else — "the Commander's own",
    /// "Lieutenant Commander", "another Commander" — has none of this shape and is left alone.
    /// </summary>
    private static Regex Pattern(string? surname)
    {
        var address = surname is { Length: > 0 }
            ? $"Commander(?:\\s+{Regex.Escape(surname)})?"
            : "Commander";

        return new Regex(
            $"(?<lead>^{address})(?:\\s*—\\s*|,\\s*)(?<after>.)?"
            + $"|(?<mid>,\\s*{address}\\s*,)"
            + $"|(?<trail>,\\s*{address}\\s*)(?=[.!?]|$)"
            + $"|(?<whole>^{address}\\s*)(?=[.!?]|$)",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }
}

using System.Text;

namespace D47.Core.Interface;

/// <summary>One stretch of help text, and where it points if it points anywhere.</summary>
/// <param name="Text">What the Commander reads.</param>
/// <param name="Target">The capability id this stretch jumps to, or null for ordinary prose.</param>
public readonly record struct HelpSegment(string Text, string? Target);

/// <summary>
/// Cross-references in settings help, written as markdown links whose target is a capability id
/// (https://github.com/dseelinger/d47/issues/65).
/// </summary>
public static class HelpLinks
{
    /// <summary>The help split into stretches, with the linked ones carrying their target.</summary>
    public static IReadOnlyList<HelpSegment> Parse(string? help)
    {
        if (string.IsNullOrEmpty(help))
        {
            return [];
        }

        var segments = new List<HelpSegment>();
        var plain = new StringBuilder();
        var at = 0;

        while (at < help.Length)
        {
            var open = help.IndexOf('[', at);

            if (open < 0)
            {
                plain.Append(help[at..]);
                break;
            }

            var close = help.IndexOf(']', open);

            if (close < 0 || close + 1 >= help.Length || help[close + 1] != '(')
            {
                // Not a link.
                plain.Append(help[at..(open + 1)]);
                at = open + 1;
                continue;
            }

            var end = help.IndexOf(')', close + 2);
            var target = end < 0 ? null : help[(close + 2)..end];

            if (end < 0 || string.IsNullOrWhiteSpace(target) || target.Contains(' ') || target.Contains('['))
            {
                plain.Append(help[at..(open + 1)]);
                at = open + 1;
                continue;
            }

            var label = help[(open + 1)..close];

            if (label.Length == 0)
            {
                plain.Append(help[at..(open + 1)]);
                at = open + 1;
                continue;
            }

            if (open > at)
            {
                plain.Append(help[at..open]);
            }

            if (plain.Length > 0)
            {
                segments.Add(new HelpSegment(plain.ToString(), null));
                plain.Clear();
            }

            segments.Add(new HelpSegment(label, target));
            at = end + 1;
        }

        if (plain.Length > 0)
        {
            segments.Add(new HelpSegment(plain.ToString(), null));
        }

        return segments;
    }

    /// <summary>The sentence with the markup taken out — what is read, searched and spoken.</summary>
    public static string Plain(string? help)
    {
        if (string.IsNullOrEmpty(help) || !help.Contains('['))
        {
            return help ?? string.Empty;
        }

        var built = new StringBuilder();

        foreach (var segment in Parse(help))
        {
            built.Append(segment.Text);
        }

        return built.ToString();
    }

    /// <summary>Every capability id this help points at, in the order it points at them.</summary>
    public static IReadOnlyList<string> TargetsIn(string? help) =>
        [.. Parse(help).Where(segment => segment.Target is not null).Select(segment => segment.Target!)];
}

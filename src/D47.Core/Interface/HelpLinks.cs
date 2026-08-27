using System.Text;

namespace D47.Core.Interface;

/// <summary>One stretch of help text, and where it points if it points anywhere.</summary>
/// <param name="Text">What the Commander reads. Never contains markup.</param>
/// <param name="Target">
/// The capability id this stretch jumps to, or null for ordinary prose.
/// </param>
public readonly record struct HelpSegment(string Text, string? Target);

/// <summary>
/// Cross-references in settings help, written as markdown links whose target is a capability id
/// (https://github.com/dseelinger/d47/issues/65).
/// <para>
/// <b>Declared, never detected.</b> The obvious implementation is to look for section names in the
/// prose and light them up, and it is wrong in both directions: <em>privacy</em> is an ordinary
/// English word that appears in help meaning nothing of the kind, and a matcher would miss a
/// section the moment it was renamed. So a link is written where the help is written —
/// <c>see [Privacy](privacy) for what is sent</c> — which is the same rule that put <c>Help</c> and
/// <c>Level</c> onto <c>NavCrumb</c> rather than into the view.
/// </para>
/// <para>
/// <b>Here rather than in the view</b>, because the target being a real capability is a fact about
/// the help rather than about the drawing, and it is checkable without a window: the help pass
/// already produced three silent link faults, and a link to a section that no longer exists should
/// fail a build rather than a Commander's click.
/// </para>
/// <para>
/// Arithmetic over a string and nothing else. It answers with the plain sentence as well as the
/// pieces, because <em>the plain sentence</em> is what search matches against, what an automation
/// peer reads out, and what a surface with no way to draw a link shows.
/// </para>
/// </summary>
public static class HelpLinks
{
    /// <summary>
    /// The help split into stretches, with the linked ones carrying their target.
    /// <para>
    /// Deliberately strict about what counts: <c>[</c> then text with no bracket, <c>](</c>, then a
    /// target with no bracket or space, then <c>)</c>. Anything else is left exactly as written —
    /// help prose contains square brackets for other reasons, and a half-recognised link would be
    /// worse than none, since it would silently eat the text it failed to parse.
    /// </para>
    /// </summary>
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
                // Not a link. Take the bracket as prose and carry on past it, rather than
                // rescanning from it and looping.
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

    /// <summary>
    /// The sentence with the markup taken out — what is read, searched and spoken.
    /// <para>
    /// Everything that is not the settings page uses this: <c>CoverageInventory</c> writes markdown
    /// and the row filter matches against prose, and neither should see a target that the Commander
    /// never does. Searching the raw string would match "privacy" inside <c>(privacy)</c> and mark a
    /// hit in text that is not on the screen.
    /// </para>
    /// </summary>
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

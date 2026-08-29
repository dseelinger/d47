using System.Globalization;
using System.Text;

namespace D47.Core.Diagnostics.Donation;

/// <summary>
/// The paperwork an excerpt carries: which build it came off, and when it was taken.
/// <para>
/// <b>Who donated it is deliberately absent.</b> The issue it rides on already says — a GitHub
/// comment has an author on it — and writing a name into the body would put an identity inside the
/// one artefact this whole path exists to keep identities out of.
/// </para>
/// </summary>
/// <param name="Build">The running build's full stamp, version and commit. What the fix is against.</param>
/// <param name="TakenAt">When the Commander cut it.</param>
public sealed record ExcerptPaperwork(string Build, DateTimeOffset TakenAt);

/// <summary>
/// The excerpt as it will look in the issue — <b>and this text is the consent</b>
/// (<a href="https://github.com/dseelinger/d47/issues/160">#160</a>).
/// <para>
/// One rendering, used twice: it is what the review window shows and it is what the clipboard
/// gets. That is the whole of "show exactly what leaves" — a preview assembled by one code path
/// and a payload assembled by another are two artefacts, and the Commander only ever read one of
/// them.
/// </para>
/// <para>
/// Markdown, because a scrubbed excerpt is kilobytes and travels inside the GitHub issue itself.
/// There is no backend and there is not going to be one. The two halves sit in
/// <c>&lt;details&gt;</c> so an issue stays readable with a hundred log lines pasted into it.
/// </para>
/// </summary>
public static class ExcerptReport
{
    /// <summary>
    /// Four backticks rather than three. A log line is free text and d47 has been known to say
    /// something with a fence in it; a three-backtick fence would end there and spill the rest of
    /// the log into the issue as prose.
    /// </summary>
    private const string Fence = "````";

    /// <summary>The marker that says what this block is, for anybody reading the issue's source.</summary>
    public const string Marker = "<!-- d47 incident excerpt -->";

    /// <summary>Renders the whole block.</summary>
    public static string Render(IncidentExcerpt excerpt, ExcerptPaperwork paperwork)
    {
        var report = new StringBuilder();

        report.AppendLine(Marker);
        report.AppendLine("### Incident excerpt");
        report.AppendLine();

        report.AppendLine(
            $"Cut from d47 {paperwork.Build} at {Stamp(paperwork.TakenAt)}, "
            + $"covering {Stamp(excerpt.From)} to {Stamp(excerpt.To)} "
            + $"({Length(excerpt.To - excerpt.From)}).");

        report.AppendLine();
        report.AppendLine(
            "**What it is for.** The journal half is a replay case: `spike/CorpusReplay` drives it "
            + "through the same fold the running app uses, so the fix is proven against what "
            + "actually happened rather than against a reconstruction, and cannot regress "
            + "silently afterwards. The log half is the diagnosis — what this build did with those "
            + "events.");

        report.AppendLine();
        report.AppendLine($"**What was done to it.** {Treatment(excerpt.Tally)}");

        report.AppendLine();
        report.AppendLine(
            "**Taking it back.** This excerpt lives in this issue and nowhere else — there is no "
            + "server behind it and nothing was sent anywhere else. Ask here and it is deleted.");

        report.AppendLine();
        Half(
            report,
            $"Journal — {Count(excerpt.Tally.JournalEvents, "event")}, replay-ready",
            "json",
            excerpt.Journal);

        report.AppendLine();
        Half(
            report,
            $"d47 log — {Count(excerpt.Tally.LogEntries, "entry", "entries")}",
            "text",
            excerpt.Log);

        return report.ToString();
    }

    /// <summary>
    /// The sentence about what is missing. <b>Said in every case, including the case where nothing
    /// was replaced</b>: "no names were found to replace" and a silence about names are the same
    /// text to a reader who does not know the rule, and only one of them is a claim.
    /// </summary>
    private static string Treatment(ExcerptTally tally)
    {
        var said = new List<string>
        {
            tally.NamesReplaced > 0
                ? $"{Count(tally.NamesReplaced, "name or ID", "names and IDs")} replaced with "
                  + "consistent stand-ins, by field list rather than by guesswork"
                : "no name or ID was found to replace",

            tally.InGameMessages > 0
                ? $"{Count(tally.InGameMessages, "in-game message", "in-game messages")} withheld — "
                  + "another player's words are not the donor's to give"
                : "no in-game message arrived in this window",

            tally.MySpeechLines == 0
                ? "the Commander said nothing aloud in this window"
                : tally.MySpeechIncluded
                    ? $"the Commander's own speech is included, on purpose "
                      + $"({Count(tally.MySpeechLines, "line")})"
                    : $"the Commander's own speech is held back "
                      + $"({Count(tally.MySpeechLines, "line")})",
        };

        if (tally.JournalWithheld > 0)
        {
            said.Add(
                $"{Count(tally.JournalWithheld, "journal event")} dropped whole, unreadable to the "
                + "scrubber and therefore not checked");
        }

        return string.Concat(char.ToUpper(said[0][0], CultureInfo.InvariantCulture), said[0][1..])
               + "; " + string.Join("; ", said.Skip(1)) + ".";
    }

    private static void Half(StringBuilder report, string summary, string language, IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            report.AppendLine($"*{summary} — nothing in the window.*");
            return;
        }

        report.AppendLine($"<details><summary>{summary}</summary>");
        report.AppendLine();
        report.AppendLine(Fence + language);

        foreach (var line in lines)
        {
            report.AppendLine(line);
        }

        report.AppendLine(Fence);
        report.AppendLine();
        report.AppendLine("</details>");
    }

    /// <summary>UTC to the second. A report is read in another timezone by definition.</summary>
    private static string Stamp(DateTimeOffset at) =>
        at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ssZ", CultureInfo.InvariantCulture);

    private static string Length(TimeSpan span) =>
        span < TimeSpan.FromMinutes(1)
            ? Count((int)Math.Round(span.TotalSeconds), "second")
            : Count((int)Math.Round(span.TotalMinutes), "minute");

    private static string Count(int howMany, string one, string? many = null) =>
        $"{howMany.ToString(CultureInfo.InvariantCulture)} {(howMany == 1 ? one : many ?? one + "s")}";
}

using System.Globalization;
using System.Text;

namespace D47.Core.Diagnostics.Donation;

/// <summary>The consent step for a payload nobody can read (#174).</summary>
public static class CorpusReport
{
    /// <summary>
    /// Four backticks, for the same reason <see cref="ExcerptReport"/> uses them: a fence inside a
    /// sample would end the block early and spill the rest into the page as prose.
    /// </summary>
    private const string Fence = "````";

    /// <summary>The marker that says what this block is, for anybody reading the source.</summary>
    public const string Marker = "<!-- d47 journal history -->";

    /// <summary>Renders the whole consent document.</summary>
    public static string Render(CorpusSurvey survey, ExcerptPaperwork paperwork)
    {
        var report = new StringBuilder();
        var kinds = survey.Kinds;
        var changed = kinds.Where(kind => kind.Touched).ToList();
        var untouched = kinds.Where(kind => !kind.Touched).ToList();

        report.AppendLine(Marker);
        report.AppendLine("### Journal history");
        report.AppendLine();

        report.AppendLine(
            $"Cut from d47 {paperwork.Build} at {Stamp(paperwork.TakenAt)}, "
            + $"covering {Range(survey)} — {Count(survey.Files, "journal file")}, "
            + $"{Count(survey.Tally.Events, "event")}, "
            + $"{Count(kinds.Count, "distinct event kind")}, {Size(survey.Bytes)}.");

        report.AppendLine();
        report.AppendLine(
            "**What it is for.** Replaying what really happened. `spike/CorpusReplay` drives these events through "
            + "the same fold the running app uses, so a defect can be proven fixed against play "
            + "that really happened and cannot regress silently afterwards. Nobody reads it; it is "
            + "test data, not a bug report.");

        report.AppendLine();
        report.AppendLine(
            $"**Why you are reading this instead of the history itself.** {Count(survey.Tally.Events, "event")} "
            + $"is {Size(survey.Bytes)}. Nobody reads that, and a yes given to a payload nobody "
            + "could have read is not consent. So this describes every *kind* of thing in the "
            + $"donation and shows a real scrubbed line of each — {Count(kinds.Count, "line")} "
            + $"instead of {Count(survey.Tally.Events, "line")}.");

        report.AppendLine();
        report.AppendLine($"**What was done to it.** {Treatment(survey.Tally)}");

        report.AppendLine();
        report.AppendLine(
            "**The limit of this, stated rather than implied.** Every event kind in the donation "
            + "appears below with one real instance, taken from the payload itself rather than "
            + "rebuilt for display. Most of the *volume* does not appear: you are consenting to a "
            + "treatment you can check on every kind, not to bytes you have read. Each sample is "
            + "that kind's **longest** instance — the one with the most fields that survived — so "
            + "what you see is the most that kind ever gives away rather than a typical case.");

        report.AppendLine();
        report.AppendLine(
            // Said whether or not any arrived, for the reason ExcerptReport.Treatment says it in every case:
            // a silence and a "none found" read the same to somebody who does not know the rule, and only one
            // of them is a claim.
            "**What is never taken.** Another player's words. Every in-game message body is dropped "
            + "rather than scrubbed, because a donor cannot consent on someone else's behalf.");

        report.AppendLine();
        report.AppendLine(
            // **The half of the paperwork this report did not have** (#168, #166).
            "**Where this goes, and for how long.** It was scrubbed on the machine it came off, and "
            + "the treatment above is checkable on every kind of thing in it. Sent to Directive 47's "
            + "own store, it is **kept indefinitely** — that is what makes it useful, because a "
            + "regression case that expires stops being one, and permanent retention is exactly "
            + "why the scrub above has to hold rather than merely look right. It is never "
            + "committed to a public repository, so it stays one object and one delete: ask, "
            + "quoting the receipt d47 kept beside its executable, and it goes.");

        report.AppendLine();
        report.AppendLine(DonationNotice.Line);

        report.AppendLine();
        Group(
            report,
            $"Kinds the scrub changed — {changed.Count} of {kinds.Count}",
            "Read these closely: something in every one of them was replaced, dropped, or withheld.",
            changed,
            collapsed: false);

        report.AppendLine();
        Group(
            report,
            $"Kinds the scrub left alone — {untouched.Count} of {kinds.Count}",
            "Nothing in these was altered, so each sample is Elite's own line as the file holds it. "
            + "Listed in full anyway, because an inventory that shows only what was touched is a "
            + "curated one.",
            untouched,
            collapsed: true);

        return report.ToString();
    }

    /// <summary>One band of kinds: the counts as a table, then a sample of each.</summary>
    /// <param name="collapsed">Whether the whole band starts folded.</param>
    private static void Group(
        StringBuilder report,
        string heading,
        string intro,
        IReadOnlyList<KindCensus> kinds,
        bool collapsed)
    {
        report.AppendLine($"#### {heading}");
        report.AppendLine();

        if (kinds.Count == 0)
        {
            report.AppendLine("*None.*");
            return;
        }

        report.AppendLine(intro);
        report.AppendLine();

        if (collapsed)
        {
            report.AppendLine($"<details><summary>All {Count(kinds.Count, "kind")}, with a sample of each</summary>");
            report.AppendLine();
        }

        report.AppendLine("| Event | How many | Changed | Withheld |");
        report.AppendLine("|---|---:|---:|---:|");

        foreach (var kind in kinds)
        {
            report.AppendLine(
                $"| `{kind.Kind}` | {Number(kind.Events)} | {Number(kind.Changed)} | {Number(kind.Withheld)} |");
        }

        report.AppendLine();

        foreach (var kind in kinds)
        {
            Sample(report, kind);
        }

        if (collapsed)
        {
            report.AppendLine("</details>");
        }
    }

    /// <summary>One kind's real scrubbed line, or the note that says why there is none.</summary>
    private static void Sample(StringBuilder report, KindCensus kind)
    {
        if (kind.Sample is not { } sample)
        {
            report.AppendLine(
                $"*`{kind.Kind}` — every one of {Count(kind.Events, "instance")} was withheld whole, "
                + "so there is nothing to show.*");
            report.AppendLine();
            return;
        }

        // **The size is on the fold, because the samples are not the same order of magnitude.** Measured over
        // a real 936-file corpus: most are a couple of hundred characters and one StoredModules is 58,535 — a
        // quarter of the whole document in one line.
        var summary = kind.Changed > 0
            ? $"{kind.Kind} — {Number(kind.Changed)} of {Number(kind.Events)} changed · {Count(sample.Length, "character")}"
            : $"{kind.Kind} — {Count(kind.Events, "instance")}, unchanged · {Count(sample.Length, "character")}";

        report.AppendLine($"<details><summary>{summary}</summary>");
        report.AppendLine();
        report.AppendLine(Fence + "json");
        report.AppendLine(sample);
        report.AppendLine(Fence);
        report.AppendLine();
        report.AppendLine("</details>");
        report.AppendLine();
    }

    /// <summary>
    /// The sentence about what is missing, in the same register as <see cref="ExcerptReport"/>'s — and
    /// said in every case, including the cases where a count is zero, because a silence is not a claim.
    /// </summary>
    private static string Treatment(CorpusTally tally)
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
                : "no in-game message arrived in this range",
        };

        if (tally.LinksDropped > 0)
        {
            said.Add(
                $"{Count(tally.LinksDropped, "squadron link")} dropped — the flag saying which minor "
                + "faction is the Commander's squadron's, which would have undone the squadron "
                + "stand-ins in one hop");
        }

        if (tally.Withheld > 0)
        {
            said.Add(
                $"{Count(tally.Withheld, "event")} dropped whole, unreadable to the scrubber and "
                + "therefore not checked");
        }

        if (tally.Unreadable > 0)
        {
            said.Add(
                $"{Count(tally.Unreadable, "line")} in the journal files could not be read as an "
                + "event at all, so they are in none of the counts above and in none of the samples "
                + "below");
        }

        return string.Concat(char.ToUpper(said[0][0], CultureInfo.InvariantCulture), said[0][1..])
               + "; " + string.Join("; ", said.Skip(1)) + ".";
    }

    /// <summary>
    /// What the donation actually covers, from the events rather than from what was asked for — a scope
    /// of "everything on disk" has no start date to echo, and echoing the request would say nothing
    /// about what was found.
    /// </summary>
    private static string Range(CorpusSurvey survey) =>
        survey is { First: { } first, Last: { } last }
            ? $"{Stamp(first)} to {Stamp(last)}"
            : "an empty range — no events were found";

    /// <summary>UTC to the day.</summary>
    private static string Stamp(DateTimeOffset at) =>
        at.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Size(long bytes) =>
        bytes >= 1024L * 1024L
            ? $"{(bytes / (1024.0 * 1024.0)).ToString("0.#", CultureInfo.InvariantCulture)} MB"
            : $"{(bytes / 1024.0).ToString("0.#", CultureInfo.InvariantCulture)} KB";

    private static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Count(int howMany, string one, string? many = null) =>
        $"{Number(howMany)} {(howMany == 1 ? one : many ?? one + "s")}";
}

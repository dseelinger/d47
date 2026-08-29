using System.Globalization;
using System.Text;

namespace D47.Core.Diagnostics.Donation;

/// <summary>
/// The consent step for a payload nobody can read
/// (<a href="https://github.com/dseelinger/d47/issues/174">#174</a>).
/// <para>
/// <b>#160's control was "the Commander reads the scrubbed excerpt and says yes to that", and it
/// does not survive a corpus.</b> Nobody reads 383 MB, or 32.5 MB gzipped, or 712,000 events, and a
/// yes given to a payload nobody could have read is the consent form this feature exists not to be.
/// This is what replaces the show step when the payload cannot be shown.
/// </para>
/// <para>
/// <b>Three things were sketched in #174 and this is all of them rather than a choice between
/// them.</b> A <i>report</i> — the counts, the treatment, what was withheld. A <i>sampled read</i> —
/// one real instance of every kind, so nothing is agreed to unseen in kind even though most is
/// unseen in volume. And the <i>staging</i> that made staged donation attractive, which turns out
/// to live in <see cref="CorpusDonation"/> rather than here: the payload is assembled one journal
/// file at a time so nothing is ever held whole.
/// </para>
/// <para>
/// <b>What makes it work is a size argument.</b> This document is <i>O(distinct event kinds)</i>
/// and the payload is <i>O(events)</i> — a few hundred lines against several hundred thousand.
/// Reviewing session by session would not have helped: 935 reviews is as unreadable as one 383 MB
/// file, and the number of kinds does not grow with the number of sittings.
/// </para>
/// <para>
/// <b>The samples are lifted from the payload, not rebuilt for display.</b> They are the same
/// strings the write pass emits, produced by the same scrub with the same
/// <see cref="Pseudonyms"/> — which is #160's "what is shown is what leaves" holding on a payload
/// that cannot be shown in full.
/// </para>
/// </summary>
public static class CorpusReport
{
    /// <summary>
    /// Four backticks, for the same reason <see cref="ExcerptReport"/> uses them: a fence inside a
    /// sample would end the block early and spill the rest into the page as prose.
    /// </summary>
    private const string Fence = "````";

    /// <summary>The marker that says what this block is, for anybody reading the source.</summary>
    public const string Marker = "<!-- d47 journal corpus -->";

    /// <summary>Renders the whole consent document.</summary>
    public static string Render(CorpusSurvey survey, ExcerptPaperwork paperwork)
    {
        var report = new StringBuilder();
        var kinds = survey.Kinds;
        var changed = kinds.Where(kind => kind.Touched).ToList();
        var untouched = kinds.Where(kind => !kind.Touched).ToList();

        report.AppendLine(Marker);
        report.AppendLine("### Journal corpus");
        report.AppendLine();

        report.AppendLine(
            $"Cut from d47 {paperwork.Build} at {Stamp(paperwork.TakenAt)}, "
            + $"covering {Range(survey)} — {Count(survey.Files, "journal file")}, "
            + $"{Count(survey.Tally.Events, "event")}, "
            + $"{Count(kinds.Count, "distinct event kind")}, {Size(survey.Bytes)}.");

        report.AppendLine();
        report.AppendLine(
            "**What it is for.** A replay corpus. `spike/CorpusReplay` drives these events through "
            + "the same fold the running app uses, so a defect can be proven fixed against play "
            + "that really happened and cannot regress silently afterwards. Nobody reads it; it is "
            + "test data, not a bug report.");

        report.AppendLine();
        report.AppendLine(
            $"**Why you are reading this instead of the corpus.** {Count(survey.Tally.Events, "event")} "
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
            // Said whether or not any arrived, for the reason ExcerptReport.Treatment says it in
            // every case: a silence and a "none found" read the same to somebody who does not know
            // the rule, and only one of them is a claim.
            "**What is never taken.** Another player's words. Every in-game message body is dropped "
            + "rather than scrubbed, because a donor cannot consent on someone else's behalf.");

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

    /// <summary>
    /// One band of kinds: the counts as a table, then a sample of each.
    /// </summary>
    /// <param name="collapsed">
    /// Whether the whole band starts folded. The changed kinds do not, because they are the reason
    /// this document exists; the untouched ones do, because two hundred unchanged lines would bury
    /// the twenty that need reading.
    /// </param>
    private static void Group(
        StringBuilder report,
        string heading,
        string lede,
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

        report.AppendLine(lede);
        report.AppendLine();

        if (collapsed)
        {
            report.AppendLine($"<details><summary>All {Count(kinds.Count, "kind")}, with a sample of each</summary>");
            report.AppendLine();
        }

        report.AppendLine("| Event | In corpus | Changed | Withheld |");
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

        // **The size is on the fold, because the samples are not the same order of magnitude.**
        // Measured over a real 936-file corpus: most are a couple of hundred characters and one
        // StoredModules is 58,535 — a quarter of the whole document in one line. The longest
        // instance is still the right one to show, because it is the most that kind ever gives
        // away, but a reader deciding whether to open it should not have to open it to find out
        // that it is an inventory dump.
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
    /// The sentence about what is missing, in the same register as
    /// <see cref="ExcerptReport"/>'s — and said in every case, including the cases where a count is
    /// zero, because a silence is not a claim.
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
    /// What the donation actually covers, from the events rather than from what was asked for — a
    /// scope of "everything on disk" has no start date to echo, and echoing the request would say
    /// nothing about what was found.
    /// </summary>
    private static string Range(CorpusSurvey survey) =>
        survey is { First: { } first, Last: { } last }
            ? $"{Stamp(first)} to {Stamp(last)}"
            : "an empty range — no events were found";

    /// <summary>UTC to the day. A corpus spans months, so the seconds say nothing.</summary>
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

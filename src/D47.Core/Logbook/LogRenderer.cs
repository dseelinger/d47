using System.Globalization;
using System.Text;
using D47.Core.Conversation;

namespace D47.Core.Logbook;

/// <summary>Everything one finished log knows about itself. What gets rendered, and what gets reported.</summary>
public sealed record LogWritten
{
    public required LogDigest Digest { get; init; }

    public required LogAudit Audit { get; init; }

    public required LogVoice Asked { get; init; }

    public required LogVoice Used { get; init; }

    public required LogLength Length { get; init; }

    public required string ProviderId { get; init; }

    public required string Model { get; init; }

    public required DateTimeOffset WrittenAt { get; init; }

    /// <summary>What it actually cost, from the provider. Null where nothing reported usage.</summary>
    public TurnCost? Cost { get; init; }

    public bool Local { get; init; }

    public string Version { get; init; } = string.Empty;

    /// <summary>Where it landed. Set after the file is written.</summary>
    public string? Path { get; init; }
}

/// <summary>
/// The markdown file (list.md Phase 33, item 1: "written to <c>data/</c> in plain markdown so it
/// can be kept, edited and posted somewhere").
/// <para>
/// <b>The citations stay in the prose.</b> Stripping them would read better and would defeat the
/// item: a Sources section no sentence points into records nothing, and "every sentence traces to
/// an event" would become a claim about a process rather than a property of the file. The header
/// says what the brackets are and that they can be deleted — the item's own framing is that this
/// file is kept and <em>edited</em>, and deleting fifteen bracketed numbers is part of the editing
/// it already expects.
/// </para>
/// <para>
/// <b>The audit is in the header, not in a log nobody reads.</b> If three sentences could not be
/// traced, the Commander finds that out at the top of the thing they are about to post, beside the
/// sentences themselves.
/// </para>
/// </summary>
public static class LogRenderer
{
    public static string Render(LogWritten written)
    {
        ArgumentNullException.ThrowIfNull(written);

        var text = new StringBuilder();
        var digest = written.Digest;
        var audit = written.Audit;

        text.Append("# Commander's log — ").AppendLine(digest.Range.Label);
        text.AppendLine();

        text.Append('*')
            .Append(string.IsNullOrWhiteSpace(digest.Commander) ? "Commander" : $"CMDR {digest.Commander}")
            .Append(", ").Append(digest.Range.Dates).Append(". ")
            .Append(Byline(written.Used))
            .AppendLine("*");
        text.AppendLine();

        text.AppendLine("> **The numbers in brackets are references.** `[4]` means that sentence rests on fact 4,");
        text.AppendLine("> listed under Sources at the foot of this file. Delete them before you post this anywhere.");
        text.Append("> ").AppendLine(audit.Summary());

        if (LogVoices.Degraded(written.Asked, written.Used) is { } degraded)
        {
            text.AppendLine(">");
            text.Append("> ").AppendLine(degraded);
        }

        text.AppendLine();
        text.AppendLine("---");
        text.AppendLine();

        text.AppendLine(audit.Annotated);
        text.AppendLine();
        text.AppendLine("---");
        text.AppendLine();

        Sources(text, written);
        Untraced(text, audit);
        Colophon(text, written);

        return text.ToString();
    }

    private static string Byline(LogVoice used) => used switch
    {
        LogVoice.ShipsAi => "Written by Directive 47, from the Commander's flight journal.",
        LogVoice.FirstPersonWithCommentary =>
            "Written from the Commander's flight journal, with Directive 47 chipping in.",
        _ => "Written from the Commander's flight journal.",
    };

    /// <summary>
    /// What the log drew on. Only the facts a sentence actually cited: this section is the
    /// provenance of what was written, not an inventory of what could have been.
    /// </summary>
    private static void Sources(StringBuilder text, LogWritten written)
    {
        var digest = written.Digest;
        var used = written.Audit.Used.ToHashSet();
        var drawn = digest.Facts.Where(fact => used.Contains(fact.Id)).ToList();

        text.AppendLine("## Sources");
        text.AppendLine();

        text.Append("Read from ")
            .Append(digest.JournalsRead.ToString("N0", CultureInfo.InvariantCulture))
            .Append(" journal file(s), ")
            .Append(digest.EventsRead.ToString("N0", CultureInfo.InvariantCulture))
            .Append(" events, ")
            .Append(digest.Range.From.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
            .Append(" to ")
            .Append(digest.Range.To.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
            .AppendLine(" UTC.");
        text.AppendLine();

        text.Append(digest.Facts.Count.ToString("N0", CultureInfo.InvariantCulture))
            .Append(" facts were computed from those events; this log draws on ")
            .Append(drawn.Count.ToString("N0", CultureInfo.InvariantCulture))
            .AppendLine(" of them.");

        if (digest.FactsDropped > 0)
        {
            text.AppendLine();
            text.Append("A further ")
                .Append(digest.FactsDropped.ToString("N0", CultureInfo.InvariantCulture))
                .AppendLine(" facts were computed and left out to keep the request within budget.");
        }

        text.AppendLine();

        if (drawn.Count == 0)
        {
            text.AppendLine("*Nothing in this log cited a fact, which is itself the problem.*");
            text.AppendLine();
            return;
        }

        text.AppendLine("| # | What D47 counted | From the journal |");
        text.AppendLine("|---|---|---|");

        foreach (var fact in drawn)
        {
            text.Append("| ").Append(fact.Id.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(Cell(fact.Statement))
                .Append(" | ").Append(Cell(fact.Provenance()))
                .AppendLine(" |");
        }

        text.AppendLine();
    }

    private static void Untraced(StringBuilder text, LogAudit audit)
    {
        if (audit.Clean)
        {
            return;
        }

        text.AppendLine("## Not traced");
        text.AppendLine();
        text.AppendLine(
            "These sentences do not rest on anything D47 counted. They are marked in the text above as well. "
            + "Treat them as unverified — check them against your own memory of the session before you keep them.");
        text.AppendLine();

        foreach (var claim in audit.Uncited)
        {
            text.Append("- ").AppendLine(Cell(claim.Text));
        }

        foreach (var id in audit.Unknown)
        {
            text.Append("- A sentence cited fact [")
                .Append(id.ToString(CultureInfo.InvariantCulture))
                .AppendLine("], which does not exist.");
        }

        text.AppendLine();
    }

    private static void Colophon(StringBuilder text, LogWritten written)
    {
        text.AppendLine("---");
        text.AppendLine();

        text.Append("*Written ")
            .Append(written.WrittenAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
            .Append(" by d47");

        if (!string.IsNullOrWhiteSpace(written.Version))
        {
            text.Append(' ').Append(written.Version);
        }

        text.Append(", using ").Append(written.Model).Append(" (").Append(written.ProviderId).Append("). ");

        text.Append(written.Local ? "Ran on this machine, so it cost nothing."
            : written.Cost is { Priced: true } cost
                ? $"Cost {cost.Dollars.ToString("C2", CultureInfo.GetCultureInfo("en-US"))}."
                : "The cost of it is not known — that model has no published price.");

        text.AppendLine("*");

        return;
    }

    /// <summary>
    /// A cell that cannot break the table it is in. Pipes and newlines out of a journal-derived
    /// statement would silently reshape the markdown around them.
    /// </summary>
    private static string Cell(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
}

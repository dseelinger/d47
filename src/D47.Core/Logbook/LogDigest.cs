using System.Globalization;
using System.Text;

namespace D47.Core.Logbook;

/// <summary>
/// The structured summary of what happened, and the only thing the generator is ever handed
/// (list.md Phase 33, "Every sentence traces to an event").
/// <para>
/// <b>Facts, not files.</b> The item is explicit that this is the reason the phase is not simply a
/// prompt: a journal handed to a model produces a better evening than the one that happened, and
/// there is no instruction that reliably stops it. A numbered list of computed statements does,
/// because a sentence with no fact behind it becomes visible rather than plausible.
/// </para>
/// <para>
/// <b>Nothing anybody typed is in here.</b> <c>ReceiveText</c> and <c>SendText</c> are excluded by
/// <see cref="LogDigestBuilder"/> outright rather than filtered — in-game comms are the untrusted
/// channel architecture.md §7 names, this is the largest prompt d47 ever assembles, and the output
/// is the one artefact of d47 that leaves the machine.
/// </para>
/// </summary>
public sealed record LogDigest
{
    public required LogRange Range { get; init; }

    /// <summary>The Commander's name as Elite reported it, or null where no journal said.</summary>
    public string? Commander { get; init; }

    /// <summary>The Frontier id, which is what a log is filed under. Never part of the prompt.</summary>
    public string? FrontierId { get; init; }

    public IReadOnlyList<LogFact> Facts { get; init; } = [];

    public required int JournalsRead { get; init; }

    public required long EventsRead { get; init; }

    /// <summary>
    /// Facts computed and then dropped to stay under the cap. Non-zero is a real state and the
    /// prompt says so out loud — a log silently written from a third of a week is the same class
    /// of untruth the whole phase exists to prevent.
    /// </summary>
    public int FactsDropped { get; init; }

    public bool Any => Facts.Count > 0;

    /// <summary>
    /// The text handed to the model. Deterministic, so the same window twice produces the same
    /// bytes and a test can assert on them.
    /// </summary>
    public string Render()
    {
        var text = new StringBuilder();

        text.Append("COMMANDER: ").AppendLine(string.IsNullOrWhiteSpace(Commander) ? "not named in the journals" : Commander);
        text.Append("WINDOW: ").Append(Range.Label).Append(" — ")
            .Append(Range.From.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
            .Append(" to ")
            .Append(Range.To.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
            .Append(" UTC (")
            .Append(Duration(Range.Length))
            .AppendLine(")");
        text.Append("READ: ")
            .Append(JournalsRead.ToString("N0", CultureInfo.InvariantCulture))
            .Append(" journal file(s), ")
            .Append(EventsRead.ToString("N0", CultureInfo.InvariantCulture))
            .AppendLine(" events");

        if (FactsDropped > 0)
        {
            text.Append("NOTE: ")
                .Append(FactsDropped.ToString("N0", CultureInfo.InvariantCulture))
                .AppendLine(" further facts were computed and left out to keep this within budget. Say so.");
        }

        text.AppendLine();

        if (Facts.Count == 0)
        {
            text.AppendLine("FACTS: none. Nothing happened in this window that D47 can account for.");
            return text.ToString();
        }

        text.AppendLine("FACTS. Every one is numbered. Cite these numbers and no others.");
        text.AppendLine();

        foreach (var fact in Facts.Where(fact => !fact.Aggregate))
        {
            Append(text, fact);
        }

        var totals = Facts.Where(fact => fact.Aggregate).ToList();

        if (totals.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("OVER THE WHOLE WINDOW (figures, not moments — do not place these in the story as events):");
            text.AppendLine();

            foreach (var fact in totals)
            {
                Append(text, fact);
            }
        }

        return text.ToString();
    }

    private static void Append(StringBuilder text, LogFact fact) =>
        text.Append('[').Append(fact.Id.ToString(CultureInfo.InvariantCulture)).Append("] (")
            .Append(fact.Kind.ToString().ToLowerInvariant()).Append(") ")
            .AppendLine(fact.Statement);

    /// <summary>A window's length in the words a person would use for it.</summary>
    public static string Duration(TimeSpan length)
    {
        if (length >= TimeSpan.FromDays(1))
        {
            return $"{length.TotalDays:0.#} days";
        }

        if (length >= TimeSpan.FromHours(1))
        {
            return length.Minutes == 0
                ? $"{length.Hours} h"
                : $"{length.Hours} h {length.Minutes} m";
        }

        return $"{Math.Max(1, (int)length.TotalMinutes)} m";
    }
}

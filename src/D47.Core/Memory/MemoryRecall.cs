using System.Text;

namespace D47.Core.Memory;

/// <summary>
/// Which remembered facts reach a prompt (Phase 31, "Recall arrives above the cache breakpoint").
/// </summary>
public static class MemoryRecall
{
    /// <summary>How many facts may reach one prompt.</summary>
    public const int MaxEntries = 8;

    /// <summary>And how many characters, whichever binds first.</summary>
    public const int MaxCharacters = 1_200;

    /// <summary>The chosen entries, best first, and how many there were to choose from.</summary>
    /// <param name="Shown">What reaches the prompt, in the order it is rendered.</param>
    /// <param name="Held">
    /// Everything the store held for this Commander, including what was left out.
    /// </param>
    public sealed record Selection(IReadOnlyList<MemoryEntry> Shown, int Held)
    {
        /// <summary>Whether anything was left behind.</summary>
        public bool IsSample => Shown.Count < Held;
    }

    /// <summary>Picks the set.</summary>
    public static Selection Select(IReadOnlyList<MemoryEntry> entries, MemorySituation situation)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var here = situation.Tags();

        var ordered = entries
            .OrderByDescending(entry => Matches(entry, here))
            .ThenBy(entry => (int)entry.Tier)
            .ThenByDescending(entry => entry.AddedAt ?? DateTimeOffset.MinValue)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .ToArray();

        var shown = new List<MemoryEntry>(MaxEntries);
        var characters = 0;

        foreach (var entry in ordered)
        {
            if (shown.Count == MaxEntries)
            {
                break;
            }

            // Counted against the rendered length rather than the raw fact, because the tier's sentence is
            // part of what is sent and the reason for the budget is to bound what is sent.
            var cost = entry.Spoken().Length + 1;

            if (characters + cost > MaxCharacters && shown.Count > 0)
            {
                break;
            }

            shown.Add(entry);
            characters += cost;
        }

        return new Selection(shown, entries.Count);
    }

    /// <summary>
    /// The block as it goes into the prompt, or null when there is nothing to recall — which is what a
    /// fresh install returns forever until something is written down, and is the difference between an
    /// empty section and no section.
    /// </summary>
    public static string? Render(IReadOnlyList<MemoryEntry> entries, MemorySituation situation)
    {
        var selection = Select(entries, situation);

        if (selection.Shown.Count == 0)
        {
            return null;
        }

        var block = new StringBuilder();

        // The sample size, stated first so it is read before the facts rather than as a footnote, and phrased
        // so a model has nothing to guess about: how many it has, how many exist, and what to do when asked
        // for the rest.
        block.Append(selection.IsSample
            ? $"What you remember about the Commander — {selection.Shown.Count} of {selection.Held} things, "
              + "chosen for where they are and what they are doing. Do not claim this is everything; "
              + "if they ask what you remember, say you can read the whole list back."
            : $"What you remember about the Commander — all {selection.Held} of it.");

        block.Append(
            "\nEach line says how sure you are. An observation is what the journal reported. Something "
            + "you worked out for yourself is never stated as fact, and never repeated back as though "
            + "the Commander said it.");

        foreach (var entry in selection.Shown)
        {
            block.Append("\n- ").Append(entry.Spoken());
        }

        return block.ToString();
    }

    /// <summary>Everything, as the Commander hears it when they ask.</summary>
    public static string Describe(IReadOnlyList<MemoryEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
        {
            return "Nothing yet. Tell me something worth keeping and I will write it down.";
        }

        var said = new StringBuilder($"{entries.Count} thing{(entries.Count == 1 ? "" : "s")}:");

        foreach (var entry in entries
                     .OrderBy(entry => (int)entry.Tier)
                     .ThenByDescending(entry => entry.AddedAt ?? DateTimeOffset.MinValue)
                     .ThenBy(entry => entry.Key, StringComparer.Ordinal))
        {
            said.Append("\n- ").Append(entry.Spoken());
        }

        return said.ToString();
    }

    /// <summary>Whether an entry is about what is happening.</summary>
    private static bool Matches(MemoryEntry entry, IReadOnlyList<string> here) =>
        entry.About.Any(tag => here.Contains(tag, StringComparer.Ordinal));
}

using System.Text;

namespace D47.Core.Memory;

/// <summary>
/// Which remembered facts reach a prompt (Phase 31, "Recall arrives above the cache
/// breakpoint").
/// <para>
/// <b>Selection is the hard part and it is not "send everything".</b> A store that grows for a year
/// cannot all reach the prompt, and the fix is not a bigger prompt — it is a bounded sample that
/// says it is one. So the block that ships states its own arithmetic: <em>8 of 41 things I
/// remember</em>. A model told it holds a sample cannot claim the set is complete, and the Commander
/// asking <em>what do you remember about me</em> reaches the keyword router, which answers from the
/// store rather than from this.
/// </para>
/// <para>
/// <b>Recall sits above the cache breakpoint, and that is the decision this type exists to make
/// survivable.</b> The obvious placement is game state — that is where changing things go — and it
/// is the wrong one: memories change rarely, so paying for them once and reading them cached is
/// strictly better. The cost of getting it wrong in the other direction is severe and easy to miss.
/// Tool schemas serialize first, so a recall block that moved per turn would invalidate the whole
/// prefix, all 39,000-odd bytes of it, on every turn (architecture.md §6). Three things keep it
/// still:
/// </para>
/// <list type="number">
/// <item>
/// The rendered text carries <b>no situation and no live figure</b> — no system name, no ship, no
/// "as of". Flying through twenty systems d47 remembers nothing about renders identically twenty
/// times.
/// </item>
/// <item>
/// The order is deterministic and total, so the same store and the same situation always produce
/// the same bytes in the same order — the property <see cref="Conversation.ToolProfiles"/> exists to
/// protect, for the same reason.
/// </item>
/// <item>
/// The caller compares before assigning, so a recomputation that produces the same text costs
/// nothing. See <c>AppHost.ApplyRecall</c>.
/// </item>
/// </list>
/// <para>
/// A miss then happens when the Commander flies somewhere they have history — which is the turn
/// where the memory is the reason they wanted it.
/// </para>
/// </summary>
public static class MemoryRecall
{
    /// <summary>
    /// How many facts may reach one prompt. Small on purpose: this is a companion recalling things
    /// about a person, and a block of forty facts is a dossier being read out.
    /// </summary>
    public const int MaxEntries = 8;

    /// <summary>
    /// And how many characters, whichever binds first. Eight facts at the 400-character ceiling
    /// would be 3,200 characters sitting above the breakpoint next to a 5,300-character system
    /// block, which is enough to be worth bounding twice.
    /// </summary>
    public const int MaxCharacters = 1_200;

    /// <summary>
    /// The chosen entries, best first, and how many there were to choose from.
    /// </summary>
    /// <param name="Shown">What reaches the prompt, in the order it is rendered.</param>
    /// <param name="Held">Everything the store held for this Commander, including what was left out.</param>
    public sealed record Selection(IReadOnlyList<MemoryEntry> Shown, int Held)
    {
        /// <summary>Whether anything was left behind. What makes the sample-size sentence worth saying.</summary>
        public bool IsSample => Shown.Count < Held;
    }

    /// <summary>
    /// Picks the set. Pure, and ordered by four keys so the result is total rather than
    /// nearly-deterministic:
    /// <list type="number">
    /// <item>whether the entry is about the situation the Commander is in;</item>
    /// <item>tier — the Commander's own word above an observation above an inference, because that
    /// is the order in which d47 is entitled to bring something up;</item>
    /// <item>newest first, since a fact restated recently is the one in play;</item>
    /// <item>key, which breaks every remaining tie and never ties itself.</item>
    /// </list>
    /// </summary>
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

            // Counted against the rendered length rather than the raw fact, because the tier's
            // sentence is part of what is sent and the whole point of the budget is to bound what
            // is sent.
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
    /// The block as it goes into the prompt, or null when there is nothing to recall — which is
    /// what a fresh install returns forever until something is written down, and is the difference
    /// between an empty section and no section.
    /// </summary>
    public static string? Render(IReadOnlyList<MemoryEntry> entries, MemorySituation situation)
    {
        var selection = Select(entries, situation);

        if (selection.Shown.Count == 0)
        {
            return null;
        }

        var block = new StringBuilder();

        // The sample size, stated first so it is read before the facts rather than as a footnote,
        // and phrased so a model has nothing to guess about: how many it has, how many exist, and
        // what to do when asked for the rest.
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

    /// <summary>
    /// Everything, as the Commander hears it when they ask. <b>The store rather than the sample</b> —
    /// which is the promise item 2 makes, and the reason this exists beside
    /// <see cref="Render"/> rather than being the same function with a bigger budget.
    /// </summary>
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

    /// <summary>
    /// Whether an entry is about what is happening. Any one tag is enough: a fact tagged with this
    /// system is about here whether or not the ship also matches.
    /// </summary>
    private static bool Matches(MemoryEntry entry, IReadOnlyList<string> here) =>
        entry.About.Any(tag => here.Contains(tag, StringComparer.Ordinal));
}

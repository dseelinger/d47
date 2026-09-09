namespace D47.Core.Memory;

/// <summary>Everything d47 remembers about the Commander, read and written as one thing (Phase 31).</summary>
/// <param name="store">The file.</param>
/// <param name="commander">
/// The Frontier id of whoever is aboard, or null before the journal has said.
/// </param>
/// <param name="situation">What is going on, for tagging a new entry.</param>
public sealed class MemoryBook(
    MemoryStore store,
    Func<string?> commander,
    Func<MemorySituation> situation)
{
    /// <summary>How long one fact may be.</summary>
    public const int MaxFact = 400;

    public MemoryStore Store => store;

    /// <summary>Who the book is pointed at right now, or null before the journal has said.</summary>
    public string? CommanderId => commander();

    /// <summary>Everything remembered about whoever is aboard right now.</summary>
    public IReadOnlyList<MemoryEntry> Mine => store.For(commander());

    /// <summary>Writes one fact down.</summary>
    public MemoryEntry Remember(string fact, MemoryArrival arrival, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fact);

        var trimmed = fact.Trim();

        if (trimmed.Length > MaxFact)
        {
            trimmed = trimmed[..MaxFact].TrimEnd() + "…";
        }

        var taken = Mine.Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);

        return store.Write(
            commander(),
            new MemoryEntry(MemoryKeys.Next(MemoryKeys.PrefixFor(arrival), taken), trimmed)
            {
                Tier = arrival switch
                {
                    MemoryArrival.Panel => MemoryTier.Stated,
                    MemoryArrival.Journal => MemoryTier.Observed,

                    // The model's, and an inference.
                    _ => MemoryTier.Inferred,
                },
                Arrival = arrival,
                About = situation().Tags(),
                AddedAt = now,
            });
    }

    /// <summary>Writes down something d47 read out of the journal, under a key it names itself.</summary>
    /// <param name="at">When the journal said so, which is not when this was called.</param>
    public MemoryEntry Observe(string key, string fact, DateTimeOffset at) =>
        store.Write(
            commander(),
            new MemoryEntry(key, fact.Trim())
            {
                Tier = MemoryTier.Observed,
                Arrival = MemoryArrival.Journal,
                About = situation().Tags(),
                AddedAt = at,
            });

    /// <summary>Forgets one entry of the Commander's own.</summary>
    public bool Forget(string key) => store.Forget(commander(), key);

    /// <summary>
    /// Removes everything past its expiry and returns what went, so the caller can say the part worth
    /// saying out loud.
    /// </summary>
    public IReadOnlyList<MemoryEntry> Expire(DateTimeOffset now, TimeSpan window) => store.Expire(now, window);

    /// <summary>The bounded set that reaches a prompt, with its own sample size in it.</summary>
    public string? Recall() => MemoryRecall.Render(Mine, situation());

    /// <summary>What is in the store, for the row above the button.</summary>
    public string Summarise()
    {
        var mine = Mine;
        var everything = store.Everything();

        if (everything.Count == 0)
        {
            return store.Problems is { Count: > 0 } none
                ? $"Nothing remembered. {none.Count} entr{(none.Count == 1 ? "y" : "ies")} could not be read back."
                : "Nothing remembered yet. D47 writes something down when you tell it to, when it "
                  + "notices something in your journal, or when it decides in conversation that a "
                  + "fact is worth keeping.";
        }

        var line = $"{everything.Count} thing{(everything.Count == 1 ? "" : "s")} remembered";

        // Said only when the two differ.
        line += mine.Count == everything.Count
            ? "."
            : $", {mine.Count} of them about the Commander currently aboard.";

        var stated = mine.Count(entry => entry.Tier == MemoryTier.Stated);
        var observed = mine.Count(entry => entry.Tier == MemoryTier.Observed);
        var inferred = mine.Count(entry => entry.Tier == MemoryTier.Inferred);

        if (mine.Count > 0)
        {
            line += $" {stated} you told D47, {observed} it noticed, {inferred} it worked out for itself.";
        }

        return store.Problems is { Count: > 0 } problems
            ? line + $" {problems.Count} could not be read back."
            : line;
    }
}

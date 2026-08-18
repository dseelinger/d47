namespace D47.Core.Memory;

/// <summary>
/// Everything d47 remembers about the Commander, read and written as one thing (list.md Phase 31).
/// <para>
/// The store is a file and this is the seam every caller uses, which is what keeps the three write
/// routes from disagreeing: <b>the tier is decided here, from the route, and is never a parameter</b>
/// (see <see cref="MemoryTier"/>). The panel supplies <see cref="MemoryArrival.Panel"/> and gets the
/// Commander's own word; the tool supplies <see cref="MemoryArrival.Model"/> and gets an inference,
/// whatever the turn looked like from inside the handler.
/// </para>
/// <para>
/// Same arrangement as <see cref="Lore.LoreBook"/> over <see cref="Lore.LoreStore"/>, and for the
/// same reason: the store knows about a file and this knows about who is flying and what they are
/// doing.
/// </para>
/// </summary>
/// <param name="store">The file.</param>
/// <param name="commander">
/// The Frontier id of whoever is aboard, or null before the journal has said. Read per call rather
/// than captured, because a Commander can log into a second character without restarting d47.
/// </param>
/// <param name="situation">
/// What is going on, for tagging a new entry. Read per call for the same reason.
/// </param>
public sealed class MemoryBook(
    MemoryStore store,
    Func<string?> commander,
    Func<MemorySituation> situation)
{
    /// <summary>
    /// How long one fact may be. A sentence or two — long enough to be a fact, short enough that it
    /// is said back rather than recited, and short enough that a store of them fits above the cache
    /// breakpoint. The same figure and the same reasoning as a lore note.
    /// </summary>
    public const int MaxFact = 400;

    public MemoryStore Store => store;

    /// <summary>
    /// Who the book is pointed at right now, or null before the journal has said. Exposed because
    /// <see cref="MemoryObserver"/> has to file an observation under the same id a remembered fact
    /// would go under, and two callers reading two different functions is how they come to disagree.
    /// </summary>
    public string? CommanderId => commander();

    /// <summary>Everything remembered about whoever is aboard right now.</summary>
    public IReadOnlyList<MemoryEntry> Mine => store.For(commander());

    /// <summary>
    /// Writes one fact down. Returns the entry as it was stored, which is not necessarily as it
    /// arrived — a fact longer than <see cref="MaxFact"/> is truncated rather than refused, for the
    /// reason a lore note is: the text may have come from a model that read untrusted input, and the
    /// file is the Commander's to read.
    /// </summary>
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

                    // The model's, and an inference. Not caution about the model — the honest label:
                    // from in here, a turn a hostile in-game message steered is indistinguishable
                    // from one the Commander asked for.
                    _ => MemoryTier.Inferred,
                },
                Arrival = arrival,
                About = situation().Tags(),
                AddedAt = now,
            });
    }

    /// <summary>
    /// Writes down something d47 read out of the journal, under a key it names itself.
    /// <para>
    /// Separate from <see cref="Remember"/> because the key is the whole difference: a remembered
    /// fact accumulates and an observation <b>replaces</b>, which is what keeps two entries from
    /// becoming two hundred. The tier is fixed here for the same reason it is fixed there — the
    /// route decides, and this is the only route that produces <see cref="MemoryTier.Observed"/>.
    /// </para>
    /// </summary>
    /// <param name="at">
    /// When the journal said so, which is not when this was called. It is what "it has been three
    /// days" is measured from, so a stamp taken from the wall clock would make a session resumed
    /// after a week read as one resumed after a second.
    /// </param>
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

    /// <summary>Forgets one entry of the Commander's own. Returns whether there was one.</summary>
    public bool Forget(string key) => store.Forget(commander(), key);

    /// <summary>
    /// Removes everything past its expiry and returns what went, so the caller can say the part
    /// worth saying out loud.
    /// </summary>
    public IReadOnlyList<MemoryEntry> Expire(DateTimeOffset now, TimeSpan window) => store.Expire(now, window);

    /// <summary>
    /// The bounded set that reaches a prompt, with its own sample size in it. See
    /// <see cref="MemoryRecall"/>, which is where the selection and the reason for it live.
    /// </summary>
    public string? Recall() => MemoryRecall.Render(Mine, situation());

    /// <summary>
    /// What is in the store, for the row above the button. Counts and tiers, never the facts — the
    /// facts are what the window is for, and a settings row that read them aloud would put the
    /// Commander's own notes on a page they might be screen-sharing.
    /// </summary>
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

        // Said only when the two differ. A store with one Commander in it saying "of which 12 are
        // about you" is arithmetic nobody asked for.
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

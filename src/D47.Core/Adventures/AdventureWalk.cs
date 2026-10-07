using D47.Core.Journal;

namespace D47.Core.Adventures;

/// <summary>Where every adventure stands, and the per-Commander state the fold reads to get there.</summary>
internal sealed class AdventureFoldState
{
    public Dictionary<string, AdventureStanding> Standings { get; } = new(StringComparer.Ordinal);

    /// <summary>The last walked event's time per Commander; a live event at or before it is not folded again.</summary>
    public Dictionary<string, DateTimeOffset> HighWater { get; } = new(StringComparer.Ordinal);

    /// <summary>The time of the newest event folded, by a walk or live; null until one is.</summary>
    public DateTimeOffset? Newest { get; set; }

    /// <summary>The system each Commander is in, which a standing begun from nothing starts in.</summary>
    public Dictionary<string, long> Here { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, IReadOnlyDictionary<string, string>> Seen { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, AdventureWorld> World { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// A walk over the journal files since the earliest acceptance, started on the tick by
/// <see cref="AdventureBook.StartWalk"/>, run on the pool, and handed back to <see cref="AdventureBook.Adopt"/> on the tick.
/// </summary>
public sealed class AdventureWalk
{
    private readonly AdventureBook _book;
    private readonly int _generation;
    private readonly string _directory;
    private readonly JournalMark? _until;

    internal AdventureWalk(AdventureBook book, int generation, string directory, JournalMark? until)
    {
        _book = book;
        _generation = generation;
        _directory = directory;
        _until = until;
    }

    /// <summary>Reads and folds the files up to the mark. Never throws: a failure is a failed result.</summary>
    public AdventureWalkResult Run()
    {
        try
        {
            var files = AdventureBook.FilesToWalk(_directory, _book.EarliestAcceptance(), _until);

            return new AdventureWalkResult(_generation, _book.Walk(files, _until), null);
        }
        catch (Exception ex)
        {
            return new AdventureWalkResult(_generation, null, ex);
        }
    }
}

/// <summary>What a walk folded, or why it failed.</summary>
public sealed class AdventureWalkResult
{
    internal AdventureWalkResult(int generation, AdventureFoldState? folded, Exception? error)
    {
        Generation = generation;
        Folded = folded;
        Error = error;
    }

    internal int Generation { get; }

    internal AdventureFoldState? Folded { get; }

    public Exception? Error { get; }

    public bool Failed => Folded is null;
}

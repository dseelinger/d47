using System.Globalization;

namespace D47.Core.Logbook;

/// <summary>What kind of thing a fact is about.</summary>
public enum LogFactKind
{
    /// <summary>Where the window opened, and in what.</summary>
    Opening,

    Travel,

    Exploration,

    Trade,

    Combat,

    Mission,

    Engineering,

    /// <summary>Ships bought, sold, swapped, transferred.</summary>
    Fleet,

    /// <summary>Rank, reputation, anything that went up.</summary>
    Progress,

    /// <summary>Deaths, rebuys, hull, interdictions that were not the Commander's idea.</summary>
    Mishap,

    OnFoot,

    /// <summary>Where the window closed.</summary>
    Closing,
}

/// <summary>One journal event a fact rests on.</summary>
public sealed record LogSource(string Event, DateTimeOffset At)
{
    public override string ToString() =>
        $"{Event} at {At.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}Z";
}

/// <summary>
/// One thing d47 computed from the journals, numbered so a sentence can point at it (Phase 33, "Every
/// sentence traces to an event").
/// </summary>
public sealed record LogFact
{
    /// <summary>What a citation names.</summary>
    public int Id { get; init; }

    public required LogFactKind Kind { get; init; }

    /// <summary>What d47 says happened, in the fewest words that keep it true.</summary>
    public required string Statement { get; init; }

    /// <summary>When, for ordering.</summary>
    public DateTimeOffset At { get; init; }

    /// <summary>The events behind it, capped.</summary>
    public IReadOnlyList<LogSource> Sources { get; init; } = [];

    /// <summary>How many events there actually were, which may exceed <see cref="Sources"/>.</summary>
    public int SourceCount { get; init; }

    /// <summary>A figure over the whole window rather than a thing that happened at a moment.</summary>
    public bool Aggregate { get; init; }

    public const int SourcesKept = 6;

    /// <summary>How the Sources section of the written file reads this fact.</summary>
    public string Provenance()
    {
        if (Sources.Count == 0)
        {
            return "no supporting event recorded";
        }

        var listed = string.Join("; ", Sources.Select(source => source.ToString()));

        return SourceCount > Sources.Count
            ? $"{listed}; and {SourceCount - Sources.Count} more"
            : listed;
    }
}

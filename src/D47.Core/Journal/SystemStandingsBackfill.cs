using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>The system factions and conflicts of the last week, recovered from earlier journals.</summary>
public static class SystemStandingsBackfill
{
    private static readonly JournalLineFilter Reads = JournalLineFilter.Events(
        "Commander", "NewCommander", "LoadGame", "Fileheader", "GameModeChange", "Shutdown",
        "FSDJump", "Location", "CarrierJump");

    /// <summary>
    /// Each Commander's standings, keyed by Frontier id, over journals oldest-first. Files written more than a
    /// week before the newest are not opened, and a <c>Location</c> inside an Operation is not folded.
    /// </summary>
    public static IReadOnlyDictionary<string, SystemStandings> FromHistory(
        IFileSystem fileSystem,
        IReadOnlyList<string> files,
        ILogger logger,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        var written = files.ToDictionary(file => file, file => fileSystem.Stat(file)?.Written);

        if (written.Values.Where(time => time is not null).Max() is not { } end)
        {
            return new Dictionary<string, SystemStandings>(StringComparer.Ordinal);
        }

        var from = end - BgsTickWatch.MostBehind;

        return LedgerBackfill.FromHistory(
                fileSystem,
                [.. files.Where(file => written[file] >= from)],
                logger,
                cancellation,
                "system standings",
                SystemStandings.Empty,
                (standings, parsed) => standings.Apply(parsed),
                Reads,
                markOperations: true)
            .Where(pair => pair.Value.IsKnown)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }
}

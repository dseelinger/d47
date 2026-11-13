using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>Every Commander's last engineer standings, recovered from every journal on disk.</summary>
public static class EngineerProgressBackfill
{
    private static readonly JournalLineFilter Reads = JournalLineFilter.Events("EngineerProgress");

    /// <summary>Each Commander's standings, keyed by Frontier id, over journals oldest-first.</summary>
    public static IReadOnlyDictionary<string, EngineerProgressState> FromHistory(
        IFileSystem fileSystem,
        IReadOnlyList<string> files,
        ILogger logger,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        var standings = new Dictionary<string, EngineerProgressState>(StringComparer.Ordinal);

        CommanderEventWalk.Each(
            fileSystem,
            files,
            logger,
            "engineer progress",
            Reads,
            cancellation,
            (parsed, commander) =>
            {
                if (parsed.Kind == "NewCommander")
                {
                    if (parsed.String("FID") is { Length: > 0 } created)
                    {
                        _ = standings.Remove(created);
                    }

                    return;
                }

                if (parsed.Kind != "EngineerProgress" || commander.Length == 0)
                {
                    return;
                }

                var now = standings.GetValueOrDefault(commander, EngineerProgressState.Empty).Apply(parsed);

                if (now.IsKnown)
                {
                    standings[commander] = now;
                }
            });

        logger.LogInformation("Engineer standings recovered from history for {Count} Commanders", standings.Count);

        return standings;
    }
}

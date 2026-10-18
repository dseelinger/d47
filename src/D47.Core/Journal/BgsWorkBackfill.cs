using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>The BGS work of the last week, recovered from earlier journals.</summary>
public static class BgsWorkBackfill
{
    private static readonly string[] Kinds =
    [
        "Commander", "LoadGame", "FSDJump", "Location", "CarrierJump", "Docked", "Undocked", "MissionCompleted",
        "RedeemVoucher", "MarketSell", "SellOrganicData", "SellExplorationData", "MultiSellExplorationData",
    ];

    /// <summary>
    /// Each Commander's work, keyed by Frontier id, over journals oldest-first. Files written more than a
    /// week before the newest are not opened.
    /// </summary>
    public static IReadOnlyDictionary<string, BgsWork> FromHistory(
        IFileSystem fileSystem,
        IReadOnlyList<string> files,
        ILogger logger,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        var written = files.ToDictionary(file => file, file => fileSystem.Stat(file)?.Written);
        var newest = written.Values.Where(time => time is not null).Max();

        var work = new Dictionary<string, BgsWork>(StringComparer.Ordinal);
        var commander = string.Empty;

        if (newest is not { } end)
        {
            return work;
        }

        var from = end - BgsTickWatch.MostBehind;

        foreach (var file in files.Where(file => written[file] >= from))
        {
            cancellation.ThrowIfCancellationRequested();

            foreach (var line in Lines(fileSystem, file, logger))
            {
                if (!Kinds.Any(kind => line.Contains($"\"event\":\"{kind}\"", StringComparison.Ordinal))
                    || !JournalEvent.TryParse(line, logger, out var parsed) || parsed is null)
                {
                    continue;
                }

                if (parsed.Kind is "Commander" or "LoadGame")
                {
                    if (parsed.String("FID") is { Length: > 0 } fid)
                    {
                        commander = fid;
                    }

                    continue;
                }

                if (commander.Length > 0)
                {
                    work[commander] = (work.TryGetValue(commander, out var held) ? held : BgsWork.None).Apply(parsed);
                }
            }
        }

        return work
            .Where(pair => pair.Value.IsKnown)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    private static IEnumerable<string> Lines(IFileSystem fileSystem, string file, ILogger logger)
    {
        Stream stream;

        try
        {
            stream = fileSystem.OpenRead(file) ?? throw new FileNotFoundException("The journal file is missing.", file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not read {File} for BGS work", file);
            yield break;
        }

        using (stream)
        {
            using var reader = new StreamReader(stream);

            while (reader.ReadLine() is { } line)
            {
                yield return line;
            }
        }
    }
}

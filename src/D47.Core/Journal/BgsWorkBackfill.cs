using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>The BGS work of the last week, recovered from earlier journals.</summary>
public static class BgsWorkBackfill
{
    private static readonly string[] Kinds = CommanderEventWalk.Events(
        "FSDJump", "Location", "CarrierJump", "Docked", "Undocked", "MissionCompleted",
        "RedeemVoucher", "MarketSell", "SellOrganicData", "SellExplorationData", "MultiSellExplorationData");

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

        if (newest is not { } end)
        {
            return work;
        }

        var from = end - BgsTickWatch.MostBehind;

        CommanderEventWalk.Each(
            fileSystem,
            [.. files.Where(file => written[file] >= from)],
            logger,
            "BGS work",
            Kinds,
            cancellation,
            (parsed, commander) =>
            {
                if (parsed.Kind == "NewCommander")
                {
                    if (parsed.String("FID") is { Length: > 0 } created)
                    {
                        _ = work.Remove(created);
                    }

                    return;
                }

                if (parsed.Kind is "Commander" or "LoadGame" || commander.Length == 0)
                {
                    return;
                }

                work[commander] = (work.TryGetValue(commander, out var held) ? held : BgsWork.None).Apply(parsed);
            });

        return work
            .Where(pair => pair.Value.IsKnown)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }
}

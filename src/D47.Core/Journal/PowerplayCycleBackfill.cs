using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>The merits earned for the pledged Power over the last week, recovered from earlier journals.</summary>
public static class PowerplayCycleBackfill
{
    /// <summary>Each Commander's merits for their Power, keyed by Frontier id, over a list of journals oldest-first.</summary>
    public static IReadOnlyDictionary<string, PowerplayCycleMerits> FromHistory(
        IFileSystem fileSystem,
        IReadOnlyList<string> files,
        ILogger logger,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        var pledges = new Dictionary<string, PowerplayPledge>(StringComparer.Ordinal);
        var merits = new Dictionary<string, PowerplayCycleMerits>(StringComparer.Ordinal);

        // Powerplay, PowerplayJoin, PowerplayDefect, PowerplayLeave, PowerplayMerits and the rest.
        CommanderEventWalk.Each(
            fileSystem,
            files,
            logger,
            "Powerplay events",
            ["\"event\":\"Powerplay"],
            cancellation,
            (parsed, commander) =>
            {
                if (parsed.Kind == "NewCommander")
                {
                    if (parsed.String("FID") is { Length: > 0 } created)
                    {
                        _ = pledges.Remove(created);
                        _ = merits.Remove(created);
                    }

                    return;
                }

                if (parsed.Kind is "Commander" or "LoadGame" || commander.Length == 0)
                {
                    return;
                }

                var pledge = (pledges.TryGetValue(commander, out var held) ? held : PowerplayPledge.None).Apply(parsed);
                pledges[commander] = pledge;

                merits[commander] = (merits.TryGetValue(commander, out var sum) ? sum : PowerplayCycleMerits.None)
                    .Apply(parsed, pledge);
            });

        return merits
            .Where(pair => pair.Value.IsKnown)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }
}

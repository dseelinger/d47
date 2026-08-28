using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>
/// Every ship's modules, recovered from journals d47 was not running for.
/// <para>
/// <b>Why this exists.</b> <see cref="ShipLoadouts"/> remembers what it has watched, and the live
/// tail reads one file — the newest. So a Commander who starts d47 already sitting in one ship
/// would know that ship and nothing about the eleven others, however many times the game has
/// described them. This is the same recovery <see cref="FleetBackfill"/> performs for ownership,
/// for the same reason and over the same window: the fleet says which ships exist and where, and
/// this says what is in them.
/// </para>
/// <para>
/// <b>Not a whole replay.</b> Driving old journals through <see cref="GameStateStore"/> would
/// restore month-old positions, missions and materials as though they were current. This reads the
/// same files for one subject, which is the pattern <see cref="FleetBackfill"/>,
/// <see cref="Goals.GoalMiner"/> follows.
/// </para>
/// <para>
/// <b>It folds rather than snapshots</b>, and must: a <c>Loadout</c> is the whole ship, but the
/// <c>EngineerCraft</c> events after it are not, and Elite writes no <c>Loadout</c> when a module
/// is engineered. Replaying both through <see cref="ShipLoadout.Apply"/> is what makes a
/// recovered ship carry the rolls that were done to it — and reuses the production fold rather
/// than restating it here.
/// </para>
/// </summary>
public static class LoadoutBackfill
{
    /// <summary>
    /// How many journal files back to look. The same window <see cref="FleetBackfill"/> uses, and
    /// for the same reason: far enough to cross the gap since d47 last ran, near enough that
    /// startup does not read a year of history. A ship not sat in inside that window stays
    /// unknown, which is the honest answer and is what the surface already renders.
    /// </summary>
    private const int MaxLookback = 25;

    public static IReadOnlyDictionary<string, ShipLoadouts> FromHistory(string directory, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (!Directory.Exists(directory))
        {
            logger.LogWarning("No journal folder at {Directory}", directory);
            return new Dictionary<string, ShipLoadouts>(StringComparer.Ordinal);
        }

        return FromHistory(
            [.. Directory.EnumerateFiles(directory, JournalFolder.FilePattern)
                .OrderBy(Path.GetFileName, StringComparer.Ordinal)],
            logger);
    }

    /// <summary>
    /// The same, over an explicit list oldest-first. What a test drives.
    /// <para>
    /// Keyed by Frontier id, because <see cref="GameStateStore"/> is: two Commanders share one
    /// journal folder and neither may be handed the other's ships — nor, here, the other's
    /// modules. The id is tracked off <c>Commander</c> and <c>LoadGame</c> and carried across
    /// files, because a continuation journal re-emits <c>Fileheader</c> and not <c>Commander</c>.
    /// </para>
    /// </summary>
    public static IReadOnlyDictionary<string, ShipLoadouts> FromHistory(
        IReadOnlyList<string> files,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        var remembered = new Dictionary<string, ShipLoadouts>(StringComparer.Ordinal);
        var flying = new Dictionary<string, ShipLoadout>(StringComparer.Ordinal);
        var commander = string.Empty;

        var floor = Math.Max(0, files.Count - MaxLookback);

        for (var i = floor; i < files.Count; i++)
        {
            var reader = new JournalReader(files[i], logger);

            while (reader.Poll() is { Count: > 0 } batch)
            {
                foreach (var journalEvent in batch)
                {
                    if (journalEvent.Kind is "Commander" or "LoadGame"
                        && journalEvent.String("FID") is { Length: > 0 } fid)
                    {
                        commander = fid;
                    }

                    if (commander.Length == 0)
                    {
                        continue;
                    }

                    var ship = (flying.TryGetValue(commander, out var held) ? held : ShipLoadout.Unknown)
                        .Apply(journalEvent);

                    flying[commander] = ship;

                    var known = remembered.TryGetValue(commander, out var existing) ? existing : ShipLoadouts.Empty;

                    remembered[commander] = known
                        .Apply(journalEvent)
                        .Remember(ship, journalEvent.Timestamp);
                }
            }
        }

        foreach (var (fid, ships) in remembered)
        {
            if (ships.Ships.Count == 0)
            {
                continue;
            }

            logger.LogInformation(
                "Modules recovered from history for {Commander}: {Ships} ship(s), oldest seen {Oldest:u}",
                fid,
                ships.Ships.Count,
                ships.Ships.Values.Min(ship => ship.SeenAt));
        }

        return remembered
            .Where(entry => entry.Value.IsKnown)
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
    }
}

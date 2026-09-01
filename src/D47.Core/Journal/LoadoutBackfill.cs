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
    /// startup does not read a year of history.
    /// <para>
    /// <b>Its job is unchanged and its meaning is not</b>
    /// (<a href="https://github.com/dseelinger/d47/issues/128">#128</a>). This window used to
    /// <em>be</em> the memory, so a ship not sat in inside it was forgotten on every launch;
    /// <see cref="LoadoutStore"/> is the long memory now and this is the catch-up over the gap
    /// since d47 last ran. The number did not need to change with the meaning: what it has to
    /// cover is a gap between sessions rather than a Commander's whole history.
    /// </para>
    /// </summary>
    private const int MaxLookback = 25;

    /// <param name="stored">
    /// What <see cref="LoadoutStore"/> held, to start from rather than to rebuild over
    /// (<a href="https://github.com/dseelinger/d47/issues/128">#128</a>). Empty before the file
    /// existed, and empty on a first run.
    /// </param>
    public static IReadOnlyDictionary<string, ShipLoadouts> FromHistory(
        string directory,
        ILogger logger,
        IReadOnlyDictionary<string, ShipLoadouts>? stored = null)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (!Directory.Exists(directory))
        {
            logger.LogWarning("No journal folder at {Directory}", directory);

            // The file still answers. A folder that has moved is not a reason to forget every
            // ship in it, which is what returning nothing here used to mean.
            return stored ?? new Dictionary<string, ShipLoadouts>(StringComparer.Ordinal);
        }

        return FromHistory(
            [.. Directory.EnumerateFiles(directory, JournalFolder.FilePattern)
                .OrderBy(Path.GetFileName, StringComparer.Ordinal)],
            logger,
            stored);
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
        ILogger logger,
        IReadOnlyDictionary<string, ShipLoadouts>? stored = null)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        // **Seeded rather than rebuilt** (#128), and that is what makes forgetting work across a
        // restart. The window replays ShipyardSell, the part-exchange ShipyardBuy and ShipyardNew
        // through the same fold the live path uses — so a ship sold while d47 was closed is
        // removed from the long memory rather than surviving in it under an id the game may have
        // handed to something else.
        var remembered = stored is null
            ? new Dictionary<string, ShipLoadouts>(StringComparer.Ordinal)
            : new Dictionary<string, ShipLoadouts>(stored, StringComparer.Ordinal);

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

                    // Remembered then forgotten, in the order CommanderGameState folds them and
                    // for the measured reason recorded there.
                    remembered[commander] = known
                        .Remember(ship, journalEvent.Timestamp)
                        .Apply(journalEvent);
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

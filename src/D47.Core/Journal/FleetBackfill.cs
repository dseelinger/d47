using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>
/// The fleet, recovered from journals d47 was not running for.
/// <para>
/// <b>Why this exists.</b> <see cref="FleetRegistry"/> is built from <c>StoredShips</c>, and Elite
/// writes that event only on docking at a station with a shipyard. The live tail reads one file —
/// the newest — so a Commander who starts d47 and flies without docking at a shipyard sees the one
/// ship <c>Loadout</c> named and none of the others, however many they own and however recently
/// the game recorded them. Measured against a real 917-journal folder: the newest journal held a
/// <c>Loadout</c> and no <c>StoredShips</c> at all, while the file before it held six, the last of
/// them listing eleven ships. The fleet was on disk and nothing read it.
/// </para>
/// <para>
/// <b>Why it is not simply "read more files".</b> Replaying whole journals through
/// <see cref="GameStateStore"/> would restore two-week-old positions, missions and materials as
/// though they were current. This reads the same files for one subject only, which is the pattern
/// <see cref="Goals.GoalMiner"/> already follows.
/// </para>
/// <para>
/// <b>Why a snapshot alone is not enough.</b> Wholesale replacement is safe when the snapshot was
/// taken minutes ago in the session being watched; it is not safe across a gap that may run to
/// weeks — the same folder shows an eleven-file, thirteen-day stretch with no <c>StoredShips</c> in
/// it. So the snapshot is folded forward through what happened after it, and
/// <see cref="FleetRegistry.Apply(JournalEvent, string?, string?)"/> carries the ownership deltas
/// that make that sound. Of 917 journals, that path covers 72 sales, 34 purchases and 737 swaps.
/// The one hole it cannot close is dying and declining the rebuy, which leaves no event naming the
/// ship — one occurrence in those 917, against a <c>Resurrect</c> that is otherwise
/// <c>rejoin</c> or <c>rebuy</c>.
/// </para>
/// </summary>
public static class FleetBackfill
{
    /// <summary>
    /// The fleet as of the newest journal that recorded one, folded forward to the end of history.
    /// <see cref="FleetRegistry.Empty"/> where no journal holds a <c>StoredShips</c> at all, which
    /// is the honest answer for a Commander who has never docked at a shipyard.
    /// </summary>
    public static IReadOnlyDictionary<string, FleetRegistry> FromHistory(string directory, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (!Directory.Exists(directory))
        {
            logger.LogWarning("No journal folder at {Directory}", directory);
            return new Dictionary<string, FleetRegistry>(StringComparer.Ordinal);
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
    /// journal folder and neither may be handed the other's ships. The fold tracks the id the
    /// same way every miner here does, off <c>Commander</c> and <c>LoadGame</c>.
    /// </para>
    /// <para>
    /// <b>Known limit.</b> The search looks back <see cref="MaxLookback"/> files. A Commander
    /// whose last shipyard visit is older than that is not recovered and stays unknown until they
    /// dock — never handed another Commander's ships, which is the property that matters. The
    /// truncation is logged rather than silent.
    /// </para>
    /// </summary>
    public static IReadOnlyDictionary<string, FleetRegistry> FromHistory(
        IReadOnlyList<string> files,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        var fleets = new Dictionary<string, FleetRegistry>(StringComparer.Ordinal);

        // Newest-first, and per Commander: seeding from the newest snapshot in the folder alone
        // would recover exactly one Commander, since a journal file belongs to one Commander and
        // every older snapshot would fall outside the fold. So the scan keeps going until each
        // Commander it has met has a snapshot, and the fold starts at the oldest of those.
        var newestSnapshot = new Dictionary<string, int>(StringComparer.Ordinal);
        var met = new HashSet<string>(StringComparer.Ordinal);

        // The whole window, without an early exit. Stopping as soon as one Commander has a
        // snapshot was the first shape of this and it was wrong: the newest file almost always
        // satisfies that, so a second Commander was never even met. The scan is text-only, so
        // reading the window out is cheap — it is the fold afterwards that costs, and that starts
        // no further back than the oldest snapshot actually found.
        var floor = Math.Max(0, files.Count - MaxLookback);

        for (var i = files.Count - 1; i >= floor; i--)
        {
            var (owner, holds) = Scan(files[i], logger);

            if (owner is null)
            {
                continue;
            }

            met.Add(owner);

            if (holds)
            {
                // Newest-first, so the first one seen for a Commander is their newest.
                _ = newestSnapshot.TryAdd(owner, i);
            }
        }

        if (floor > 0 && met.Count > newestSnapshot.Count)
        {
            // Said out loud rather than silently truncating: a Commander who last docked beyond
            // this window is not recovered, and a limit nobody knows about is worse than a log
            // line nobody reads.
            logger.LogInformation(
                "Looked back {Window} journal files for fleet snapshots; {Missing} Commander(s) had none in that window",
                MaxLookback,
                met.Count - newestSnapshot.Count);
        }

        if (newestSnapshot.Count == 0)
        {
            logger.LogInformation("No StoredShips in {Count} journal files; the fleet stays unknown", files.Count);
            return fleets;
        }

        var seed = newestSnapshot.Values.Min();

        // Forward from the seeding file to the end of history, so every sale, purchase and swap
        // that happened after the snapshot is applied in the order it happened.
        var location = JournalLocation.Unknown;

        // Carried across files rather than reset per file, and the reason is the journals rather
        // than a preference: a
        // continuation journal re-emits Fileheader and does not re-emit Commander.
        var commander = string.Empty;

        for (var i = seed; i < files.Count; i++)
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

                    // Tracked for the same reason CommanderGameState tracks it: storing a ship
                    // happens wherever the Commander is standing, and the event does not say.
                    location = location.Apply(journalEvent);

                    if (commander.Length == 0)
                    {
                        continue;
                    }

                    var held = fleets.TryGetValue(commander, out var existing) ? existing : FleetRegistry.Empty;
                    var next = held.Apply(journalEvent, location.StarSystem, location.StationName);

                    // Only a fleet that actually knows something is filed. Creating an entry for
                    // every Commander whose name goes past would hand the caller an empty
                    // registry that is indistinguishable from "owns no ships" — and unknown and
                    // empty are different answers.
                    if (next.IsKnown)
                    {
                        fleets[commander] = next;
                    }
                }
            }
        }

        foreach (var (fid, fleet) in fleets)
        {
            logger.LogInformation(
                "Fleet recovered from history for {Commander}: {Ships} ships, snapshot taken {TakenAt:u} at {Station}",
                fid,
                fleet.Ships.Count,
                fleet.TakenAt,
                fleet.SnapshotStation ?? fleet.SnapshotSystem ?? "somewhere unrecorded");
        }

        return fleets;
    }

    /// <summary>
    /// How far back the search for a snapshot goes, in files.
    /// <para>
    /// A bound rather than the whole folder, because the fold that follows starts at the oldest
    /// file the search settled on and re-reads everything after it. A real folder holds hundreds
    /// of journals; the measured case had its snapshot one file back, and a Commander who has not
    /// docked at a shipyard in twenty-five sessions is better served by their next dock than by
    /// d47 parsing a year of history on the first tick.
    /// </para>
    /// </summary>
    private const int MaxLookback = 25;

    /// <summary>
    /// Whose file this is, and whether it holds a <c>StoredShips</c> at all — both in one pass,
    /// as a text scan rather than a parse. This runs over files from the newest backwards, and
    /// the only question being asked is where the real fold should start.
    /// </summary>
    private static (string? Owner, bool Holds) Scan(string file, ILogger logger)
    {
        string? owner = null;
        var holds = false;

        try
        {
            // **FileShare.ReadWrite | Delete, exactly as JournalReader opens the same files**
            // (architecture.md §4). Elite holds the live journal open for writing for the whole
            // session, and File.ReadLines asks for a share mode that excludes that — so scanning
            // the newest file threw IOException every single run, which was caught and logged and
            // left the one file most likely to hold a fresh snapshot unread. Found in the debug
            // log rather than by any test: every test writes its own journals and no test has
            // Elite holding one open.
            using var stream = new FileStream(
                file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            using var reader = new StreamReader(stream);

            while (reader.ReadLine() is { } line)
            {
                if (owner is null && line.Contains("\"FID\":\"", StringComparison.Ordinal))
                {
                    var start = line.IndexOf("\"FID\":\"", StringComparison.Ordinal) + 7;
                    var end = line.IndexOf('"', start);

                    if (end > start)
                    {
                        owner = line[start..end];
                    }
                }

                holds = holds || line.Contains("\"event\":\"StoredShips\"", StringComparison.Ordinal);

                if (owner is not null && holds)
                {
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A journal that cannot be read is one file's worth of history missing, not a reason
            // to start with no fleet at all.
            logger.LogWarning(ex, "Could not scan {File} for a fleet snapshot", file);
        }

        return (owner, holds);
    }
}

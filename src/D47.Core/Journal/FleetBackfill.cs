using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>The fleet, recovered from journals d47 was not running for.</summary>
public static class FleetBackfill
{
    /// <summary>
    /// The fleet as of the newest journal that recorded one, folded forward to the end of history.
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

    /// <summary>The same, over an explicit list oldest-first.</summary>
    public static IReadOnlyDictionary<string, FleetRegistry> FromHistory(
        IReadOnlyList<string> files,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        var fleets = new Dictionary<string, FleetRegistry>(StringComparer.Ordinal);

        // Newest-first, and per Commander: seeding from the newest snapshot in the folder alone would recover
        // exactly one Commander, since a journal file belongs to one Commander and every older snapshot would
        // fall outside the fold.
        var newestSnapshot = new Dictionary<string, int>(StringComparer.Ordinal);
        var met = new HashSet<string>(StringComparer.Ordinal);

        // The whole window, without an early exit.
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
            // Said out loud rather than silently truncating: a Commander who last docked beyond this window
            // is not recovered, and a limit nobody knows about is worse than a log line nobody reads.
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

        // Forward from the seeding file to the end of history, so every sale, purchase and swap that happened
        // after the snapshot is applied in the order it happened.
        var location = JournalLocation.Unknown;

        // Carried across files rather than reset per file, and the reason is the journals rather than a
        // preference: a continuation journal re-emits Fileheader and does not re-emit Commander.
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

                    // Tracked for the same reason CommanderGameState tracks it: storing a ship happens
                    // wherever the Commander is standing, and the event does not say.
                    location = location.Apply(journalEvent);

                    if (commander.Length == 0)
                    {
                        continue;
                    }

                    var held = fleets.TryGetValue(commander, out var existing) ? existing : FleetRegistry.Empty;
                    var next = held.Apply(journalEvent, location.StarSystem, location.StationName);

                    // Only a fleet that actually knows something is filed.
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

    /// <summary>How far back the search for a snapshot goes, in files.</summary>
    private const int MaxLookback = 25;

    /// <summary>
    /// Whose file this is, and whether it holds a <c>StoredShips</c> at all — both in one pass, as a
    /// text scan rather than a parse.
    /// </summary>
    private static (string? Owner, bool Holds) Scan(string file, ILogger logger)
    {
        string? owner = null;
        var holds = false;

        try
        {
            // **FileShare.ReadWrite | Delete, exactly as JournalReader opens the same files**
            //.
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
            // A journal that cannot be read is one file's worth of history missing, not a reason to start
            // with no fleet at all.
            logger.LogWarning(ex, "Could not scan {File} for a fleet snapshot", file);
        }

        return (owner, holds);
    }
}

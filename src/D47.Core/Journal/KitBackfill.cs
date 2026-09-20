using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>
/// Every suit and hand weapon the Commander owns, recovered from journals d47 was not running for
/// (#294). On-foot purchases have no re-listing event the way <see cref="LoadoutBackfill"/> gets from
/// StoredShips, so unlike it this walks every file with no watermark rather than falling back to a
/// fixed floor.
/// </summary>
public static class KitBackfill
{
    /// <param name="stored">What <see cref="KitStore"/> held, to start from rather than to rebuild over.</param>
    /// <param name="since">How far the stored file has already been folded, or null where nothing says.</param>
    public static IReadOnlyDictionary<string, OwnedKit> FromHistory(
        string directory,
        ILogger logger,
        IReadOnlyDictionary<string, OwnedKit>? stored = null,
        DateTimeOffset? since = null,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (!Directory.Exists(directory))
        {
            logger.LogWarning("No journal folder at {Directory}", directory);

            // The file still answers.
            return stored ?? new Dictionary<string, OwnedKit>(StringComparer.Ordinal);
        }

        var all = Directory.EnumerateFiles(directory, JournalFolder.FilePattern)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();

        var walking = Window(all, since);

        if (walking.Count > 0)
        {
            logger.LogInformation(
                "Catching up on {Files} journal files for suits and weapons; the stored kit was last folded through {Since:u}",
                walking.Count,
                since);
        }

        return FromHistory(walking, logger, stored, cancellation: cancellation);
    }

    /// <summary>
    /// Which files the catch-up walks: everything since the file was last folded, or every file on disk
    /// where nothing says — no floor, because a suit bought and never worn again has no re-listing event
    /// to bound the search the way a docked fleet does.
    /// </summary>
    public static IReadOnlyList<string> Window(IReadOnlyList<string> files, DateTimeOffset? since)
    {
        ArgumentNullException.ThrowIfNull(files);

        if (since is not { } stamp)
        {
            return files;
        }

        // Compared as text against the same shape the folder's own ordering already relies on:
        // Journal.2026-08-22T190000.01.log.
        var cutoff = "Journal." + stamp.ToUniversalTime().ToString(
            "yyyy-MM-dd'T'HHmmss", System.Globalization.CultureInfo.InvariantCulture);

        var first = -1;

        for (var i = 0; i < files.Count; i++)
        {
            if (string.CompareOrdinal(Path.GetFileName(files[i]), cutoff) >= 0)
            {
                first = i;
                break;
            }
        }

        // Nothing at or after the stamp means every file predates it, so the newest is the only one that can
        // hold something the file has not already seen.
        var from = first < 0 ? files.Count - 1 : first - 1;

        return [.. files.Skip(Math.Max(0, from))];
    }

    /// <summary>The same, over an explicit list oldest-first.</summary>
    /// <param name="progress">How far through the files it has got, nought to one, or null to say nothing.</param>
    public static IReadOnlyDictionary<string, OwnedKit> FromHistory(
        IReadOnlyList<string> files,
        ILogger logger,
        IReadOnlyDictionary<string, OwnedKit>? stored = null,
        IProgress<double>? progress = null,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        // Seeded rather than rebuilt, same as LoadoutBackfill, and for the same reason: that is what makes
        // forgetting work across a restart.
        var remembered = stored is null
            ? new Dictionary<string, OwnedKit>(StringComparer.Ordinal)
            : new Dictionary<string, OwnedKit>(stored, StringComparer.Ordinal);

        var commander = string.Empty;

        // Every file it was handed.
        for (var i = 0; i < files.Count; i++)
        {
            cancellation.ThrowIfCancellationRequested();

            // Before the file rather than after it, so a walk of 983 starts at nought rather than sitting
            // empty through the first one.
            progress?.Report((double)i / Math.Max(1, files.Count));

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

                    // Comes before the Commander/LoadGame of the session that follows it, so the FID it
                    // names is read from the event, not from the variable above.
                    if (journalEvent.Kind == "NewCommander"
                        && journalEvent.String("FID") is { Length: > 0 } resetFid)
                    {
                        remembered.Remove(resetFid);
                        continue;
                    }

                    if (commander.Length == 0)
                    {
                        continue;
                    }

                    var known = remembered.TryGetValue(commander, out var existing) ? existing : OwnedKit.Empty;
                    remembered[commander] = known.Apply(journalEvent);
                }
            }
        }

        foreach (var (fid, kit) in remembered)
        {
            if (!kit.IsKnown)
            {
                continue;
            }

            logger.LogInformation(
                "Kit recovered from history for {Commander}: {Suits} suit(s), {Weapons} weapon(s)",
                fid,
                kit.Suits.Count,
                kit.Weapons.Count);
        }

        progress?.Report(1);

        return remembered
            .Where(entry => entry.Value.IsKnown)
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
    }
}

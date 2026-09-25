using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.Core.Journal;

/// <summary>Every ship's modules, recovered from journals d47 was not running for.</summary>
public static class LoadoutBackfill
{
    /// <summary>The fewest journal files to look back over, whatever else is asked for.</summary>
    private const int MinLookback = 25;

    /// <summary>
    /// <param name="stored"> What <see cref="LoadoutStore"/> held, to start from rather than to rebuild
    /// over (#128).
    /// </summary>
    /// <param name="stored">
    /// What <see cref="LoadoutStore"/> held, to start from rather than to rebuild over (#128).
    /// </param>
    /// <param name="since">
    /// How far the stored file has already been folded, or null where nothing says.
    /// </param>
    /// <param name="wanted">
    /// Ships to look for in files older than the window when the window does not hold them, keyed on the
    /// Frontier id, where an empty id matches any Commander (#475).
    /// </param>
    public static IReadOnlyDictionary<string, ShipLoadouts> FromHistory(
        string directory,
        ILogger logger,
        IReadOnlyDictionary<string, ShipLoadouts>? stored = null,
        DateTimeOffset? since = null,
        IReadOnlyCollection<(string Fid, int ShipId)>? wanted = null,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (!Directory.Exists(directory))
        {
            logger.LogWarning("No journal folder at {Directory}", directory);

            // The file still answers.
            return stored ?? new Dictionary<string, ShipLoadouts>(StringComparer.Ordinal);
        }

        var all = Directory.EnumerateFiles(directory, JournalFolder.FilePattern)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();

        var walking = Window(all, since);

        if (walking.Count > MinLookback)
        {
            // Said out loud, because this is the one start that takes noticeably longer and a Commander
            // watching it should be able to find out why.
            logger.LogInformation(
                "Catching up on {Files} journal files; the stored loadouts were last folded through {Since:u}",
                walking.Count,
                since);
        }

        var found = FromHistory(walking, logger, stored, cancellation: cancellation);

        var missing = (wanted ?? [])
            .Where(ship => !Holds(found, ship))
            .Distinct()
            .ToList();

        if (missing.Count == 0)
        {
            return found;
        }

        var from = EarliestLoadout(all, all.Count - walking.Count, missing, cancellation);

        if (from is not { } start)
        {
            logger.LogInformation(
                "No journal older than the last {Window} holds a loadout for {Missing} ship(s) a build names",
                walking.Count,
                missing.Count);

            return found;
        }

        logger.LogInformation(
            "Walking {Files} journal files, from {First}, for {Missing} ship(s) a build names",
            all.Count - start,
            Path.GetFileName(all[start]),
            missing.Count);

        return FromHistory([.. all.Skip(start)], logger, stored, cancellation: cancellation);
    }

    /// <summary>Whether this ship is remembered, or was forgotten by a sale the walk saw.</summary>
    private static bool Holds(IReadOnlyDictionary<string, ShipLoadouts> found, (string Fid, int ShipId) ship) =>
        found.Any(entry =>
            (ship.Fid.Length == 0 || string.Equals(entry.Key, ship.Fid, StringComparison.Ordinal))
            && (entry.Value.Ships.ContainsKey(ship.ShipId) || entry.Value.Forgotten.Contains(ship.ShipId)));

    /// <summary>
    /// The index of the oldest file holding the last <c>Loadout</c> of a missing ship, searching newest
    /// first from the file before <paramref name="before"/>, or null where none does. A ship sold or
    /// replaced in a file is not looked for in older ones.
    /// </summary>
    private static int? EarliestLoadout(
        IReadOnlyList<string> files,
        int before,
        List<(string Fid, int ShipId)> missing,
        CancellationToken cancellation)
    {
        int? earliest = null;

        for (var i = before - 1; i >= 0 && missing.Count > 0; i--)
        {
            cancellation.ThrowIfCancellationRequested();

            string text;

            try
            {
                using var stream = new FileStream(
                    files[i], FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

                using var reader = new StreamReader(stream);
                text = reader.ReadToEnd();
            }
            catch (IOException)
            {
                continue;
            }

            // Searched as text before any line is parsed, since most files name none of the ships.
            if (!missing.Any(ship => MentionsShip(text, ship.ShipId)))
            {
                continue;
            }

            var commander = string.Empty;

            // The last event in this file for each missing ship: true for a Loadout, false for a sale.
            var last = new Dictionary<(string Fid, int ShipId), bool>();

            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!JournalEvent.TryParse(line, NullLogger.Instance, out var journalEvent) || journalEvent is null)
                {
                    continue;
                }

                if (journalEvent.Kind is "Commander" or "LoadGame"
                    && journalEvent.String("FID") is { Length: > 0 } fid)
                {
                    commander = fid;
                }

                var (id, boarded) = journalEvent.Kind switch
                {
                    "Loadout" => (journalEvent.Int("ShipID"), true),
                    "ShipyardSell" or "ShipyardBuy" => (journalEvent.Int("SellShipID"), false),
                    "ShipyardNew" => (journalEvent.Int("NewShipID"), false),
                    _ => (null, false),
                };

                if (id is not { } shipId)
                {
                    continue;
                }

                foreach (var ship in missing)
                {
                    if (ship.ShipId == shipId
                        && (ship.Fid.Length == 0 || string.Equals(ship.Fid, commander, StringComparison.Ordinal)))
                    {
                        last[ship] = boarded;
                    }
                }
            }

            foreach (var (ship, boarded) in last)
            {
                _ = missing.Remove(ship);

                if (boarded)
                {
                    earliest = i;
                }
            }
        }

        return earliest;
    }

    /// <summary>Whether the text carries this id as the value of a key ending in <c>ShipID</c>.</summary>
    private static bool MentionsShip(string text, int shipId)
    {
        var needle = "ShipID\":" + shipId.ToString(System.Globalization.CultureInfo.InvariantCulture);

        for (var at = text.IndexOf(needle, StringComparison.Ordinal);
             at >= 0;
             at = text.IndexOf(needle, at + 1, StringComparison.Ordinal))
        {
            var after = at + needle.Length;

            if (after >= text.Length || !char.IsAsciiDigit(text[after]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Which files the catch-up walks: everything since the file was last folded, and never fewer than
    /// <see cref="MinLookback"/> (#128).
    /// </summary>
    public static IReadOnlyList<string> Window(IReadOnlyList<string> files, DateTimeOffset? since)
    {
        ArgumentNullException.ThrowIfNull(files);

        var floor = Math.Max(0, files.Count - MinLookback);

        if (since is not { } stamp)
        {
            return [.. files.Skip(floor)];
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

        return [.. files.Skip(Math.Min(Math.Max(0, from), floor))];
    }

    /// <summary>The same, over an explicit list oldest-first.</summary>
    /// <param name="progress">
    /// How far through the files it has got, nought to one, or null to say nothing (#128).
    /// </param>
    public static IReadOnlyDictionary<string, ShipLoadouts> FromHistory(
        IReadOnlyList<string> files,
        ILogger logger,
        IReadOnlyDictionary<string, ShipLoadouts>? stored = null,
        IProgress<double>? progress = null,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        // **Seeded rather than rebuilt** (#128), and that is what makes forgetting work across a restart.
        var remembered = stored is null
            ? new Dictionary<string, ShipLoadouts>(StringComparer.Ordinal)
            : new Dictionary<string, ShipLoadouts>(stored, StringComparer.Ordinal);

        var flying = new Dictionary<string, ShipLoadout>(StringComparer.Ordinal);
        var commander = string.Empty;

        // Every file it was handed.
        for (var i = 0; i < files.Count; i++)
        {
            cancellation.ThrowIfCancellationRequested();

            // Before the file rather than after it, so a walk of 943 starts at nought rather than sitting
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
                        remembered[resetFid] = ShipLoadouts.NoShips;
                        flying.Remove(resetFid);
                        continue;
                    }

                    if (commander.Length == 0)
                    {
                        continue;
                    }

                    var ship = (flying.TryGetValue(commander, out var held) ? held : ShipLoadout.Unknown)
                        .Apply(journalEvent);

                    flying[commander] = ship;

                    var known = remembered.TryGetValue(commander, out var existing) ? existing : ShipLoadouts.Empty;

                    // Remembered then forgotten, in the order CommanderGameState folds them and for the
                    // measured reason recorded there.
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

        progress?.Report(1);

        // Whole, because the walk started from everything the file held.
        return remembered
            .Where(entry => entry.Value.IsKnown)
            .ToDictionary(entry => entry.Key, entry => entry.Value with { IsWhole = true }, StringComparer.Ordinal);
    }

    /// <summary>Everything, from the first journal on disk, discarding what was stored (#128).</summary>
    public static LoadoutRescan Rescan(
        string directory,
        ILogger logger,
        IProgress<double>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (!Directory.Exists(directory))
        {
            logger.LogWarning("Asked to rescan {Directory}, which is not there", directory);
            return new LoadoutRescan(0, 0, new Dictionary<string, ShipLoadouts>(StringComparer.Ordinal));
        }

        var files = Directory.EnumerateFiles(directory, JournalFolder.FilePattern)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();

        logger.LogInformation("Rescanning {Files} journal files at the Commander's request", files.Count);

        var found = FromHistory(files, logger, stored: null, progress);

        return new LoadoutRescan(
            files.Count,
            found.Values.Sum(ships => ships.Ships.Count),
            found);
    }
}

/// <summary>What a rescan found (#128).</summary>
/// <param name="Files">How many journals were read.</param>
/// <param name="Ships">How many ships were found across every Commander, for the sentence.</param>
/// <param name="ByCommander">The picture itself, keyed on the Frontier id.</param>
public sealed record LoadoutRescan(
    int Files,
    int Ships,
    IReadOnlyDictionary<string, ShipLoadouts> ByCommander);

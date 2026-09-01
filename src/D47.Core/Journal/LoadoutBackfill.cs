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
    /// The fewest journal files to look back over, whatever else is asked for. The same floor
    /// <see cref="FleetBackfill"/> uses, and it was the whole window until
    /// <a href="https://github.com/dseelinger/d47/issues/128">#128</a>.
    /// <para>
    /// <b>Its job is unchanged and its meaning is not.</b> This used to <em>be</em> the memory, so
    /// a ship not sat in inside it was forgotten on every launch; <see cref="LoadoutStore"/> is
    /// the long memory now and this is the catch-up. It stays as the floor rather than as the
    /// whole answer because a run with no watermark to work from — a first run, or a file
    /// somebody deleted — has no gap to measure and this is a good guess at one.
    /// </para>
    /// </summary>
    private const int MinLookback = 25;

    /// <param name="stored">
    /// What <see cref="LoadoutStore"/> held, to start from rather than to rebuild over
    /// (<a href="https://github.com/dseelinger/d47/issues/128">#128</a>). Empty before the file
    /// existed, and empty on a first run.
    /// </param>
    /// <param name="since">
    /// How far the stored file has already been folded, or null where nothing says. <b>This is
    /// what makes a sale d47 was closed for findable rather than permanent</b>: the event is still
    /// on disk in an older journal, so the walk reaches back to wherever the file left off rather
    /// than to a fixed number of files, and a <c>ShipyardSell</c> from thirty journals ago is
    /// replayed through the same fold as one from this morning.
    /// <para>
    /// <b>Unbounded on purpose, and measured rather than feared.</b> What it costs is proportional
    /// to how long d47 has been away, not to how much history exists. Measured on the 943-journal,
    /// 382 MB corpus this was built against: the whole of it — all 716,653 events — reads and
    /// folds in <b>3.1 seconds</b>, 300 files in 996 ms, 100 files in 432 ms, and the 25-file
    /// floor in about 190 ms. A cap would buy a fraction of a second back on the rarest start
    /// there is and reintroduce exactly the hole this closes.
    /// </para>
    /// </param>
    public static IReadOnlyDictionary<string, ShipLoadouts> FromHistory(
        string directory,
        ILogger logger,
        IReadOnlyDictionary<string, ShipLoadouts>? stored = null,
        DateTimeOffset? since = null)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (!Directory.Exists(directory))
        {
            logger.LogWarning("No journal folder at {Directory}", directory);

            // The file still answers. A folder that has moved is not a reason to forget every
            // ship in it, which is what returning nothing here used to mean.
            return stored ?? new Dictionary<string, ShipLoadouts>(StringComparer.Ordinal);
        }

        var all = Directory.EnumerateFiles(directory, JournalFolder.FilePattern)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();

        var walking = Window(all, since);

        if (walking.Count > MinLookback)
        {
            // Said out loud, because this is the one start that takes noticeably longer and a
            // Commander watching it should be able to find out why.
            logger.LogInformation(
                "Catching up on {Files} journal files; the stored loadouts were last folded through {Since:u}",
                walking.Count,
                since);
        }

        return FromHistory(walking, logger, stored);
    }

    /// <summary>
    /// Which files the catch-up walks: everything since the file was last folded, and never fewer
    /// than <see cref="MinLookback"/> (#128).
    /// <para>
    /// <b>One file before the cutoff rather than the cutoff itself.</b> A journal is named for
    /// when its session <em>started</em>, so the file holding an event at 10:05 may well be named
    /// 09:40 — stepping back one is what stops the walk beginning after the event it was sent to
    /// find. The same reasoning <c>AdventureBook.FilesToWalk</c> records.
    /// </para>
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

        // Nothing at or after the stamp means every file predates it, so the newest is the only
        // one that can hold something the file has not already seen.
        var from = first < 0 ? files.Count - 1 : first - 1;

        return [.. files.Skip(Math.Min(Math.Max(0, from), floor))];
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
    /// <param name="progress">
    /// How far through the files it has got, nought to one, or null to say nothing (#128). Only
    /// the Commander's own rescan asks for it: the catch-up at startup is a fraction of a second
    /// and has nothing watching it.
    /// </param>
    public static IReadOnlyDictionary<string, ShipLoadouts> FromHistory(
        IReadOnlyList<string> files,
        ILogger logger,
        IReadOnlyDictionary<string, ShipLoadouts>? stored = null,
        IProgress<double>? progress = null)
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

        // Every file it was handed. Choosing them is Window's job now (#128) rather than a clamp
        // here, so a caller asking for a wide catch-up is not quietly given 25 files back.
        for (var i = 0; i < files.Count; i++)
        {
            // Before the file rather than after it, so a walk of 943 starts at nought rather than
            // sitting empty through the first one.
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

        progress?.Report(1);

        return remembered
            .Where(entry => entry.Value.IsKnown)
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
    }

    /// <summary>
    /// Everything, from the first journal on disk, discarding what was stored
    /// (<a href="https://github.com/dseelinger/d47/issues/128">#128</a>). <b>The Commander's own
    /// repair</b>, for when what is drawn does not look right.
    /// <para>
    /// <b>It rebuilds rather than catching up, which is the whole difference.</b> The startup walk
    /// is seeded with the file so that a session's worth of events lands on top of what is already
    /// known; this one throws the file away and derives the answer again from the journals, so a
    /// ship that nothing on disk supports stops existing. That is what makes it a repair rather
    /// than another pass of the same thing.
    /// </para>
    /// <para>
    /// <b>It reports how many files it read, and the caller needs that rather than the ship
    /// count.</b> A folder that has moved, or a Commander pointed at the wrong one, reads nothing
    /// and would otherwise look identical to a fleet that has genuinely been sold — and replacing
    /// a good file with that answer is the one thing a repair must not do.
    /// </para>
    /// </summary>
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

/// <summary>
/// What a rescan found (<a href="https://github.com/dseelinger/d47/issues/128">#128</a>).
/// </summary>
/// <param name="Files">
/// How many journals were read. <b>Nought is the answer that must not be acted on</b> — see
/// <see cref="LoadoutBackfill.Rescan"/>.
/// </param>
/// <param name="Ships">How many ships were found across every Commander, for the sentence.</param>
/// <param name="ByCommander">The picture itself, keyed on the Frontier id.</param>
public sealed record LoadoutRescan(
    int Files,
    int Ships,
    IReadOnlyDictionary<string, ShipLoadouts> ByCommander);

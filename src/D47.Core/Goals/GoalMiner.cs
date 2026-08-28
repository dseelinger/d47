using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace D47.Core.Goals;

/// <summary>
/// What the corpus could say about one arc: when it started, and how far it had got by the end of
/// the journals on this disk (Phase 34, "Progress is derived, never typed").
/// </summary>
public sealed record GoalMark
{
    public required string Key { get; init; }

    /// <summary>
    /// The first evidence in the corpus that the Commander had begun this arc at all.
    /// <para>
    /// <b>Null is "no evidence they have started", not "unknown".</b> A Commander at rank 0 in
    /// Exobiology has not begun that one, and an arc that invented a start date from the earliest
    /// journal on the disk would report a year of progress on something never attempted.
    /// </para>
    /// </summary>
    public DateTimeOffset? Started { get; init; }

    /// <summary>What history counted. Null where only live state can say, which is most arcs.</summary>
    public long? Have { get; init; }

    /// <summary>When <see cref="Have"/> was last true in the corpus.</summary>
    public DateTimeOffset? AsOf { get; init; }
}

/// <summary>One mining run's conclusions about one Commander.</summary>
public sealed record GoalMine
{
    public required string FrontierId { get; init; }

    public required DateTimeOffset MinedAt { get; init; }

    public int Journals { get; init; }

    /// <summary>The window the corpus covers, which is not the window the Commander has played.</summary>
    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public IReadOnlyList<GoalMark> Marks { get; init; } = [];

    public GoalMark? For(string key) =>
        Marks.FirstOrDefault(mark => string.Equals(mark.Key, key, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// The batch walk that gives every arc its age (Phase 34, "Goals that outlive a
/// checklist" — <em>how long it has been running</em>).
/// <para>
/// <b>It exists because live state structurally cannot say two things.</b> The journal tells d47
/// what the Commander's rank is now and how many engineers are unlocked now; it says nothing about
/// when either started, and nothing about how many systems they have ever visited. Those are
/// facts about a history, and a history is a folder of files.
/// </para>
/// <para>
/// One pass over the journals in name order, and deliberately to the letter the shape every
/// other miner in this repository uses: files in name order,
/// per Commander on the Frontier id carried across continuation files, no thread, no clock, and the
/// instant injected. It is the second walk over the same corpus and the first one measured it at
/// 697,787 events in 3.6 seconds, so the cost of this was known before it was written.
/// </para>
/// <para>
/// <b>Nothing leaves the machine and no journal reaches a model.</b> What comes out is counts and
/// dates.
/// </para>
/// </summary>
public sealed class GoalMiner(ILogger<GoalMiner> logger)
{
    public IReadOnlyList<GoalMine> Mine(string directory, DateTimeOffset now)
    {
        if (!Directory.Exists(directory))
        {
            logger.LogWarning("No journal folder at {Directory}", directory);
            return [];
        }

        var files = Directory.EnumerateFiles(directory, JournalFolder.FilePattern)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();

        return Mine(files, now);
    }

    /// <summary>The same, over a list of files. What a test drives.</summary>
    public IReadOnlyList<GoalMine> Mine(IReadOnlyList<string> files, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(files);

        var folds = new Dictionary<string, Fold>(StringComparer.Ordinal);

        // Carried across files rather than reset per file, and the reason is the journals rather
        // than a preference: a
        // continuation journal re-emits Fileheader and does not re-emit Commander.
        var commander = GoalStore.NoCommander;
        var events = 0L;

        foreach (var file in files)
        {
            var reader = new JournalReader(file, logger);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            while (reader.Poll() is { Count: > 0 } batch)
            {
                foreach (var journalEvent in batch)
                {
                    events++;

                    if (journalEvent.Kind is "Commander" or "LoadGame" &&
                        journalEvent.Raw.String("FID") is { Length: > 0 } fid)
                    {
                        commander = fid;
                    }

                    var fold = folds.TryGetValue(commander, out var existing)
                        ? existing
                        : folds[commander] = new Fold();

                    if (seen.Add(commander))
                    {
                        fold.Journals++;
                    }

                    fold.Observe(journalEvent);
                }
            }
        }

        logger.LogInformation(
            "Walked {Events} events from {Files} journals for {Commanders} Commander(s) of goal progress",
            events,
            files.Count,
            folds.Count);

        if (folds.Count > 1)
        {
            folds.Remove(GoalStore.NoCommander);
        }

        return
        [
            .. folds
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Value.Conclude(pair.Key, now)),
        ];
    }

    /// <summary>One Commander's counters. Arithmetic over events and nothing else.</summary>
    private sealed class Fold
    {
        private readonly Dictionary<string, (int Rank, DateTimeOffset At)> _ranks = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTimeOffset> _rankStarted = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _systems = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _hulls = new(StringComparer.OrdinalIgnoreCase);

        private double _lightYears;
        private int _engineers;
        private DateTimeOffset? _engineersAt;
        private DateTimeOffset? _engineersStarted;
        private DateTimeOffset? _hullsStarted;
        private DateTimeOffset? _hullsAt;
        private DateTimeOffset? _flyingStarted;
        private DateTimeOffset? _flyingAt;

        public int Journals { get; set; }

        private DateTimeOffset From { get; set; } = DateTimeOffset.MaxValue;

        private DateTimeOffset To { get; set; } = DateTimeOffset.MinValue;

        public void Observe(JournalEvent journalEvent)
        {
            var stamp = journalEvent.Timestamp;

            if (stamp > DateTimeOffset.MinValue)
            {
                if (stamp < From)
                {
                    From = stamp;
                }

                if (stamp > To)
                {
                    To = stamp;
                }
            }

            switch (journalEvent.Kind)
            {
                case "Rank" or "Promotion":
                    foreach (var career in RankState.Careers)
                    {
                        if (journalEvent.Raw.Int(career) is not { } rank)
                        {
                            continue;
                        }

                        // A rank that goes DOWN is a save that was started again under the same
                        // Frontier id, and the corpus proved it on the first real run: one of these
                        // three accounts reports Trade 7 in July, Trade 2 in January and Trade 0 in
                        // June. The id is the account and a person can begin a new Commander in it,
                        // so the arc began again too — dating it from the first character's first
                        // rank would report a year of progress towards a ladder somebody has since
                        // restarted from nothing.
                        var restarted = _ranks.TryGetValue(career, out var previous) && rank < previous.Rank;

                        _ranks[career] = (rank, stamp);

                        if (restarted)
                        {
                            _rankStarted.Remove(career);
                        }

                        // The first sighting of a rank above nothing is the first evidence the
                        // Commander began that career. Rank 0 is not a start.
                        if (rank > 0 && !_rankStarted.ContainsKey(career))
                        {
                            _rankStarted[career] = stamp;
                        }
                    }

                    break;

                case "EngineerProgress":
                    var unlocked = journalEvent.Items("Engineers")
                        .Count(engineer => string.Equals(engineer.String("Progress"), "Unlocked", StringComparison.OrdinalIgnoreCase));

                    // The snapshot only. A single-engineer event is a delta and counting it as a
                    // total would report one unlocked engineer the moment anybody ranked up.
                    if (unlocked > 0)
                    {
                        // The latest snapshot rather than the largest, and the start moves with it
                        // when it falls — the same restarted-save rule the ranks above carry, from
                        // the same evidence. A maximum would report twenty-three engineers to
                        // somebody who has four.
                        if (unlocked < _engineers)
                        {
                            _engineersStarted = stamp;
                        }

                        _engineers = unlocked;
                        _engineersAt = stamp;
                        _engineersStarted ??= stamp;
                    }

                    break;

                case "FSDJump":
                    _flyingStarted ??= stamp;
                    _flyingAt = stamp;

                    if (journalEvent.Raw.String("StarSystem") is { Length: > 0 } system)
                    {
                        _systems.Add(system);
                    }

                    if (journalEvent.Raw.Double("JumpDist") is { } distance and > 0)
                    {
                        _lightYears += distance;
                    }

                    break;

                case "Loadout" or "ShipyardBuy" or "ShipyardNew" or "ShipyardSwap":
                    // ShipyardBuy writes ShipType; Loadout writes Ship. Both are the symbol.
                    if (journalEvent.Raw.String("Ship") is { Length: > 0 } flown)
                    {
                        Hull(flown, stamp);
                    }

                    if (journalEvent.Raw.String("ShipType") is { Length: > 0 } bought)
                    {
                        Hull(bought, stamp);
                    }

                    break;

                case "StoredShips":
                    foreach (var stored in journalEvent.Items("ShipsHere").Concat(journalEvent.Items("ShipsRemote")))
                    {
                        if (stored.String("ShipType") is { Length: > 0 } type)
                        {
                            Hull(type, stamp);
                        }
                    }

                    break;
            }
        }

        public GoalMine Conclude(string frontierId, DateTimeOffset now)
        {
            var marks = new List<GoalMark>();

            foreach (var career in RankState.Careers)
            {
                var key = GoalCatalogue.RankPrefix + career.ToLowerInvariant();

                if (!_ranks.TryGetValue(career, out var standing) && !_rankStarted.ContainsKey(career))
                {
                    continue;
                }

                marks.Add(new GoalMark
                {
                    Key = key,
                    Started = _rankStarted.GetValueOrDefault(career) is { } started && started != default
                        ? started
                        : null,
                    Have = _ranks.ContainsKey(career) ? standing.Rank : null,
                    AsOf = _ranks.ContainsKey(career) ? standing.At : null,
                });
            }

            marks.Add(new GoalMark
            {
                Key = GoalCatalogue.Engineers,
                Started = _engineersStarted,
                Have = _engineers > 0 ? _engineers : null,
                AsOf = _engineersAt,
            });

            marks.Add(new GoalMark
            {
                Key = GoalCatalogue.Ships,

                // Every hull ever flown, which is not the collection: a hull sold is still a hull
                // the Commander once had. So this is a floor under the live count and it says so
                // by only ever being used where live state is silent.
                Started = _hullsStarted,
                Have = _hulls.Count > 0 ? _hulls.Count : null,
                AsOf = _hullsAt,
            });

            marks.Add(new GoalMark
            {
                Key = GoalCatalogue.Systems,
                Started = _flyingStarted,
                Have = _systems.Count > 0 ? _systems.Count : null,
                AsOf = _flyingAt,
            });

            marks.Add(new GoalMark
            {
                Key = GoalCatalogue.Distance,
                Started = _flyingStarted,
                Have = _lightYears > 0 ? (long)Math.Round(_lightYears) : null,
                AsOf = _flyingAt,
            });

            return new GoalMine
            {
                FrontierId = frontierId,
                MinedAt = now,
                Journals = Journals,
                From = From == DateTimeOffset.MaxValue ? null : From,
                To = To == DateTimeOffset.MinValue ? null : To,
                Marks = marks,
            };
        }

        private void Hull(string symbol, DateTimeOffset stamp)
        {
            _hulls.Add(symbol);
            _hullsStarted ??= stamp;
            _hullsAt = stamp;
        }
    }
}

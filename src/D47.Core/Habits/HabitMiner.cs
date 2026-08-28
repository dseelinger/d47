using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace D47.Core.Habits;

/// <summary>
/// What one mining run concluded about one Commander (Phase 32).
/// <para>
/// Two lists rather than one, and that is item 2's requirement rather than tidiness: a detector
/// that found no problem and a detector that has not seen enough of the Commander to say are
/// different answers, and reporting the second as the first tells somebody something untrue about
/// themselves.
/// </para>
/// </summary>
public sealed record HabitReport
{
    /// <summary>Who this is about. <see cref="HabitStore.NoCommander"/> where the journals never said.</summary>
    public required string FrontierId { get; init; }

    public IReadOnlyList<HabitClaim> Claims { get; init; } = [];

    /// <summary>Detectors that ran and declined, with the reason each one gave.</summary>
    public IReadOnlyList<HabitSilence> Quiet { get; init; } = [];

    /// <summary>How many journal files this Commander's events came from.</summary>
    public required int Journals { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    /// <summary>When the mining ran. Injected, like every instant in Core.</summary>
    public required DateTimeOffset MinedAt { get; init; }

    /// <summary>
    /// Set when the corpus was too small to consult a detector at all, and says so in words. Null
    /// on an ordinary run.
    /// </summary>
    public string? Refusal { get; init; }
}

/// <summary>
/// The batch job over journals that already exist (Phase 32, "Mine the Commander's own
/// journals").
/// <para>
/// <b>The most defensible thing d47 can do, because it cannot be copied.</b> A competitor can ship
/// the same tables and the same personas and cannot ship your history. This is 914 files and
/// 697,787 events on the Commander's own disk, and the analysis is arithmetic over them.
/// </para>
/// <para>
/// <b>Nothing leaves the machine and no journal reaches a model.</b> That is item 1's requirement
/// and it is also what keeps the phase out of the untrusted-input path entirely: there is no prompt
/// here, no tool the model can reach, and nothing a hostile in-game message could aim at.
/// </para>
/// <para>
/// <b>Per Commander, on the Frontier id.</b> This corpus is three accounts under nine character
/// names across thirteen months. A miner that pooled them would report one person's habits to
/// another and would be confidently wrong about somebody, which is the failure item 2 spends its
/// whole length guarding against. The id comes from <c>Commander</c> and <c>LoadGame</c> and it
/// carries across a continuation file, which does not re-emit either.
/// </para>
/// <para>
/// <b>It owns no thread and reads no clock</b>, like everything else in Core: it is handed the
/// files and the instant, and the App is what decides not to run it on the UI thread. That is the
/// same invariant that makes <c>spike/CorpusReplay</c> possible at seven seconds, which is where
/// the cost of this was measured before the phase was written.
/// </para>
/// </summary>
public sealed class HabitMiner(ILogger<HabitMiner> logger)
{
    /// <summary>
    /// Every detector that ships, in the order a report reads them. Built fresh per Commander,
    /// because a detector holds the fold state for the person it is about.
    /// </summary>
    public static IReadOnlyList<IHabitDetector> Detectors() =>
    [
        new ArrivalCollisionDetector(),
        new OvershootDetector(),
        new InterdictionSubmitDetector(),
        new OnFootDeathDetector(),
        new HighGravityLandingDetector(),
    ];

    /// <summary>
    /// Reads every journal in a folder and concludes, one report per Commander found.
    /// <para>
    /// Files in name order, which for Elite's naming is chronological — the detectors fold a
    /// stream and several of them are about what followed what.
    /// </para>
    /// </summary>
    public IReadOnlyList<HabitReport> Mine(string directory, DateTimeOffset now)
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
    public IReadOnlyList<HabitReport> Mine(IReadOnlyList<string> files, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(files);

        var folds = new Dictionary<string, Fold>(StringComparer.Ordinal);

        // Carried across files rather than reset per file: a continuation journal re-emits
        // Fileheader and does not re-emit Commander, so resetting here would file the second half
        // of a long session under nobody.
        var commander = HabitStore.NoCommander;
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
            "Mined {Events} events from {Files} journals for {Commanders} Commander(s)",
            events,
            files.Count,
            folds.Count);

        // Events that arrived before any Commander event named somebody are dropped once somebody
        // has been named — they are the first few lines of a file whose owner is identified three
        // lines later, and filing them under nobody produces a fourth "Commander" in the report who
        // is really the header of three journals. Kept when nothing identified anybody at all, which
        // is a corpus that would otherwise mine to nothing.
        if (folds.Count > 1)
        {
            folds.Remove(HabitStore.NoCommander);
        }

        return
        [
            .. folds
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Value.Conclude(pair.Key, now)),
        ];
    }

    /// <summary>One Commander's detectors and the window their events span.</summary>
    private sealed class Fold
    {
        private readonly IReadOnlyList<IHabitDetector> _detectors = Detectors();

        public int Journals { get; set; }

        private DateTimeOffset From { get; set; } = DateTimeOffset.MaxValue;

        private DateTimeOffset To { get; set; } = DateTimeOffset.MinValue;

        public void Observe(JournalEvent journalEvent)
        {
            if (journalEvent.Timestamp > DateTimeOffset.MinValue)
            {
                if (journalEvent.Timestamp < From)
                {
                    From = journalEvent.Timestamp;
                }

                if (journalEvent.Timestamp > To)
                {
                    To = journalEvent.Timestamp;
                }
            }

            foreach (var detector in _detectors)
            {
                detector.Observe(journalEvent);
            }
        }

        public HabitReport Conclude(string frontierId, DateTimeOffset now)
        {
            var window = new HabitWindow(Journals, From, To, now);

            // Below the floor nothing is consulted and the report says why. A detector's own
            // decline would say the same thing five times over and would read as five findings.
            if (Journals < HabitFloor.Journals)
            {
                return new HabitReport
                {
                    FrontierId = frontierId,
                    Journals = Journals,
                    From = From == DateTimeOffset.MaxValue ? null : From,
                    To = To == DateTimeOffset.MinValue ? null : To,
                    MinedAt = now,
                    Refusal =
                        $"There are {Journals} of your journals here, and I do not draw conclusions about "
                        + $"somebody from fewer than {HabitFloor.Journals}. Fly for a while and ask me again.",
                };
            }

            var outcomes = _detectors.Select(detector => detector.Conclude(window)).ToList();

            return new HabitReport
            {
                FrontierId = frontierId,
                Journals = Journals,
                From = From,
                To = To,
                MinedAt = now,
                Claims = [.. outcomes.Select(outcome => outcome.Claim).OfType<HabitClaim>()],
                Quiet = [.. outcomes.Select(outcome => outcome.Silence).OfType<HabitSilence>()],
            };
        }
    }
}

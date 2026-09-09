using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace D47.Core.Adventures;

/// <summary>
/// One beat reached, or an opening spoken, waiting to be said (Phase 47, "The ship's AI tells it, and
/// the authored beat is the floor").
/// </summary>
/// <param name="Beat">The beat index, or <c>-1</c> for the opening.</param>
public sealed record AdventureMoment(string FrontierId, Adventure Adventure, int Beat, DateTimeOffset At)
{
    public bool IsOpening => Beat < 0;

    public string Line => IsOpening
        ? Adventure.Opening ?? $"{Adventure.Name} begins."
        : Adventure.Beats[Beat].Line;

    public string Title => IsOpening ? Adventure.Name : Adventure.Beats[Beat].Title;

    /// <summary>The beat this one hands over to — the first, after the opening — or null after the last.</summary>
    public AdventureBeat? Next => Adventure.Beats.ElementAtOrDefault(Beat + 1);

    /// <summary>Where the Commander goes next, said with the line rather than waited for.</summary>
    public string? HandOff => Next?.Trigger.HandOff();

    /// <summary>The line and the hand-off together, which is what is said.</summary>
    public string Spoken => HandOff is { } next ? $"{Line} {next}" : Line;

    /// <summary>
    /// Stable per adventure and beat, so the engine's cooldown keys on the beat and not the text.
    /// </summary>
    public string Key => $"{AdventureCallout.KeyPrefix}{Adventure.Key}.{(IsOpening ? "opening" : Beat.ToString(System.Globalization.CultureInfo.InvariantCulture))}";
}

/// <summary>The Commander's adventures, read as one thing (Phase 47).</summary>
public sealed class AdventureBook(AdventureStore store, ILogger<AdventureBook> logger)
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, AdventureStanding> _standings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _highWater = new(StringComparer.Ordinal);
    private readonly Queue<AdventureMoment> _moments = new();

    /// <summary>
    /// Which stories are owed a spoken line right now — a beat has fired and the Commander has not
    /// heard about it yet (asked for 2026-08-22).
    /// </summary>
    private readonly HashSet<string> _stirring = new(StringComparer.Ordinal);

    private bool _needsCatchUp = true;

    public AdventureStore Store => store;

    /// <summary>Raised when a story starts or stops being owed a line.</summary>
    public event Action? StirringChanged;

    /// <summary>
    /// Whether this story is between a beat firing and the line being said — which is up to the
    /// callout's twenty-second settle plus whatever the model spends rewriting it.
    /// </summary>
    public bool IsStirring(string? frontierId, string key)
    {
        var standing = StandingKey(frontierId ?? AdventureStore.NoCommander, key);

        lock (_gate)
        {
            return _stirring.Contains(standing);
        }
    }

    /// <summary>Whether anything of this Commander's is owed a line, for the mini panel's one glance.</summary>
    public bool IsStirringAnywhere(string? frontierId)
    {
        var prefix = StandingKey(frontierId ?? AdventureStore.NoCommander, string.Empty);

        lock (_gate)
        {
            return _stirring.Any(key => key.StartsWith(prefix, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// Records what was actually said about a story and stops the waiting (asked for 2026-08-22).
    /// </summary>
    public void Told(string? frontierId, string key, AdventureTold entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var commander = frontierId ?? AdventureStore.NoCommander;

        Quiet(commander, key);

        if (store.Find(commander, key) is not { } adventure)
        {
            return;
        }

        var kept = adventure.Told.Count + 1 > AdventureLimits.MaxTold
            ? adventure.Told.Skip(adventure.Told.Count + 1 - AdventureLimits.MaxTold).Append(entry).ToList()
            : adventure.Told.Append(entry).ToList();

        // Straight to the store, which is what the panel and the file both read.
        if (store.Save(commander, adventure with { Told = kept }) is { } refusal)
        {
            logger.LogWarning("Could not record what was said about {Name}: {Refusal}", adventure.Name, refusal);
        }
    }

    /// <summary>
    /// Stops the waiting without recording anything — the beat was dropped rather than spoken, which is
    /// what the callout does when it comes due mid-interdiction.
    /// </summary>
    public void Quiet(string? frontierId, string key)
    {
        var standing = StandingKey(frontierId ?? AdventureStore.NoCommander, key);
        bool moved;

        lock (_gate)
        {
            moved = _stirring.Remove(standing);
        }

        if (moved)
        {
            StirringChanged?.Invoke();
        }
    }

    /// <summary>Marks a story as owed a line.</summary>
    private void Stir(string commander, string key)
    {
        // Inside the caller's lock, and so the event is raised by them after it is released.
        _stirring.Add(StandingKey(commander, key));
    }

    /// <summary>Whether a walk over the journal files is owed — at startup, and after a stamp moved.</summary>
    public bool NeedsCatchUp
    {
        get
        {
            lock (_gate)
            {
                return _needsCatchUp;
            }
        }
    }

    /// <summary>Every adventure this Commander has, with where each stands.</summary>
    public IReadOnlyList<AdventureStanding> Standings(string? frontierId)
    {
        var commander = frontierId ?? AdventureStore.NoCommander;

        lock (_gate)
        {
            return [.. store.For(commander).Select(adventure => StandingOf(commander, adventure))];
        }
    }

    public AdventureStanding? Standing(string? frontierId, string key)
    {
        var commander = frontierId ?? AdventureStore.NoCommander;

        if (store.Find(commander, key) is not { } adventure)
        {
            return null;
        }

        lock (_gate)
        {
            return StandingOf(commander, adventure);
        }
    }

    /// <summary>The adventures under way for this Commander — begun, not abandoned, not finished.</summary>
    public IReadOnlyList<AdventureStanding> Active(string? frontierId) =>
        [.. Standings(frontierId).Where(standing => standing.Adventure.IsActive && !standing.IsDone)];

    /// <summary>
    /// Walks journal files, oldest first, folding every event after each active adventure's acceptance
    /// into its standing.
    /// </summary>
    public void CatchUp(IReadOnlyList<string> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var commander = AdventureStore.NoCommander;
        var events = 0L;

        lock (_gate)
        {
            _standings.Clear();
            _highWater.Clear();
        }

        foreach (var file in files)
        {
            var reader = new JournalReader(file, logger);

            while (reader.Poll() is { Count: > 0 } batch)
            {
                foreach (var journalEvent in batch)
                {
                    events++;

                    if (journalEvent.Kind is "Commander" or "LoadGame"
                        && journalEvent.Raw.String("FID") is { Length: > 0 } fid)
                    {
                        commander = fid;
                    }

                    lock (_gate)
                    {
                        Fold(commander, journalEvent, announce: false);
                        _highWater[commander] = journalEvent.Timestamp;
                    }
                }
            }
        }

        lock (_gate)
        {
            _needsCatchUp = false;
        }

        logger.LogInformation("Caught adventures up over {Events} events in {Files} journals", events, files.Count);
    }

    /// <summary>One live event.</summary>
    public void Observe(JournalEvent journalEvent, string? frontierId)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        var commander = frontierId ?? AdventureStore.NoCommander;
        bool stirred;

        lock (_gate)
        {
            if (_highWater.TryGetValue(commander, out var mark) && journalEvent.Timestamp <= mark)
            {
                return;
            }

            var before = _stirring.Count;
            Fold(commander, journalEvent, announce: true);
            stirred = _stirring.Count != before;
        }

        // Outside the lock: a handler that redraws a panel has no business running under the gate every other
        // read of this book takes.
        if (stirred)
        {
            StirringChanged?.Invoke();
        }
    }

    /// <summary>Everything reached since the last call, oldest first.</summary>
    public IReadOnlyList<AdventureMoment> Drain()
    {
        lock (_gate)
        {
            if (_moments.Count == 0)
            {
                return [];
            }

            var drained = _moments.ToList();
            _moments.Clear();
            return drained;
        }
    }

    /// <summary>Writes an adventure the Commander authored or a draft the generator produced.</summary>
    public string? Write(string? frontierId, Adventure adventure) => store.Save(frontierId, adventure);

    /// <summary>The acceptance act.</summary>
    public string? Begin(string? frontierId, string key, DateTimeOffset now)
    {
        var commander = frontierId ?? AdventureStore.NoCommander;

        if (store.Find(commander, key) is not { } adventure)
        {
            return "There is no adventure by that name.";
        }

        if (adventure.IsActive)
        {
            return $"{adventure.Name} is already under way.";
        }

        if (AdventureValidation.NotReady(adventure) is { Count: > 0 } reasons)
        {
            return string.Join(" ", reasons);
        }

        // Told is cleared with the stamp: a story begun again is being told again, and the feed the Commander
        // reads is what happened this time round rather than a splice of two runs.
        var begun = adventure with { AcceptedAt = now, AbandonedAt = null, Previous = null, Told = [] };

        if (store.Save(commander, begun) is { } refusal)
        {
            return refusal;
        }

        lock (_gate)
        {
            _standings[StandingKey(commander, begun.Key)] = AdventureFold.Start(begun);
            _moments.Enqueue(new AdventureMoment(commander, begun, -1, now));
            Stir(commander, begun.Key);
        }

        StirringChanged?.Invoke();
        return null;
    }

    /// <summary>Stop telling me this.</summary>
    public string? Abandon(string? frontierId, string key, DateTimeOffset now)
    {
        var commander = frontierId ?? AdventureStore.NoCommander;

        if (store.Find(commander, key) is not { } adventure)
        {
            return "There is no adventure by that name.";
        }

        if (!adventure.IsActive)
        {
            return $"{adventure.Name} is not under way.";
        }

        var abandoned = adventure with { AbandonedAt = now };

        if (store.Save(commander, abandoned) is { } refusal)
        {
            return refusal;
        }

        lock (_gate)
        {
            if (_standings.TryGetValue(StandingKey(commander, key), out var standing))
            {
                _standings[StandingKey(commander, key)] = standing with { Adventure = abandoned };
            }

            // A beat waiting out its settle window belongs to a story that has just been stopped.
            var kept = _moments.Where(moment => !SameStory(moment, commander, key)).ToList();
            _moments.Clear();

            foreach (var moment in kept)
            {
                _moments.Enqueue(moment);
            }

            // And so does the animation that was waiting with it.
            _stirring.Remove(StandingKey(commander, key));
        }

        StirringChanged?.Invoke();
        return null;
    }

    /// <summary>I do not want this record.</summary>
    public bool Remove(string? frontierId, string key)
    {
        var commander = frontierId ?? AdventureStore.NoCommander;
        var removed = store.Remove(commander, key);

        lock (_gate)
        {
            _standings.Remove(StandingKey(commander, key));
            _stirring.Remove(StandingKey(commander, key));
        }

        StirringChanged?.Invoke();
        return removed;
    }

    /// <summary>Called on the store's change event.</summary>
    public void Reconcile()
    {
        lock (_gate)
        {
            var present = new HashSet<string>(StringComparer.Ordinal);

            foreach (var commander in store.Commanders)
            {
                foreach (var adventure in store.For(commander))
                {
                    var key = StandingKey(commander, adventure.Key);
                    present.Add(key);

                    if (_standings.TryGetValue(key, out var standing))
                    {
                        if (standing.Adventure.AcceptedAt == adventure.AcceptedAt)
                        {
                            _standings[key] = standing with { Adventure = adventure };
                            continue;
                        }

                        _standings.Remove(key);
                    }

                    if (adventure.IsActive)
                    {
                        _needsCatchUp = true;
                    }
                }
            }

            foreach (var stale in _standings.Keys.Where(key => !present.Contains(key)).ToList())
            {
                _standings.Remove(stale);
            }
        }
    }

    private void Fold(string commander, JournalEvent journalEvent, bool announce)
    {
        foreach (var adventure in store.For(commander))
        {
            if (!adventure.IsActive)
            {
                continue;
            }

            var key = StandingKey(commander, adventure.Key);
            var before = StandingOf(commander, adventure);
            var after = AdventureFold.Apply(before, journalEvent);

            if (ReferenceEquals(before, after))
            {
                continue;
            }

            _standings[key] = after;

            if (announce)
            {
                _moments.Enqueue(new AdventureMoment(commander, adventure, after.Fired.Count - 1, journalEvent.Timestamp));

                // From here until the line is said or dropped, the tab has something to animate.
                Stir(commander, adventure.Key);
            }

            logger.LogInformation(
                "Adventure {Name} reached beat {Beat} ({Title}) at {When}",
                adventure.Name,
                after.Fired.Count,
                adventure.Beats[after.Fired.Count - 1].Title,
                journalEvent.Timestamp);
        }
    }

    private AdventureStanding StandingOf(string commander, Adventure adventure)
    {
        var key = StandingKey(commander, adventure.Key);

        if (_standings.TryGetValue(key, out var standing) && standing.Adventure.AcceptedAt == adventure.AcceptedAt)
        {
            return ReferenceEquals(standing.Adventure, adventure) ? standing : standing with { Adventure = adventure };
        }

        var fresh = AdventureFold.Start(adventure);
        _standings[key] = fresh;
        return fresh;
    }

    private static string StandingKey(string commander, string key) => commander + "\n" + key.ToLowerInvariant();

    private static bool SameStory(AdventureMoment moment, string commander, string key) =>
        string.Equals(moment.FrontierId, commander, StringComparison.Ordinal)
        && string.Equals(moment.Adventure.Key, key, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Which journal files a catch-up has to read: every file whose session started at or after the
    /// earliest acceptance on record, and the one before it — the session that was running when Begin
    /// was pressed.
    /// </summary>
    public static IReadOnlyList<string> FilesToWalk(string directory, DateTimeOffset? earliestAcceptance)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var files = Directory.EnumerateFiles(directory, JournalFolder.FilePattern)
            .OrderBy(System.IO.Path.GetFileName, StringComparer.Ordinal)
            .ToList();

        if (earliestAcceptance is not { } since)
        {
            return [];
        }

        // Elite's file names carry the session start as Journal.2026-08-22T190000.01.log.
        var cutoff = $"Journal.{since.ToUniversalTime():yyyy-MM-dd'T'HHmmss}";
        var first = files.FindIndex(file => string.CompareOrdinal(System.IO.Path.GetFileName(file), cutoff) >= 0);

        return first switch
        {
            < 0 => files.Count > 0 ? [files[^1]] : [],
            0 => files,
            _ => files.Skip(first - 1).ToList(),
        };
    }

    /// <summary>
    /// The earliest acceptance still under way across every Commander, for <see cref="FilesToWalk"/>.
    /// </summary>
    public DateTimeOffset? EarliestAcceptance() =>
        store.Commanders
            .SelectMany(store.For)
            .Where(adventure => adventure.IsActive)
            .Select(adventure => adventure.AcceptedAt)
            .Min();
}

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

    /// <summary>What is said, in order: the opening's one line, or the beat's lines.</summary>
    public IReadOnlyList<AdventureLine> Lines => IsOpening
        ? [new AdventureLine { Text = Adventure.Opening ?? $"{Adventure.Name} begins.", Speaker = Adventure.OpeningSpeaker }]
        : Adventure.Beats[Beat].Lines;

    /// <summary>The first line.</summary>
    public string Line => Lines.Count > 0 ? Lines[0].Text : string.Empty;

    public string Title => IsOpening ? Adventure.Name : Adventure.Beats[Beat].Title;

    /// <summary>Who says <see cref="Line"/>: a story's speaker id, or null for the ship.</summary>
    public string? Speaker => Lines.Count > 0 ? Lines[0].Speaker : null;

    /// <summary>The beat this one hands over to — the first, after the opening — or null after the last.</summary>
    public AdventureBeat? Next => Adventure.Beats.ElementAtOrDefault(Beat + 1);

    /// <summary>Where the Commander goes next, said with the line rather than waited for.</summary>
    public string? HandOff => Next?.HandOff();

    /// <summary>Line <paramref name="index"/> and the hand-off together, which is what is said when the ship speaks the last line.</summary>
    public string Spoken(int index)
    {
        var line = index < Lines.Count ? Lines[index].Text : string.Empty;
        return HandOff is { } next ? $"{line} {next}" : line;
    }

    /// <summary>The announcement key of line <paramref name="index"/>: <see cref="Key"/> for the first, its own family for the rest.</summary>
    public string LineKey(int index) => index == 0
        ? Key
        : $"{AdventureCallout.LinePrefix}{Adventure.Key}.{Beat.ToString(System.Globalization.CultureInfo.InvariantCulture)}.{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Stable per adventure and beat, so the engine's cooldown keys on the beat and not the text.
    /// </summary>
    public string Key => $"{AdventureCallout.KeyPrefix}{Adventure.Key}.{(IsOpening ? "opening" : Beat.ToString(System.Globalization.CultureInfo.InvariantCulture))}";
}

/// <summary>The Commander's adventures, read as one thing (Phase 47).</summary>
public sealed class AdventureBook(AdventureStore store, ILogger<AdventureBook> logger)
{
    private readonly Lock _gate = new();
    private readonly Queue<AdventureMoment> _moments = new();
    private AdventureFoldState _fold = new();

    /// <summary>Live events observed while a walk is owed or running, folded when it is adopted.</summary>
    private readonly List<(JournalEvent Event, string Commander)> _held = [];

    /// <summary>
    /// Which stories are owed a spoken line right now — a beat has fired and the Commander has not
    /// heard about it yet (asked for 2026-08-22).
    /// </summary>
    private readonly HashSet<string> _stirring = new(StringComparer.Ordinal);

    private bool _needsCatchUp = true;

    /// <summary>Whether live events are held rather than folded: from <see cref="StartWalk"/> until a walk is adopted.</summary>
    private bool _walking;

    /// <summary>Whether a walk is out on the pool.</summary>
    private bool _running;

    /// <summary>Moved whenever a walk in progress would produce standings that are already out of date.</summary>
    private int _generation;

    public AdventureStore Store => store;

    /// <summary>Whether the story a chapter belongs to was switched off at a moment: commander, story id, time.</summary>
    public Func<string, string, DateTimeOffset, bool> Silenced { get; set; } = (_, _, _) => false;

    /// <summary>Whether this adventure is a chapter of a story that is switched off at <paramref name="at"/>.</summary>
    public bool IsSilenced(string? frontierId, Adventure adventure, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(adventure);

        return adventure.StoryId is { } story && Silenced(frontierId ?? AdventureStore.NoCommander, story, at);
    }

    /// <summary>The standings that are not silenced at <paramref name="now"/>.</summary>
    public IReadOnlyList<AdventureStanding> Audible(string? frontierId, DateTimeOffset now) =>
        [.. Standings(frontierId).Where(standing => !IsSilenced(frontierId, standing.Adventure, now))];

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
            return [.. store.For(commander).Select(adventure => StandingOf(_fold, commander, adventure))];
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
            return StandingOf(_fold, commander, adventure);
        }
    }

    /// <summary>The chapter ending at <paramref name="key"/> with the conflicts its standing shows the Commander took part in; null when the key is not on file.</summary>
    public AdventureChapter? ChapterOf(string? frontierId, string key)
    {
        var commander = frontierId ?? AdventureStore.NoCommander;

        return AdventureChapter.Of(store.For(commander), key) is { } chapter
            ? chapter with { Conflicts = Standing(frontierId, key)?.Parts ?? [] }
            : null;
    }

    /// <summary>The adventures under way for this Commander — begun, not abandoned, not finished.</summary>
    public IReadOnlyList<AdventureStanding> Active(string? frontierId) =>
        [.. Standings(frontierId).Where(standing => standing.Adventure.IsActive && !standing.IsDone)];

    /// <summary>
    /// Walks journal files on the calling thread and adopts the result, keeping each Commander's last walked
    /// time so the priming replay does not fold the same events again. For startup, before the first tick.
    /// </summary>
    public void CatchUp(IReadOnlyList<string> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var folded = Walk(files, until: null);

        lock (_gate)
        {
            _fold = folded;
            _needsCatchUp = false;
        }
    }

    /// <summary>
    /// Starts the walk the book is owed, reading no further than <paramref name="until"/>; null when none is owed or
    /// one is already running. From here until <see cref="Adopt"/> installs a walk, live events are held rather than
    /// folded.
    /// </summary>
    public AdventureWalk? StartWalk(string directory, JournalMark? until)
    {
        ArgumentNullException.ThrowIfNull(directory);

        lock (_gate)
        {
            if (!_needsCatchUp || _running)
            {
                return null;
            }

            _needsCatchUp = false;
            _held.Clear();
            _walking = true;
            _running = true;
            return new AdventureWalk(this, _generation, directory, until);
        }
    }

    /// <summary>
    /// Installs a finished walk's standings and folds the events held while it ran. A walk overtaken by a moved stamp
    /// is discarded and the events stay held for the next one; a failed walk folds them onto the standings already
    /// held.
    /// </summary>
    public void Adopt(AdventureWalkResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        bool stirred;

        lock (_gate)
        {
            _running = false;

            if (result.Generation != _generation)
            {
                return;
            }

            if (result.Folded is { } folded)
            {
                // The held events start where the walk stopped, so none of them is behind a mark.
                folded.HighWater.Clear();
                _fold = folded;
            }

            var before = _stirring.Count;

            foreach (var (journalEvent, commander) in _held)
            {
                FoldLive(commander, journalEvent);
            }

            _held.Clear();
            _walking = false;
            stirred = _stirring.Count != before;
        }

        if (result.Error is { } error)
        {
            logger.LogWarning(error, "The adventure walk failed; the standings stay as they were");
        }

        if (stirred)
        {
            StirringChanged?.Invoke();
        }
    }

    /// <summary>
    /// Folds journal files, oldest first, into a fresh fold state, reading the file named by <paramref name="until"/>
    /// only to its position. Touches nothing else on the book.
    /// </summary>
    internal AdventureFoldState Walk(IReadOnlyList<string> files, JournalMark? until)
    {
        var state = new AdventureFoldState();
        var commander = AdventureStore.NoCommander;
        var events = 0L;

        foreach (var file in files)
        {
            var limit = until is { } mark && string.Equals(System.IO.Path.GetFileName(file), System.IO.Path.GetFileName(mark.Path), StringComparison.Ordinal)
                ? mark.Position
                : (long?)null;

            var reader = new JournalReader(file, logger, limit);

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

                    Fold(state, commander, journalEvent, announce: false);
                    state.HighWater[commander] = journalEvent.Timestamp;
                }
            }
        }

        logger.LogInformation("Caught adventures up over {Events} events in {Files} journals", events, files.Count);
        return state;
    }

    /// <summary>One live event; held rather than folded while a walk is owed or running.</summary>
    public void Observe(JournalEvent journalEvent, string? frontierId)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        var commander = frontierId ?? AdventureStore.NoCommander;
        bool stirred;

        lock (_gate)
        {
            if (_walking)
            {
                _held.Add((journalEvent, commander));
                return;
            }

            var before = _stirring.Count;
            FoldLive(commander, journalEvent);
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
        var begun = adventure with { AcceptedAt = now, AbandonedAt = null, RewrittenAt = null, RewrittenFrom = null, Previous = null, Told = [] };

        if (store.Save(commander, begun) is { } refusal)
        {
            return refusal;
        }

        lock (_gate)
        {
            _fold.Standings[StandingKey(commander, begun.Key)] = AdventureFold.Start(begun, Here(_fold, commander), Seen(_fold, commander));
            _moments.Enqueue(new AdventureMoment(commander, begun, -1, now));
            Stir(commander, begun.Key);
        }

        StirringChanged?.Invoke();
        return null;
    }

    /// <summary>
    /// Replaces the beats from <paramref name="from"/> on in an adventure that is waiting on that beat. Events before
    /// <paramref name="at"/> do not count toward the new beats. Returns a refusal or null.
    /// </summary>
    public string? ReplaceBeats(string? frontierId, string key, int from, IReadOnlyList<AdventureBeat> beats, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(beats);

        var commander = frontierId ?? AdventureStore.NoCommander;

        lock (_gate)
        {
            if (store.Find(commander, key) is not { IsActive: true } adventure
                || StandingOf(_fold, commander, adventure) is not { IsDone: false } standing
                || standing.Current != from)
            {
                return "The story has moved on from that objective.";
            }

            var rewritten = adventure with
            {
                Beats = [.. adventure.Beats.Take(from), .. beats],
                RewrittenAt = at,
                RewrittenFrom = from,
            };

            if (AdventureValidation.Problems(rewritten) is { Count: > 0 } problems)
            {
                return string.Join(" ", problems);
            }

            if (store.Save(commander, rewritten) is { } refusal)
            {
                return refusal;
            }

            _fold.Standings[StandingKey(commander, key)] = StandingOf(_fold, commander, rewritten) with { Adventure = rewritten, Counted = 0 };

            // A walk under way would bring back the count this just reset.
            if (_walking)
            {
                _generation++;
                _needsCatchUp = true;
            }

            // A line waiting out its settle window hands off to the beat that was refused.
            var queued = _moments.Select(moment => SameStory(moment, commander, key) ? moment with { Adventure = rewritten } : moment).ToList();
            _moments.Clear();

            foreach (var moment in queued)
            {
                _moments.Enqueue(moment);
            }

            return null;
        }
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
            if (_fold.Standings.TryGetValue(StandingKey(commander, key), out var standing))
            {
                _fold.Standings[StandingKey(commander, key)] = standing with { Adventure = abandoned };
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
            _fold.Standings.Remove(StandingKey(commander, key));
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

                    if (_fold.Standings.TryGetValue(key, out var standing))
                    {
                        if (standing.Adventure.AcceptedAt == adventure.AcceptedAt)
                        {
                            _fold.Standings[key] = standing with { Adventure = adventure };
                            continue;
                        }

                        _fold.Standings.Remove(key);
                    }

                    if (adventure.IsActive)
                    {
                        _generation++;
                        _needsCatchUp = true;
                    }
                }
            }

            foreach (var stale in _fold.Standings.Keys.Where(key => !present.Contains(key)).ToList())
            {
                _fold.Standings.Remove(stale);
            }
        }
    }

    /// <summary>One live event onto the book's fold state, unless the startup walk already folded it. Under the gate.</summary>
    private void FoldLive(string commander, JournalEvent journalEvent)
    {
        if (_fold.HighWater.TryGetValue(commander, out var mark) && journalEvent.Timestamp <= mark)
        {
            return;
        }

        Fold(_fold, commander, journalEvent, announce: true);
    }

    /// <summary>
    /// Folds one event into <paramref name="state"/>. Announcing also queues and stirs, under the gate; without it, it
    /// touches only <paramref name="state"/>, the store and <see cref="Silenced"/>, so a walk can run it off the tick.
    /// </summary>
    private void Fold(AdventureFoldState state, string commander, JournalEvent journalEvent, bool announce)
    {
        if (journalEvent.Kind is "FSDJump" or "Location" or "CarrierJump"
            && journalEvent.Raw.Long("SystemAddress") is { } arrived)
        {
            state.Here[commander] = arrived;
        }

        var world = state.World.GetValueOrDefault(commander, AdventureWorld.Empty).Apply(journalEvent);
        state.World[commander] = world;

        foreach (var adventure in store.For(commander))
        {
            if (!adventure.IsActive)
            {
                continue;
            }

            // A place visited while the story was off is not remembered: its beat waits for the next visit.
            if (IsSilenced(commander, adventure, journalEvent.Timestamp))
            {
                continue;
            }

            var key = StandingKey(commander, adventure.Key);
            var before = StandingOf(state, commander, adventure);
            var after = AdventureFold.Apply(before, journalEvent, world);

            if (ReferenceEquals(before, after))
            {
                continue;
            }

            state.Standings[key] = after;

            if (after.Fired.Count == before.Fired.Count)
            {
                continue;
            }

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

        state.Seen[commander] = AdventureWatch.Observe(Seen(state, commander), journalEvent).Seen;
    }

    private static AdventureStanding StandingOf(AdventureFoldState state, string commander, Adventure adventure)
    {
        var key = StandingKey(commander, adventure.Key);

        if (state.Standings.TryGetValue(key, out var standing) && standing.Adventure.AcceptedAt == adventure.AcceptedAt)
        {
            return ReferenceEquals(standing.Adventure, adventure) ? standing : standing with { Adventure = adventure };
        }

        var fresh = AdventureFold.Start(adventure, Here(state, commander), Seen(state, commander));
        state.Standings[key] = fresh;
        return fresh;
    }

    private static IReadOnlyDictionary<string, string> Seen(AdventureFoldState state, string commander) =>
        state.Seen.TryGetValue(commander, out var seen) ? seen : AdventureWatch.Nothing;

    private static long? Here(AdventureFoldState state, string commander) =>
        state.Here.TryGetValue(commander, out var address) ? address : null;

    private static string StandingKey(string commander, string key) => commander + "\n" + key.ToLowerInvariant();

    private static bool SameStory(AdventureMoment moment, string commander, string key) =>
        string.Equals(moment.FrontierId, commander, StringComparison.Ordinal)
        && string.Equals(moment.Adventure.Key, key, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Which journal files a catch-up has to read: every file whose session started at or after the
    /// earliest acceptance on record, and the one before it — the session that was running when Begin
    /// was pressed. No file sorting after <paramref name="until"/>'s is considered.
    /// </summary>
    public static IReadOnlyList<string> FilesToWalk(string directory, DateTimeOffset? earliestAcceptance, JournalMark? until = null)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var last = until is { } mark ? System.IO.Path.GetFileName(mark.Path) : null;

        var files = Directory.EnumerateFiles(directory, JournalFolder.FilePattern)
            .Where(file => last is null || string.CompareOrdinal(System.IO.Path.GetFileName(file), last) <= 0)
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

using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Adventures;

/// <summary>
/// One beat reached, one step of a beat done, or an opening spoken, waiting to be said (Phase 47, "The ship's AI tells it, and
/// the authored beat is the floor").
/// </summary>
/// <param name="Beat">The beat index, <c>-1</c> for the opening, <see cref="FailedBeat"/> or <see cref="EndingBeat"/>.</param>
/// <param name="Reason">Why a chapter failed, for a <see cref="FailedBeat"/> moment; the ending's line, for an <see cref="EndingBeat"/> one.</param>
/// <param name="Step">The step of <paramref name="Beat"/> that was done, for a moment that is a step and not the whole beat.</param>
/// <param name="Chosen">The option chosen in <paramref name="Beat"/>, which decides the step after <paramref name="Step"/>.</param>
public sealed record AdventureMoment(string FrontierId, Adventure Adventure, int Beat, DateTimeOffset At, string? Reason = null, int? Step = null, int? Chosen = null)
{
    /// <summary>The <see cref="Beat"/> of the line said when a chapter fails.</summary>
    public const int FailedBeat = -2;

    /// <summary>The <see cref="Beat"/> of the line said when a chapter ends in an option's ending.</summary>
    public const int EndingBeat = -3;

    public bool IsOpening => Beat == -1;

    public bool IsFailure => Beat == FailedBeat;

    public bool IsEnding => Beat == EndingBeat;

    /// <summary>Whether the beat this one hands over to begins with a choice, which this moment's lines offer.</summary>
    public bool OffersChoice => Next is { OpensWithChoice: true };

    /// <summary>A step that is not its beat's last: it has no lines, only the hand-off to the next step.</summary>
    public bool IsStep => Step is not null;

    /// <summary>Where this moment falls in the story: later beats and, within a beat, later steps are larger; a whole beat follows its steps.</summary>
    public long Position => (long)Beat * 1_000_000 + (Step ?? 999_999);

    /// <summary>What is said, in order: the opening's lines, or the beat's.</summary>
    public IReadOnlyList<AdventureLine> Lines => IsStep
        ? []
        : IsFailure || IsEnding
        ? [new AdventureLine { Text = Reason ?? string.Empty }]
        : IsOpening
            ? Adventure.OpeningLines.Count > 0 ? Adventure.OpeningLines : [new AdventureLine { Text = $"{Adventure.Name} begins." }]
            : Adventure.Beats[Beat].Lines;

    /// <summary>The first line.</summary>
    public string Line => Lines.Count > 0 ? Lines[0].Text : string.Empty;

    public string Title => IsOpening || IsFailure || IsEnding ? Adventure.Name : Adventure.Beats[Beat].Title;

    /// <summary>The step this one hands over to, after a step that is not its beat's last.</summary>
    public BeatStep? NextStep => IsStep ? Adventure.Beats[Beat].StepsWith(Chosen).ElementAtOrDefault(Step!.Value + 1) : null;

    /// <summary>Who says <see cref="Line"/>: a story's speaker id, or null for the ship.</summary>
    public string? Speaker => Lines.Count > 0 ? Lines[0].Speaker : null;

    /// <summary>The beat this one hands over to — the first, after the opening — or null after the last.</summary>
    public AdventureBeat? Next => IsFailure || IsEnding || IsStep ? null : Adventure.Beats.ElementAtOrDefault(Beat + 1);

    /// <summary>Where the Commander goes next, said with the line rather than waited for; after a step, the next step.</summary>
    public string? HandOff => IsStep ? NextStep is { } step ? $"Next: {step.HandOff()}" : null : Next?.HandOff();

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
    public string Key => IsStep
        ? $"{AdventureCallout.StepPrefix}{Adventure.Key}.{Beat.ToString(System.Globalization.CultureInfo.InvariantCulture)}.{Step!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
        : IsFailure
        ? $"{AdventureCallout.FailedPrefix}{Adventure.Key}"
        : IsEnding
        ? $"{AdventureCallout.EndedPrefix}{Adventure.Key}"
        : $"{AdventureCallout.KeyPrefix}{Adventure.Key}.{(IsOpening ? "opening" : Beat.ToString(System.Globalization.CultureInfo.InvariantCulture))}";
}

/// <summary>The Commander's adventures, read as one thing (Phase 47).</summary>
public sealed class AdventureBook(AdventureStore store, ILogger<AdventureBook> logger)
{
    private readonly Lock _gate = new();

    /// <summary>Held across a rewrite's read, save and fold update; never taken under <see cref="_gate"/>.</summary>
    private readonly Lock _rewriteGate = new();
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

    /// <summary>Files one exchange as an aside on each active story it mentions, returning how many it filed.</summary>
    public int FileAsides(string? frontierId, string? asked, string? answered, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(answered))
        {
            return 0;
        }

        var filed = 0;

        foreach (var standing in Active(frontierId))
        {
            if (!AdventureMention.InExchange(standing.Adventure, asked, answered))
            {
                continue;
            }

            Told(frontierId, standing.Adventure.Key, new AdventureTold
            {
                Kind = AdventureToldKind.Aside,
                Text = answered.Trim(),
                Asked = asked?.Trim(),
                At = at,
            });

            filed++;
        }

        return filed;
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

    /// <summary>Owes a fresh walk over the journal files and discards any walk already running, for a store that now reads another file.</summary>
    public void WalkAgain()
    {
        lock (_gate)
        {
            _generation++;
            _needsCatchUp = true;
        }
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

    /// <summary>The chapter ending at <paramref name="key"/> with the conflicts and notable events its standing shows; null when the key is not on file.</summary>
    public AdventureChapter? ChapterOf(string? frontierId, string key)
    {
        var commander = frontierId ?? AdventureStore.NoCommander;

        return AdventureChapter.Of(store.For(commander), key) is { } chapter
            ? chapter with { Conflicts = Standing(frontierId, key)?.Parts ?? [], Notables = Standing(frontierId, key)?.Notables ?? [] }
            : null;
    }

    /// <summary>The adventures under way for this Commander — begun, not abandoned, not finished.</summary>
    public IReadOnlyList<AdventureStanding> Active(string? frontierId) =>
        [.. Standings(frontierId).Where(standing => standing.Adventure.IsActive && !standing.IsDone && !standing.IsFailed)];

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

            var reader = new JournalReader(file, store.Files, logger, limit);

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

        foreach (var known in store.Commanders)
        {
            foreach (var adventure in store.For(known).Where(adventure => adventure.Stepped.Count > 0 || adventure.Choices.Count > 0))
            {
                var standing = StandingOf(state, known, adventure);

                if (AdventureFold.ApplySteps(standing, DateTimeOffset.MaxValue) is var stepped && !ReferenceEquals(stepped, standing))
                {
                    state.Standings[StandingKey(known, adventure.Key)] = stepped;
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
            return "There is no story by that name.";
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
        var begun = adventure with { AcceptedAt = now, AbandonedAt = null, Ending = null, RewrittenAt = null, RewrittenFrom = null, RewrittenStep = 0, Previous = null, Told = [], Stepped = [], Choices = [], Offered = null };

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

        return Rewrite(frontierId, key, from, null, adventure => adventure with
        {
            Beats = [.. adventure.Beats.Take(from), .. beats],
            RewrittenAt = at,
            RewrittenFrom = from,
            RewrittenStep = 0,
            Choices = [.. adventure.Choices.Where(choice => choice.Beat < from)],
            Offered = adventure.Offered is { } offered && offered.Beat < from ? offered : null,
        });
    }

    /// <summary>
    /// Replaces step <paramref name="step"/> of beat <paramref name="beat"/> in an adventure that is waiting on that step, and
    /// nothing else. Events before <paramref name="at"/> do not count toward it. Returns a refusal or null.
    /// </summary>
    public string? ReplaceStep(string? frontierId, string key, int beat, int step, BeatStep replacement, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        return Rewrite(frontierId, key, beat, step, adventure => adventure with
        {
            Beats = [.. adventure.Beats.Select((known, index) => index == beat
                ? known with { Steps = [.. known.Steps.Select((existing, position) => position == step ? replacement : existing)] }
                : known)],
            RewrittenAt = at,
            RewrittenFrom = beat,
            RewrittenStep = step,
        });
    }

    private string? Rewrite(string? frontierId, string key, int from, int? step, Func<Adventure, Adventure> build)
    {
        var commander = frontierId ?? AdventureStore.NoCommander;

        // Rewrites run one at a time, so each builds on the adventure the last one saved.
        lock (_rewriteGate)
        {
            Adventure rewritten;

            lock (_gate)
            {
                if (store.Find(commander, key) is not { IsActive: true } adventure
                    || StandingOf(_fold, commander, adventure) is not { IsDone: false } standing
                    || standing.Current != from
                    || (step is { } wanted && standing.CurrentStep != wanted))
                {
                    return "The story has moved on from that objective.";
                }

                if (standing.InChoice)
                {
                    return AdventureStanding.ChoiceRefusal;
                }

                rewritten = build(adventure);
            }

            if (AdventureValidation.Problems(rewritten) is { Count: > 0 } problems)
            {
                return string.Join(" ", problems);
            }

            // Outside the gate: the save writes the file and runs the store's change handlers.
            if (store.Save(commander, rewritten) is { } refusal)
            {
                return refusal;
            }

            lock (_gate)
            {
                var before = StandingOf(_fold, commander, rewritten);

                _fold.Standings[StandingKey(commander, key)] = before with
                {
                    Adventure = rewritten,
                    Counted = 0,
                    Chosen = before.Chosen.Where(pair => pair.Key < from).ToDictionary(pair => pair.Key, pair => pair.Value),
                };

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
            }

            return null;
        }
    }

    /// <summary>
    /// Fires the step an active story is waiting on without its journal event, recording it so a walk fires it again.
    /// <paramref name="by"/> is the source the step records, the bench when null. Returns a refusal or null.
    /// </summary>
    internal string? Step(string? frontierId, string key, DateTimeOffset now, string? by = null)
    {
        var commander = frontierId ?? AdventureStore.NoCommander;

        if (store.Find(commander, key) is not { } adventure)
        {
            return "There is no story by that name.";
        }

        if (!adventure.IsBegun)
        {
            return $"{adventure.Name} has not begun.";
        }

        if (adventure.IsAbandoned)
        {
            return $"{adventure.Name} was abandoned.";
        }

        AdventureStanding standing;

        lock (_gate)
        {
            standing = StandingOf(_fold, commander, adventure);

            if (standing.IsDone || standing.CurrentBeat is null)
            {
                return $"{adventure.Name} is finished.";
            }

            if (_stirring.Contains(StandingKey(commander, key)))
            {
                return $"{adventure.Name} is still being told.";
            }

            if (standing.CurrentObjective is { IsChoice: true } choice)
            {
                return $"{adventure.Name} is waiting on a choice. {choice.HandOff()}";
            }
        }

        var step = new AdventureStep(standing.Current, now, by, standing.CurrentStep);
        var stepped = adventure with { Stepped = [.. adventure.Stepped.Where(known => known.Beat != step.Beat || known.Step != step.Step), step] };

        if (AdventureValidation.Problems(stepped) is { Count: > 0 } problems)
        {
            return string.Join(" ", problems);
        }

        if (store.Save(commander, stepped) is { } refusal)
        {
            return refusal;
        }

        stepped = store.Find(commander, key) ?? stepped;

        lock (_gate)
        {
            var current = StandingOf(_fold, commander, adventure);

            if (current.IsDone || current.Current != step.Beat || current.CurrentStep != step.Step)
            {
                return $"{adventure.Name} has moved on from that objective.";
            }

            var after = AdventureFold.Stepped(current, now, by) with { Adventure = stepped };

            _fold.Standings[StandingKey(commander, key)] = after;
            Announce(commander, current, after, now);

            if (_walking)
            {
                _generation++;
                _needsCatchUp = true;
            }
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
            return "There is no story by that name.";
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
            DropMoments(commander, key);

            // And so does the animation that was waiting with it.
            _stirring.Remove(StandingKey(commander, key));
        }

        StirringChanged?.Invoke();
        return null;
    }

    /// <summary>
    /// Records that the offer of the choice beginning beat <paramref name="beat"/> was said at <paramref name="at"/>, when the story is
    /// waiting on that choice; from then the choice can be made.
    /// </summary>
    public void Offered(string? frontierId, string key, int beat, DateTimeOffset at)
    {
        var commander = frontierId ?? AdventureStore.NoCommander;

        if (store.Find(commander, key) is not { IsActive: true } adventure)
        {
            return;
        }

        lock (_gate)
        {
            if (StandingOf(_fold, commander, adventure) is not { InChoice: true, CurrentStep: 0 } standing || standing.Current != beat)
            {
                return;
            }
        }

        if (store.Save(commander, adventure with { Offered = new AdventureOffer(beat, at) }) is { } refusal)
        {
            logger.LogWarning("Could not record that the choice in {Name} was offered: {Refusal}", adventure.Name, refusal);
        }
    }

    /// <summary>
    /// Makes the d47 event <see cref="AdventureChoice.EventName"/>: the Commander chose <paramref name="option"/>, from zero, of the open
    /// choice in story <paramref name="key"/>. Stores it so a walk applies it again in order with the journal.
    /// </summary>
    public AdventureChoiceOutcome Choose(string? frontierId, string key, int option, DateTimeOffset now)
    {
        var commander = frontierId ?? AdventureStore.NoCommander;

        if (store.Find(commander, key) is not { IsActive: true } adventure)
        {
            return AdventureChoiceOutcome.Refused(AdventureChoiceOutcome.NoChoice);
        }

        BeatStep choice;
        int beat;

        lock (_gate)
        {
            if (StandingOf(_fold, commander, adventure) is not { OpenChoice: { } open } standing)
            {
                return AdventureChoiceOutcome.Refused(AdventureChoiceOutcome.NoChoice);
            }

            choice = open;
            beat = standing.Current;
        }

        if (option < 0 || option >= choice.Options.Count)
        {
            return AdventureChoiceOutcome.Refused($"There is no option {(option + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)}. {choice.HandOff()}");
        }

        var made = new AdventureChoice(beat, option, now);
        var chosen = adventure with { Choices = [.. adventure.Choices.Where(known => known.Beat != beat), made] };

        if (store.Save(commander, chosen) is { } refusal)
        {
            return AdventureChoiceOutcome.Refused(refusal);
        }

        chosen = store.Find(commander, key) ?? chosen;
        BeatStep? next = null;

        lock (_gate)
        {
            var current = StandingOf(_fold, commander, adventure);

            if (current.OpenChoice is null || current.Current != beat)
            {
                return AdventureChoiceOutcome.Refused($"{adventure.Name} has moved on from that choice.");
            }

            var after = AdventureFold.Chose(current, option, now) with { Adventure = chosen };
            _fold.Standings[StandingKey(commander, key)] = after;

            // The next step's hand-off is the reply; a beat or an ending the choice itself reaches is said as usual.
            if (after.Fired.Count != current.Fired.Count || after.Reached is not null)
            {
                Announce(commander, current, after, now);
            }
            else
            {
                next = after.CurrentObjective;
                Logged(current, after, now);
            }

            if (_walking)
            {
                _generation++;
                _needsCatchUp = true;
            }
        }

        logger.LogInformation(
            "d47 event {Event}: {Story} beat {Beat} option {Option} ({Label}) at {At}",
            AdventureChoice.EventName,
            key,
            beat + 1,
            option + 1,
            choice.Options[option].Label,
            now);

        StirringChanged?.Invoke();

        var reply = $"You chose to {choice.Options[option].Label.Trim()}.";

        return new AdventureChoiceOutcome(null, next is null ? reply : $"{reply} {next.HandOff()}");
    }

    /// <summary>
    /// Stores the ending of a chapter whose standing a <c>failsOn</c> trigger failed, or whose chosen option reached its own ending. Saves
    /// outside the gate, then updates the standing. Returns a refusal or null.
    /// </summary>
    public string? StoreEnding(string? frontierId, string key)
    {
        var commander = frontierId ?? AdventureStore.NoCommander;

        if (store.Find(commander, key) is not { } adventure)
        {
            return "There is no story by that name.";
        }

        if (!adventure.IsActive)
        {
            return $"{adventure.Name} is not under way.";
        }

        AdventureEnding ending;

        lock (_gate)
        {
            var standing = StandingOf(_fold, commander, adventure);

            if (standing.Failure is { } failure)
            {
                ending = new AdventureEnding(AdventureEnding.Failed, failure.At, failure.Reason);
            }
            else if (standing.Reached is { } reached)
            {
                ending = reached;
            }
            else
            {
                return $"{adventure.Name} has not ended.";
            }
        }

        var ended = adventure with { Ending = ending };

        if (store.Save(commander, ended) is { } refusal)
        {
            return refusal;
        }

        lock (_gate)
        {
            if (_fold.Standings.TryGetValue(StandingKey(commander, key), out var standing))
            {
                _fold.Standings[StandingKey(commander, key)] = standing with { Adventure = store.Find(commander, key) ?? ended };
            }
        }

        return null;
    }

    /// <summary>The standings of this Commander that failed or reached an option's ending and whose ending is not stored yet.</summary>
    public IReadOnlyList<AdventureStanding> Unstored(string? frontierId) =>
        [.. Standings(frontierId).Where(standing => (standing.Failure is not null || standing.Reached is not null) && standing.Adventure is { IsActive: true })];

    /// <summary>I do not want this record.</summary>
    public bool Remove(string? frontierId, string key)
    {
        var commander = frontierId ?? AdventureStore.NoCommander;
        var removed = store.Remove(commander, key);

        lock (_gate)
        {
            _fold.Standings.Remove(StandingKey(commander, key));

            var kept = _moments.Where(moment => !SameStory(moment, commander, key)).ToList();
            _moments.Clear();

            foreach (var moment in kept)
            {
                _moments.Enqueue(moment);
            }

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

                    // A stamp after the newest folded event has nothing folded behind it, so a fresh standing is exact.
                    var aheadOfTheFold = !_walking
                        && _fold.Newest is { } newest
                        && adventure.AcceptedAt is { } stamp
                        && stamp > newest;

                    if (adventure.IsActive && !aheadOfTheFold)
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
        if (state.Newest is not { } newest || journalEvent.Timestamp > newest)
        {
            state.Newest = journalEvent.Timestamp;
        }

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
            var original = StandingOf(state, commander, adventure);
            var before = AdventureFold.ApplySteps(original, journalEvent.Timestamp);
            var after = AdventureFold.Apply(before, journalEvent, world);

            if (ReferenceEquals(original, after))
            {
                continue;
            }

            if (after.Failure is { } failure && before.Failure is null)
            {
                after = after with { Failure = failure with { Live = announce } };
            }

            state.Standings[key] = after;

            if (after.Failure is { Live: true } && before.Failure is null)
            {
                DropMoments(commander, adventure.Key);
                _moments.Enqueue(new AdventureMoment(commander, adventure, AdventureMoment.FailedBeat, journalEvent.Timestamp, after.Failure.Reason));
                _stirring.Remove(StandingKey(commander, adventure.Key));
                logger.LogInformation("Adventure {Name} failed at beat {Beat}: {Reason}", adventure.Name, after.Current + 1, after.Failure.Reason);
                continue;
            }

            if (announce)
            {
                Announce(commander, before, after, journalEvent.Timestamp);
            }
            else
            {
                Logged(before, after, journalEvent.Timestamp);
            }
        }

        state.Seen[commander] = AdventureWatch.Observe(Seen(state, commander), journalEvent).Seen;
    }

    /// <summary>Queues and logs what <paramref name="after"/> reached beyond <paramref name="before"/>: an option's ending, a beat, or a step's hand-off. Under the gate.</summary>
    private void Announce(string commander, AdventureStanding before, AdventureStanding after, DateTimeOffset at)
    {
        var adventure = after.Adventure;

        if (after.Reached is not null && before.Reached is null)
        {
            DropMoments(commander, adventure.Key);
            _moments.Enqueue(new AdventureMoment(commander, adventure, AdventureMoment.EndingBeat, at, EndingLine(after)));
            _stirring.Remove(StandingKey(commander, adventure.Key));
        }
        else if (after.StepFires.Count == before.StepFires.Count)
        {
            return;
        }
        else if (after.Fired.Count == before.Fired.Count)
        {
            // Nothing records a step hand-off as told, so it does not hold the story as being told.
            _moments.Enqueue(new AdventureMoment(
                commander, adventure, before.Current, at, Step: after.StepFires[^1].Step, Chosen: after.Chosen.TryGetValue(before.Current, out var chosen) ? chosen : null));
        }
        else
        {
            _moments.Enqueue(new AdventureMoment(commander, adventure, after.Fired.Count - 1, at));

            // From here until the line is said or dropped, the tab has something to animate.
            Stir(commander, adventure.Key);
        }

        Logged(before, after, at);
    }

    private void Logged(AdventureStanding before, AdventureStanding after, DateTimeOffset at)
    {
        var adventure = after.Adventure;

        if (after.Reached is { } reached && before.Reached is null)
        {
            logger.LogInformation("Adventure {Name} ended in {Ending} at beat {Beat}", adventure.Name, reached.Name, after.Current + 1);
        }
        else if (after.StepFires.Count == before.StepFires.Count)
        {
            return;
        }
        else if (after.Fired.Count == before.Fired.Count)
        {
            logger.LogInformation("Adventure {Name} did step {Step} of beat {Beat}", adventure.Name, after.StepFires[^1].Step + 1, before.Current + 1);
        }
        else
        {
            logger.LogInformation(
                "Adventure {Name} reached beat {Beat} ({Title}) at {When}",
                adventure.Name,
                after.Fired.Count,
                adventure.Beats[after.Fired.Count - 1].Title,
                at);
        }
    }

    /// <summary>The line of the option whose ending <paramref name="standing"/> reached.</summary>
    private static string? EndingLine(AdventureStanding standing) =>
        standing.Adventure.Beats.ElementAtOrDefault(standing.Current) is { OpensWithChoice: true } beat && standing.Chosen.TryGetValue(standing.Current, out var chosen)
            ? beat.Steps[0].Options.ElementAtOrDefault(chosen)?.EndingLine
            : null;

    /// <summary>Drops the lines of a story that are waiting out their settle window. Under the gate.</summary>
    private void DropMoments(string commander, string key)
    {
        var kept = _moments.Where(moment => !SameStory(moment, commander, key)).ToList();
        _moments.Clear();

        foreach (var moment in kept)
        {
            _moments.Enqueue(moment);
        }
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
    /// was pressed. No file sorting after <paramref name="until"/>'s is considered. File names are read in
    /// <paramref name="zone"/>, the local zone when null.
    /// </summary>
    public static IReadOnlyList<string> FilesToWalk(IFileSystem files, string directory, DateTimeOffset? earliestAcceptance, JournalMark? until = null, TimeZoneInfo? zone = null)
    {
        var last = until is { } mark ? System.IO.Path.GetFileName(mark.Path) : null;

        var names = files.Enumerate(directory, JournalFolder.FilePattern)
            .Where(file => last is null || string.CompareOrdinal(System.IO.Path.GetFileName(file), last) <= 0)
            .OrderBy(System.IO.Path.GetFileName, StringComparer.Ordinal)
            .ToList();

        if (earliestAcceptance is not { } since)
        {
            return [];
        }

        // Elite's file names carry the session start in local time as Journal.2026-08-22T190000.01.log.
        var cutoff = $"Journal.{TimeZoneInfo.ConvertTime(since, zone ?? TimeZoneInfo.Local):yyyy-MM-dd'T'HHmmss}";
        var first = names.FindIndex(file => string.CompareOrdinal(System.IO.Path.GetFileName(file), cutoff) >= 0);

        return first switch
        {
            < 0 => names.Count > 0 ? [names[^1]] : [],
            0 => names,
            _ => names.Skip(first - 1).ToList(),
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

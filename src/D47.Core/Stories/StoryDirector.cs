using D47.Core.Adventures;
using D47.Core.Audio;
using D47.Core.Journal;
using D47.Core.Persona;
using Microsoft.Extensions.Logging;

namespace D47.Core.Stories;

/// <summary>A story picked with a narrated beacon scan, waiting for the tick to say its scan line and wake the cores. <see cref="Line"/> is null when the entry has none.</summary>
public sealed record StoryScanDue(string StoryId, string Title, StoryLine? Line);

/// <summary>
/// Runs a stock story as a chain of adventures: Pick writes and begins chapter one, and a finished chapter has
/// the next written and begun.
/// </summary>
public sealed class StoryDirector(
    StoryStore stories,
    AdventureBook book,
    Func<StoryCatalog> catalog,
    Func<AdventureAsk, DateTimeOffset, CancellationToken, Task<AdventureOutcome>> write,
    Func<StarPosition?> here,
    Action<string> setBackstory,
    ILogger logger)
{
    private readonly Lock _gate = new();

    /// <summary>Commanders with a chapter being written.</summary>
    private readonly HashSet<string> _writing = new(StringComparer.Ordinal);

    /// <summary>Commanders whose last chapter could not be written; the tick does not retry until WriteNextAsync is called.</summary>
    private readonly HashSet<string> _failed = new(StringComparer.Ordinal);

    /// <summary>Commanders with a replacement beat being written.</summary>
    private readonly HashSet<string> _rewriting = new(StringComparer.Ordinal);

    /// <summary>Narrated scans picked and not yet taken by the tick, by Commander.</summary>
    private readonly Dictionary<string, StoryScanDue> _scans = new(StringComparer.Ordinal);

    private JournalLocation _where = JournalLocation.Unknown;

    private bool? _odyssey;

    /// <summary>Why stories are off when the game runs without Odyssey: shown on the Stories page and returned as the refusal.</summary>
    public const string NeedsOdyssey =
        "Stories need Elite Dangerous: Odyssey. Most chapters use it: on-foot missions, settlements, exobiology and suits. "
        + "Your last session ran without it, so stories are off. Odyssey is a low-cost expansion from the Frontier Store, "
        + "Steam or Epic, and stories start working at the next session after it is installed.";

    /// <summary>Why a story with a member in two versions cannot be picked yet.</summary>
    public const string NeedsGender = "Choose whether your Commander is a man or a woman first. This story has a character written for each.";

    public StoryStore Stories => stories;

    /// <summary>The Commander's gender as settings hold it: <see cref="CommanderGender.Man"/>, <see cref="CommanderGender.Woman"/> or null.</summary>
    public Func<string?> Gender { get; set; } = () => null;

    /// <summary>Stores the Commander's gender; a running story uses it from its next line.</summary>
    public Action<string> SetGender { get; set; } = _ => { };

    /// <summary>Whether the story has a cast member in two versions, so the Commander's gender must be set to pick it.</summary>
    public bool NeedsGenderFor(string id) => catalog().Secret(id)?.Cast.Any(speaker => speaker.Versions is not null) == true;

    /// <summary>A cast member of the current story as this Commander meets them, or null.</summary>
    public StorySpeakerShown? Speaker(string? frontierId, string castId) =>
        stories.Current(frontierId) is { } story ? catalog().Secret(story.Id)?.Speaker(castId, Gender()) : null;

    public StoryCatalog Catalog => catalog();

    /// <summary>The Commander's game state, for whether the ship they are in can reach the beacon.</summary>
    public Func<CommanderGameState?> Game { get; set; } = () => null;

    /// <summary>Raised as a chapter starts and stops being written, on the thread doing it.</summary>
    public event Action? WritingChanged;

    /// <summary>Whether a chapter is being written for this Commander.</summary>
    public bool IsWriting(string? frontierId)
    {
        lock (_gate)
        {
            return _writing.Contains(frontierId ?? AdventureStore.NoCommander);
        }
    }

    /// <summary>The time a replacement beat takes effect: after it is written, so events during the write do not count toward it.</summary>
    public Func<DateTimeOffset> Clock { get; set; } = () => DateTimeOffset.UtcNow;

    /// <summary>Whether a replacement beat is being written for this Commander.</summary>
    public bool IsRewriting(string? frontierId)
    {
        lock (_gate)
        {
            return _rewriting.Contains(frontierId ?? AdventureStore.NoCommander);
        }
    }

    /// <summary>The beat of the running story's chapter that <see cref="RefuseBeatAsync"/> would replace, or null.</summary>
    public AdventureBeat? RefusableBeat(string? frontierId) =>
        stories.Current(frontierId) is { State: StoryState.Running, CurrentChapter: { } key }
        && book.Standing(frontierId, key) is { IsRefusable: true } standing
            ? standing.CurrentBeat
            : null;

    /// <summary>Raised when a LoadGame changes whether the game runs with Odyssey.</summary>
    public event Action? OdysseyChanged;

    /// <summary>
    /// Whether stories are off for want of Odyssey: the last LoadGame seen ran without it, or the current story's
    /// last one did. False before any LoadGame is seen.
    /// </summary>
    public bool WithoutOdyssey(string? frontierId)
    {
        lock (_gate)
        {
            if (_odyssey == false)
            {
                return true;
            }
        }

        return stories.Current(frontierId) is { IsWithoutOdyssey: true };
    }

    /// <summary>Whether the last chapter could not be written and waits for <see cref="WriteNextAsync"/>.</summary>
    public bool WriteFailed(string? frontierId)
    {
        lock (_gate)
        {
            return _failed.Contains(frontierId ?? AdventureStore.NoCommander);
        }
    }

    /// <summary>Starts a story when none is current: sets the Backstory and writes chapter one. Returns a refusal or null.</summary>
    public Task<string?> PickAsync(string? frontierId, string id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (catalog().Find(id) is not { } card)
        {
            return Task.FromResult<string?>("There is no story by that name.");
        }

        if (WithoutOdyssey(frontierId))
        {
            return Task.FromResult<string?>(NeedsOdyssey);
        }

        if (NeedsGenderFor(card.Id) && !CommanderGender.IsSet(Gender()))
        {
            return Task.FromResult<string?>(NeedsGender);
        }

        if (stories.Current(frontierId) is { } current)
        {
            return Task.FromResult<string?>(string.Equals(current.Id, card.Id, StringComparison.OrdinalIgnoreCase)
                ? $"{current.Title} is already your story."
                : $"{current.Title} is your story. Switch to change it.");
        }

        setBackstory(card.InYourWords);

        var narrated = card.Pacing.NarratedScan;
        var story = new Story
        {
            Id = card.Id,
            Title = card.Title,
            PublicLayer = card.Describe(),
            Length = card.Pacing.Key,
            PickedAt = now,
            BeaconScanAt = narrated ? now : null,
            BeaconNarrated = narrated,
        };

        stories.Save(frontierId, story);
        logger.LogInformation("Picked the story {Title}", card.Title);

        if (narrated)
        {
            lock (_gate)
            {
                _scans[frontierId ?? AdventureStore.NoCommander] = new StoryScanDue(card.Id, card.Title, Hidden(card.Id)?.Scan);
            }
        }

        return WriteChapterAsync(frontierId, now, cancellationToken);
    }

    /// <summary>Abandons the current story, keeping its chapters on file, and picks another.</summary>
    public Task<string?> SwitchAsync(string? frontierId, string id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (WithoutOdyssey(frontierId))
        {
            return Task.FromResult<string?>(NeedsOdyssey);
        }

        if (NeedsGenderFor(id) && !CommanderGender.IsSet(Gender()))
        {
            return Task.FromResult<string?>(NeedsGender);
        }

        if (stories.Current(frontierId) is { } current)
        {
            if (string.Equals(current.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<string?>($"{current.Title} is already your story.");
            }

            Stop(frontierId, current, StoryState.Abandoned, now);
        }

        return PickAsync(frontierId, id, now, cancellationToken);
    }

    /// <summary>Ends the current story. Its chapters stay on file.</summary>
    public string? Abandon(string? frontierId, DateTimeOffset now)
    {
        if (stories.Current(frontierId) is not { } current)
        {
            return "No story is running.";
        }

        Stop(frontierId, current, StoryState.Ended, now);
        return null;
    }

    /// <summary>Begins a paused story's chapter again, or writes it again when it is no longer on file.</summary>
    public async Task<string?> ResumeAsync(string? frontierId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (stories.Current(frontierId) is not { State: StoryState.Paused } paused)
        {
            return "No story is paused.";
        }

        if (paused.CurrentChapter is { } key && book.Store.Find(frontierId, key) is not null)
        {
            if (book.Begin(frontierId, key, now) is { } refusal)
            {
                return refusal;
            }

            stories.Save(frontierId, paused.Resumed(now));
            return null;
        }

        stories.Save(frontierId, paused.Resumed(now) with { Chapters = [.. paused.Chapters.SkipLast(1)] });
        return await WriteChapterAsync(frontierId, now, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes the chapter the running story is owed, after a failure.</summary>
    public Task<string?> WriteNextAsync(string? frontierId, DateTimeOffset now, CancellationToken cancellationToken) =>
        stories.Current(frontierId) is { State: StoryState.Running }
            ? WriteChapterAsync(frontierId, now, cancellationToken)
            : Task.FromResult<string?>("No story is running.");

    /// <summary>
    /// Replaces the beat the running story's chapter is waiting on, and every beat after it, with new ones, and remembers
    /// the activity as refused for the rest of the story. Returns a refusal or null; on a refusal the chapter is as it was.
    /// </summary>
    public async Task<string?> RefuseBeatAsync(string? frontierId, CancellationToken cancellationToken)
    {
        var commander = frontierId ?? AdventureStore.NoCommander;

        if (stories.Current(frontierId) is not { } story)
        {
            return "No story is running.";
        }

        if (story.State != StoryState.Running)
        {
            return "Resume the story first.";
        }

        if (WithoutOdyssey(frontierId))
        {
            return NeedsOdyssey;
        }

        if (story.CurrentChapter is not { } key || book.Standing(frontierId, key) is not { Adventure.IsActive: true, IsDone: false } standing
            || standing.CurrentBeat is not { } beat)
        {
            return "The story is not waiting on a beat.";
        }

        if (!standing.IsRefusable)
        {
            return "The Guardian beacon scan ends act one, so that beat cannot be swapped.";
        }

        if (IsWriting(frontierId))
        {
            return "A chapter is being written.";
        }

        lock (_gate)
        {
            if (!_rewriting.Add(commander))
            {
                return "A different beat is already being written.";
            }
        }

        WritingChanged?.Invoke();

        try
        {
            return await RefuseAsync(frontierId, story, standing, beat, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Writing a replacement beat failed");
            return "The beat could not be written. Try again in a moment.";
        }
        finally
        {
            lock (_gate)
            {
                _rewriting.Remove(commander);
            }

            WritingChanged?.Invoke();
        }
    }

    private async Task<string?> RefuseAsync(string? frontierId, Story story, AdventureStanding standing, AdventureBeat beat, CancellationToken cancellationToken)
    {
        if (Hidden(story.Id) is not { } secret)
        {
            return $"The hidden layer of {story.Title} is missing from this build.";
        }

        var chapter = standing.Adventure;
        var from = standing.Current;
        var refusedKey = RefusedActivities.Key(beat.Trigger.Kind, beat.Trigger.MissionFamily);
        var refused = refusedKey is null || story.Refused.Contains(refusedKey) ? story.Refused : [.. story.Refused, refusedKey];

        // The Story as it stood when this chapter was written: before the chapter itself was counted.
        var before = story with { Chapters = [.. story.Chapters.SkipLast(1)] };
        var number = story.Chapters.Count;
        var finaleChapter = story.FinaleChapter;
        var stage = finaleChapter is null ? StoryClues.Stage(before) : StoryStage.Finale;
        var beaconBeat = chapter.Beats[^1].Trigger is { Kind: TriggerKind.Beacon, SystemAddress: { } address } trigger
            ? new AdventureBeacon(address, trigger.System ?? GuardianCores.Beacons.GetValueOrDefault(address) ?? string.Empty)
            : null;
        var reach = story.BeaconScanAt is null && beaconBeat is null
            ? BeaconReach.Of(here(), Game()?.Ship ?? ShipLoadout.Unknown, Game()?.Carrier.Owned == true)
            : null;
        var game = Game();
        var longHaul = ChapterFit.IsLongHaul(story.Pacing, game);
        var comfort = ChapterFit.IsComfortChapter(SinceBeacon(frontierId, story))
            ? ChapterFit.LeastDone(game?.Statistics ?? CareerStatistics.Empty, refused)
            : null;
        var now = Clock();

        var ask = new AdventureAsk(
            longHaul ? AdventureReach.Anywhere : AdventureReach.Session,
            AdventureLength.Evening,
            Chapter: chapter.Follows is { } follows ? book.ChapterOf(frontierId, follows) : null,
            Story: Asked(
                story with { Refused = refused },
                secret,
                number,
                now,
                stage,
                finaleChapter,
                beaconBeat,
                reach is { InReach: false, Why: { } why } ? new AdventureBeaconAway(reach.System, reach.LightYears, why) : null,
                reach?.InReach != false,
                longHaul,
                comfort,
                finaleChapter is null ? null : story.FinaleDestination),
            Rewrite: new AdventureRewrite(chapter, from));

        var outcome = await write(ask, now, cancellationToken).ConfigureAwait(false);

        if (outcome.Draft is not { } draft)
        {
            return outcome.Refusal ?? "Nothing came back.";
        }

        if (stories.Current(frontierId) is not { State: StoryState.Running } still
            || still.PickedAt != story.PickedAt
            || still.Chapters.Count != story.Chapters.Count)
        {
            return "The story changed while the beat was being written.";
        }

        if (book.ReplaceBeats(frontierId, chapter.Key, from, [.. draft.Beats.Skip(from)], Clock()) is { } refusal)
        {
            return refusal;
        }

        stories.Update(frontierId, story.Id, current =>
            refusedKey is null || current.Refused.Contains(refusedKey) ? current : current with { Refused = [.. current.Refused, refusedKey] });

        logger.LogInformation("{Title}: beat {Beat} of {Chapter} was refused and replaced", story.Title, from + 1, chapter.Name);
        return null;
    }

    /// <summary>
    /// Pauses a story whose chapter was abandoned or removed, and starts the next chapter on the pool when one
    /// finishes. Returns the write it started, or null. Does not block.
    /// </summary>
    public Task<string?>? Tick(string? frontierId, DateTimeOffset now)
    {
        if (stories.Current(frontierId) is not { } story || story.CurrentChapter is not { } key)
        {
            return null;
        }

        var standing = book.Standing(frontierId, key);

        if (story.State == StoryState.Running && standing is null or { Adventure.IsAbandoned: true })
        {
            stories.Save(frontierId, story.Paused(now));
            return null;
        }

        if (story.State == StoryState.Paused && standing is { Adventure.IsActive: true })
        {
            stories.Save(frontierId, story.Resumed(now));
            return null;
        }

        if (story.State != StoryState.Running || story.IsWithoutOdyssey || standing is not { IsDone: true } || WriteFailed(frontierId) || IsWriting(frontierId))
        {
            return null;
        }

        if (story.FinaleChapter >= story.Pacing.FinaleChapters)
        {
            if (StoryClues.AtTheEnd(story))
            {
                stories.Save(frontierId, story with { State = StoryState.Finished, StoppedAt = now });
                logger.LogInformation("{Title} is finished", story.Title);
            }

            return null;
        }

        return Task.Run(() => WriteChapterAsync(frontierId, now, CancellationToken.None));
    }

    /// <summary>
    /// Counts the current story's play sessions, records the stretches the game runs without Odyssey, and records
    /// each Guardian beacon system the Commander data-links while it is current. Returns what a scan woke when it
    /// lifted the story's hold on the cores.
    /// </summary>
    public CoreWaking? Observe(JournalEvent journalEvent, string? frontierId)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        _where = _where.Apply(journalEvent);

        bool? odyssey = null;

        if (journalEvent.Kind == "LoadGame")
        {
            odyssey = SessionSummary.OdysseyOf(journalEvent);
            bool changed;

            lock (_gate)
            {
                changed = _odyssey != odyssey;
                _odyssey = odyssey;
            }

            if (changed)
            {
                OdysseyChanged?.Invoke();
            }
        }

        if (journalEvent.Kind == "LoadGame"
            && stories.Current(frontierId) is { } playing
            && journalEvent.Timestamp >= playing.PickedAt)
        {
            stories.Update(frontierId, playing.Id, story =>
            {
                if (story.LastSessionAt is { } last && journalEvent.Timestamp <= last)
                {
                    return story;
                }

                var counted = story with { Sessions = story.Sessions + 1, LastSessionAt = journalEvent.Timestamp };
                return odyssey is { } flag ? counted.Loaded(flag, journalEvent.Timestamp) : counted;
            });
        }

        if (!GuardianCores.IsBeaconScan(journalEvent, _where.SystemAddress)
            || _where.SystemAddress is not { } address
            || stories.Current(frontierId) is not { } current
            || journalEvent.Timestamp < current.PickedAt
            || current.BeaconSystems.Contains(address))
        {
            return null;
        }

        CoreWaking? woke = null;

        stories.Update(frontierId, current.Id, story =>
        {
            if (story.BeaconSystems.Contains(address))
            {
                return story;
            }

            var held = story.HeldCores;
            var scanned = story with
            {
                BeaconScanAt = story.BeaconScanAt ?? journalEvent.Timestamp,
                BeaconSystems = [.. story.BeaconSystems, address],
            };

            woke = (held, scanned.HeldCores) switch
            {
                (HeldCores.All, not HeldCores.All) => CoreWaking.Cores,
                (HeldCores.Heretic, HeldCores.None) => CoreWaking.Heretic,
                _ => null,
            };

            return scanned;
        });

        logger.LogInformation("{Title}: the beacon in {System} was scanned", current.Title, GuardianCores.Beacons[address]);
        return woke;
    }

    /// <summary>
    /// The narrated scan of the story just picked, once, while it is still the running story; the caller says the line
    /// and then wakes the cores. Null when none waits.
    /// </summary>
    public StoryScanDue? TakeNarratedScan(string? frontierId)
    {
        StoryScanDue? due;

        lock (_gate)
        {
            if (!_scans.Remove(frontierId ?? AdventureStore.NoCommander, out due))
            {
                return null;
            }
        }

        return stories.Current(frontierId) is { State: StoryState.Running, BeaconNarrated: true } story
               && string.Equals(story.Id, due.StoryId, StringComparison.OrdinalIgnoreCase)
            ? due
            : null;
    }

    /// <summary>The Guardian core the current story is written for, or null when no story is current.</summary>
    public Persona.Persona? CoreOf(string? frontierId) =>
        stories.Current(frontierId) is { } current && catalog().Find(current.Id) is { } card
            ? PersonaCatalog.Resolve(card.Core)
            : null;

    /// <summary>Whether the Commander has the current story switched off.</summary>
    public bool IsOff(string? frontierId) => stories.Current(frontierId) is { IsOff: true };

    /// <summary>Switches the current story on or off. Returns a refusal or null.</summary>
    public string? SetOn(string? frontierId, bool on, DateTimeOffset now)
    {
        if (stories.Current(frontierId) is not { } current)
        {
            return "No story is running.";
        }

        stories.Update(frontierId, current.Id, story => on ? story.SwitchedOn(now) : story.SwitchedOff(now));
        logger.LogInformation("{Title} is switched {State}", current.Title, on ? "on" : "off");
        return null;
    }

    /// <summary>The current story's hidden layer as every speaker reads it, or null when no story is current or it is switched off.</summary>
    public string? HiddenBrief(string? frontierId) =>
        stories.Current(frontierId) is { IsOff: false } story && Hidden(story.Id) is { } secret
            ? StoryClues.Brief(story, secret)
            : null;

    /// <summary>The hidden layer as this speaker reads it, or null; a stock core's own voice never reads it.</summary>
    public string? HiddenBrief(string? frontierId, VoiceRole speaker, Persona.Persona core) =>
        StoryClues.ReadsHiddenStory(speaker, core) ? HiddenBrief(frontierId) : null;

    /// <summary>Whether the current story is running and switched on.</summary>
    public bool IsRunning(string? frontierId) => stories.Current(frontierId) is { State: StoryState.Running, IsOff: false };

    /// <summary>The clue the running story owes now, or null.</summary>
    public StoryClueDue? ClueDue(string? frontierId, DateTimeOffset now) =>
        stories.Current(frontierId) is { IsOff: false, IsWithoutOdyssey: false } story && catalog().Secret(story.Id) is not null
            ? StoryClues.Due(story, now)
            : null;

    /// <summary>The text of a clue that is still the next one owed, or null.</summary>
    public (string Title, string Clue)? Clue(string? frontierId, StoryClueDue due)
    {
        ArgumentNullException.ThrowIfNull(due);

        return stories.Current(frontierId) is { State: StoryState.Running, IsOff: false, IsWithoutOdyssey: false } story
               && string.Equals(story.Id, due.StoryId, StringComparison.OrdinalIgnoreCase)
               && story.CluesGiven == due.Index
               && Hidden(story.Id) is { } secret
               && StoryClues.Text(secret, story.Pacing, due.Index) is { Length: > 0 } clue
            ? (story.Title, clue)
            : null;
    }

    /// <summary>Records that a clue was spoken, so the next waits for its day, a new session and a finished chapter.</summary>
    public void ClueGiven(string? frontierId, StoryClueDue due)
    {
        ArgumentNullException.ThrowIfNull(due);

        stories.Update(frontierId, due.StoryId, story => story.IsCurrent && story.CluesGiven == due.Index
            ? story with { CluesGiven = due.Index + 1, ClueSession = story.Sessions, ClueChapter = story.Chapters.Count }
            : story);
    }

    /// <summary>The most recently finished story the Commander has not answered, or null.</summary>
    private Story? Unanswered(string? frontierId) => stories.For(frontierId)
        .Where(story => story is { State: StoryState.Finished, EndingChoice: null })
        .OrderByDescending(story => story.StoppedAt)
        .FirstOrDefault();

    /// <summary>The ending to post, when a story has finished and its message has not been posted.</summary>
    public StoryEndingDue? EndingDue(string? frontierId) =>
        Unanswered(frontierId) is { EndingPostedAt: null } story && Hidden(story.Id) is { } secret
            ? new StoryEndingDue(story.Id, story.Title, secret.End, secret.Options)
            : null;

    /// <summary>The title of the story whose posted ending waits for an answer, or null.</summary>
    public string? EndingTitle(string? frontierId) => Unanswered(frontierId) is { EndingPostedAt: not null } story ? story.Title : null;

    /// <summary>The id of the story whose posted ending waits for an answer, or null.</summary>
    public string? EndingStoryId(string? frontierId) => Unanswered(frontierId) is { EndingPostedAt: not null } story ? story.Id : null;

    /// <summary>Records that the ending message was posted, so it is not posted again.</summary>
    public void EndingPosted(string? frontierId, string storyId, DateTimeOffset now) =>
        stories.Update(frontierId, storyId, story => story is { State: StoryState.Finished, EndingPostedAt: null }
            ? story with { EndingPostedAt = now }
            : story);

    /// <summary>The options of the ending waiting for an answer, or none.</summary>
    public IReadOnlyList<StoryOption> EndingOptions(string? frontierId) =>
        Unanswered(frontierId) is { EndingPostedAt: not null } story && Hidden(story.Id) is { } secret ? secret.Options : [];

    /// <summary>
    /// Records the Commander's answer to the ending, <paramref name="choice"/> counting from one, and returns the
    /// story's last line and the waking line of each core the option adds. Only the Commander reaches this.
    /// </summary>
    public StoryAnswer Answer(string? frontierId, int? choice)
    {
        if (Unanswered(frontierId) is not { EndingPostedAt: not null } story || Hidden(story.Id) is not { } secret)
        {
            return StoryAnswer.Refused("No ending is waiting for an answer.");
        }

        var options = secret.Options;

        if (choice is null && options.Count != 1)
        {
            return StoryAnswer.Refused($"Which ending? Choose a number from one to {options.Count}.");
        }

        var index = (choice ?? 1) - 1;

        if (index < 0 || index >= options.Count)
        {
            return StoryAnswer.Refused($"There is no option {choice}. Choose a number from one to {options.Count}.");
        }

        var picked = options[index];
        var recorded = false;

        stories.Update(frontierId, story.Id, current =>
        {
            recorded = current.EndingChoice is null;
            return recorded ? current with { EndingChoice = picked.Id } : current;
        });

        if (!recorded)
        {
            return StoryAnswer.Refused("That ending has been answered.");
        }

        logger.LogInformation("{Title}: the ending {Option} was chosen", story.Title, picked.Id);
        return new StoryAnswer(null, picked.After, [.. picked.Add.Select(StoryEnding.Waking)]);
    }

    private void Stop(string? frontierId, Story story, StoryState state, DateTimeOffset now)
    {
        if (story.CurrentChapter is { } key && book.Standing(frontierId, key) is { Adventure.IsActive: true, IsDone: false })
        {
            book.Abandon(frontierId, key, now);
        }

        stories.Save(frontierId, story with { State = state, StoppedAt = now });
        logger.LogInformation("{Title} is {State}", story.Title, state);
    }

    private async Task<string?> WriteChapterAsync(string? frontierId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var commander = frontierId ?? AdventureStore.NoCommander;

        if (stories.Current(frontierId) is { IsWithoutOdyssey: true })
        {
            return NeedsOdyssey;
        }

        lock (_gate)
        {
            if (!_writing.Add(commander))
            {
                return "A chapter is already being written.";
            }

            _failed.Remove(commander);
        }

        WritingChanged?.Invoke();

        try
        {
            string? refusal;

            // A throw counts as a failure, so the tick does not start another paid write on every pass.
            try
            {
                refusal = await WriteAsync(frontierId, now, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Writing a story chapter failed");
                refusal = "The chapter could not be written. Try again in a moment.";
            }

            if (refusal is not null)
            {
                lock (_gate)
                {
                    _failed.Add(commander);
                }

                logger.LogWarning("A story chapter was not written: {Refusal}", refusal);
            }

            return refusal;
        }
        finally
        {
            lock (_gate)
            {
                _writing.Remove(commander);
            }

            WritingChanged?.Invoke();
        }
    }

    private async Task<string?> WriteAsync(string? frontierId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (stories.Current(frontierId) is not { State: StoryState.Running } story)
        {
            return "No story is running.";
        }

        if (Hidden(story.Id) is not { } secret)
        {
            return $"The hidden layer of {story.Title} is missing from this build.";
        }

        if (story.FinaleChapter >= story.Pacing.FinaleChapters)
        {
            return $"{story.Title} has had its last chapter.";
        }

        var number = story.Chapters.Count + 1;
        AdventureChapter? previous = null;

        if (story.CurrentChapter is { } last
            && (previous = book.ChapterOf(frontierId, last)) is null)
        {
            return "The chapter before is no longer on file.";
        }

        var finaleFrom = story.FinaleFrom ?? (StoryClues.FinaleDue(story, now) ? number : null);
        var finaleChapter = number - finaleFrom + 1;
        var stage = finaleChapter is null ? StoryClues.Stage(story) : StoryStage.Finale;
        var reach = story.BeaconScanAt is null
            ? BeaconReach.Of(here(), Game()?.Ship ?? ShipLoadout.Unknown, Game()?.Carrier.Owned == true)
            : null;

        var game = Game();
        var longHaul = ChapterFit.IsLongHaul(story.Pacing, game);
        var comfort = ChapterFit.IsComfortChapter(SinceBeacon(frontierId, story) + 1)
            ? ChapterFit.LeastDone(game?.Statistics ?? CareerStatistics.Empty, story.Refused)
            : null;

        var ask = new AdventureAsk(
            longHaul ? AdventureReach.Anywhere : AdventureReach.Session,
            AdventureLength.Evening,
            Chapter: previous,
            Story: Asked(
                story,
                secret,
                number,
                now,
                stage,
                finaleChapter,
                reach is { InReach: true } ? new AdventureBeacon(reach.Address, reach.System) : null,
                reach is { InReach: false, Why: { } why } ? new AdventureBeaconAway(reach.System, reach.LightYears, why) : null,
                reach?.InReach != false,
                longHaul,
                comfort,
                finaleChapter > 1 ? story.FinaleDestination : null));

        var outcome = await write(ask, now, cancellationToken).ConfigureAwait(false);

        if (outcome.Draft is not { } draft)
        {
            return outcome.Refusal ?? "Nothing came back.";
        }

        // The story may have been switched or abandoned while the model wrote.
        if (stories.Current(frontierId) is not { State: StoryState.Running } still || still.PickedAt != story.PickedAt || still.Chapters.Count != story.Chapters.Count)
        {
            return "The story changed while its chapter was being written.";
        }

        var key = UniqueKey(frontierId, draft.Key);

        if ((book.Write(frontierId, draft with { Key = key, StoryId = story.Id }) ?? book.Begin(frontierId, key, now)) is { } refusal)
        {
            return refusal;
        }

        stories.Save(frontierId, still with
        {
            Chapters = [.. still.Chapters, key],
            FinaleFrom = finaleFrom,
            FinaleDestination = finaleChapter == 1 ? outcome.Destination : still.FinaleDestination,
        });
        logger.LogInformation("{Title}: chapter {Number}, {Name}, begins", story.Title, number, draft.Name);
        return null;
    }

    private AdventureStory Asked(
        Story story,
        StorySecret secret,
        int number,
        DateTimeOffset now,
        StoryStage stage,
        int? finaleChapter,
        AdventureBeacon? beacon,
        AdventureBeaconAway? away,
        bool beaconInReach,
        bool longHaul,
        AdventureActivity? comfort,
        AdventureDestination? destination)
    {
        var card = catalog().Find(story.Id);

        return new AdventureStory(
            story.Id,
            story.Title,
            story.PublicLayer,
            StoryClues.Brief(story, secret),
            number,
            Math.Max(0, (now - story.PickedAt).Days),
            story.SinceBeacon(now)?.Days,
            beacon,
            card?.Level,
            StoryClues.StageName(stage),
            StoryClues.Beats(secret.Beats, story.Pacing, stage, beaconInReach, finaleChapter),
            finaleChapter,
            away,
            story.Pacing.Name,
            story.Pacing.FinaleChapters,
            card?.Genre,
            card?.Genre is { } genre ? ChapterFit.Elements.GetValueOrDefault(genre) : null,
            ChapterFit.Size(story.Pacing),
            longHaul,
            comfort,
            destination,
            story.Refused);
    }

    /// <summary>The story's chapters written since the beacon scan; none before it.</summary>
    private int SinceBeacon(string? frontierId, Story story) => story.BeaconScanAt is { } scanned
        ? story.Chapters.Count(key => book.Store.Find(frontierId, key) is { } chapter && chapter.Written >= scanned)
        : 0;

    /// <summary>The hidden layer with every name token resolved for this Commander.</summary>
    private StorySecret? Hidden(string id) => catalog().Secret(id)?.For(Gender());

    private string UniqueKey(string? frontierId, string wanted)
    {
        var existing = book.Store.For(frontierId);
        var key = string.IsNullOrWhiteSpace(wanted) ? "chapter" : wanted;
        var candidate = key;
        var suffix = 2;

        while (existing.Any(other => string.Equals(other.Key, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{key}-{suffix++}";
        }

        return candidate;
    }
}

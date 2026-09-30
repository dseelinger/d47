using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Persona;
using Microsoft.Extensions.Logging;

namespace D47.Core.Stories;

/// <summary>
/// Runs a stock story as a chain of adventures: Pick writes and begins chapter one, and a finished chapter has
/// the next written and begun. Nothing here touches the Guardian cores.
/// </summary>
public sealed class StoryDirector(
    StoryStore stories,
    AdventureBook book,
    StoryCatalog catalog,
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

    private JournalLocation _where = JournalLocation.Unknown;

    public StoryStore Stories => stories;

    public StoryCatalog Catalog => catalog;

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
        if (catalog.Offered.FirstOrDefault(card => string.Equals(card.Id, id, StringComparison.OrdinalIgnoreCase)) is not { } card)
        {
            return Task.FromResult<string?>("There is no story by that name.");
        }

        if (stories.Current(frontierId) is { } current)
        {
            return Task.FromResult<string?>(string.Equals(current.Id, card.Id, StringComparison.OrdinalIgnoreCase)
                ? $"{current.Title} is already your story."
                : $"{current.Title} is your story. Switch to change it.");
        }

        setBackstory(card.InYourWords);

        var story = new Story
        {
            Id = card.Id,
            Title = card.Title,
            PublicLayer = card.Describe(),
            PickedAt = now,
        };

        stories.Save(frontierId, story);
        logger.LogInformation("Picked the story {Title}", card.Title);

        return WriteChapterAsync(frontierId, now, cancellationToken);
    }

    /// <summary>Abandons the current story, keeping its chapters on file, and picks another.</summary>
    public Task<string?> SwitchAsync(string? frontierId, string id, DateTimeOffset now, CancellationToken cancellationToken)
    {
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

        if (story.State != StoryState.Running || standing is not { IsDone: true } || WriteFailed(frontierId) || IsWriting(frontierId))
        {
            return null;
        }

        return Task.Run(() => WriteChapterAsync(frontierId, now, CancellationToken.None));
    }

    /// <summary>
    /// Counts the current story's play sessions, and stamps it the first time the Commander data-links a beacon
    /// while it runs.
    /// </summary>
    public void Observe(JournalEvent journalEvent, string? frontierId)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        _where = _where.Apply(journalEvent);

        if (journalEvent.Kind == "LoadGame"
            && stories.Current(frontierId) is { } playing
            && journalEvent.Timestamp >= playing.PickedAt)
        {
            stories.Update(frontierId, playing.Id, story => story.LastSessionAt is { } last && journalEvent.Timestamp <= last
                ? story
                : story with { Sessions = story.Sessions + 1, LastSessionAt = journalEvent.Timestamp });
        }

        if (journalEvent.Kind == "DataScanned"
            && _where.SystemAddress is { } address
            && GuardianCores.Beacons.ContainsKey(address)
            && stories.Current(frontierId) is { BeaconScanAt: null } story
            && journalEvent.Timestamp >= story.PickedAt)
        {
            stories.Update(frontierId, story.Id, current => current with { BeaconScanAt = journalEvent.Timestamp });
            logger.LogInformation("{Title}: the beacon in {System} was scanned", story.Title, GuardianCores.Beacons[address]);
        }
    }

    /// <summary>The current story's hidden layer as every speaker reads it, or null when no story is current.</summary>
    public string? HiddenBrief(string? frontierId) =>
        stories.Current(frontierId) is { } story && catalog.Secret(story.Id) is { } secret
            ? StoryClues.Brief(story, secret)
            : null;

    /// <summary>The clue the running story owes now, or null.</summary>
    public StoryClueDue? ClueDue(string? frontierId, DateTimeOffset now) =>
        stories.Current(frontierId) is { } story && catalog.Secret(story.Id) is not null
            ? StoryClues.Due(story, now)
            : null;

    /// <summary>The text of a clue that is still the next one owed, or null.</summary>
    public (string Title, string Clue)? Clue(string? frontierId, StoryClueDue due)
    {
        ArgumentNullException.ThrowIfNull(due);

        return stories.Current(frontierId) is { State: StoryState.Running } story
               && string.Equals(story.Id, due.StoryId, StringComparison.OrdinalIgnoreCase)
               && story.CluesGiven == due.Index
               && catalog.Secret(story.Id) is { } secret
               && StoryClues.Text(secret, due.Index) is { Length: > 0 } clue
            ? (story.Title, clue)
            : null;
    }

    /// <summary>Records that a clue was spoken, so the next waits for its day and four more sessions.</summary>
    public void ClueGiven(string? frontierId, StoryClueDue due)
    {
        ArgumentNullException.ThrowIfNull(due);

        stories.Update(frontierId, due.StoryId, story => story.IsCurrent && story.CluesGiven == due.Index
            ? story with { CluesGiven = due.Index + 1, ClueSession = story.Sessions }
            : story);
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

        if (catalog.Secret(story.Id) is not { } secret)
        {
            return $"The hidden layer of {story.Title} is missing from this build.";
        }

        var number = story.Chapters.Count + 1;
        AdventureChapter? previous = null;

        if (story.CurrentChapter is { } last
            && (previous = AdventureChapter.Of(book.Store.For(frontierId), last)) is null)
        {
            return "The chapter before is no longer on file.";
        }

        var beacon = number == 1 ? GuardianCores.NearestBeacon(here()) : ((long, string)?)null;

        var ask = new AdventureAsk(
            AdventureReach.Session,
            AdventureLength.Evening,
            Chapter: previous,
            Story: new AdventureStory(
                story.Id,
                story.Title,
                story.PublicLayer,
                StoryClues.Brief(story, secret),
                number,
                Math.Max(0, (now - story.PickedAt).Days),
                story.SinceBeacon(now)?.Days,
                beacon is var (address, system) ? new AdventureBeacon(address, system) : null));

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

        stories.Save(frontierId, still with { Chapters = [.. still.Chapters, key] });
        logger.LogInformation("{Title}: chapter {Number}, {Name}, begins", story.Title, number, draft.Name);
        return null;
    }

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

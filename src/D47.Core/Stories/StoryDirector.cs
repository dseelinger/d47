using System.Diagnostics.CodeAnalysis;
using D47.Core.Adventures;
using D47.Core.Audio;
using D47.Core.Journal;
using D47.Core.Persona;
using Microsoft.Extensions.Logging;

namespace D47.Core.Stories;

/// <summary>A story picked with a narrated beacon scan, waiting for the tick to say its scan line and wake the cores. <see cref="Line"/> is null when the entry has none.</summary>
public sealed record StoryScanDue(string StoryId, string Title, StoryLine? Line);

/// <summary>A story picked with fixed opening lines, waiting for the tick to say them before anything else of the story.</summary>
public sealed record StoryOpeningDue(string StoryId, string Title, IReadOnlyList<StoryLine> Lines);

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

    /// <summary>Adventures outside a story with a replacement beat being written, by Commander and key.</summary>
    private readonly HashSet<string> _rewritingAdventures = new(StringComparer.Ordinal);

    /// <summary>Narrated scans picked and not yet taken by the tick, by Commander.</summary>
    private readonly Dictionary<string, StoryScanDue> _scans = new(StringComparer.Ordinal);

    /// <summary>Openings picked and not yet taken by the tick, by Commander.</summary>
    private readonly Dictionary<string, StoryOpeningDue> _openings = new(StringComparer.Ordinal);

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

    /// <summary>Where a story's chapters go once they are neither the current chapter nor the one before it.</summary>
    public StoryChapterArchive Archive { get; set; } = StoryChapterArchive.InMemory();

    /// <summary>The Commander's gender as settings hold it: <see cref="CommanderGender.Man"/>, <see cref="CommanderGender.Woman"/> or null.</summary>
    public Func<string?> Gender { get; set; } = () => null;

    /// <summary>Stores the Commander's gender; a running story uses it from its next line.</summary>
    public Action<string> SetGender { get; set; } = _ => { };

    /// <summary>The core aboard, which speaks the ship's story lines unless it is stock.</summary>
    public Func<Persona.Persona?> Aboard { get; set; } = () => null;

    /// <summary>What is on this PC for a cast to speak with. Read at a pick, a resume, and every <see cref="VoiceCheck"/> while a story runs.</summary>
    public Func<CastVoicesHere> VoicesHere { get; set; } = () => CastVoicesHere.All;

    /// <summary>How often a running story checks that its cast can still speak.</summary>
    public static readonly TimeSpan VoiceCheck = TimeSpan.FromSeconds(5);

    /// <summary>When the running story's cast was last checked, by Commander.</summary>
    private readonly Dictionary<string, DateTimeOffset> _voicesChecked = new(StringComparer.Ordinal);

    /// <summary>
    /// Raised with a story's title and the ship's message when a pick is refused or a running story is paused because a
    /// cast voice is not ready.
    /// </summary>
    public event Action<string, string>? VoicesNotReady;

    /// <summary>What the cast of story <paramref name="id"/> needs and does not have here; empty when ready or when its hidden layer is not on disk.</summary>
    public IReadOnlyList<string> VoicesMissing(string id) =>
        catalog().Secret(id) is { } secret ? StoryVoices.Missing(secret, Gender(), VoicesHere(), Choices()) : [];

    /// <summary>The voices the Commander chose for story characters, keyed as <see cref="Configuration.D47Settings.StoryVoices"/>.</summary>
    public Func<IReadOnlyDictionary<string, Configuration.StoryVoiceChoice>> Choices { get; set; } =
        () => new Dictionary<string, Configuration.StoryVoiceChoice>();

    /// <summary>
    /// The primary cast of story <paramref name="id"/> as this Commander meets it, in cast order; a member with versions
    /// is left out while the Commander's gender is unset. Empty when the hidden layer is not on disk.
    /// </summary>
    public IReadOnlyList<StoryCastMember> PrimaryCast(string id)
    {
        var stories = catalog();

        if (stories.Secret(id) is not { } secret)
        {
            return [];
        }

        var gender = Gender();
        var title = stories.Find(id)?.Title ?? id;

        return [.. secret.Cast
            .Where(member => member.Primary && (member.Versions is null || CommanderGender.IsSet(gender)))
            .Select(member => Member(title, secret, member, gender))];
    }

    /// <summary>
    /// The cast member whose <see cref="Configuration.D47Settings.StoryVoices"/> key is <paramref name="key"/>, in any
    /// story whose hidden layer is on disk, or null.
    /// </summary>
    public StoryCastMember? CastMember(string key)
    {
        var stories = catalog();
        var gender = Gender();

        foreach (var secret in stories.Secrets)
        {
            foreach (var member in secret.Cast)
            {
                foreach (var asked in new[] { gender, CommanderGender.Man, CommanderGender.Woman })
                {
                    if (string.Equals(member.Shown(secret.Id, asked).Picture, key, StringComparison.Ordinal))
                    {
                        return Member(stories.Find(secret.Id)?.Title ?? secret.Id, secret, member, asked);
                    }
                }
            }
        }

        return null;
    }

    private StoryCastMember Member(string title, StorySecret secret, StorySpeaker member, string? gender)
    {
        var shown = member.Shown(secret.Id, gender);

        var choices = Choices();

        return new StoryCastMember(secret.Id, title, shown, StoryVoices.Voice(member, shown, null), StoryVoices.Voice(member, shown, choices))
        {
            SpeaksName = choices.GetValueOrDefault(shown.Picture)?.VoiceName,
        };
    }

    /// <summary>Whether the cast of any story whose hidden layer is on disk speaks, in the Commander's version, through <paramref name="providerId"/>.</summary>
    public bool CastUses(string providerId)
    {
        var stories = catalog();

        return stories.Cards.Any(card => stories.Secret(card.Id) is { } secret && StoryVoices.Uses(secret, Gender(), providerId, Choices()));
    }

    /// <summary>Who speaks a line by <paramref name="speaker"/> in the current story, decided now; null when no story is current.</summary>
    public StoryLineVoice? LineVoice(string? frontierId, string? speaker) =>
        stories.Current(frontierId) is { } story && Hidden(story.Id) is { } secret
            ? StoryVoices.Of(speaker, secret, Gender(), Aboard(), Choices())
            : null;

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

        if (Unready(card.Id, card.Title) is { } unready)
        {
            return Task.FromResult<string?>(unready);
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

        var hidden = Hidden(card.Id);

        lock (_gate)
        {
            var who = frontierId ?? AdventureStore.NoCommander;

            if (hidden?.Opening is { Count: > 0 } opening)
            {
                _openings[who] = new StoryOpeningDue(card.Id, card.Title, opening);
            }
            else
            {
                _openings.Remove(who);
            }

            if (narrated)
            {
                _scans[who] = new StoryScanDue(card.Id, card.Title, hidden?.Scan);
            }
        }

        return WriteChapterAsync(frontierId, now, cancellationToken);
    }

    /// <summary>Abandons the current story, moving its chapters to the archive, and picks another.</summary>
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

            if (catalog().Find(id) is { } card && Unready(card.Id, card.Title) is { } unready)
            {
                return Task.FromResult<string?>(unready);
            }

            Stop(frontierId, current, StoryState.Abandoned, now);
        }

        return PickAsync(frontierId, id, now, cancellationToken);
    }

    /// <summary>Ends the current story and moves its chapters to the archive.</summary>
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

        if (VoicesMissing(paused.Id) is { Count: > 0 } missing)
        {
            return StoryVoices.Paused(paused.Title, missing);
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
        if (!TryRewritable(frontierId, out var at, out var refusal))
        {
            return refusal;
        }

        if (!Claim(frontierId))
        {
            return "A different objective is already being written.";
        }

        return await RewriteAsync(frontierId, at, closedMarketId: null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// When the journal says the station the current beat docks at has its docks offline, says so through
    /// <see cref="Says"/> and has the beat written again on the pool, then says the new beat's hand-off. Covers the running
    /// story's chapter and every active adventure outside a story. Returns the work it started, or null. Does not block.
    /// </summary>
    public Task<string?>? DockOffline(JournalEvent journalEvent, string? frontierId)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        if (journalEvent.Kind != "DockingDenied"
            || !string.Equals(journalEvent.String("Reason"), "DockOffline", StringComparison.OrdinalIgnoreCase)
            || journalEvent.Long("MarketID") is not { } market)
        {
            return null;
        }

        var work = new List<Func<Task<string?>>>();
        string? station = null;

        if (TryRewritable(frontierId, out var at, out _)
            && journalEvent.Timestamp >= at.Story.PickedAt
            && at.Beat.Trigger is { Kind: TriggerKind.Dock } trigger
            && trigger.MarketId == market
            && Claim(frontierId))
        {
            station = trigger.Station;
            work.Add(() => HandOffAfter(frontierId, at.Standing.Adventure.Key, RewriteAsync(frontierId, at, market, CancellationToken.None)));
        }

        foreach (var standing in book.Active(frontierId))
        {
            if (standing.Adventure is { StoryId: null, AcceptedAt: { } accepted } adventure
                && journalEvent.Timestamp >= accepted
                && standing.CurrentBeat?.Trigger is { Kind: TriggerKind.Dock } beat
                && beat.MarketId == market
                && ClaimAdventure(frontierId, adventure.Key))
            {
                station ??= beat.Station;
                work.Add(() => HandOffAfter(frontierId, adventure.Key, RewriteAdventureAsync(frontierId, standing, market)));
            }
        }

        if (work.Count == 0)
        {
            return null;
        }

        Says?.Invoke($"The docks at {station ?? journalEvent.String("StationName") ?? "that station"} are offline.");

        return Task.Run(async () =>
        {
            string? first = null;

            foreach (var rewrite in work)
            {
                first ??= await rewrite().ConfigureAwait(false);
            }

            return first;
        });
    }

    /// <summary>Says the hand-off of the beat that replaced the one at <paramref name="key"/>, or why none could be written.</summary>
    private async Task<string?> HandOffAfter(string? frontierId, string key, Task<string?> rewrite)
    {
        var refusal = await rewrite.ConfigureAwait(false);
        var said = refusal is null
            ? book.Standing(frontierId, key)?.CurrentBeat?.Trigger.HandOff()
            : $"A different objective could not be written. {refusal}";

        if (said is not null)
        {
            Says?.Invoke(said);
        }

        return refusal;
    }

    /// <summary>Marks an adventure outside a story as having a replacement beat written; false when one already is.</summary>
    private bool ClaimAdventure(string? frontierId, string key)
    {
        lock (_gate)
        {
            return _rewritingAdventures.Add(AdventureClaim(frontierId, key));
        }
    }

    private static string AdventureClaim(string? frontierId, string key) => $"{frontierId ?? AdventureStore.NoCommander}\n{key}";

    /// <summary>Writes the beats of an adventure outside a story again from its current beat, avoiding the closed station, and releases the claim.</summary>
    private async Task<string?> RewriteAdventureAsync(string? frontierId, AdventureStanding standing, long closedMarketId)
    {
        var adventure = standing.Adventure;
        var from = standing.Current;

        try
        {
            var ask = new AdventureAsk(
                AdventureReach.Session,
                Chapter: adventure.Follows is { } follows ? book.ChapterOf(frontierId, follows) : null,
                Rewrite: new AdventureRewrite(adventure, from, closedMarketId));

            var outcome = await write(ask, Clock(), CancellationToken.None).ConfigureAwait(false);

            if (outcome.Draft is not { } draft)
            {
                return outcome.Refusal ?? "Nothing came back.";
            }

            if (book.ReplaceBeats(frontierId, adventure.Key, from, [.. draft.Beats.Skip(from)], Clock()) is { } refusal)
            {
                return refusal;
            }

            logger.LogInformation("{Adventure}: beat {Beat} was replaced, its docks offline", adventure.Name, from + 1);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Adventure}: the replacement beat could not be written", adventure.Name);
            return "The objective could not be written.";
        }
        finally
        {
            lock (_gate)
            {
                _rewritingAdventures.Remove(AdventureClaim(frontierId, adventure.Key));
            }
        }
    }

    /// <summary>Raised with a line to say unprompted: a dock beat's station has its docks offline, then the beat that replaces it.</summary>
    public event Action<string>? Says;

    private sealed record BeatAt(Story Story, AdventureStanding Standing, AdventureBeat Beat);

    /// <summary>The running story's current beat when it can be written again, or the refusal saying why not.</summary>
    private bool TryRewritable(string? frontierId, [NotNullWhen(true)] out BeatAt? at, [NotNullWhen(false)] out string? refusal)
    {
        at = null;
        refusal = null;

        if (stories.Current(frontierId) is not { } story)
        {
            refusal = "No story is running.";
        }
        else if (story.State != StoryState.Running)
        {
            refusal = "Resume the story first.";
        }
        else if (WithoutOdyssey(frontierId))
        {
            refusal = NeedsOdyssey;
        }
        else if (story.CurrentChapter is not { } key || book.Standing(frontierId, key) is not { Adventure.IsActive: true, IsDone: false } standing
                 || standing.CurrentBeat is not { } beat)
        {
            refusal = "The story is not waiting on an objective.";
        }
        else if (!standing.IsRefusable)
        {
            refusal = "The Guardian beacon scan ends act one, so that objective cannot be swapped.";
        }
        else if (IsWriting(frontierId))
        {
            refusal = "A chapter is being written.";
        }
        else
        {
            at = new BeatAt(story, standing, beat);
            return true;
        }

        return false;
    }

    /// <summary>Marks a replacement beat as being written; false when one already is.</summary>
    private bool Claim(string? frontierId)
    {
        lock (_gate)
        {
            if (!_rewriting.Add(frontierId ?? AdventureStore.NoCommander))
            {
                return false;
            }
        }

        WritingChanged?.Invoke();
        return true;
    }

    /// <summary>Writes the replacement a <see cref="Claim"/> was taken for, and releases the claim.</summary>
    private async Task<string?> RewriteAsync(string? frontierId, BeatAt at, long? closedMarketId, CancellationToken cancellationToken)
    {
        try
        {
            return await RefuseAsync(frontierId, at.Story, at.Standing, at.Beat, closedMarketId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Writing a replacement beat failed");
            return "The objective could not be written. Try again in a moment.";
        }
        finally
        {
            lock (_gate)
            {
                _rewriting.Remove(frontierId ?? AdventureStore.NoCommander);
            }

            WritingChanged?.Invoke();
        }
    }

    private async Task<string?> RefuseAsync(
        string? frontierId, Story story, AdventureStanding standing, AdventureBeat beat, long? closedMarketId, CancellationToken cancellationToken)
    {
        if (Hidden(story.Id) is not { } secret)
        {
            return $"The hidden layer of {story.Title} is missing from this build.";
        }

        var chapter = standing.Adventure;
        var from = standing.Current;
        var refusedKey = closedMarketId is null ? RefusedActivities.Key(beat.Trigger.Kind, beat.Trigger.MissionFamily) : null;
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
            Chapter: ChapterAt(frontierId, story, story.Chapters.Count - 2),
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
            Rewrite: new AdventureRewrite(chapter, from, closedMarketId));

        var outcome = await write(ask, now, cancellationToken).ConfigureAwait(false);

        if (outcome.Draft is not { } draft)
        {
            return outcome.Refusal ?? "Nothing came back.";
        }

        if (stories.Current(frontierId) is not { State: StoryState.Running } still
            || still.PickedAt != story.PickedAt
            || still.Chapters.Count != story.Chapters.Count)
        {
            return "The story changed while the objective was being written.";
        }

        if (book.ReplaceBeats(frontierId, chapter.Key, from, [.. draft.Beats.Skip(from)], Clock()) is { } refusal)
        {
            return refusal;
        }

        stories.Update(frontierId, story.Id, current =>
            refusedKey is null || current.Refused.Contains(refusedKey) ? current : current with { Refused = [.. current.Refused, refusedKey] });

        logger.LogInformation(
            "{Title}: beat {Beat} of {Chapter} was replaced, {Why}",
            story.Title,
            from + 1,
            chapter.Name,
            closedMarketId is null ? "refused" : "its docks offline");
        return null;
    }

    /// <summary>
    /// Pauses a story whose chapter was abandoned or removed, starts the next chapter on the pool when one finishes,
    /// and archives a finished story's chapters on the pool. Returns the work it started, or null. Does not block.
    /// </summary>
    public Task<string?>? Tick(string? frontierId, DateTimeOffset now)
    {
        if (stories.Current(frontierId) is not { } story || story.CurrentChapter is not { } key)
        {
            return null;
        }

        var standing = book.Standing(frontierId, key);

        if (story.State == StoryState.Running && standing is { Adventure.IsActive: true } && LostAVoice(frontierId, story, now))
        {
            book.Abandon(frontierId, key, now);
            stories.Save(frontierId, story.Paused(now));
            return null;
        }

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
            if (!StoryClues.AtTheEnd(story))
            {
                return null;
            }

            stories.Save(frontierId, story with { State = StoryState.Finished, StoppedAt = now });
            logger.LogInformation("{Title} is finished", story.Title);

            return Task.Run(() =>
            {
                ArchiveChapters(frontierId);
                return (string?)null;
            });
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

    /// <summary>
    /// The opening of the story just picked, once, while it is still the running story; the caller says its lines before
    /// the narrated scan and chapter one. Null when none waits.
    /// </summary>
    public StoryOpeningDue? TakeOpening(string? frontierId)
    {
        StoryOpeningDue? due;

        lock (_gate)
        {
            if (!_openings.Remove(frontierId ?? AdventureStore.NoCommander, out due))
            {
                return null;
            }
        }

        return stories.Current(frontierId) is { State: StoryState.Running } story
               && string.Equals(story.Id, due.StoryId, StringComparison.OrdinalIgnoreCase)
            ? due
            : null;
    }

    /// <summary>Whether an opening has been picked and not yet taken by <see cref="TakeOpening"/>.</summary>
    public bool OpeningWaits(string? frontierId)
    {
        lock (_gate)
        {
            return _openings.ContainsKey(frontierId ?? AdventureStore.NoCommander);
        }
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

    /// <summary>The asides that tie mission speech to the running story; the app sets its excerpt for the active Commander.</summary>
    public StoryMissionAsides MissionAsides { get; } = new();

    /// <summary>The running story's excerpt for mission speech, or null unless it is running, switched on and played with Odyssey.</summary>
    public string? MissionExcerpt(string? frontierId) =>
        stories.Current(frontierId) is { State: StoryState.Running, IsOff: false, IsWithoutOdyssey: false } story
            ? catalog().Find(story.Id)?.Excerpt() ?? story.PublicLayer.Split('\n')[0].Trim()
            : null;

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

    /// <summary>Who speaks a due clue, decided now; null when it is not the clue the running story owes.</summary>
    public StoryLineVoice? ClueVoice(string? frontierId, StoryClueDue due)
    {
        ArgumentNullException.ThrowIfNull(due);

        return Clue(frontierId, due) is not null
               && stories.Current(frontierId) is { } story
               && Hidden(story.Id) is { } secret
               && StoryClues.Line(secret, story.Pacing, due.Index) is { } line
            ? StoryVoices.Of(line.Speaker, secret, Gender(), Aboard(), Choices())
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
        ArchiveChapters(frontierId);
    }

    /// <summary>
    /// Moves every story chapter in the adventure file to the archive except the current story's last two, with when
    /// each beat fired. Writes both files, so it never runs on the tick.
    /// </summary>
    public void ArchiveChapters(string? frontierId)
    {
        var commander = frontierId ?? AdventureStore.NoCommander;
        var kept = stories.Current(frontierId)?.Chapters.TakeLast(2).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        var moving = book.Store.For(frontierId).Where(adventure => adventure.StoryId is not null && !kept.Contains(adventure.Key)).ToList();

        foreach (var adventure in moving)
        {
            var run = stories.For(frontierId).FirstOrDefault(story =>
                string.Equals(story.Id, adventure.StoryId, StringComparison.OrdinalIgnoreCase)
                && story.Chapters.Contains(adventure.Key, StringComparer.OrdinalIgnoreCase));
            var standing = book.Standing(frontierId, adventure.Key);

            Archive.Append(new ArchivedChapter(commander, adventure.StoryId!, run?.PickedAt, adventure, standing?.Fired ?? [], standing?.FiredBy ?? []));
            book.Remove(frontierId, adventure.Key);
        }

        if (moving.Count > 0)
        {
            logger.LogInformation("Moved {Count} story chapters to the archive", moving.Count);
        }
    }

    /// <summary><see cref="ArchiveChapters(string?)"/> for every Commander with adventures on file.</summary>
    public void ArchiveChapters()
    {
        foreach (var commander in book.Store.Commanders)
        {
            ArchiveChapters(commander);
        }
    }

    /// <summary>
    /// The chapter at <paramref name="index"/> of the story's chapters in full, with every chapter before it from the
    /// adventure file or the archive; null when there is none or it is not on file.
    /// </summary>
    private AdventureChapter? ChapterAt(string? frontierId, Story story, int index)
    {
        if (index < 0 || index >= story.Chapters.Count || book.ChapterOf(frontierId, story.Chapters[index]) is not { } chapter)
        {
            return null;
        }

        return chapter with { Earlier = [.. story.Chapters.Take(index).Select(key => ChapterOf(frontierId, story, key)).OfType<Adventure>()] };
    }

    /// <summary>A chapter of this run of the story, from the adventure file or the archive.</summary>
    private Adventure? ChapterOf(string? frontierId, Story story, string key) =>
        book.Store.Find(frontierId, key) ?? Archive.Find(frontierId, story, key)?.Adventure;

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

        if (story.CurrentChapter is not null
            && (previous = ChapterAt(frontierId, story, story.Chapters.Count - 1)) is null)
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

        var key = UniqueKey(frontierId, still, draft.Key);

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
        ArchiveChapters(frontierId);
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
            story.Refused,
            [.. secret.Cast.Select(member => new AdventureSpeaker(member.Id, member.Shown(secret.Id, Gender()).Name, member.Who))],
            Aboard()?.Name,
            Aboard()?.Stock == true);
    }

    /// <summary>The story's chapters written since the beacon scan; none before it.</summary>
    private int SinceBeacon(string? frontierId, Story story) => story.BeaconScanAt is { } scanned
        ? story.Chapters.Count(key => ChapterOf(frontierId, story, key) is { } chapter && chapter.Written >= scanned)
        : 0;

    /// <summary>The refusal for a story whose cast cannot speak yet, raising <see cref="VoicesNotReady"/>; null when it can.</summary>
    private string? Unready(string id, string title)
    {
        if (VoicesMissing(id) is not { Count: > 0 } missing)
        {
            return null;
        }

        var message = StoryVoices.CannotStart(title, missing);
        logger.LogInformation("{Title} was not picked: {Count} voice prerequisite(s) missing", title, missing.Count);
        VoicesNotReady?.Invoke(title, message);
        return message;
    }

    /// <summary>
    /// Whether the running story's cast has lost a voice, raising <see cref="VoicesNotReady"/> when it has. Checked at
    /// most once every <see cref="VoiceCheck"/>.
    /// </summary>
    private bool LostAVoice(string? frontierId, Story story, DateTimeOffset now)
    {
        var commander = frontierId ?? AdventureStore.NoCommander;

        lock (_gate)
        {
            if (_voicesChecked.TryGetValue(commander, out var last) && now >= last && now - last < VoiceCheck)
            {
                return false;
            }

            _voicesChecked[commander] = now;
        }

        if (VoicesMissing(story.Id) is not { Count: > 0 } missing)
        {
            return false;
        }

        logger.LogInformation("{Title} is paused: {Count} voice prerequisite(s) missing", story.Title, missing.Count);
        VoicesNotReady?.Invoke(story.Title, StoryVoices.Paused(story.Title, missing));
        return true;
    }

    /// <summary>The hidden layer with every name token resolved for this Commander.</summary>
    private StorySecret? Hidden(string id) => catalog().Secret(id)?.For(Gender());

    /// <summary>A key used by no adventure on file and no chapter of the story, archived ones included.</summary>
    private string UniqueKey(string? frontierId, Story story, string wanted)
    {
        var existing = book.Store.For(frontierId).Select(adventure => adventure.Key).Concat(story.Chapters).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var key = string.IsNullOrWhiteSpace(wanted) ? "chapter" : wanted;
        var candidate = key;
        var suffix = 2;

        while (existing.Contains(candidate))
        {
            candidate = $"{key}-{suffix++}";
        }

        return candidate;
    }
}

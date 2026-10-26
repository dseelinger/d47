using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using D47.Core.Activities;
using D47.Core.Adventures;
using D47.Core.Audio;
using D47.Core.Engineers;
using D47.Core.Goals;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Persona;
using Microsoft.Extensions.Logging;

namespace D47.Core.Stories;

/// <summary>A story picked with a narrated beacon scan, waiting for the tick to say its scan line and wake the cores. <see cref="Line"/> is null when the entry has none.</summary>
public sealed record StoryScanDue(string StoryId, string Title, StoryLine? Line);

/// <summary>A story picked with fixed opening lines, waiting for the tick to say them before anything else of the story.</summary>
public sealed record StoryOpeningDue(string StoryId, string Title, IReadOnlyList<StoryLine> Lines);

/// <summary>A story picked with a beacon scan on record, waiting for the tick to bring its core aboard after the opening.</summary>
public sealed record StoryRememberedDue(string StoryId, string Title);

/// <summary>
/// The current objective and the ones after it written again from the Commander's words, with the model's
/// <see cref="Reply"/>; <see cref="Exchange"/> is every round so far, this one last. <see cref="ChapterKey"/> is the
/// adventure's key; <see cref="StoryId"/>, <see cref="PickedAt"/> and <see cref="Chapters"/> are null for a short story.
/// </summary>
public sealed record ObjectiveProposal(
    string? StoryId,
    DateTimeOffset? PickedAt,
    int? Chapters,
    string ChapterKey,
    int From,
    AdventureBeat Current,
    Adventure Draft,
    string Reply,
    IReadOnlyList<AdventureRemark> Exchange,
    int Step = 0)
{
    /// <summary>The trigger of the step the Commander is on.</summary>
    public AdventureTrigger Trigger => Current.Steps[Math.Min(Step, Current.Steps.Count - 1)].Trigger;

    /// <summary>Whether only the step the Commander is on is replaced, because its objective has several.</summary>
    public bool ReplacesStep => Current.Steps.Count > 1 && Draft.Beats.ElementAtOrDefault(From)?.Steps.Count > 0;

    /// <summary>The objectives that would replace the current one and those after it; the one step that would replace a step of several.</summary>
    public IReadOnlyList<AdventureBeat> Beats => ReplacesStep
        ? [Draft.Beats[From] with { Steps = [Draft.Beats[From].Steps[0]] }]
        : [.. Draft.Beats.Skip(From)];
}

/// <summary>A proposal, or the refusal saying why none was written.</summary>
public sealed record ObjectiveProposalOutcome(ObjectiveProposal? Proposal, string? Refusal);

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

    /// <summary>The galaxy search, or null while it is off; with none, a chapter's draw is not gated on stations.</summary>
    public Func<IGalaxyService?>? Galaxy { get; set; }

    /// <summary>The activity ledger, or null before it exists; with none, a comfort chapter does not look for unrun mission families.</summary>
    public Func<ActivityLedger?>? Ledger { get; set; }

    /// <summary>The tools a chapter may send the Commander to run, or null before it exists; with none, a chapter draws no event step.</summary>
    public Func<IReadOnlyList<DrawableTool>>? Tools { get; set; }

    /// <summary>The activities Commanders have changed away from often enough to bar them, or null before it exists.</summary>
    public ActivityBars? Bars { get; set; }

    /// <summary>The engineer route a Goals story's engineers arc reads, or null; with none, that arc gives no step.</summary>
    public Func<EngineerReport?>? Engineers { get; set; }

    /// <summary>The last walk over the Commander's journals, for the Goals arcs the game has not reported this session.</summary>
    public Func<GoalMine?>? Mine { get; set; }

    /// <summary>Commanders with a chapter being written.</summary>
    private readonly HashSet<string> _writing = new(StringComparer.Ordinal);

    /// <summary>Failed chapters whose ending is being stored on the pool, by commander and key.</summary>
    private readonly HashSet<string> _failing = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StoryProgress> _progress = new(StringComparer.Ordinal);

    /// <summary>Commanders whose last chapter could not be written; the tick does not retry until WriteNextAsync is called.</summary>
    private readonly HashSet<string> _failed = new(StringComparer.Ordinal);

    /// <summary>Commanders with a replacement beat being written.</summary>
    private readonly HashSet<string> _rewriting = new(StringComparer.Ordinal);

    /// <summary>Proposed objective changes held for a Commander, by Commander.</summary>
    private readonly Dictionary<string, ObjectiveProposal> _proposals = new(StringComparer.Ordinal);

    /// <summary>Proposed objective changes held for a short story, by Commander and key.</summary>
    private readonly Dictionary<string, ObjectiveProposal> _adventureProposals = new(StringComparer.Ordinal);

    /// <summary>Adventures outside a story with a replacement beat being written, by Commander and key.</summary>
    private readonly HashSet<string> _rewritingAdventures = new(StringComparer.Ordinal);

    /// <summary>Narrated scans picked and not yet taken by the tick, by Commander.</summary>
    private readonly Dictionary<string, StoryScanDue> _scans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CoreWaking> _steppedWakings = new(StringComparer.Ordinal);

    /// <summary>Openings picked and not yet taken by the tick, by Commander.</summary>
    private readonly Dictionary<string, StoryOpeningDue> _openings = new(StringComparer.Ordinal);

    /// <summary>Remembered scans picked and not yet taken by the tick, by Commander.</summary>
    private readonly Dictionary<string, StoryRememberedDue> _remembered = new(StringComparer.Ordinal);

    /// <summary>Whether the story files are being swapped; the tick and journal events are skipped and every write is refused.</summary>
    private bool _switching;

    private JournalLocation _where = JournalLocation.Unknown;

    private bool? _odyssey;

    /// <summary>Why stories are off when the game runs without Odyssey: shown on the Stories page and returned as the refusal.</summary>
    public const string NeedsOdyssey =
        "Stories need Elite Dangerous: Odyssey. Most chapters use it: on-foot missions, settlements, exobiology and suits. "
        + "Your last session ran without it, so stories are off. Odyssey is a low-cost expansion from the Frontier Store, "
        + "Steam or Epic, and stories start working at the next session after it is installed.";

    /// <summary>Why no chapter is written while the story files are swapped.</summary>
    public const string Swapping = "The story files are being swapped. Try again in a moment.";

    /// <summary>Why a story with a member in two versions cannot be picked yet.</summary>
    public const string NeedsGender = "Choose whether your Commander is a man or a woman first. This story has a character written for each.";

    public StoryStore Stories => stories;

    /// <summary>Where a story's chapters go once they are neither the current chapter nor the one before it.</summary>
    public StoryChapterArchive Archive { get; set; } = StoryChapterArchive.InMemory();

    /// <summary>The Commander's gender as settings hold it: <see cref="CommanderGender.Man"/>, <see cref="CommanderGender.Woman"/> or null.</summary>
    public Func<string?> Gender { get; set; } = () => null;

    /// <summary>The Commander's Backstory as settings hold it.</summary>
    public Func<string?> Backstory { get; set; } = () => null;

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

    /// <summary>Raised when a story starts, resumes or is switched on. A handler runs on the caller's thread and must not block.</summary>
    public event Action? Began;

    /// <summary>The current story's hidden layer and title, or null when no story is current or its hidden layer is not on disk.</summary>
    public (StorySecret Secret, string Title)? CurrentCast(string? frontierId) =>
        stories.Current(frontierId) is { } story && Hidden(story.Id) is { } secret ? (secret, story.Title) : null;

    /// <summary>The voices the running story pins for its cast, as the Commander meets them; empty when no story is running.</summary>
    public IReadOnlyList<Configuration.RunningStoryVoice> RunningVoices(string? frontierId)
    {
        if (stories.Current(frontierId) is not { State: StoryState.Running } story || Hidden(story.Id) is not { } secret)
        {
            return [];
        }

        var gender = Gender();

        return [.. secret.Cast
            .Select(member => member.Shown(secret.Id, gender))
            .Where(shown => shown.Pinned)
            .Select(shown => new Configuration.RunningStoryVoice(shown.Picture, story.Title, shown.Name, shown.Provider))];
    }

    /// <summary>What the cast of story <paramref name="id"/> needs and does not have here; empty when ready or when its hidden layer is not on disk.</summary>
    public IReadOnlyList<string> VoicesMissing(string id) =>
        catalog().Secret(id) is { } secret ? StoryVoices.Missing(secret, Gender(), VoicesHere(), Choices(), Ship()) : [];

    /// <summary>The provider the ship speaks through and the voices paired with the cast on it; null leaves every member in the story's voice.</summary>
    public Func<ShipSpeech?> Ship { get; set; } = () => null;

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
        var ship = Ship();

        return new StoryCastMember(secret.Id, title, shown, StoryVoices.Voice(member, shown, null, ship), StoryVoices.Voice(member, shown, choices, ship))
        {
            Role = member.Role,
            SpeaksName = choices.GetValueOrDefault(shown.Picture) is { } choice
                ? choice.VoiceName
                : shown.Pinned ? null : ship?.Pairings?.GetValueOrDefault(shown.Picture)?.VoiceName,
        };
    }

    /// <summary>Whether the cast of any story whose hidden layer is on disk speaks, in the Commander's version, through <paramref name="providerId"/>.</summary>
    public bool CastUses(string providerId)
    {
        var stories = catalog();

        return stories.Cards.Any(card => stories.Secret(card.Id) is { } secret && StoryVoices.Uses(secret, Gender(), providerId, Choices(), Ship()));
    }

    /// <summary>
    /// Who speaks a line by <paramref name="speaker"/> in the current story, decided now, at <paramref name="beat"/> of the
    /// current chapter as <see cref="CastAboard(string?, int?)"/> reads it; null when no story is current.
    /// </summary>
    public StoryLineVoice? LineVoice(string? frontierId, string? speaker, int? beat = null) =>
        stories.Current(frontierId) is { } story && Hidden(story.Id) is { } secret
            ? StoryVoices.Of(speaker, secret, Gender(), Aboard(), Choices(), Ship(), CastAboard(frontierId, story, secret, beat))
            : null;

    /// <summary>
    /// The current story's cast ids aboard the ship at <paramref name="beat"/> of its current chapter, -1 for the opening and by
    /// default the last beat that fired; for a story with no recorded chapter, those aboard by span. Empty when no story is current.
    /// </summary>
    public IReadOnlyList<string> CastAboard(string? frontierId, int? beat = null) =>
        stories.Current(frontierId) is { } story && Hidden(story.Id) is { } secret ? CastAboard(frontierId, story, secret, beat) : [];

    private IReadOnlyList<string> CastAboard(string? frontierId, Story story, StorySecret secret, int? beat = null) =>
        story.CurrentChapter is { } key && book.Store.Find(frontierId, key) is { Aboard: not null } chapter
            ? StoryAboard.At(chapter, beat ?? (book.Standing(frontierId, key)?.Fired.Count ?? 0) - 1)
            : StoryAboard.BySpan(secret, story);

    /// <summary>
    /// The story with its boardings and leavings as a chapter handed <paramref name="handed"/> opens: the current chapter's at
    /// its opening and its fired beats folded in, then each one the stage has passed applied.
    /// </summary>
    private Story Opening(string? frontierId, Story story, StorySecret secret, IReadOnlyList<string> handed, string? hull)
    {
        if (story.CurrentChapter is { } key)
        {
            if (book.Store.Find(frontierId, key) is { } onFile)
            {
                story = StoryAboard.Folded(story, onFile, book.Standing(frontierId, key)?.Fired.Count ?? 0);
            }
            else if (Archive.Find(frontierId, story, key) is { } archived)
            {
                story = StoryAboard.Folded(story, archived.Adventure, archived.Fired.Count);
            }
        }

        return StoryAboard.Missed(story, secret, handed, hull);
    }

    /// <summary>The Commander's notes on a story's cast member, by Frontier ID, story id and cast id; null for none.</summary>
    public Func<string?, string, string, string?> CastNotes { get; set; } = (_, _, _) => null;

    /// <summary>
    /// The current story's cast member <paramref name="castId"/> as this Commander meets them, for the Commander to answer;
    /// null when there is none, or the member has not been introduced, or the story is not running, is switched off or is
    /// without Odyssey.
    /// </summary>
    public AdventureSpeaker? Answering(string? frontierId, string castId) =>
        stories.Current(frontierId) is { State: StoryState.Running, IsOff: false } story
        && !WithoutOdyssey(frontierId)
        && Hidden(story.Id) is { } secret
        && secret.Cast.FirstOrDefault(member => string.Equals(member.Id, castId, StringComparison.Ordinal)) is { } member
        && StoryIntroductions.Met(story, secret, member.Id)
            ? new AdventureSpeaker(member.Id, member.Shown(secret.Id, Gender()).Name, member.Who)
            {
                Role = member.Role,
                Notes = CastNotes(frontierId, secret.Id, member.Id),
            }
            : null;

    /// <summary>The narrator's introduction owed before cast member <paramref name="castId"/> speaks in the current story, or null.</summary>
    public string? Introduction(string? frontierId, string? castId) =>
        stories.Current(frontierId) is { } story && Hidden(story.Id) is { } secret
            ? StoryIntroductions.Line(story, secret, Gender(), castId)
            : null;

    /// <summary>Records that the narrator said or posted the introduction of cast member <paramref name="castId"/> in story <paramref name="storyId"/>.</summary>
    public void Introduced(string? frontierId, string storyId, string castId) =>
        stories.Update(frontierId, storyId, story => story.IsCurrent && !story.Introduced.Contains(castId, StringComparer.Ordinal)
            ? story with { Introduced = [.. story.Introduced, castId] }
            : story);

    /// <summary>Whether the story has a cast member in two versions, so the Commander's gender must be set to pick it.</summary>
    public bool NeedsGenderFor(string id) => catalog().Secret(id)?.Cast.Any(speaker => speaker.Versions is not null) == true;

    /// <summary>A cast member of the current story as this Commander meets them, or null.</summary>
    public StorySpeakerShown? Speaker(string? frontierId, string castId) =>
        stories.Current(frontierId) is { } story ? catalog().Secret(story.Id)?.Speaker(castId, Gender()) : null;

    public StoryCatalog Catalog => catalog();

    /// <summary>The Commander's game state, for whether the ship they are in can reach the beacon.</summary>
    public Func<CommanderGameState?> Game { get; set; } = () => null;

    /// <summary>Every built-in Goals arc as it stands now, which a Goals story's clues and finale wait on; an empty list holds them.</summary>
    public Func<IReadOnlyList<GoalStanding>> Goals { get; set; } = () => [];

    /// <summary>How much the Commander likes each career, for the draw of a chapter's activities.</summary>
    public Func<Configuration.CareerRatingSettings?> Ratings { get; set; } = () => null;

    /// <summary>Raised as a chapter starts and stops being written, on the thread doing it.</summary>
    public event Action? WritingChanged;

    internal AdventureBook Book => book;

    /// <summary>Whether a chapter is being written for this Commander.</summary>
    public bool IsWriting(string? frontierId)
    {
        lock (_gate)
        {
            return _writing.Contains(frontierId ?? AdventureStore.NoCommander);
        }
    }

    /// <summary>How far the chapter being written for this Commander has got; null when none is.</summary>
    public StoryProgress? Progress(string? frontierId)
    {
        lock (_gate)
        {
            return _progress.GetValueOrDefault(frontierId ?? AdventureStore.NoCommander);
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

    /// <summary>The beat of the running story's chapter that <see cref="RefuseBeatAsync"/> would replace, narrowed to the step it waits on, or null.</summary>
    public AdventureBeat? RefusableBeat(string? frontierId) =>
        stories.Current(frontierId) is { State: StoryState.Running, CurrentChapter: { } key }
        && book.Standing(frontierId, key) is { IsRefusable: true } standing
            ? standing.CurrentStepBeat
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

    /// <summary>
    /// While the story files are swapped, skips the tick and journal events and refuses every chapter or beat write.
    /// False, and nothing held, while a chapter or a beat is being written for any Commander or another swap holds it.
    /// </summary>
    public bool HoldForSwitch()
    {
        lock (_gate)
        {
            if (_switching || _writing.Count > 0 || _rewriting.Count > 0 || _rewritingAdventures.Count > 0)
            {
                return false;
            }

            _switching = true;
            return true;
        }
    }

    /// <summary>Forgets the failed writes, narrated and remembered scans, openings and standings of the files swapped out, and ends the hold.</summary>
    public void ReleaseSwitch()
    {
        book.WalkAgain();

        lock (_gate)
        {
            _failed.Clear();
            _scans.Clear();
            _openings.Clear();
            _remembered.Clear();
            _switching = false;
        }
    }

    private bool Switching
    {
        get
        {
            lock (_gate)
            {
                return _switching;
            }
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
    public Task<string?> PickAsync(string? frontierId, string id, DateTimeOffset now, CancellationToken cancellationToken) =>
        PickAsync(frontierId, id, now, cancellationToken, null);

    private Task<string?> PickAsync(string? frontierId, string id, DateTimeOffset now, CancellationToken cancellationToken, string? carried)
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

        var before = carried ?? BackstoryBeforePick();
        setBackstory(card.InYourWords);

        var narrated = card.Pacing.NarratedScan;
        var remembered = narrated ? 0 : BeaconsOnRecord(frontierId, card.Id);
        var story = new Story
        {
            Id = card.Id,
            Title = card.Title,
            PublicLayer = card.Describe(withBeacon: remembered == 0),
            Length = card.Pacing.Key,
            PickedAt = now,
            BeaconScanAt = narrated || remembered > 0 ? now : null,
            BeaconNarrated = narrated,
            BeaconsRemembered = remembered,
            BackstoryBefore = before,
            GoalsAtPick = card.Goals ? GoalClimb.Overall(Goals()) : null,
        };

        stories.Save(frontierId, story);
        logger.LogInformation("Picked the story {Title}", card.Title);
        Began?.Invoke();

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

            if (remembered > 0)
            {
                _remembered[who] = new StoryRememberedDue(card.Id, card.Title);
            }
            else
            {
                _remembered.Remove(who);
            }
        }

        return WriteChapterAsync(frontierId, now, cancellationToken);
    }

    /// <summary>
    /// The beacon scans a story picked now would open with, 1 or 2: the most scanned in any other story of this Commander
    /// that recorded a real or remembered scan, in any state. 0 when none did, and the story flies to its own beacon.
    /// </summary>
    public int BeaconsOnRecord(string? frontierId, string pickingId) =>
        stories.BeaconForgotten(frontierId) ? 0 : ScansOnRecord(frontierId, pickingId);

    /// <summary>Whether any story of this Commander recorded a real or remembered scan, whether or not they asked to fly to a beacon again.</summary>
    public bool HasScanOnRecord(string? frontierId) => ScansOnRecord(frontierId, null) > 0;

    private int ScansOnRecord(string? frontierId, string? pickingId)
    {
        var most = stories.For(frontierId)
            .Where(story => !string.Equals(story.Id, pickingId, StringComparison.OrdinalIgnoreCase)
                            && (story.BeaconSystems.Count > 0 || story.BeaconsRemembered > 0))
            .Select(story => story.BeaconsScanned)
            .DefaultIfEmpty(0)
            .Max();

        return most == 0 ? 0 : Math.Clamp(most, 1, 2);
    }

    /// <summary>Whether picking this card now opens with the beacon scan behind the Commander, without a narrated scan line.</summary>
    public bool Remembers(string? frontierId, StoryCard card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return !card.Pacing.NarratedScan && BeaconsOnRecord(frontierId, card.Id) > 0;
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

        string? carried = null;

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

            carried = current.BackstoryBefore ?? "";
            Stop(frontierId, current, StoryState.Abandoned, now, restoreBackstory: false);
        }

        return PickAsync(frontierId, id, now, cancellationToken, carried);
    }

    /// <summary>Ends the current story and moves its chapters to the archive.</summary>
    public string? Abandon(string? frontierId, DateTimeOffset now)
    {
        if (stories.Current(frontierId) is not { } current)
        {
            return "No story is running.";
        }

        Stop(frontierId, current, StoryState.Ended, now, restoreBackstory: true);
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
            Began?.Invoke();
            return null;
        }

        stories.Save(frontierId, paused.Resumed(now) with { Chapters = [.. paused.Chapters.SkipLast(1)] });
        Began?.Invoke();
        return await WriteChapterAsync(frontierId, now, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Moves the current story forward so the target is due now: drops the current chapter and has the next one written
    /// at the new stage. Refused while a chapter is being written.
    /// </summary>
    public Task<string?> JumpAsync(string? frontierId, StoryJumpTarget target, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (IsWriting(frontierId))
        {
            return Task.FromResult<string?>("A chapter is being written.");
        }

        if (stories.Current(frontierId) is not { } current)
        {
            return Task.FromResult<string?>("No story is running.");
        }

        if (StoryJump.Jump(current, target, now) is not { } jumped)
        {
            return Task.FromResult(StoryJump.Refusal(current, target));
        }

        if (jumped.CurrentChapter is { } key)
        {
            book.Remove(frontierId, key);
        }

        stories.Save(frontierId, jumped.State == StoryState.Paused ? jumped : jumped.Paused(now));
        return ResumeAsync(frontierId, now, cancellationToken);
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

    /// <summary>The proposal held for this Commander, or null.</summary>
    public ObjectiveProposal? Proposal(string? frontierId)
    {
        lock (_gate)
        {
            return _proposals.GetValueOrDefault(frontierId ?? AdventureStore.NoCommander);
        }
    }

    /// <summary>
    /// Has the current objective and the ones after it written again from the Commander's remark, and holds the result as
    /// the Commander's proposal, replacing any held one. <paramref name="exchange"/> is the earlier rounds, which
    /// <see cref="ObjectiveProposal.Exchange"/> of the held proposal carries. Changes nothing in the chapter.
    /// </summary>
    public async Task<ObjectiveProposalOutcome> ReviseObjectiveAsync(
        string? frontierId, string remark, IReadOnlyList<AdventureRemark> exchange, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exchange);

        if (string.IsNullOrWhiteSpace(remark))
        {
            return new ObjectiveProposalOutcome(null, "Say what you would rather do.");
        }

        if (!TryRewritable(frontierId, out var at, out var refusal))
        {
            return new ObjectiveProposalOutcome(null, refusal);
        }

        if (!Claim(frontierId))
        {
            return new ObjectiveProposalOutcome(null, "A different objective is already being written.");
        }

        try
        {
            return await ProposeAsync(frontierId, at, remark.Trim(), exchange, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Writing a proposed objective failed");
            return new ObjectiveProposalOutcome(null, "The objective could not be written. Say it again and I will try a new draft.");
        }
        finally
        {
            Release(frontierId);
        }
    }

    /// <summary>Replaces the current objective and the ones after it with the held proposal. Returns a refusal or null; the refused proposal is dropped.</summary>
    public string? ApplyObjective(string? frontierId)
    {
        if (Proposal(frontierId) is not { } proposal)
        {
            return "There is no change to apply.";
        }

        if (Switching)
        {
            return Swapping;
        }

        if (IsWriting(frontierId) || IsRewriting(frontierId))
        {
            return "An objective is being written.";
        }

        var refusal = ApplyHeld(frontierId, proposal);

        if (refusal is not null)
        {
            DiscardObjective(frontierId);
        }

        return refusal;
    }

    /// <summary>Drops the held proposal.</summary>
    public void DiscardObjective(string? frontierId)
    {
        lock (_gate)
        {
            _proposals.Remove(frontierId ?? AdventureStore.NoCommander);
        }
    }

    /// <summary>The proposal held for this Commander's short story, or null.</summary>
    public ObjectiveProposal? AdventureProposal(string? frontierId, string key)
    {
        lock (_gate)
        {
            return _adventureProposals.GetValueOrDefault(AdventureClaim(frontierId, key));
        }
    }

    /// <summary>
    /// Has the current objective of the active short story <paramref name="key"/> and the ones after it written again from the
    /// Commander's remark, and holds the result as that story's proposal. Changes nothing in the adventure.
    /// </summary>
    public async Task<ObjectiveProposalOutcome> ReviseAdventureObjectiveAsync(
        string? frontierId, string key, string remark, IReadOnlyList<AdventureRemark> exchange, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exchange);

        if (string.IsNullOrWhiteSpace(remark))
        {
            return new ObjectiveProposalOutcome(null, "Say what you would rather do.");
        }

        if (!TryRewritableAdventure(frontierId, key, out var standing, out var refusal))
        {
            return new ObjectiveProposalOutcome(null, refusal);
        }

        if (!ClaimShortStory(frontierId, key))
        {
            return new ObjectiveProposalOutcome(null, "A different objective is already being written.");
        }

        try
        {
            var from = standing.Current;
            var beat = standing.CurrentBeat!;
            var trimmed = remark.Trim();
            var ask = ShortStoryAsk(frontierId, new AdventureRewrite(standing.Adventure, from, Remark: trimmed, Exchange: exchange, Step: standing.CurrentStep), null);
            var outcome = await write(ask, Clock(), cancellationToken).ConfigureAwait(false);

            if (outcome.Draft is not { } draft)
            {
                return new ObjectiveProposalOutcome(null, outcome.Message ?? "Nothing came back.");
            }

            if (!StillWaitingOn(frontierId, key, from))
            {
                return new ObjectiveProposalOutcome(null, "The story changed while the objective was being written.");
            }

            var proposal = new ObjectiveProposal(
                null, null, null, key, from, beat, draft, outcome.Reply ?? string.Empty, [.. exchange, new AdventureRemark(trimmed, outcome.Reply)], standing.CurrentStep);

            lock (_gate)
            {
                _adventureProposals[AdventureClaim(frontierId, key)] = proposal;
            }

            return new ObjectiveProposalOutcome(proposal, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Writing a proposed objective for a short story failed");
            return new ObjectiveProposalOutcome(null, "The objective could not be written. Say it again and I will try a new draft.");
        }
        finally
        {
            ReleaseShortStory(frontierId, key);
        }
    }

    /// <summary>Replaces the current objective of a short story and the ones after it with the held proposal. Returns a refusal or null; the refused proposal is dropped.</summary>
    public string? ApplyAdventureObjective(string? frontierId, string key)
    {
        if (AdventureProposal(frontierId, key) is not { } proposal)
        {
            return "There is no change to apply.";
        }

        if (Switching)
        {
            return Swapping;
        }

        if (IsRewriting(frontierId))
        {
            return "An objective is being written.";
        }

        var refusal = ApplyHeldAdventure(frontierId, proposal);

        if (refusal is not null)
        {
            DiscardAdventureObjective(frontierId, key);
        }

        return refusal;
    }

    /// <summary>Drops the proposal held for a short story.</summary>
    public void DiscardAdventureObjective(string? frontierId, string key)
    {
        lock (_gate)
        {
            _adventureProposals.Remove(AdventureClaim(frontierId, key));
        }
    }

    /// <summary>
    /// Replaces the current objective of a short story and the ones after it with new ones that do not ask for the replaced
    /// activity. Returns a refusal or null; on a refusal the adventure is as it was.
    /// </summary>
    public async Task<string?> RefuseAdventureBeatAsync(string? frontierId, string key, CancellationToken cancellationToken)
    {
        if (!TryRewritableAdventure(frontierId, key, out var standing, out var refusal))
        {
            return refusal;
        }

        if (!ClaimShortStory(frontierId, key))
        {
            return "A different objective is already being written.";
        }

        try
        {
            var from = standing.Current;
            var trigger = standing.CurrentTrigger!;
            var barred = Bars?.Barred(frontierId) ?? [];
            var replaced = RefusedActivities.Key(trigger);
            var ask = ShortStoryAsk(frontierId, new AdventureRewrite(standing.Adventure, from, Step: standing.CurrentStep), replaced is null ? barred : [.. barred.Union([replaced])]);
            var outcome = await write(ask, Clock(), cancellationToken).ConfigureAwait(false);

            if (outcome.Draft is not { } draft)
            {
                return outcome.Message ?? "Nothing came back.";
            }

            if (Replace(frontierId, key, standing, draft) is { } replacement)
            {
                return replacement;
            }

            logger.LogInformation("{Adventure}: beat {Beat} was replaced, refused", standing.Adventure.Name, from + 1);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Writing a replacement beat for a short story failed");
            return "The objective could not be written. Write it again and I will try a new draft.";
        }
        finally
        {
            ReleaseShortStory(frontierId, key);
        }
    }

    /// <summary>
    /// Fires the beat the running story's chapter is waiting on as if the journal had shown it; with no story running, the
    /// one active short story's. Returns a refusal or null.
    /// </summary>
    public string? MarkObjectiveDone(string? frontierId, DateTimeOffset now)
    {
        if (stories.Current(frontierId) is { CurrentChapter: { } chapter }
            && book.Standing(frontierId, chapter) is { Adventure.IsActive: true, IsDone: false })
        {
            return IsRunning(frontierId)
                ? MarkObjectiveDone(frontierId, chapter, now)
                : "Your story is paused. Resume it first, or press Mark done on the page of the story you mean.";
        }

        var shortStories = book.Standings(frontierId)
            .Where(standing => standing.Adventure is { StoryId: null, IsActive: true } && !standing.IsDone)
            .Select(standing => standing.Adventure)
            .ToList();

        return shortStories.Count switch
        {
            1 => MarkObjectiveDone(frontierId, shortStories[0].Key, now),
            0 => stories.Current(frontierId) is not { CurrentChapter: not null }
                ? "No story is under way."
                : IsRunning(frontierId) ? "The story is not waiting on an objective." : "Resume the story first.",
            _ => $"{string.Join(", ", shortStories.SkipLast(1).Select(adventure => adventure.Name))} and {shortStories[^1].Name} are under way. "
                 + "Press Mark done on the page of the one you mean.",
        };
    }

    /// <summary>
    /// Fires the beat the chapter or short story <paramref name="key"/> is waiting on as if the journal had shown it. With
    /// <paramref name="beat"/>, refuses when the story is no longer waiting on that beat. Returns a refusal or null.
    /// </summary>
    public string? MarkObjectiveDone(string? frontierId, string key, DateTimeOffset now, int? beat = null)
    {
        if (MarkDoneRefusal(frontierId, key) is { } refusal)
        {
            return refusal;
        }

        if (book.Standing(frontierId, key) is not { } standing)
        {
            return "There is no such story.";
        }

        if (beat is { } expected && standing.Current != expected)
        {
            return "The story has moved on from that objective.";
        }

        if (book.Step(frontierId, key, now, AdventureFold.CommanderSource) is { } stepped)
        {
            return stepped;
        }

        logger.LogInformation("{Adventure}: beat {Beat} was marked done by the Commander", standing.Adventure.Name, standing.Current + 1);
        return null;
    }

    /// <summary>Whether a story is waiting on a choice the Commander can make now, for the phrases that make it.</summary>
    public bool ChoiceOpen(string? frontierId) => Choosing(frontierId) is not null;

    /// <summary>Chooses option <paramref name="option"/>, from zero, of the open choice: the running story's chapter's, or with none the one short story's.</summary>
    public AdventureChoiceOutcome Choose(string? frontierId, int option, DateTimeOffset now) =>
        Choosing(frontierId) is { } standing
            ? book.Choose(frontierId, standing.Adventure.Key, option, now)
            : AdventureChoiceOutcome.Refused(AdventureChoiceOutcome.NoChoice);

    /// <summary>The standing whose choice is open: the running story's chapter, or with none the one short story waiting on a choice; null when neither is.</summary>
    private AdventureStanding? Choosing(string? frontierId)
    {
        if (Switching)
        {
            return null;
        }

        if (stories.Current(frontierId) is { CurrentChapter: { } chapter } && IsRunning(frontierId) && !WithoutOdyssey(frontierId)
            && book.Standing(frontierId, chapter) is { OpenChoice: not null } running)
        {
            return running;
        }

        var open = book.Standings(frontierId).Where(standing => standing.Adventure.StoryId is null && standing.OpenChoice is not null).ToList();

        return open.Count == 1 ? open[0] : null;
    }

    /// <summary>Why the objective of the chapter or short story <paramref name="key"/> cannot be marked done now, or null.</summary>
    public string? MarkDoneRefusal(string? frontierId, string key)
    {
        if (book.Standing(frontierId, key) is not { } standing)
        {
            return "There is no such story.";
        }

        if (!standing.Adventure.IsActive || standing.CurrentBeat is null)
        {
            return "That story is not waiting on an objective.";
        }

        if (standing.Adventure.StoryId is not null)
        {
            if (stories.Current(frontierId) is not { } story || !string.Equals(story.CurrentChapter, key, StringComparison.Ordinal))
            {
                return "That chapter is not the one your story is on.";
            }

            if (!IsRunning(frontierId))
            {
                return "Resume the story first.";
            }

            if (WithoutOdyssey(frontierId))
            {
                return NeedsOdyssey;
            }
        }

        if (Switching)
        {
            return Swapping;
        }

        if (IsWriting(frontierId) || IsRewriting(frontierId))
        {
            return "A different objective is being written.";
        }

        if (standing.CurrentTrigger is { Kind: TriggerKind.Beacon } beacon)
        {
            return "The beacon scan is the one objective the journal has to show. "
                   + (string.IsNullOrWhiteSpace(beacon.System) ? "Scan the beacon." : $"Scan the beacon in {beacon.System}.");
        }

        return null;
    }

    private string? ApplyHeldAdventure(string? frontierId, ObjectiveProposal proposal)
    {
        if (!StillWaitingOn(frontierId, proposal.ChapterKey, proposal.From)
            || book.Standing(frontierId, proposal.ChapterKey) is not { CurrentBeat: { } beat, CurrentTrigger: { } trigger } waiting
            || waiting.CurrentStep != proposal.Step
            || beat.Title != proposal.Current.Title
            || trigger.Describe() != proposal.Trigger.Describe())
        {
            return "The story has moved on from that objective.";
        }

        if (Replace(frontierId, proposal.ChapterKey, waiting, proposal.Draft) is { } refusal)
        {
            return refusal;
        }

        if (RefusedActivities.Key(proposal.Trigger) is { } replaced)
        {
            Bars?.Count(frontierId, replaced);
        }

        DiscardAdventureObjective(frontierId, proposal.ChapterKey);
        logger.LogInformation("{Adventure}: beat {Beat} was changed by the Commander", proposal.Draft.Name, proposal.From + 1);
        return null;
    }

    /// <summary>Replaces what <paramref name="standing"/> waits on from <paramref name="draft"/>: its current step when the beat has several, otherwise the beat and those after it.</summary>
    private string? Replace(string? frontierId, string key, AdventureStanding standing, Adventure draft)
    {
        var from = standing.Current;

        return standing.CurrentBeat is { Steps.Count: > 1 } && draft.Beats.ElementAtOrDefault(from)?.Steps.FirstOrDefault() is { } step
            ? book.ReplaceStep(frontierId, key, from, standing.CurrentStep, step, Clock())
            : book.ReplaceBeats(frontierId, key, from, [.. draft.Beats.Skip(from)], Clock());
    }

    private bool StillWaitingOn(string? frontierId, string key, int from) =>
        book.Standing(frontierId, key) is { Adventure.IsActive: true, IsDone: false } standing && standing.Current == from;

    private AdventureAsk ShortStoryAsk(string? frontierId, AdventureRewrite rewrite, IReadOnlyList<string>? barred) => new(
        AdventureReach.Session,
        Chapter: rewrite.Chapter.Follows is { } follows ? book.ChapterOf(frontierId, follows) : null,
        Rewrite: rewrite,
        Barred: barred);

    /// <summary>The standing of the short story <paramref name="key"/> when its current objective can be written again, or the refusal saying why not.</summary>
    private bool TryRewritableAdventure(string? frontierId, string key, [NotNullWhen(true)] out AdventureStanding? standing, [NotNullWhen(false)] out string? refusal)
    {
        standing = book.Standing(frontierId, key);
        refusal = null;

        if (standing is null)
        {
            refusal = "There is no such story.";
        }
        else if (standing.Adventure.StoryId is not null)
        {
            refusal = "That is a chapter of a story. Change its objective from the story.";
        }
        else if (!standing.Adventure.IsBegun)
        {
            refusal = "That story has not begun.";
        }
        else if (standing.Adventure.IsAbandoned)
        {
            refusal = "That story was abandoned.";
        }
        else if (standing.Adventure.IsFailed)
        {
            refusal = "That story failed.";
        }
        else if (standing.IsDone || standing.CurrentBeat is null)
        {
            refusal = "That story is done.";
        }
        else if (standing.InChoice)
        {
            refusal = AdventureStanding.ChoiceRefusal;
        }
        else
        {
            return true;
        }

        return false;
    }

    private bool ClaimShortStory(string? frontierId, string key)
    {
        if (!Claim(frontierId))
        {
            return false;
        }

        if (!ClaimAdventure(frontierId, key))
        {
            Release(frontierId);
            return false;
        }

        return true;
    }

    private void ReleaseShortStory(string? frontierId, string key)
    {
        lock (_gate)
        {
            _rewritingAdventures.Remove(AdventureClaim(frontierId, key));
        }

        Release(frontierId);
    }

    private async Task<ObjectiveProposalOutcome> ProposeAsync(
        string? frontierId, BeatAt at, string remark, IReadOnlyList<AdventureRemark> exchange, CancellationToken cancellationToken)
    {
        if (Hidden(at.Story.Id) is not { } secret)
        {
            return new ObjectiveProposalOutcome(null, $"The hidden layer of {at.Story.Title} is missing from this build.");
        }

        var chapter = at.Standing.Adventure;
        var from = at.Standing.Current;
        var now = Clock();
        var ask = await RewriteAskAsync(frontierId, at.Story, secret, chapter, new AdventureRewrite(chapter, from, Remark: remark, Exchange: exchange, Step: at.Standing.CurrentStep), now, cancellationToken).ConfigureAwait(false);
        var outcome = await write(ask, now, cancellationToken).ConfigureAwait(false);

        if (outcome.Draft is not { } draft)
        {
            return new ObjectiveProposalOutcome(null, outcome.Message ?? "Nothing came back.");
        }

        if (!Unchanged(frontierId, at.Story))
        {
            return new ObjectiveProposalOutcome(null, "The story changed while the objective was being written.");
        }

        var proposal = new ObjectiveProposal(
            at.Story.Id,
            at.Story.PickedAt,
            at.Story.Chapters.Count,
            chapter.Key,
            from,
            at.Beat,
            draft,
            outcome.Reply ?? string.Empty,
            [.. exchange, new AdventureRemark(remark, outcome.Reply)]);

        lock (_gate)
        {
            _proposals[frontierId ?? AdventureStore.NoCommander] = proposal;
        }

        return new ObjectiveProposalOutcome(proposal, null);
    }

    private string? ApplyHeld(string? frontierId, ObjectiveProposal proposal)
    {
        if (stories.Current(frontierId) is not { State: StoryState.Running } still
            || still.Id != proposal.StoryId
            || still.PickedAt != proposal.PickedAt
            || still.Chapters.Count != proposal.Chapters
            || still.CurrentChapter != proposal.ChapterKey)
        {
            return "The story changed while the objective was being written.";
        }

        if (book.Standing(frontierId, proposal.ChapterKey) is not { IsDone: false } standing
            || standing.Current != proposal.From
            || standing.CurrentBeat is not { } beat
            || standing.CurrentStep != proposal.Step
            || beat.Title != proposal.Current.Title
            || standing.CurrentTrigger!.Describe() != proposal.Trigger.Describe())
        {
            return "The story has moved on from that objective.";
        }

        if (Hidden(still.Id) is not { } secret)
        {
            return $"The hidden layer of {still.Title} is missing from this build.";
        }

        var draft = StoryIntroductions.Introduce(proposal.Draft, still, secret, Gender(), proposal.From);

        if (Replace(frontierId, proposal.ChapterKey, standing, draft) is { } refusal)
        {
            return refusal;
        }

        if (!still.ChangedChapters.Contains(proposal.ChapterKey))
        {
            stories.Save(frontierId, still with { ChangedChapters = [.. still.ChangedChapters, proposal.ChapterKey] });
        }

        if (RefusedActivities.Key(proposal.Trigger) is { } replaced)
        {
            Bars?.Count(frontierId, replaced);
        }

        DiscardObjective(frontierId);
        logger.LogInformation("{Title}: beat {Beat} of {Chapter} was changed by the Commander", still.Title, proposal.From + 1, proposal.Draft.Name);
        return null;
    }

    /// <summary>The story with the Commander's barred activities added to what the story refused. Not saved.</summary>
    private Story WithBar(string? frontierId, Story story) =>
        Bars?.Barred(frontierId) is { Count: > 0 } barred ? story with { Refused = [.. story.Refused.Union(barred)] } : story;

    private bool Unchanged(string? frontierId, Story story) =>
        stories.Current(frontierId) is { State: StoryState.Running } still
        && still.PickedAt == story.PickedAt
        && still.Chapters.Count == story.Chapters.Count;

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
            && at.Standing.CurrentTrigger is { Kind: TriggerKind.Dock } trigger
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
                && standing.CurrentTrigger is { Kind: TriggerKind.Dock } beat
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
            ? book.Standing(frontierId, key)?.CurrentObjective?.HandOff()
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
            return !_switching && _rewritingAdventures.Add(AdventureClaim(frontierId, key));
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
                Rewrite: new AdventureRewrite(adventure, from, closedMarketId, Step: standing.CurrentStep));

            var outcome = await write(ask, Clock(), CancellationToken.None).ConfigureAwait(false);

            if (outcome.Draft is not { } draft)
            {
                return outcome.Message ?? "Nothing came back.";
            }

            if (Replace(frontierId, adventure.Key, standing, draft) is { } refusal)
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
        else if (standing.InChoice)
        {
            refusal = AdventureStanding.ChoiceRefusal;
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
            if (_switching || !_rewriting.Add(frontierId ?? AdventureStore.NoCommander))
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
            return "The objective could not be written. Write it again and I will try a new draft.";
        }
        finally
        {
            Release(frontierId);
        }
    }

    private void Release(string? frontierId)
    {
        lock (_gate)
        {
            _rewriting.Remove(frontierId ?? AdventureStore.NoCommander);
        }

        WritingChanged?.Invoke();
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
        var refusedKey = closedMarketId is null && standing.CurrentTrigger is { } refusedTrigger ? RefusedActivities.Key(refusedTrigger) : null;
        var refused = refusedKey is null || story.Refused.Contains(refusedKey) ? story.Refused : [.. story.Refused, refusedKey];
        var now = Clock();
        var ask = await RewriteAskAsync(frontierId, story with { Refused = refused }, secret, chapter, new AdventureRewrite(chapter, from, closedMarketId, Step: standing.CurrentStep), now, cancellationToken).ConfigureAwait(false);

        var outcome = await write(ask, now, cancellationToken).ConfigureAwait(false);

        if (outcome.Draft is not { } draft)
        {
            return outcome.Message ?? "Nothing came back.";
        }

        if (stories.Current(frontierId) is not { State: StoryState.Running } still
            || still.PickedAt != story.PickedAt
            || still.Chapters.Count != story.Chapters.Count)
        {
            return "The story changed while the objective was being written.";
        }

        draft = StoryIntroductions.Introduce(draft, still, secret, Gender(), from);

        if (Replace(frontierId, chapter.Key, standing, draft) is { } refusal)
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

    /// <summary>The ask that writes the beats of the story's chapter from <paramref name="rewrite"/>'s index on again.</summary>
    private async Task<AdventureAsk> RewriteAskAsync(
        string? frontierId, Story story, StorySecret secret, Adventure chapter, AdventureRewrite rewrite, DateTimeOffset now, CancellationToken cancellationToken)
    {
        story = WithBar(frontierId, story);
        var refused = story.Refused;

        // The Story as it stood when this chapter was written: before the chapter itself was counted.
        var before = story with { Chapters = [.. story.Chapters.SkipLast(1)] };
        var number = story.Chapters.Count;
        var finaleChapter = story.FinaleChapter;
        var stage = finaleChapter is null ? StoryClues.Stage(before) : StoryStage.Finale;
        var beaconBeat = chapter.Beats[^1].Steps[^1].Trigger is { Kind: TriggerKind.Beacon, SystemAddress: { } address } trigger
            ? new AdventureBeacon(address, trigger.System ?? GuardianCores.Beacons.GetValueOrDefault(address) ?? string.Empty)
            : null;
        var reach = story.BeaconScanAt is null && beaconBeat is null
            ? BeaconReach.Of(here(), Game()?.Ship ?? ShipLoadout.Unknown, Game()?.Carrier.Owned == true)
            : null;
        var game = Game();
        var previous = ChapterAt(frontierId, story, story.Chapters.Count - 2);
        var afterBeacon = story.BeaconScanAt is { } scanned && chapter.Written >= scanned;
        var slots = afterBeacon
            ? await RewriteSlotsAsync(story, chapter, rewrite, game, previous, number, finaleChapter, cancellationToken).ConfigureAwait(false)
            : null;
        var rank = chapter.Beats.SelectMany(beat => beat.Steps).Select(step => step.Trigger).FirstOrDefault(trigger => trigger.Kind == TriggerKind.Rank);
        var longHaul = ChapterFit.IsLongHaul(story.Pacing, game)
            && (!afterBeacon || finaleChapter is not null || ChapterFit.ServesLongHaul(slots, rank));

        return new AdventureAsk(
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
                beaconBeat,
                reach is { InReach: false, Why: { } why } ? new AdventureBeaconAway(reach.System, reach.LightYears, why, reach.Unarmed, reach.Carrier) : null,
                reach?.InReach != false,
                longHaul,
                slots,
                finaleChapter is null ? null : story.FinaleDestination),
            Rewrite: rewrite);
    }

    /// <summary>
    /// The drawn activities a rewritten chapter is still built on: those its beats already hold, except the one at the beat
    /// being replaced, which is drawn again when the Commander refused it. Null when the chapter holds none.
    /// </summary>
    private async Task<IReadOnlyList<DrawnActivity>?> RewriteSlotsAsync(
        Story story, Adventure chapter, AdventureRewrite rewrite, CommanderGameState? game, AdventureChapter? previous, int number, int? finaleChapter,
        CancellationToken cancellationToken)
    {
        var statistics = game?.Statistics ?? CareerStatistics.Empty;
        var held = chapter.Beats
            .SelectMany((beat, index) => beat.Steps.Select(step => (Trigger: step.Trigger, Index: index)))
            .Select(step => (step.Trigger, step.Index, Row: ChapterActivities.Find(step.Trigger.Kind, step.Trigger.MissionFamily, step.Trigger.Organic)))
            .Where(slot => slot.Row is not null)
            .ToList();
        var others = held
            .Where(slot => slot.Index != rewrite.From)
            .Select(slot => new DrawnActivity(slot.Row!.Kind, slot.Row.Family, slot.Trigger.Count ?? slot.Row.Min) { Organic = slot.Trigger.Organic, PlaceBefore = slot.Row.PlaceBefore })
            .ToList();

        if (rewrite is { Remark: null, ClosedMarketId: null } && held.Any(slot => slot.Index == rewrite.From))
        {
            var refused = story.Refused.Concat(others.Select(slot => RefusedActivities.Key(slot.Kind, slot.Family)).OfType<string>()).ToList();
            var redrawn = ChapterDraw.Draw(
                Weights(statistics),
                game?.Ranks ?? RankState.Empty,
                game?.Ship ?? ShipLoadout.Unknown,
                odyssey: true,
                previous is null ? [] : AdventureGenerator.RecentActivities(previous),
                refused,
                comfort: false,
                statistics,
                FixedPlaces(story, finaleChapter) + others.Count(slot => slot.PlaceBefore),
                ChapterDraw.Seed(story.Id, number, rewrite.From),
                stationsInReach: await StationsInReachAsync(story, game, cancellationToken).ConfigureAwait(false));

            others.AddRange(redrawn.Take(1));
        }

        return others.Count > 0 ? others : null;
    }

    /// <summary>Whether the ledger has no completion of a mission family; null while there is no ledger.</summary>
    private Func<string, bool>? NeverCompleted() =>
        Ledger?.Invoke() is { } ledger
            ? family => RefusedActivities.Key(TriggerKind.Mission, family) is { } key && ledger.LastDone(key) is null
            : null;

    /// <summary>
    /// Whether a fixed station with a pad for the ship aboard, in a system needing no permit, lies within the chapter's reach.
    /// True when it cannot be told: galaxy search off, no known position, or the query failed.
    /// </summary>
    private async Task<bool> StationsInReachAsync(Story story, CommanderGameState? game, CancellationToken cancellationToken)
    {
        if (Galaxy?.Invoke() is not { } search || game?.Location.StarSystem is not { Length: > 0 } system)
        {
            return true;
        }

        var ship = game.Ship;
        PadSize? pad = Enum.TryParse<PadSize>(EliteSpecifications.Ship(ship.Type)?.Pad, ignoreCase: true, out var size) ? size : null;

        try
        {
            var found = await search
                .FindStationsAsync(StationQuery.Near(system, AdventureGenerator.Radius(AdventureReach.Session, ship.MaxJumpRange, game.Carrier.Owned), 20), cancellationToken)
                .ConfigureAwait(false);

            return found.Stations.Any(station =>
                station.IsFixed && !PermitSystemTable.Locked(station.SystemName) && (pad is not { } needed || station.Admits(needed)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The galaxy search could not list the stations within reach of a story chapter, so its activities are not gated on them");
            return true;
        }
    }

    /// <summary>The place objectives a finale chapter must already have: the landing of the last one, or the stop nearer the destination.</summary>
    private static int FixedPlaces(Story story, int? finaleChapter) =>
        finaleChapter is { } finale && (finale > 1 || story.Pacing.FinaleChapters <= 1) ? 1 : 0;

    private CareerWeights Weights(CareerStatistics statistics)
    {
        var rating = Ratings();

        return CareerWeights.From(
            rating is null
                ? null
                : new Dictionary<Career, int>
                {
                    [Career.Combat] = rating.Combat,
                    [Career.Trading] = rating.Trading,
                    [Career.Mining] = rating.Mining,
                    [Career.Exploration] = rating.Exploration,
                    [Career.Exobiology] = rating.Exobiology,
                    [Career.Passengers] = rating.Passengers,
                    [Career.SearchAndRescue] = rating.SearchAndRescue,
                    [Career.Crime] = rating.Crime,
                },
            statistics);
    }

    /// <summary>
    /// Pauses a story whose chapter was abandoned or removed, starts the next chapter on the pool when one finishes,
    /// and archives a finished story's chapters on the pool. Returns the work it started, or null. Does not block.
    /// </summary>
    public Task<string?>? Tick(string? frontierId, DateTimeOffset now)
    {
        if (!Switching && StoreFailures(frontierId) is { } storing)
        {
            return storing;
        }

        if (Switching || stories.Current(frontierId) is not { } story || story.CurrentChapter is not { } key)
        {
            return null;
        }

        var standing = book.Standing(frontierId, key);

        if (story.State == StoryState.Running && standing is { Adventure.IsFailed: true })
        {
            return story.IsWithoutOdyssey || WriteFailed(frontierId) || IsWriting(frontierId)
                ? null
                : Task.Run(() => WriteChapterAsync(frontierId, now, CancellationToken.None));
        }

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
            Began?.Invoke();
            return null;
        }

        if (story.State != StoryState.Running || story.IsWithoutOdyssey || standing is not { IsDone: true } || standing is { Reached: not null, Adventure.IsEnded: false }
            || WriteFailed(frontierId) || IsWriting(frontierId))
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
                RestoreBackstory(story);
                ArchiveChapters(frontierId);
                return (string?)null;
            });
        }

        return Task.Run(() => WriteChapterAsync(frontierId, now, CancellationToken.None));
    }

    /// <summary>Hands each chapter the fold failed or ended in an option's ending, and no ending is stored for, to the pool once; null when there is none.</summary>
    private Task<string?>? StoreFailures(string? frontierId)
    {
        var commander = frontierId ?? AdventureStore.NoCommander;
        var failing = book.Unstored(frontierId);
        List<string> keys = [];

        lock (_gate)
        {
            foreach (var standing in failing)
            {
                if (_failing.Add(AdventureClaim(frontierId, standing.Adventure.Key)))
                {
                    keys.Add(standing.Adventure.Key);
                }
            }
        }

        if (keys.Count == 0)
        {
            return null;
        }

        return Task.Run(() =>
        {
            foreach (var key in keys)
            {
                try
                {
                    if (book.StoreEnding(frontierId, key) is { } refusal)
                    {
                        logger.LogWarning("The ending of {Key} was not stored for {Commander}: {Refusal}", key, commander, refusal);
                    }
                }
                finally
                {
                    lock (_gate)
                    {
                        _failing.Remove(AdventureClaim(frontierId, key));
                    }
                }
            }

            return (string?)null;
        });
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

        if (Switching)
        {
            return null;
        }

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

        if (journalEvent.StartsSession
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

        var woke = RecordScan(frontierId, current.Id, address, journalEvent.Timestamp);

        logger.LogInformation("{Title}: the beacon in {System} was scanned", current.Title, GuardianCores.Beacons[address]);
        return woke;
    }

    /// <summary>
    /// Records the scan of the beacon at <paramref name="address"/> on the story and returns what it woke when it lifted
    /// the hold on the cores. Null when the address was already recorded.
    /// </summary>
    private CoreWaking? RecordScan(string? frontierId, string storyId, long address, DateTimeOffset at)
    {
        CoreWaking? woke = null;
        var added = false;

        stories.Update(frontierId, storyId, story =>
        {
            if (story.BeaconSystems.Contains(address))
            {
                return story;
            }

            added = true;

            var held = story.HeldCores;
            var scanned = story with
            {
                BeaconScanAt = story.BeaconScanAt ?? at,
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

        if (added)
        {
            stories.SetBeaconForgotten(frontierId, false);
        }

        return woke;
    }

    /// <summary>
    /// Records a stepped beacon beat's scan on the current story. The waking waits for <see cref="TakeSteppedWaking"/>,
    /// so only the tick wakes the cores. Returns a refusal or null.
    /// </summary>
    public string? RecordSteppedScan(string? frontierId, long? address, DateTimeOffset now)
    {
        if (address is not { } beacon || !GuardianCores.Beacons.ContainsKey(beacon))
        {
            return "The beacon beat has no beacon system to scan.";
        }

        if (stories.Current(frontierId) is not { } current)
        {
            return "There is no story to record the scan on.";
        }

        if (RecordScan(frontierId, current.Id, beacon, now) is { } waking)
        {
            lock (_gate)
            {
                _steppedWakings[frontierId ?? AdventureStore.NoCommander] = waking;
            }
        }

        logger.LogInformation("{Title}: the beacon in {System} was stepped", current.Title, GuardianCores.Beacons[beacon]);
        return null;
    }

    /// <summary>The waking a stepped beacon scan left, once; the tick adds it to the wakings.</summary>
    public CoreWaking? TakeSteppedWaking(string? frontierId)
    {
        lock (_gate)
        {
            return _steppedWakings.Remove(frontierId ?? AdventureStore.NoCommander, out var waking) ? waking : null;
        }
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
    /// The remembered scan of the story just picked, once, while it is still the running story; the caller brings the
    /// story's core aboard after the opening. Null when none waits.
    /// </summary>
    public StoryRememberedDue? TakeRememberedScan(string? frontierId)
    {
        StoryRememberedDue? due;

        lock (_gate)
        {
            if (!_remembered.Remove(frontierId ?? AdventureStore.NoCommander, out due))
            {
                return null;
            }
        }

        return stories.Current(frontierId) is { State: StoryState.Running, BeaconsRemembered: > 0 } story
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

        if (on)
        {
            Began?.Invoke();
        }

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

    /// <summary>
    /// What a recap may say: of the active short story at <paramref name="adventureKey"/> when one is given; otherwise of the
    /// current stock story, or else of the active short story whose objective fired last. Null when there is none.
    /// </summary>
    public RecapFacts? Recap(string? frontierId, string? adventureKey = null)
    {
        if (adventureKey is not null)
        {
            return book.Standing(frontierId, adventureKey) is { Adventure: { StoryId: null, IsActive: true }, IsDone: false } standing
                ? StoryRecap.Of(standing)
                : null;
        }

        if (stories.Current(frontierId) is { } story)
        {
            return Recap(frontierId, story);
        }

        return book.Active(frontierId)
            .Where(standing => standing.Adventure.StoryId is null)
            .MaxBy(standing => standing.LastFiredAt ?? standing.Adventure.AcceptedAt) is { } shortStory
            ? StoryRecap.Of(shortStory)
            : null;
    }

    private RecapFacts Recap(string? frontierId, Story story)
    {
        var secret = Hidden(story.Id);
        var commander = Game()?.Identity.Name;

        string? Named(string? speaker) => speaker switch
        {
            StorySpeaker.Narrator => null,
            null or StorySpeaker.Ship => Aboard()?.Name ?? "The core",
            _ => secret?.Cast.FirstOrDefault(member => string.Equals(member.Id, speaker, StringComparison.Ordinal))?.Shown(secret.Id, Gender()).Name,
        };

        var fired = new List<RecapObjective>();

        foreach (var key in story.Chapters)
        {
            if (book.Store.Find(frontierId, key) is { } chapter)
            {
                fired.AddRange(StoryRecap.Fired(chapter, book.Standing(frontierId, key)?.Fired.Count ?? 0, Named));
            }
            else if (Archive.Find(frontierId, story, key) is { } archived)
            {
                fired.AddRange(StoryRecap.Fired(archived.Adventure, archived.Fired.Count, Named));
            }
        }

        var current = story.CurrentChapter is { } at ? book.Standing(frontierId, at) : null;

        return new RecapFacts
        {
            Title = story.Title,
            Backstory = catalog().Find(story.Id)?.InYourWords,
            Fired = fired,
            Clues = secret is null
                ? []
                : [.. Enumerable.Range(0, Math.Min(story.CluesGiven, story.Pacing.Lines))
                    .Select(index => StoryClues.Text(secret, story.Pacing, index))
                    .Where(clue => !string.IsNullOrWhiteSpace(clue))
                    .Select(clue => StorySecret.ForCommander(clue!, commander, Gender()))],
            HandOff = current is { Adventure.IsAbandoned: false, CurrentTrigger: { } trigger } ? StoryRecap.HandOff(trigger, current.Counted) : null,
            Paused = story.State == StoryState.Paused,
        };
    }

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
            ? StoryClues.Due(story, now, () => GoalClimb.Overall(Goals()))
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
            ? StoryVoices.Of(line.Speaker, secret, Gender(), Aboard(), Choices(), Ship(), CastAboard(frontierId, story, secret))
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

    /// <summary>
    /// The current story's window for cast reactions, or null unless it is running, switched on and played with Odyssey,
    /// its chapter one has begun, and it has a primary cast member this Commander meets.
    /// </summary>
    public StoryReactionWindow? ReactionWindow(string? frontierId)
    {
        if (stories.Current(frontierId) is not { State: StoryState.Running, IsOff: false, IsWithoutOdyssey: false, CurrentChapter: not null } story
            || Hidden(story.Id) is not { } secret
            || Reactors(story, secret).Count == 0)
        {
            return null;
        }

        lock (_gate)
        {
            var who = frontierId ?? AdventureStore.NoCommander;

            if (_openings.ContainsKey(who) || _scans.ContainsKey(who) || _remembered.ContainsKey(who))
            {
                return null;
            }
        }

        return new StoryReactionWindow(frontierId, story.Id, story.Sessions, story.ReactionSession == story.Sessions);
    }

    /// <summary>The primary cast, as this Commander meets them, who may give a reaction still owed; empty once it is not.</summary>
    public IReadOnlyList<StoryReactor> ReactionCast(string? frontierId, StoryReactionDue due)
    {
        ArgumentNullException.ThrowIfNull(due);

        return ReactionWindow(frontierId) is { Reacted: false } window
               && window.Due == due
               && stories.Current(frontierId) is { } story
               && Hidden(due.StoryId) is { } secret
            ? Reactors(story, secret)
            : [];
    }

    /// <summary>The primary cast this Commander meets who have been introduced or need no introduction.</summary>
    private List<StoryReactor> Reactors(Story story, StorySecret secret)
    {
        var gender = Gender();

        return [.. secret.Cast
            .Where(member => member.Primary && (member.Versions is null || CommanderGender.IsSet(gender)) && StoryIntroductions.Met(story, secret, member.Id))
            .Select(member => new StoryReactor(member.Id, member.Shown(secret.Id, gender).Name, member.Who))];
    }

    /// <summary>Records that a reaction was spoken, so the story gives no other this session.</summary>
    public void ReactionGiven(string? frontierId, StoryReactionDue due)
    {
        ArgumentNullException.ThrowIfNull(due);

        stories.Update(frontierId, due.StoryId, story => story.IsCurrent && story.Sessions == due.Session
            ? story with { ReactionSession = due.Session }
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

    private void Stop(string? frontierId, Story story, StoryState state, DateTimeOffset now, bool restoreBackstory)
    {
        if (story.CurrentChapter is { } key && book.Standing(frontierId, key) is { Adventure.IsActive: true, IsDone: false })
        {
            book.Abandon(frontierId, key, now);
        }

        stories.Save(frontierId, story with { State = state, StoppedAt = now });
        logger.LogInformation("{Title} is {State}", story.Title, state);

        if (restoreBackstory)
        {
            RestoreBackstory(story);
        }

        ArchiveChapters(frontierId);
    }

    /// <summary>The Backstory to keep across a pick: empty when none is set or it is a catalog card's words.</summary>
    private string BackstoryBeforePick()
    {
        var current = Backstory();

        return string.IsNullOrWhiteSpace(current)
            || catalog().Cards.Any(card => string.Equals(card.InYourWords, current, StringComparison.Ordinal))
            ? ""
            : current;
    }

    private void RestoreBackstory(Story story) => setBackstory(story.BackstoryBefore ?? "");

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
            if (_switching)
            {
                return Swapping;
            }

            if (!_writing.Add(commander))
            {
                return "A chapter is already being written.";
            }

            _failed.Remove(commander);
            _progress[commander] = new StoryProgress(Time);
        }

        WritingChanged?.Invoke();

        try
        {
            var attempt = new Attempt(null);
            var stationsInReach = stories.Current(frontierId) is { BeaconScanAt: not null } running
                ? await StationsInReachAsync(running, Game(), cancellationToken).ConfigureAwait(false)
                : true;

            for (var draft = 1; draft <= Drafts; draft++)
            {
                if (draft > 1)
                {
                    logger.LogInformation("Writing draft {Draft} of {Drafts} of a story chapter", draft, Drafts);
                    Progress(frontierId)?.NextDraft();
                    WritingAgain?.Invoke(WritingAgainLine(draft, attempt.Detail is not null));
                }

                // A throw counts as a failure, so the tick does not start another paid write on every pass.
                try
                {
                    attempt = await WriteAsync(frontierId, now, draft, attempt.Detail, draft == 2 ? attempt.Outline : null, stationsInReach, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Writing a story chapter failed");
                    attempt = new Attempt(AdventureOutcome.NotWritten, Retry: !cancellationToken.IsCancellationRequested);
                }

                if (!attempt.Retry)
                {
                    break;
                }
            }

            var refusal = attempt.Retry ? NoDraftStood : attempt.Refusal;

            if (refusal is null)
            {
                Progress(frontierId)?.Done();
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
                _progress.Remove(commander);
            }

            WritingChanged?.Invoke();
        }
    }

    /// <summary>How many drafts a chapter gets before the write stops and waits for the Commander.</summary>
    public const int Drafts = 3;

    /// <summary>The refusal once every draft has failed.</summary>
    public static readonly string NoDraftStood =
        $"The chapter could not be written in {Drafts.ToString(CultureInfo.InvariantCulture)} drafts. Write it again and I will try {Drafts.ToString(CultureInfo.InvariantCulture)} more.";

    /// <summary>The clock the progress of a chapter write runs on.</summary>
    public TimeProvider Time { get; init; } = TimeProvider.System;

    /// <summary>Raised with the line to say before each draft after the first, on the thread doing the write.</summary>
    public event Action<string>? WritingAgain;

    private static readonly string[] Ordinals = ["first", "second", "third", "fourth", "fifth"];

    /// <summary>What is said before <paramref name="draft"/>; <paramref name="told"/> is whether that draft is told what went wrong.</summary>
    public static string WritingAgainLine(int draft, bool told)
    {
        var failed = Ordinals[Math.Clamp(draft - 2, 0, Ordinals.Length - 1)];
        var opening = draft == 2
            ? $"The {failed} draft of the chapter could not be used, so I am writing another."
            : $"The {failed} draft could not be used either, so I am writing {(draft == Drafts ? "a final one" : "another")}.";

        return opening + (told
            ? " The new draft is told what was wrong with the one before, so it can avoid the same mistake."
            : " No draft came back that time, and a fault like that does not usually repeat.");
    }

    /// <summary>One draft's result: the refusal or null, whether another draft may succeed, and what to tell it.</summary>
    private sealed record Attempt(string? Refusal, bool Retry = false, string? Detail = null, AdventureOutline? Outline = null);

    private async Task<Attempt> WriteAsync(
        string? frontierId, DateTimeOffset now, int draftNumber, string? lastDraftRefused, AdventureOutline? outline, bool stationsInReach, CancellationToken cancellationToken)
    {
        if (stories.Current(frontierId) is not { State: StoryState.Running } story)
        {
            return new("No story is running.");
        }

        if (Hidden(story.Id) is not { } secret)
        {
            return new($"The hidden layer of {story.Title} is missing from this build.");
        }

        var replacing = story.CurrentChapter is { } failedKey && book.Store.Find(frontierId, failedKey) is { IsFailed: true };

        if (!replacing && story.FinaleChapter >= story.Pacing.FinaleChapters)
        {
            return new($"{story.Title} has had its last chapter.");
        }

        story = WithBar(frontierId, story);
        var number = replacing ? story.Chapters.Count : story.Chapters.Count + 1;
        AdventureChapter? previous = null;
        AdventureChapter? failed = null;

        if (replacing)
        {
            failed = ChapterAt(frontierId, story, story.Chapters.Count - 1);
            previous = ChapterAt(frontierId, story, story.Chapters.Count - 2);

            if (failed is null || (story.Chapters.Count > 1 && previous is null))
            {
                return new("The chapter before is no longer on file.");
            }
        }
        else if (story.CurrentChapter is not null
            && (previous = ChapterAt(frontierId, story, story.Chapters.Count - 1)) is null)
        {
            return new("The chapter before is no longer on file.");
        }

        var finaleFrom = replacing ? story.FinaleFrom : story.FinaleFrom ?? (FinaleDue(story, now) ? number : null);
        var finaleChapter = number - finaleFrom + 1;
        var stage = finaleChapter is null ? StoryClues.Stage(story) : StoryStage.Finale;
        var reach = story.BeaconScanAt is null
            ? BeaconReach.Of(here(), Game()?.Ship ?? ShipLoadout.Unknown, Game()?.Carrier.Owned == true)
            : null;

        var game = Game();
        var sinceBeacon = SinceBeacon(frontierId, story) + (replacing ? 0 : 1);
        var beaconChapter = reach is { InReach: true };
        var home = HomeLeg.Next(story.HomeTarget, game?.Location.StarPos, replacing ? story.Chapters.SkipLast(1).ToList() : story.Chapters, story.ChangedChapters, finaleChapter is not null || beaconChapter);

        var statistics = game?.Statistics ?? CareerStatistics.Empty;
        var goal = story.BeaconScanAt is not null && finaleChapter is null && catalog().Find(story.Id)?.Goals == true
            ? GoalClimb.Next(
                game,
                Mine?.Invoke(),
                Engineers?.Invoke(),
                row => ChapterDraw.Drawable(row, ChapterActivities.Held(game?.Ranks ?? RankState.Empty, game?.Ship ?? ShipLoadout.Unknown, odyssey: true), stationsInReach, story.Refused))
            : null;
        var slots = story.BeaconScanAt is null
            ? null
            : ChapterDraw.Draw(
                Weights(statistics),
                game?.Ranks ?? RankState.Empty,
                game?.Ship ?? ShipLoadout.Unknown,
                odyssey: true,
                previous is null ? [] : AdventureGenerator.RecentActivities(previous),
                story.Refused,
                ChapterFit.IsComfortChapter(sinceBeacon),
                statistics,
                FixedPlaces(story, finaleChapter) + (finaleChapter is null && (ChapterBand.Of(game?.Location.StarPos) is not null || home is not null) ? 1 : 0),
                ChapterDraw.Seed(story.Id, number, replacing ? -1 : null),
                sinceBeacon / ChapterFit.ComfortEvery,
                stationsInReach,
                NeverCompleted(),
                Tools is null ? null : StepEventCatalogue.Default,
                Tools?.Invoke(),
                goal?.Objective is null ? ChapterDraw.Slots : ChapterDraw.Slots - 1,
                goal?.Draw is null ? null : goal.Earns) is { Count: > 0 } drawn
                ? drawn
                : null;

        // A limited draw that fell back to the whole table works no arc.
        if (goal?.Draw is not null && slots?.Any(slot => ChapterActivities.Find(slot.Kind, slot.Family, slot.Organic) is { } row && goal.Earns(row)) != true)
        {
            goal = null;
        }

        var longHaul = ChapterFit.IsLongHaul(story.Pacing, game)
            && (story.BeaconScanAt is null || finaleChapter is not null || home is not null || ChapterFit.ServesLongHaul(slots, goal?.Objective));

        var opening = Opening(frontierId, story, secret, StoryClues.BeatKeys(story.Pacing, stage, reach?.InReach != false, finaleChapter), game?.Ship.Type);

        var ask = new AdventureAsk(
            longHaul || home is not null ? AdventureReach.Anywhere : AdventureReach.Session,
            AdventureLength.Evening,
            Chapter: previous,
            Story: Asked(
                opening,
                secret,
                number,
                now,
                stage,
                finaleChapter,
                reach is { InReach: true } ? new AdventureBeacon(reach.Address, reach.System) : null,
                reach is { InReach: false, Why: { } why } ? new AdventureBeaconAway(reach.System, reach.LightYears, why, reach.Unarmed, reach.Carrier) : null,
                reach?.InReach != false,
                longHaul,
                slots,
                finaleChapter > 1 ? story.FinaleDestination : null,
                home,
                goal,
                game?.Ship.Type),
            LastDraftRefused: lastDraftRefused,
            Progress: Progress(frontierId),
            Draft: draftNumber,
            Outline: outline,
            Failed: failed);

        var outcome = await write(ask, now, cancellationToken).ConfigureAwait(false);

        if (outcome.Draft is not { } draft)
        {
            return outcome.Internal
                ? new(outcome.Message, Retry: outcome.Redraftable, Detail: outcome.ForNextDraft, Outline: outcome.Outline)
                : new(outcome.Message ?? "Nothing came back.");
        }

        // The story may have been switched or abandoned while the model wrote.
        if (stories.Current(frontierId) is not { State: StoryState.Running } still || still.PickedAt != story.PickedAt || still.Chapters.Count != story.Chapters.Count)
        {
            return new("The story changed while its chapter was being written.");
        }

        var key = UniqueKey(frontierId, still, draft.Key);

        // The opening and the narrated scan are said before chapter one, introducing whoever speaks in them.
        var saidFirst = number == 1 && !replacing ? (secret.Opening ?? []).Append(secret.Scan).OfType<StoryLine>().Select(line => line.Speaker) : [];
        draft = StoryIntroductions.Introduce(draft, still with { Introduced = [.. still.Introduced, .. saidFirst] }, secret, Gender());

        if ((book.Write(frontierId, draft with { Key = key, StoryId = story.Id }) ?? book.Begin(frontierId, key, now)) is { } refusal)
        {
            return new(refusal);
        }

        stories.Save(frontierId, still with
        {
            Chapters = [.. replacing ? still.Chapters.SkipLast(1) : still.Chapters, key],
            Boarded = opening.Boarded,
            Left = opening.Left,
            AwaitingSeat = [.. opening.AwaitingSeat, .. Unseated(ask.Story?.Due, draft, opening.AwaitingSeat)],
            FinaleFrom = finaleFrom,
            FinaleDestination = finaleChapter == 1 ? outcome.Destination : still.FinaleDestination,
            HomeTarget = home,
        });
        logger.LogInformation("{Title}: chapter {Number}, {Name}, begins", story.Title, number, draft.Name);

        if (failed is not null)
        {
            ArchiveFailed(frontierId, still, failed.Previous);
        }

        ArchiveChapters(frontierId);
        return new(null);
    }

    /// <summary>The members of the boardings handed with no seat that the chapter does not place and the list does not already hold.</summary>
    private static IEnumerable<string> Unseated(IReadOnlyList<StorySwitch>? due, Adventure chapter, IReadOnlyList<string> waiting)
    {
        var placed = chapter.OpeningBoards.Concat(chapter.Beats.SelectMany(beat => beat.Boards)).ToHashSet(StringComparer.Ordinal);

        return (due ?? [])
            .Where(entry => entry.Seats is not null && !placed.Contains(entry.Id) && !waiting.Contains(entry.Id, StringComparer.Ordinal))
            .Select(entry => entry.Id)
            .Distinct(StringComparer.Ordinal);
    }

    /// <summary>Moves a failed chapter the replacement has taken the place of to the archive under this run, and off the adventure file.</summary>
    private void ArchiveFailed(string? frontierId, Story story, Adventure failed)
    {
        var commander = frontierId ?? AdventureStore.NoCommander;
        var standing = book.Standing(frontierId, failed.Key);

        Archive.Append(new ArchivedChapter(commander, story.Id, story.PickedAt, failed, standing?.Fired ?? [], standing?.FiredBy ?? []));
        book.Remove(frontierId, failed.Key);
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
        IReadOnlyList<DrawnActivity>? slots,
        AdventureDestination? destination,
        string? home = null,
        GoalClimbStep? goal = null,
        string? hull = null)
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
            slots,
            destination,
            story.Refused,
            [.. secret.Cast.Select(member => new AdventureSpeaker(member.Id, member.Shown(secret.Id, Gender()).Name, member.Who) { Role = member.Role })],
            Aboard()?.Name,
            Aboard()?.Stock == true,
            home,
            story.BeaconsRemembered > 0,
            goal,
            StoryAboard.BySpan(secret, story),
            StoryAboard.Due(secret, story, StoryClues.BeatKeys(story.Pacing, stage, beaconInReach, finaleChapter), hull));
    }

    private bool FinaleDue(Story story, DateTimeOffset now) =>
        StoryClues.FinaleDue(story, now, story.GoalsAtPick is not null && GoalClimb.AllDone(Goals()));

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

    /// <summary>A key used by no adventure on file and no chapter of the story, archived ones and this run's failed ones included.</summary>
    private string UniqueKey(string? frontierId, Story story, string wanted)
    {
        var existing = book.Store.For(frontierId).Select(adventure => adventure.Key).Concat(story.Chapters)
            .Concat(Archive.For(frontierId).Where(chapter => chapter.Of(story)).Select(chapter => chapter.Adventure.Key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
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

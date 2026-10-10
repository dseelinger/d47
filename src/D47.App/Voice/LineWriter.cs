using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Catalog;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Lore;
using D47.Core.Persona;
using D47.Core.Ships;
using D47.Core.Ticking;
using D47.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace D47.App.Voice;

/// <summary>Writes, with the model, the lines d47 says unasked: rewordings, invented chatter and story lines.</summary>
internal sealed class LineWriter
{
    private readonly ILogger _logger;
    private readonly Rewording _rewording;

    private SettingsService Settings { get; }

    private PersonaHost Personas { get; }

    private GameStateStore GameState { get; }

    private TurnLoop Turns { get; }

    private SpendTracker Spend { get; }

    private D47.Core.Stories.StoryDirector Stories { get; }

    private D47.Core.Adventures.AdventureBook Adventures { get; }

    private Func<D47.Core.Knowledge.IGalaxyService?> Galaxy { get; }

    private NearbyFight Fight { get; }

    private SceneTracker Scenes { get; }

    internal Func<GameStatus> LiveStatus { get; }

    private Func<VoiceGroup, bool> DirectableIn { get; }

    private Func<string, ITtsProvider?> CastClient { get; }

    private Func<Announcement, VoiceCast> CastFor { get; }

    private Func<bool> CanSearch { get; }

    private Func<D47.Core.Stories.StorySpeakerShown?, string?> PictureOf { get; }

    internal LineWriter(
        SettingsService settings,
        PersonaHost personas,
        GameStateStore gameState,
        TurnLoop turns,
        SpendTracker spend,
        ILogger logger,
        D47.Core.Stories.StoryDirector stories,
        D47.Core.Adventures.AdventureBook adventures,
        Func<D47.Core.Knowledge.IGalaxyService?> galaxy,
        NearbyFight fight,
        SceneTracker scenes,
        Func<GameStatus> liveStatus,
        Func<VoiceGroup, bool> directableIn,
        Func<string, ITtsProvider?> castClient,
        Func<Announcement, VoiceCast> castFor,
        Func<bool> canSearch,
        Func<D47.Core.Stories.StorySpeakerShown?, string?> pictureOf)
    {
        Settings = settings;
        Personas = personas;
        GameState = gameState;
        Turns = turns;
        Spend = spend;
        _logger = logger;
        Stories = stories;
        Adventures = adventures;
        Galaxy = galaxy;
        Fight = fight;
        Scenes = scenes;
        LiveStatus = liveStatus;
        DirectableIn = directableIn;
        CastClient = castClient;
        CastFor = castFor;
        CanSearch = canSearch;
        PictureOf = pictureOf;
        _rewording = new Rewording(new RewordChance(), logger);
    }

    /// <summary>Whether the next carrier exchange may make his owning it the subject (#88).</summary>
    private readonly NpcChatterOwnershipSpotlight _carrierSpotlight = new();

    /// <summary>Decides, line by line, which model-written lines get humor.</summary>
    private readonly HumorRoll _humor = new();

    /// <summary>Decides, line by line, which accented NPC lines are written to suit the accent.</summary>
    private readonly AccentRoll _accent = new();

    /// <summary>The humor instruction for one line from a group, or null on a miss.</summary>
    internal string? HumorFor(HumorGroup group, bool canBeDirected) =>
        _humor.ForLine(Humor.DialFor(Settings.Current.Persona, group, Personas.Current.Stock), canBeDirected);

    /// <summary>
    /// The same announcement, said in character, when there is a model to ask and it is one of the
    /// lines the checklist wants varied (Phase 11: "with varied LLM arrival and departure responses").
    /// </summary>
    internal Task<Announcement?> VaryAsync(Announcement announcement)
    {
        return _rewording.VaryAsync(
            announcement,
            hasModel: Turns.Provider is not null,
            Settings.Current.Llm.PersonalityEnabled,
            Settings.Current.Llm.RewordPercent,
            () => ShipFacts.Of(GameState.Active),
            GameState.Active?.Identity.Name,
            (brief, ask, token) =>
            {
                // Against the slot this line will be spoken in, not the ship's.
                var directed = DirectableIn(VoiceGroups.Of(announcement.Voice, announcement.CommsChannel));

                return FlavourTurn.AskForAsync(
                    Turns.Provider,
                    Turns.BackgroundModel,
                    brief.NeedsPersona
                        ? Personas.RenderBlock(personalityEnabled: true)
                        : SpeakerAccent.Join(
                            brief.Speaker,
                            SpeakerAccent.For(CastFor(announcement), announcement, _accent, Settings.Current.Speech.AccentPercent)),
                    StoryFor(brief, announcement.Voice),
                    ask,
                    brief.NeedsGameState ? Turns.LiveGameState?.Invoke() : null,
                    Spend,
                    PriceTable.Default,
                    _logger,
                    token,
                    canBeDirected: directed,
                    humor: FlavourBriefs.HumorGroupOf(announcement, brief) is { } group
                        ? HumorFor(group, directed)
                        : null,
                    scenario: ScenarioFor(brief, announcement.Voice),
                    hiddenStory: brief.NeedsPersona ? HiddenStory(VoiceRole.ShipAi)
                        : announcement.Voice == VoiceRole.Narrator ? HiddenStory(VoiceRole.Narrator)
                        : null);
            },
            stockCoreAboard: () => Personas.Current.Stock);
    }

    /// <summary>The cast invented chatter is voiced from.</summary>
    internal VoiceCast NpcCast =>
        CastFor(new Announcement(NpcChatter.LineKey, string.Empty) { Voice = VoiceRole.Comms, CommsChannel = "npc" });

    /// <summary>How long an exchange may spend being written.</summary>
    private static readonly TimeSpan ChatterBudget = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The invented exchange a chatter marker asked for (#244), as one announcement per parsed line.
    /// </summary>
    internal async Task<IReadOnlyList<Announcement>> ComposeNpcChatterAsync(Announcement marker)
    {
        if (Turns.Provider is null || !Settings.Current.Llm.PersonalityEnabled)
        {
            return [];
        }

        var kind = NpcChatter.KindOf(marker.Key);

        if (kind is not NpcChatterKind.Hail && Settings.Current.Audio.Overheard.Muted)
        {
            return [];
        }

        // A scene is written from its beat and the scenario, and without a scenario there is no scene.
        var scene = kind == NpcChatterKind.Scene ? marker.Scene : null;
        var scenario = Settings.Current.Llm.Scenario;

        if (kind == NpcChatterKind.Scene
            && (scene is null || string.IsNullOrWhiteSpace(scenario) || !SceneCallout.IsStillHappening(scene, Scenes.Snapshot)))
        {
            return [];
        }

        var location = GameState.Active?.Location;
        var docked = location?.Docked ?? false;

        // A combat marker is made in normal space; a Commander who has since docked, landed or entered
        // supercruise has left the fight behind.
        if (kind == NpcChatterKind.Combat
            && AmbientLines.Situate(LiveStatus()) != AmbientSituation.NormalSpace)
        {
            return [];
        }

        // The kind was picked from the Docked flag when the marker was made; the exchange is composed
        // later. A controller needs a dock to be at — one lifted off in between is worse than silence
        // (#43).
        if (kind == NpcChatterKind.Controller && !docked)
        {
            return [];
        }

        var carrier = scene is null ? NpcChatterCarrier.Of(GameState.Active?.Carrier, location) : NpcChatterCarrier.None;
        var spotlight = scene is null && _carrierSpotlight.Claim(carrier.Present);

        // The voices are cast before the exchange is written, so each line can be written for its accent (#415).
        var npcs = NpcCast;
        var posts = CastFor(new Announcement(NpcChatter.LineKey, string.Empty) { Voice = VoiceRole.TowerControl, CommsChannel = "npc" });

        var roster = NpcChatterRoster.Cast(
            npcs,
            kind,
            marker.Variant ?? 0,
            location?.StarSystem,
            docked ? location?.StationAllegiance : null,
            posts.AccentOf(posts.For(VoiceRole.TowerControl).VoiceId),
            posts.AccentOf(posts.For(VoiceRole.CarrierCaptain).VoiceId),
            Fight.Snapshot.Dead).Rolled(_accent, Settings.Current.Speech.AccentPercent);

        using var budget = new CancellationTokenSource(ChatterBudget);

        var directed = DirectableIn(VoiceGroup.Npcs);

        var script = await FlavourTurn.AskAsync(
            Turns.Provider,
            Turns.BackgroundModel,
            NpcChatter.Speaker,
            null,
            NpcChatter.WithHumor(
                scene is null
                    ? NpcChatter.Instruction(kind, carrier, docked, spotlight, marker.Variant ?? 0, location?.StationType, roster, Fight.Snapshot,
                        NpcChatter.ScenarioFor(Settings.Current.Llm.ScenarioAudience, scenario))
                    : NpcChatter.SceneInstruction(
                        scene,
                        scenario!,
                        roster,
                        marker.Variant ?? 0,
                        scene.Place == ScenePlace.Mission ? Stories.MissionAsides.Take(scene.Missions ?? []) : null),
                carrier,
                Settings.Current.Persona,
                _humor,
                directed),
            Turns.LiveGameState?.Invoke(),
            Spend,
            PriceTable.Default,
            _logger,
            budget.Token,
            canBeDirected: directed,
            hiddenStory: HiddenStory(VoiceRole.Comms)).ConfigureAwait(false);

        // Checked again once written: the Commander may have left the scene while the model was writing.
        if (scene is not null && !SceneCallout.IsStillHappening(scene, Scenes.Snapshot))
        {
            return [];
        }

        var facts = ShipFacts.Of(GameState.Active);
        var heard = new List<Announcement>();
        var exchange = marker.Variant ?? 0;
        var answerable = NpcChatter.MayNotice(kind, exchange);

        foreach (var line in NpcChatter.Parse(script, kind, carrier, roster))
        {
            var accent = roster.IsFlavoured(line)
                ? SpeakerAccent.Sentence(line.Role is { } post
                    ? posts.AccentOf(posts.For(post).VoiceId)
                    : npcs.AccentOf(line.VoiceId))
                : null;

            // **Per line rather than per exchange** (#338).
            var said = ContradictedClaims.AboutTheCommandersShip(line.Text)
                ? await ContradictedClaims.SayableAsync(
                    line.Text,
                    facts,
                    async contradiction => NpcChatter.Rewritten(
                        await FlavourTurn.AskAsync(
                            Turns.Provider,
                            Turns.BackgroundModel,
                            SpeakerAccent.Join(NpcChatter.Speaker, accent),
                            null,
                            ContradictedClaims.Rewrite(line.Text, contradiction),
                            Turns.LiveGameState?.Invoke(),
                            Spend,
                            PriceTable.Default,
                            _logger,
                            budget.Token,
                            canBeDirected: DirectableIn(VoiceGroup.Npcs)).ConfigureAwait(false),
                        line.Role,
                        carrier),
                    _logger,
                    NpcChatter.LineKey).ConfigureAwait(false)
                : line.Text;

            if (said is null)
            {
                continue;
            }

            // The slot's voice, whatever name the model gave it.
            if (line.VoiceId is { } voice)
            {
                npcs.Keep(line.Name, voice);
            }

            heard.Add(new Announcement(NpcChatter.LineKey, said)
            {
                Urgency = CalloutUrgency.Routine,
                Voice = line.Role ?? D47.Core.Audio.VoiceRole.Comms,
                Speaker = line.Name,
                SpeakerIsPlayer = false,
                CommsChannel = "npc",
                Overheard = kind is not NpcChatterKind.Hail,
                Invented = new NpcChatterHeard(line with { Text = said }, exchange, answerable),
            });
        }

        return heard;
    }

    /// <summary>The running stock story's hidden layer as this speaker reads it, or null.</summary>
    internal string? HiddenStory(VoiceRole speaker) =>
        Stories.HiddenBrief(GameState.Active?.Identity.FrontierId, speaker, Personas.Current);

    /// <summary>A due clue, written by the model as its speaker says it, or null when no line came back.</summary>
    internal async Task<Announcement?> ComposeClueAsync(Announcement marker, D47.Core.Stories.StoryClueDue due)
    {
        var commander = GameState.Active?.Identity.FrontierId;

        if (Stories.Clue(commander, due) is not var (_, clue) || Stories.ClueVoice(commander, due) is not { } voice)
        {
            return null;
        }

        var brief = voice.Cast is { } cast
            ? D47.Core.Stories.StoryClues.Speaking(clue, cast.Name, voice.Who)
            : D47.Core.Stories.StoryClues.Speaking(clue, voice.Narrated);
        var directed = voice.Pinned is { } pinned ? CastClient(pinned.ProviderId)?.ReadsAudioTags == true : (bool?)null;

        return await ComposeStoryLineAsync(brief, voice.Role, marker.CommsChannel, marker.Key, directed).ConfigureAwait(false) is { } said
            ? Voiced(marker with { Text = said }, voice)
            : null;
    }

    /// <summary>A story line as its speaker says it: the role, and for a cast member the name and the pinned voice.</summary>
    internal Announcement Voiced(Announcement line, D47.Core.Stories.StoryLineVoice voice) => line with
    {
        Voice = voice.Role,
        Speaker = voice.Cast?.Name,
        Pinned = voice.Pinned,
        Picture = PictureOf(voice.Cast),
    };

    /// <summary>
    /// A beat of a story chapter whose line is not the ship's own, voiced by its speaker and said as written; null for
    /// any other announcement, and for a line the core aboard speaks, which is reworded as every beat is.
    /// </summary>
    internal Announcement? StoryVoiced(Announcement announcement)
    {
        var commander = GameState.Active?.Identity.FrontierId;

        if (D47.Core.Adventures.AdventureCallout.Spoken(announcement.Key) is not var (key, beat, line)
            || Adventures.Store.Find(commander, key) is not { StoryId: not null } chapter
            || Stories.LineVoice(commander, chapter.SpeakerOf(beat, line)) is not { } voice
            || voice.Role == VoiceRole.ShipAi)
        {
            return null;
        }

        return Voiced(announcement, voice);
    }

    /// <summary>
    /// A story line the model writes from the brief in the given voice, or null when no line came back. The ship's lines
    /// are written by the core aboard; every other speaker's by the brief's own speaker.
    /// </summary>
    internal async Task<string?> ComposeStoryLineAsync(FlavourBrief brief, VoiceRole voice, string? channel, string key, bool? canBeDirected = null)
    {
        if (Turns.Provider is null || !Settings.Current.Llm.PersonalityEnabled)
        {
            return null;
        }

        var ship = voice == VoiceRole.ShipAi;
        var directed = canBeDirected ?? DirectableIn(VoiceGroups.Of(voice, channel));

        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        Task<string?> AskAsync(string ask) => FlavourTurn.AskAsync(
            Turns.Provider,
            Turns.BackgroundModel,
            ship ? Personas.RenderBlock(personalityEnabled: true) : brief.Speaker,
            StoryFor(brief, voice),
            ask,
            Turns.LiveGameState?.Invoke(),
            Spend,
            PriceTable.Default,
            _logger,
            budget.Token,
            canBeDirected: directed,
            scenario: ScenarioFor(brief, voice),
            hiddenStory: HiddenStory(voice));

        var facts = ShipFacts.Of(GameState.Active);

        return await ContradictedClaims.SayableAsync(
            await AskAsync(brief.Instruction).ConfigureAwait(false),
            facts,
            contradiction => AskAsync($"{brief.Instruction} {contradiction.Correction}"),
            _logger,
            key).ConfigureAwait(false);
    }

    /// <summary>
    /// Position 4 for a flavour line, to the depth the brief asked for (Phase 43). The Narrator, given no
    /// character sheet, names the Commander from the journal.
    /// </summary>
    internal string? StoryFor(FlavourBrief brief, VoiceRole speaker = VoiceRole.ShipAi) =>
        brief.NeedsAboutMe
            ? CommanderStory.Compose(
                speaker == VoiceRole.Narrator
                    ? CommanderStory.SheetOrName(Settings.Current.Llm.CharacterSheet, GameState.Active?.Identity.Name)
                    : Settings.Current.Llm.CharacterSheet,
                Settings.Current.Llm.AboutMe,
                withStory: brief.NeedsStory)
            : null;

    /// <summary>The Commander's scenario for a flavour line, or null when the brief or the audience leaves it out.</summary>
    internal string? ScenarioFor(FlavourBrief brief, VoiceRole speaker) =>
        FlavourBriefs.ScenarioFor(brief, Settings.Current.Llm.ScenarioAudience, speaker, Settings.Current.Llm.Scenario);

    /// <summary>The same lore remark, told that nothing further is coming when nothing further can.</summary>
    internal Announcement Owing(Announcement announcement) =>
        LoreCallout.AddressOf(announcement.Key) is not null
        && Settings.Current.Callouts.Lore == LoreRemarks.Lookup
        && !CanSearch()
            ? announcement with { Text = $"{announcement.Text} {LoreLookup.CannotSearch}" }
            : announcement;

    /// <summary>A nudge with the route distance to the adventure's next beat added, when it can be found.</summary>
    internal async Task<Announcement> RoutedAsync(Announcement announcement)
    {
        if (D47.Core.Callouts.NarratorCallout.Nudged(announcement.Key) is not { } key
            || Adventures.Standing(GameState.Active?.Identity.FrontierId, key) is not { } standing)
        {
            return announcement;
        }

        double? distance = null;

        if (Galaxy() is { } galaxy
            && GameState.Active?.Location.StarSystem is { Length: > 0 } here
            && D47.Core.Adventures.AdventureNudge.Destination(standing) is { } there)
        {
            try
            {
                using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                distance = string.Equals(here, there, StringComparison.OrdinalIgnoreCase)
                    ? 0
                    : await galaxy.DistanceAsync(here, there, budget.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogInformation("No route distance for the nudge toward {Name}: {Reason}", standing.Adventure.Name, ex.Message);
            }
        }

        return announcement with { Text = D47.Core.Adventures.AdventureNudge.Facts(standing, distance) };
    }
}

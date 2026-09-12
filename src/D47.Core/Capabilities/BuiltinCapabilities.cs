using D47.Core.Callouts;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Diagnostics;
using D47.Core.Journal;

namespace D47.Core.Capabilities;

/// <summary>The capability set d47 ships with.</summary>
public static class BuiltinCapabilities
{
    public static IReadOnlyList<CapabilityDescriptor> All(
        AppPaths paths,
        ILogVerbosityControl verbosity,
        GameStateStore gameState,
        SettingsService settings,
        LlmAvailabilityState llmAvailability,
        SpendTracker spend,
        string version,
        SpeechCapability.SpeechSurface speech,

        // What the App does for the fleet: describing what is stored, and rebuilding it from every journal on
        // disk when the Commander says it does not look right (#128).
        ShipsCapability.ShipsSurface fleet,

        // What this Commander has met and how a misheard name is recovered (#134).
        Builtin.SpokenNamesSurface heard,
        Conversation.TurnCancellation cancellation,
        CalloutEngine callouts,
        Func<CapabilityRegistry> registry,
        ListeningCapability.ListeningSurface listening,
        VrCapability.HeadsetSurface headset,
        Builtin.ActionSurface actions,
        Func<string> autonomous,
        Builtin.NavigationSurface navigation,
        Actions.MacroStore macros,
        Persona.PersonaHost personas,
        Checklists.ChecklistService checklists,

        // What was found in the Commander's own audio folder, and what was skipped.
        Func<string>? audioDrops = null,

        // Optional and therefore last: null in every normal run, and the diagnostics card then carries no
        // coverage row at all.
        Func<string>? coverage = null,

        // Null where nothing composed one — under the designer, and in a test that is not about it.
        Knowledge.IGalaxyService? galaxy = null,

        // Same story as the galaxy service, and the same host behind it — but a different protocol, so a
        // different seam (see RouteCapability).
        Knowledge.IRouteService? routes = null,

        // And a third protocol against that same host: lookups plus arithmetic run here, rather than a job
        // somebody else queues (Phase 36).
        Knowledge.ITradePlanService? trade = null,

        // Third of the same family, and the only one that needs a credential before it can do anything at
        // all.
        Knowledge.ICommunityGoalService? communityGoals = null,

        // The clock, injected because no Core component reads one.
        Func<DateTimeOffset>? now = null,

        // Tries a language-model provider's stored key against the real service, by provider id (Phase 16).
        Func<string, CancellationToken, Task<Configuration.SecretCheck>>? verifyLlmKey = null,

        // The live Status.json, which is the only thing that knows where the Commander is standing (Phase
        // 18).
        Func<Journal.GameStatus>? gameStatus = null,

        // Puts Elite in front (docs/plans/change-requests.md item 10).
        Func<Task<Builtin.FocusResult>>? raiseGame = null,

        // The Commander's mapped HOTAS switches (Phase 21).
        SwitchSurface? switches = null,

        // The Commander's own notes about systems (Phase 23).
        Lore.LoreBook? lore = null,

        // The Commander's timers and alarms (Phase 24).
        Utilities.Timekeeper? timekeeper = null,

        // How to present an instant locally.
        Func<TimeZoneInfo>? zone = null,

        // The Commander's ship builds (Phase 26).
        Ships.ShipPlanService? ships = null,

        // And their suit and weapon plans (Phase 27), on exactly the same terms.
        Loadout.OnFootPlanService? onFoot = null,

        // The engineer solver (Phase 28), on the same terms again.
        Engineers.EngineerPlanService? unlocks = null,

        // Whether the provider and model in use offer a server-side web search.
        Func<bool>? searchAvailable = null,

        // What the language-model endpoint said it serves, when d47 has asked it (Phase 29).
        Func<IReadOnlyList<string>>? endpointModels = null,

        // What d47 remembers about the Commander (Phase 31).
        Memory.MemoryBook? memories = null,

        // The Commander's log (Phase 33).
        Logbook.LogbookBook? logbook = null,

        // The Commander's long arcs (Phase 34).
        Goals.GoalBook? goals = null,

        // What pressing "read my journals" does for the arcs.
        Func<Action?>? backfillGoals = null,

        // Which core flies which ship (Phase 35).
        Persona.ShipCoreService? shipCores = null,

        // Where a plan is kept once it is made (Phase 37).
        Knowledge.RoutePlanBook? plans = null,

        // What d47 last offered to put on the clipboard (asked for 2026-08-21). **Last, and optional, for a
        // reason worth writing down.** It went in beside `checklists` first, as a required parameter, and
        // that silently re-bound every positional argument after it — `AppHost` compiled a `Func<bool>` into
        // a `Func<IReadOnlyList<string>>` slot and the error named neither the cause nor the caller.
        Conversation.ClipboardOffer? clipboard = null,

        // The waits the compound ship commands need (Phase 52).
        Builtin.ShipCommandSurface? shipCommands = null,

        // Where the last commodity answer is posted (Phase 49), so the Routing tab draws what the Commander
        // was told rather than searching again.
        Knowledge.CommodityBoard? commodities = null,

        // What the Commander says is on their carrier, and where the last shopping list is posted (Phase 50).
        Knowledge.CarrierManifest? carrier = null,
        Knowledge.SourcingBoard? sourcing = null,

        // What this build is, for the About area (#50).
        Builtin.AboutSurface? about = null,

        // What the audio recorder has kept (#164).
        Diagnostics.Recording.RecordingLog? recording = null,

        // Withdrawal that reaches the store and not only this machine (#167).
        LongPress? forgetDonations = null,

        // The standing directions the debrief pass drafts and the Commander adopts (<a
        // href=".com/dseelinger/d47/issues/162">#162</a>).
        Debrief.DebriefBook? debrief = null,

        // What the Community Goal commodity has made or lost, and the saved search that names it (<a
        // href=".com/dseelinger/d47/issues/296">#296</a>).
        Journal.CommodityLedger? ledger = null,
        Knowledge.CommunityGoalSearch? communityGoalSearch = null,

        // The system a nearest-first commodity search last found (#325), so "set a course" and "set a course
        // and take us out" have something to act on.
        Conversation.LastFoundSystem? lastFoundSystem = null,

        // The walk over older journals, which finishes after the window is up (#148).
        HistoryBackfill? history = null,

        // The plotted route as Elite last wrote it, so a jump count is read from the route file rather than
        // from a journal note the game overwrites mid-jump (#152).
        Func<Journal.NavRoute>? route = null,

        // The loop itself, so the diagnostics card can name a subscriber it has paused (#58).
        Ticking.TickLoop? ticking = null) =>
    [
        HelpCapability.Create(registry),
        DiagnosticsCapability.Create(paths, verbosity, settings, version, coverage, history, ticking),
        JournalCapability.Create(gameState, () => history?.State ?? Journal.HistoryState.Done, route),
        CrewCapability.Create(() => gameState.Active),
        GalaxyCapability.Create(
            galaxy,
            () => gameState.Active?.Location.StarSystem,
            settings,

            // Whole markets already live behind the trade planner, and this fetches nothing new (Phase 49).
            trade,
            () => gameState.Active?.Location.StationName,
            commodities,
            now,
            heard,

            // Nearest first puts its winner on the clipboard the same way plot_course does, and remembers it
            // for the two voice commands (#325).
            navigation.Clipboard,
            lastFoundSystem),
        RouteCapability.Create(
            routes,
            trade,
            () => gameState.Active,
            settings,
            plans,
            now),
        SpecificationCapability.Create(() => gameState.Active),
        EngineerCapability.Create(() => gameState.Active, unlocks),
        EngineeringCapability.Create(() => gameState.Active, galaxy, clipboard),
        OnFootCapability.Create(() => gameState.Active, onFoot),
        ChecklistCapability.Create(checklists, ships, onFoot),
        ShipsCapability.Create(ships, fleet),
        GapCapability.Create(ships, onFoot, () => gameState.Active),
        ColonisationCapability.Create(
            () => gameState.Active,
            galaxy,
            settings,

            // The same sweep and the same cache the commodity search uses, so a build's shopping list and
            // "where do I buy tritium" cost one pull between them (Phase 50).
            trade,
            carrier,
            sourcing,
            now),

        // At the end of the run of ledgers, which is where the Commander put the tab itself (Phase 47).
        AdventureCapability.Create(),

        SystemNameCapability.Create(() => gameState.Active),
        LoreCapability.Create(
            lore,
            () => LoreCapability.PlaceOf(gameState.Active),
            now ?? (() => DateTimeOffset.MinValue)),
        ExobiologyCapability.Create(routes, () => gameState.Active, gameStatus),
        CommunityGoalCapability.Create(
            () => gameState.Active,
            communityGoals,
            now ?? (() => DateTimeOffset.MinValue),
            ledger,
            communityGoalSearch,
            settings),
        ConversationCapability.Create(
            settings, llmAvailability, spend, cancellation, speech.Silence, verifyLlmKey,

            // The same tracker the speech rows read, so "what has this cost" cannot answer two different
            // numbers depending on where it is asked (Phase 19).
            speech.SpeechSpend,

            // Late-bound like the voice list, and for the same reason: it is fetched from the endpoint over
            // the network well after this point in composition.
            endpointModels),
        PersonaCapability.Create(personas, settings, shipCores),
        SpeechCapability.Create(speech),
        AudioCapability.Create(audioDrops),
        ListeningCapability.Create(settings, listening),
        CalloutCapability.Create(settings, () => CalloutCapability.Describe(callouts, settings.Current)),
        InterfaceCapability.Create(),
        VrCapability.Create(settings, headset),
        FocusCapability.Create(raiseGame),
        .. ActionCapabilities.All(actions, shipCommands, navigation, lastFoundSystem),
        AutonomousCapability.Create(autonomous),
        NavigationCapability.Create(navigation),
        CommsCapability.Create(actions, () => settings.Current.Actions.Chat),
        MacroCapability.Create(macros, actions),
        SwitchCapability.Create(switches ?? SwitchSurface.Inert, () => settings.Current.Actions.Keyboard),
        UtilitiesCapability.Create(timekeeper, now, zone),

        // Beside Privacy rather than anywhere near the game capabilities, and immediately before it so that
        // adding this shifted two documentation pages rather than twenty-seven — the nav order is the
        // registry index, which Phase 26 learned the expensive way.
        MemoryCapability.Create(memories, now ?? (() => DateTimeOffset.MinValue), settings),

        // Beside Memory, which is the other card about what d47 carries between sessions — and directly after
        // it, because a Commander looking for "what has D47 learned about how I like to be talked to" looks
        // next to "what does D47 remember about me".
        DebriefCapability.Create(debrief),

        LogbookCapability.Create(logbook),

        // And after the log, for the fourth time and the same reason.
        GoalsCapability.Create(
            goals,
            backfillGoals ?? (() => null),
            now ?? (() => DateTimeOffset.MinValue)),

        // The donation identifier is read from the same AppPaths the diagnostics rows already take, so this
        // list keeps the shape it has — no parameter inserted in the middle, which is the one edit this file
        // records as silently rebinding everything after it.
        PrivacyCapability.Create(
            settings, searchAvailable, memories, recording, paths.DonorTokenFile, forgetDonations),
        SettingsCapability.Create(settings),

        // LAST, and it has to be last twice over (#50) - for two different reasons, which is what makes this
        // easy to get wrong twice (#83).
        AboutCapability.Create(
            paths,

            // **Derived from the stamp rather than taken from the caller** (#92).
            Updates.ReleaseVersion.Semantic(about?.Build ?? version),
            about?.Build ?? version,
            about?.ShowChangelog,
            about?.ShowChangelogOnline,
            about?.AddToStartMenu,
            about?.StartMenuWanted,
            about?.SetUpKeys,
            about?.ShowCommunity,
            about?.Channel,
            about?.OpenDataFolder),
    ];
}

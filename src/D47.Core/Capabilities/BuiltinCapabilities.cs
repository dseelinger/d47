using D47.Core.Callouts;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Diagnostics;
using D47.Core.Journal;

namespace D47.Core.Capabilities;

/// <summary>
/// The capability set d47 ships with. One list: the app registers from it and the
/// documentation gate reads it, so a capability cannot exist in the app and be invisible to
/// the gate (list.md Phase 1, "Every capability has a documentation page").
/// <para>
/// Declaration order is display order, and it is also the order the settings surface renders
/// its cards in.
/// </para>
/// </summary>
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

        // What was found in the Commander's own audio folder, and what was skipped. Null under
        // the designer and in a test that is not about it, and the row is then absent.
        Func<string>? audioDrops = null,

        // Optional and therefore last: null in every normal run, and the diagnostics card then
        // carries no coverage row at all.
        Func<string>? coverage = null,

        // Null where nothing composed one — under the designer, and in a test that is not about
        // it. The capability still registers either way, so its settings row and its
        // documentation page exist; its tools report that they cannot act, which is what a
        // capability being off looks like rather than one being absent (list.md Phase 3).
        Knowledge.IGalaxyService? galaxy = null,

        // Same story as the galaxy service, and the same host behind it — but a different
        // protocol, so a different seam (see RouteCapability).
        Knowledge.IRouteService? routes = null,

        // And a third protocol against that same host: lookups plus arithmetic run here, rather
        // than a job somebody else queues (list.md Phase 36).
        Knowledge.ITradePlanService? trade = null,

        // Third of the same family, and the only one that needs a credential before it can do
        // anything at all. Null composes a capability that answers from the journal alone,
        // which is also what a machine with no Inara key gets.
        Knowledge.ICommunityGoalService? communityGoals = null,

        // The clock, injected because no Core component reads one. Defaults to the epoch under
        // the designer and in tests that do not care; the community goal board is the only
        // reader, and every goal then reads as live, which is the harmless direction.
        Func<DateTimeOffset>? now = null,

        // Tries a language-model provider's stored key against the real service, by provider id
        // (list.md Phase 16). Null under the designer and in every test that does not press the
        // button; the row then offers no check rather than offering one that cannot be made.
        Func<string, CancellationToken, Task<Configuration.SecretCheck>>? verifyLlmKey = null,

        // The live Status.json, which is the only thing that knows where the Commander is standing
        // (list.md Phase 18). Null under the designer and in tests that are not about it; the
        // sampling answer then carries counts without distances.
        Func<Journal.GameStatus>? gameStatus = null,

        // Puts Elite in front (docs/plans/change-requests.md item 10). Null under the designer and
        // in tests that are not about it; the phrase then answers that it cannot reach the game
        // window rather than going quiet, which is the one thing this must never do.
        Func<Builtin.FocusResult>? raiseGame = null,

        // The Commander's mapped HOTAS switches (list.md Phase 21). Null under the designer and
        // in tests that are not about them; the capability still registers, so its rows and its
        // documentation page exist, and it reports that it is reading nothing — which is what a
        // machine with no stick honestly looks like.
        SwitchSurface? switches = null,

        // The Commander's own notes about systems (list.md Phase 23). Null under the designer and
        // in tests that are not about them; the capability still registers and still answers from
        // the shipped table, because that table is compiled in rather than composed.
        Lore.LoreBook? lore = null,

        // The Commander's timers and alarms (list.md Phase 24). Null under the designer and in
        // tests that are not about them; the capability still registers, so its rows and its
        // documentation page exist, and every tool answers that nothing is keeping time.
        Utilities.Timekeeper? timekeeper = null,

        // How to present an instant locally. A function because a Commander who changes time zone
        // mid-session should not have to restart to see it.
        Func<TimeZoneInfo>? zone = null,

        // The Commander's ship builds (list.md Phase 26). Null under the designer and in tests
        // that are not about them; the capability still registers, so its page exists, and every
        // tool answers that nothing is being tracked.
        Ships.ShipPlanService? ships = null,

        // And their suit and weapon plans (list.md Phase 27), on exactly the same terms. Two
        // parameters rather than one because they are two stores: the game separates ship and
        // on-foot hard, and so does everything that reads them.
        Loadout.OnFootPlanService? onFoot = null,

        // The engineer solver (list.md Phase 28), on the same terms again. It reads both stores
        // above rather than being handed their contents, because a ranking is only as current as
        // the plans under it and both of them move while the panel is open.
        Engineers.EngineerPlanService? unlocks = null,

        // Whether the provider and model in use offer a server-side web search. Null is "assume
        // it can", which is what a caller with no provider to ask is entitled to say — the
        // designer and every test that is not about egress. The app supplies the real answer, or
        // the disclosure describes searches at an endpoint that will never make one.
        Func<bool>? searchAvailable = null,

        // What the language-model endpoint said it serves, when d47 has asked it (list.md Phase
        // 29). Null under the designer and in every test, and null again in the app until the
        // first handshake answers — the model picker then behaves exactly as it did before there
        // was anybody to ask, which is the state it was designed for.
        Func<IReadOnlyList<string>>? endpointModels = null,

        // What d47 remembers about the Commander (list.md Phase 31). Null under the designer and in
        // tests that are not about it; the capability still registers, so its rows and its
        // documentation page exist, and every tool answers that there is nowhere to keep anything.
        Memory.MemoryBook? memories = null,

        // What d47 has noticed the Commander keeps doing (list.md Phase 32). Null under the
        // designer and in tests that are not about it, on the same terms as every other optional
        // service: the capability still registers, so its rows and its documentation page exist,
        // and every tool answers that nothing is reading the journals.
        Habits.HabitBook? habits = null,

        // What pressing "read my journals" does. A function returning an action rather than the
        // action, because Core owns no thread and the pass is seven seconds long — the App decides
        // what to run it on, and a caller with nowhere to run it answers null and gets a row with
        // no button.
        Func<Action?>? mineHabits = null,

        // The Commander's log (list.md Phase 33). Null under the designer and in tests that are not
        // about it, on the same terms as the two above: the capability still registers, so its four
        // rows and its documentation page exist, and every tool answers that there is nothing set
        // up to write with.
        Logbook.LogbookBook? logbook = null,

        // The Commander's long arcs (list.md Phase 34). Null under the designer and in tests that
        // are not about it, on the same terms as the three above: the capability still registers,
        // so its row and its documentation page exist, and every tool answers that nothing is
        // being tracked.
        Goals.GoalBook? goals = null,

        // What pressing "read my journals" does for the arcs. A function returning an action for
        // the reason mineHabits gives — Core owns no thread, and the pass is seconds long.
        Func<Action?>? backfillGoals = null,

        // Which core flies which ship (list.md Phase 35). Null under the designer and in tests
        // that are not about it, on the same terms as every other optional service — and here the
        // absence is more than a row: with no store there is nothing to bind, so the persona
        // capability registers without its two protected tools rather than registering tools that
        // would answer that they cannot act.
        Persona.ShipCoreService? shipCores = null,

        // Where a plan is kept once it is made (list.md Phase 37). Null where nothing draws them,
        // which is every test that is not about the Routing tab: a plot still answers, it just
        // leaves nothing behind for a surface to show.
        Knowledge.RoutePlanBook? plans = null,

        // What d47 last offered to put on the clipboard (asked for 2026-08-21).
        //
        // **Last, and optional, for a reason worth writing down.** It went in beside `checklists`
        // first, as a required parameter, and that silently re-bound every positional argument
        // after it — `AppHost` compiled a `Func<bool>` into a `Func<IReadOnlyList<string>>` slot
        // and the error named neither the cause nor the caller. This list is long and mostly
        // optional, so **a new parameter goes at the end**: anywhere else changes what every
        // existing call means. Null under the designer and in tests that are not about it, and the
        // searches then make no offer.
        Conversation.ClipboardOffer? clipboard = null) =>
    [
        HelpCapability.Create(registry),
        DiagnosticsCapability.Create(paths, verbosity, settings, version, coverage),
        JournalCapability.Create(gameState),
        CrewCapability.Create(() => gameState.Active),
        GalaxyCapability.Create(galaxy, () => gameState.Active?.Location.StarSystem, settings),
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
        ShipsCapability.Create(ships),
        GapCapability.Create(ships, onFoot, () => gameState.Active),
        ColonisationCapability.Create(() => gameState.Active, galaxy, settings),

        // At the end of the run of ledgers, which is where the Commander put the tab
        // itself (list.md Phase 47). Registry order is nav order on the site, so this
        // position is what a reader sees rather than an implementation detail.
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
            now ?? (() => DateTimeOffset.MinValue)),
        ConversationCapability.Create(
            settings, llmAvailability, spend, cancellation, speech.Silence, verifyLlmKey,

            // The same tracker the speech rows read, so "what has this cost" cannot answer two
            // different numbers depending on where it is asked (list.md Phase 19). Passed as the
            // surface's own late-bound function, because the host that owns it does not exist
            // yet at this point in composition.
            speech.SpeechSpend,

            // Late-bound like the voice list, and for the same reason: it is fetched from the
            // endpoint over the network well after this point in composition.
            endpointModels),
        PersonaCapability.Create(personas, settings, shipCores),
        SpeechCapability.Create(speech),
        AudioCapability.Create(audioDrops),
        ListeningCapability.Create(settings, listening),
        CalloutCapability.Create(settings, () => CalloutCapability.Describe(callouts, settings.Current)),
        InterfaceCapability.Create(),
        VrCapability.Create(settings, headset),
        ReanchorCapability.Create(headset),
        FocusCapability.Create(raiseGame),
        .. ActionCapabilities.All(actions),
        AutonomousCapability.Create(autonomous),
        NavigationCapability.Create(navigation),
        CommsCapability.Create(actions, () => settings.Current.Actions.Chat),
        MacroCapability.Create(macros, actions),
        SwitchCapability.Create(switches ?? SwitchSurface.Inert, () => settings.Current.Actions.Keyboard),
        UtilitiesCapability.Create(timekeeper, now, zone),

        // Beside Privacy rather than anywhere near the game capabilities, and immediately before it
        // so that adding this shifted two documentation pages rather than twenty-seven — the nav
        // order is the registry index, which Phase 26 learned the expensive way.
        MemoryCapability.Create(memories, now ?? (() => DateTimeOffset.MinValue), settings),

        // Immediately after Memory, which is the other capability about the person rather than the
        // game — and here rather than earlier because nav_order is the registry index, so inserting
        // near the end shifts two documentation pages instead of twenty-eight.
        HabitsCapability.Create(habits, mineHabits ?? (() => null)),

        // And immediately after Habits, for the third time and the same reason: this is the other
        // thing d47 does with the journals rather than with the game, and the tail of the list is
        // where a new capability costs two documentation pages a nav_order instead of twenty-nine.
        LogbookCapability.Create(logbook),

        // And after the log, for the fourth time and the same reason. This one is about a history
        // too — the arcs are aged from the same corpus the last two phases read.
        GoalsCapability.Create(
            goals,
            backfillGoals ?? (() => null),
            now ?? (() => DateTimeOffset.MinValue)),

        PrivacyCapability.Create(settings, searchAvailable, memories, habits),
        SettingsCapability.Create(settings),
    ];
}

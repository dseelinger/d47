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
        Func<Journal.GameStatus>? gameStatus = null) =>
    [
        HelpCapability.Create(registry),
        DiagnosticsCapability.Create(paths, verbosity, settings, version, coverage),
        JournalCapability.Create(gameState),
        CrewCapability.Create(() => gameState.Active),
        GalaxyCapability.Create(galaxy, () => gameState.Active?.Location.StarSystem, settings),
        RouteCapability.Create(routes, () => gameState.Active, settings),
        SpecificationCapability.Create(() => gameState.Active),
        EngineerCapability.Create(() => gameState.Active),
        EngineeringCapability.Create(() => gameState.Active, galaxy),
        ChecklistCapability.Create(checklists),
        ColonisationCapability.Create(() => gameState.Active, galaxy, settings),
        SystemNameCapability.Create(() => gameState.Active),
        ExobiologyCapability.Create(routes, () => gameState.Active, gameStatus),
        CommunityGoalCapability.Create(
            () => gameState.Active,
            communityGoals,
            now ?? (() => DateTimeOffset.MinValue)),
        ConversationCapability.Create(
            settings, llmAvailability, spend, cancellation, speech.Silence, verifyLlmKey),
        PersonaCapability.Create(personas, settings),
        SpeechCapability.Create(speech),
        AudioCapability.Create(audioDrops),
        ListeningCapability.Create(settings, listening),
        CalloutCapability.Create(settings, () => CalloutCapability.Describe(callouts, settings.Current)),
        InterfaceCapability.Create(),
        VrCapability.Create(settings, headset),
        ReanchorCapability.Create(headset),
        .. ActionCapabilities.All(actions),
        AutonomousCapability.Create(autonomous),
        NavigationCapability.Create(navigation),
        CommsCapability.Create(actions, () => settings.Current.Actions.Chat),
        MacroCapability.Create(macros, actions),
        PrivacyCapability.Create(settings),
        SettingsCapability.Create(settings),
    ];
}

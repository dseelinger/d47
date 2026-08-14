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
        Knowledge.IRouteService? routes = null) =>
    [
        HelpCapability.Create(registry),
        DiagnosticsCapability.Create(paths, verbosity, settings, version, coverage),
        JournalCapability.Create(gameState),
        CrewCapability.Create(() => gameState.Active),
        GalaxyCapability.Create(galaxy, () => gameState.Active?.Location.StarSystem, settings),
        RouteCapability.Create(routes, () => gameState.Active, settings),
        ConversationCapability.Create(settings, llmAvailability, spend, cancellation, speech.Silence),
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

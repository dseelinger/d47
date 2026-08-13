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

        // Optional and therefore last: null in every normal run, and the diagnostics card then
        // carries no coverage row at all.
        Func<string>? coverage = null) =>
    [
        HelpCapability.Create(registry),
        DiagnosticsCapability.Create(paths, verbosity, settings, version, coverage),
        JournalCapability.Create(gameState),
        CrewCapability.Create(() => gameState.Active),
        ConversationCapability.Create(settings, llmAvailability, spend, cancellation, speech.Silence),
        PersonaCapability.Create(personas, settings),
        SpeechCapability.Create(speech),
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

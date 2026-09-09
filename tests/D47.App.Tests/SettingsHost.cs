using Avalonia.Controls;
using D47.App.Panel;
using D47.Core.Interface;
using D47.App.Settings;
using D47.Core;
using D47.Core.Configuration;
using D47.Core.Coverage;
using D47.Core.Listening;

namespace D47.App.Tests;

/// <summary>
/// The settings surface in the arrangement it actually ships in: a page of the panel, chosen from the
/// tab strip, inside the one window.
/// </summary>
internal sealed class SettingsHost
{
    /// <summary>
    /// Wide enough for the nav column, which is the arrangement the section-and-scroll tests are about.
    /// </summary>
    private const double DefaultWidth = 1180;

    private const double DefaultHeight = 880;

    private SettingsHost(Window window, PanelView panel, SettingsView view)
    {
        Window = window;
        Panel = panel;
        View = view;
    }

    /// <summary>The window it all hangs in.</summary>
    public Window Window { get; }

    public PanelView Panel { get; }

    /// <summary>The settings surface itself.</summary>
    public SettingsView View { get; }

    public static SettingsHost Open(
        SettingsService settings,
        ViewStateStore viewState,
        AppPaths paths,
        Func<CoverageReport>? coverage = null,
        D47.Core.Actions.MacroStore? macros = null,
        D47.Core.Checklists.ChecklistService? checklists = null,
        IReadOnlyList<string>? reservedPhrases = null,
        Func<WhisperModel, IProgress<ModelProgress>, Task<ModelInstallResult>>? downloadModel = null,
        D47.App.Settings.SwitchEditing? switches = null,
        double width = DefaultWidth,
        double height = DefaultHeight,

 // At the end, so no existing caller's positional width and height move.
        (D47.Core.Diagnostics.Recording.RecordingLog Log, Func<DateTimeOffset> Now)? recording = null,

        // At the end, by the same rule.
        D47.Core.Persona.OwnPersonaStore? ownPersonas = null)
    {
        // The whole page, for every test that is about a row rather than about the
        // fold.com/dseelinger/d47/issues/60).
        settings.Apply(
            D47.Core.Capabilities.Builtin.InterfaceCapability.ShowEverySettingKey,
            "true",
            D47.Core.Configuration.SettingsCaller.Panel);

        var view = new SettingsView();
        var panel = new PanelView { DataContext = new PanelViewModel() };

        // Handed a builder rather than a control, exactly as the window does it, so the deferred build is on
        // the path this exercises too.
        panel.EnableSettings(() =>
        {
            view.Attach(
                settings,
                viewState,
                paths,
                coverage,
                macros,
                checklists,
                reservedPhrases,
                switches,
                downloadModel,
                ownPersonas: ownPersonas,
                recording: recording);
            return view;
        });

        // As the window wires it.
        view.EnableHelp(capabilityId => panel.OpenHelpFor(capabilityId));

        panel.EnableSearch();
        panel.Tab = PanelTab.Settings;

        var window = new Window { Content = panel, Width = width, Height = height };
        window.Show();

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return new SettingsHost(window, panel, view);
    }

    public void Close() => Window.Close();
}

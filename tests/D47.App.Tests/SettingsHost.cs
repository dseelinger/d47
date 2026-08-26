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
/// The settings surface in the arrangement it actually ships in: a page of the panel, chosen
/// from the tab strip, inside the one window (list.md Phase 12).
/// <para>
/// There used to be a <c>SettingsWindow</c> and every test here opened one. Hosting it the way
/// the app does is not ceremony — the surface now sits under a strip, beside a transcript, in a
/// window that already tunnels the push-to-talk key, and every one of those is a thing that
/// could break it. A test against a bare window would go on passing through all of them.
/// </para>
/// </summary>
internal sealed class SettingsHost
{
    /// <summary>
    /// Wide enough for the nav column, which is the arrangement the section-and-scroll tests are
    /// about. The narrow one — what the Commander sees at the default window size — is its own
    /// test rather than the ambient condition of every other one.
    /// </summary>
    private const double DefaultWidth = 1180;

    private const double DefaultHeight = 880;

    private SettingsHost(Window window, PanelView panel, SettingsView view)
    {
        Window = window;
        Panel = panel;
        View = view;
    }

    /// <summary>The window it all hangs in. Captures come from here.</summary>
    public Window Window { get; }

    public PanelView Panel { get; }

    /// <summary>
    /// The settings surface itself. Queries for rows, cards and the scroller go through this
    /// rather than through the window: the transcript is still in the tree behind the page, so
    /// a <c>GetVisualDescendants().OfType&lt;ScrollViewer&gt;().First()</c> off the window finds
    /// the transcript's scroller and not this one.
    /// </summary>
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
        double height = DefaultHeight)
    {
        // The whole page, for every test that is about a row rather than about the fold
        // (https://github.com/dseelinger/d47/issues/60). These tests press controls, read labels
        // and measure columns; none of them is asking what a new Commander sees first, and the
        // fold has its own tests in Core where the predicate lives.
        settings.Apply(
            D47.Core.Capabilities.Builtin.InterfaceCapability.ShowEverySettingKey,
            "true",
            D47.Core.Configuration.SettingsCaller.Panel);

        var view = new SettingsView();
        var panel = new PanelView { DataContext = new PanelViewModel() };

        // Handed a builder rather than a control, exactly as the window does it, so the deferred
        // build is on the path this exercises too.
        panel.EnableSettings(() =>
        {
            view.Attach(
                settings, viewState, paths, coverage, macros, checklists, reservedPhrases, switches, downloadModel);
            return view;
        });

        // As the window wires it. Not optional in a test: unwired, a card's question mark falls
        // back to Process.Start and a test that presses one opens a real browser on the runner.
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

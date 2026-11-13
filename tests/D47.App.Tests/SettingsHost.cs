using Microsoft.Extensions.Logging.Abstractions;
using Avalonia;
using Avalonia.Controls;
using D47.App.Panel;
using D47.Core.Interface;
using D47.App.Settings;
using D47.Core;
using D47.Core.Configuration;
using D47.Core.Coverage;
using D47.App.Theming;
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

    private SettingsHost(Window window, PanelView panel, SettingsView view, SettingsService settings)
    {
        Window = window;
        Panel = panel;
        View = view;
        Settings = settings;
    }

    /// <summary>The window it all hangs in.</summary>
    public Window Window { get; }

    public PanelView Panel { get; }

    /// <summary>The settings surface itself.</summary>
    public SettingsView View { get; }

    /// <summary>The settings the view draws.</summary>
    public SettingsService Settings { get; }

    /// <inheritdoc cref="SettingsRowLookup.ShowPlaceOf"/>
    public bool ShowPlaceOf(string key) => View.ShowPlaceOf(Settings, key);

    /// <inheritdoc cref="SettingsRowLookup.ControlFor"/>
    public Control? ControlFor(string key) => View.ControlFor(Settings, key);

    /// <inheritdoc cref="SettingsRowLookup.LabelFor"/>
    public TextBlock? LabelFor(string key) => View.LabelFor(Settings, key);

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
        D47.Core.Persona.OwnPersonaStore? ownPersonas = null,

        // At the end, by the same rule.
        D47.Core.Input.BindingProfiles? bindingProfiles = null,
        bool headset = false,
        D47.App.Settings.CustomVoicesEditing? customVoices = null,

        // At the end, by the same rule.
        Func<D47.App.Panel.PhrasesPage>? phrases = null,

        // At the end, by the same rule.
        ModulesSource? modules = null,

        // At the end, by the same rule.
        D47.App.Settings.BindingConflictEditing? bindingConflicts = null)
    {
        // The whole page, for every test that is about a row rather than about the
        // fold.com/dseelinger/d47/issues/60).
        settings.Apply(
            D47.Core.Capabilities.Builtin.InterfaceCapability.ShowEverySettingKey,
            "true",
            D47.Core.Configuration.SettingsCaller.Panel);

        var view = new SettingsView();
        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        // Handed a builder rather than a control, exactly as the window does it, so the deferred build is on
        // the path this exercises too.
        panel.EnableSettings(() =>
        {
            if (modules is not null)
            {
                view.EnableModules(modules);
            }

            view.Attach(
                settings,
                viewState,
                coverage,
                macros,
                checklists,
                reservedPhrases,
                switches,
                downloadModel,
                ownPersonas: ownPersonas,
                recording: recording,
                bindingProfiles: bindingProfiles,
                customVoices: customVoices,
                bindingConflicts: bindingConflicts);
            return view;
        }, phrases: phrases);

        panel.EnableSearch();
        panel.Tab = PanelTab.Settings;

        var window = new Window { Content = panel, Width = width, Height = height };
        window.Show();

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return new SettingsHost(window, panel, view, settings);
    }

    /// <summary>Opens on the Voice Input place with the theme following the settings.</summary>
    public static SettingsHost OpenOnVoiceInput()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        viewState.Save(viewState.Load() with { SettingsSection = "voice-input" });

        return Open(settings, viewState, paths);
    }

    /// <summary>Types a query into the tab's search box.</summary>
    public void Search(string query)
    {
        var box = (TextBox)Panel.FindControl<Control>("SearchInput")!;
        box.Text = query;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    public void Close() => Window.Close();
}

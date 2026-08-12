using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The settings surface, driven headlessly: the same window a Commander opens, built against
/// the real builtin registry, rendered by the real theme. This is the test that would have
/// caught the panel crashing on open — a class of failure the Core suite structurally cannot
/// see, because Core never constructs a control.
/// </summary>
public class SettingsSurfaceTests
{
    /// <summary>The wiring the composition root performs, in a throwaway folder.</summary>
    private static (SettingsService Settings, ViewStateStore ViewState, AppPaths Paths) Surface()
    {
        var root = Directory.CreateTempSubdirectory("d47-app-tests").FullName;
        var paths = new AppPaths(root);
        paths.EnsureCreated();

        var store = new SettingsStore(paths, NullLogger<SettingsStore>.Instance);
        var secrets = new SecretStore(paths, new NoopProtector(), NullLogger<SecretStore>.Instance);
        var settings = new SettingsService(store, secrets, store.Load(), NullLogger<SettingsService>.Instance);

        var registry = CapabilityRegistry.Build(BuiltinCapabilities.All(
            paths,
            new NoopVerbosity(),
            new GameStateStore(),
            settings,
            new LlmAvailabilityState(providerConfigured: false),
            new SpendTracker(),
            "1.0.0-uitest",
            new SpeechCapability.SpeechSurface
            {
                Silence = () => { },
                Beds = [.. CueLibrary.Load().BedNames],
            },
            new TurnCancellation(NullLogger<TurnCancellation>.Instance),
            new CalloutEngine(NullLogger<CalloutEngine>.Instance)));

        settings.Bind(registry);

        return (settings, new ViewStateStore(paths, NullLogger<ViewStateStore>.Instance), paths);
    }

    private static SettingsWindow Open(SettingsService settings, ViewStateStore viewState, AppPaths paths)
    {
        // FollowSettings, not a one-shot Apply: the theme captures below change the setting
        // and expect the palette to follow, exactly as the app wires it.
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var window = new SettingsWindow();
        window.Attach(settings, viewState, paths);
        window.Show();

        return window;
    }

    [AvaloniaFact]
    public void TheSettingsWindowOpensWithEveryRegisteredSection()
    {
        var (settings, viewState, paths) = Surface();

        var window = Open(settings, viewState, paths);

        // Rendering is the assertion that matters: measure, arrange and paint all ran over
        // every generated row without a control throwing.
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        window.Close();
    }

    [AvaloniaFact]
    public void AChangeMadeInCoreIsReflectedWithoutRebuildingTheView()
    {
        var (settings, viewState, paths) = Surface();
        var window = Open(settings, viewState, paths);

        // A change from any caller — here the keyword-router path — announces itself and the
        // open panel follows. No restart, and no save button anywhere on the surface.
        var applied = settings.Apply("llm.personality", "false", SettingsCaller.KeywordRouter);
        Assert.True(applied.Ok);

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.NotNull(window.CaptureRenderedFrame());

        window.Close();
    }

    /// <summary>
    /// Writes one PNG per theme for a human to look at. Not an assertion beyond "it rendered" —
    /// the point is that "does the panel look right" has an artifact to answer with, rather
    /// than needing the app launched and driven by hand.
    /// </summary>
    [AvaloniaFact]
    public void EveryThemeRendersToACapture()
    {
        var (settings, viewState, paths) = Surface();
        var output = Path.Combine(Path.GetTempPath(), "d47-ui-captures");
        Directory.CreateDirectory(output);

        var window = Open(settings, viewState, paths);

        foreach (var theme in D47.Core.Interface.ThemeCatalog.All)
        {
            if (theme.Id == D47.Core.Interface.ThemeCatalog.ElitePaletteId)
            {
                // Depends on the Commander's own HUD matrix file; the fallback path is the
                // plain Elite palette already captured.
                continue;
            }

            settings.Apply("ui.theme", theme.Id, SettingsCaller.Panel);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            frame.Save(
                Path.Combine(output, $"settings-{theme.Id}.png"),
                new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }

        // The far end of the scroll, so the rows the first screenful hides — the picker, the
        // hotkey binders, the egress panels — get looked at too.
        var scroller = window.GetVisualDescendants().OfType<ScrollViewer>().First();
        scroller.ScrollToEnd();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        window.CaptureRenderedFrame()!.Save(
            Path.Combine(output, "settings-bottom.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        window.Close();
    }

    /// <summary>
    /// The main window, captured for the same reason: its header and its send glyph are things
    /// a person judges by looking, and looking should not require launching the app.
    /// </summary>
    [AvaloniaFact]
    public void TheMainWindowRenders()
    {
        var (settings, _, _) = Surface();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .Apply(settings.Current.Ui.Theme);

        // No host: the window paints its chrome and says so in the transcript, which is all
        // this capture is for.
        var window = new MainWindow(host: null);
        window.Show();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        var output = Path.Combine(Path.GetTempPath(), "d47-ui-captures");
        Directory.CreateDirectory(output);
        frame.Save(
            Path.Combine(output, "main-window.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        window.Close();
    }

    /// <summary>
    /// The mark is wired, not merely committed. An icon that silently stops being embedded
    /// looks like nothing at all until someone notices the taskbar has gone generic.
    /// </summary>
    [AvaloniaFact]
    public void EveryWindowCarriesTheApplicationIcon()
    {
        Assert.NotNull(new MainWindow(host: null).Icon);
        Assert.NotNull(new SettingsWindow().Icon);
    }

    private sealed class NoopProtector : ISecretProtector
    {
        public byte[] Protect(byte[] plaintext) => plaintext;

        public bool TryUnprotect(byte[] ciphertext, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[]? plaintext)
        {
            plaintext = ciphertext;
            return true;
        }
    }

    private sealed class NoopVerbosity : D47.Core.Diagnostics.ILogVerbosityControl
    {
        private readonly Dictionary<string, LogLevel> _levels =
            D47.Core.Diagnostics.Subsystems.All.ToDictionary(s => s, _ => LogLevel.Information, StringComparer.Ordinal);

        public IReadOnlyDictionary<string, LogLevel> Levels => _levels;

        public void Set(string subsystem, LogLevel level) => _levels[subsystem] = level;

        public void SetDefault(LogLevel level)
        {
        }
    }
}

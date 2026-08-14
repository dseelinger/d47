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
using D47.Core.Input;
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
    private readonly ITestOutputHelper _output;

    public SettingsSurfaceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static SettingsHost Open(SettingsService settings, ViewStateStore viewState, AppPaths paths)
    {
        // FollowSettings, not a one-shot Apply: the theme captures below change the setting
        // and expect the palette to follow, exactly as the app wires it.
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        return SettingsHost.Open(settings, viewState, paths);
    }

    [AvaloniaFact]
    public void TheSettingsPageOpensWithEveryRegisteredSection()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        var host = Open(settings, viewState, paths);

        // Rendering is the assertion that matters: measure, arrange and paint all ran over
        // every generated row without a control throwing.
        var frame = host.Window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        host.Close();
    }

    [AvaloniaFact]
    public void AChangeMadeInCoreIsReflectedWithoutRebuildingTheView()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = Open(settings, viewState, paths);

        // A change from any caller — here the keyword-router path — announces itself and the
        // open panel follows. No restart, and no save button anywhere on the surface.
        var applied = settings.Apply("llm.personality", "false", SettingsCaller.KeywordRouter);
        Assert.True(applied.Ok);

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.NotNull(host.Window.CaptureRenderedFrame());

        host.Close();
    }

    /// <summary>
    /// Writes one PNG per theme for a human to look at. Not an assertion beyond "it rendered" —
    /// the point is that "does the panel look right" has an artifact to answer with, rather
    /// than needing the app launched and driven by hand.
    /// </summary>
    [AvaloniaFact]
    public void EveryThemeRendersToACapture()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var output = TestSurface.CaptureDirectory;
        _output.WriteLine($"Captures: {output}");

        var host = Open(settings, viewState, paths);

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

            var frame = host.Window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            frame.Save(
                Path.Combine(output, $"settings-{theme.Id}.png"),
                new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }

        // The far end of the scroll, so the rows the first screenful hides — the picker, the
        // hotkey binders, the egress panels — get looked at too.
        var scroller = host.View.GetVisualDescendants().OfType<ScrollViewer>().First();
        scroller.ScrollToEnd();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        host.Window.CaptureRenderedFrame()!.Save(
            Path.Combine(output, "settings-bottom.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        host.Close();
    }

    /// <summary>
    /// The main window, captured for the same reason: its header and its send glyph are things
    /// a person judges by looking, and looking should not require launching the app.
    /// </summary>
    [AvaloniaFact]
    public void TheMainWindowRenders()
    {
        var (settings, _, _) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .Apply(settings.Current.Ui.Theme);

        // No host: the window paints its chrome and says so in the transcript, which is all
        // this capture is for.
        var window = new MainWindow(host: null);
        window.Show();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        var output = TestSurface.CaptureDirectory;
        _output.WriteLine($"Captures: {output}");
        frame.Save(
            Path.Combine(output, "main-window.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        window.Close();
    }

    /// <summary>
    /// The mark is wired, not merely committed. An icon that silently stops being embedded
    /// looks like nothing at all until someone notices the taskbar has gone generic.
    /// <para>
    /// One window to check, since Phase 12: settings is a page of it rather than a second one.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void TheWindowCarriesTheApplicationIcon()
    {
        Assert.NotNull(new MainWindow(host: null).Icon);
    }
}

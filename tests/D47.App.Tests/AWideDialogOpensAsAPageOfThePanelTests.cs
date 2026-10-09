using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Coverage;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The dialogs wider than the design's 640 open as pages of the panel, named by a breadcrumb that
/// leads back to where they were opened (#529).
/// </summary>
public class AWideDialogOpensAsAPageOfThePanelTests
{
    private static readonly PixelSize Quad = new(1180, 880);

    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static SettingsView Attached()
    {
        var (settings, viewState, _) = TestSurface.Create(coverage: () => "One line, never exercised.");

        settings.Apply(InterfaceCapability.ShowEverySettingKey, "true", SettingsCaller.Panel);

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        var view = new SettingsView();
        view.Attach(settings, viewState, () => new CoverageReport([]));

        return view;
    }

    private static (Window Window, PanelView Panel, SettingsView View) Desktop()
    {
        var view = Attached();
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableSettings(() => view);

        var window = new Window { Content = panel, Width = Quad.Width, Height = Quad.Height };
        window.Show();

        panel.Tab = PanelTab.Settings;
        Jobs();

        view.ShowPlaceOf(DiagnosticsCapability.CoverageKey);
        Jobs();

        return (window, panel, view);
    }

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();
    }

    private static Button Named(Visual root, string name) =>
        root.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void TheCoverageRowOpensAPageNamedByItsBreadcrumbAndNoWindow()
    {
        var (window, panel, view) = Desktop();

        Press(Named(view, "OpenCoverage"));

        Assert.Empty(window.OwnedWindows);
        Assert.Equal(["Settings", "Coverage"], panel.Nav.Trail.Select(crumb => crumb.Word));

        var page = Assert.Single(panel.GetVisualDescendants().OfType<CoveragePage>());

        // Drawn at the panel's width, not a window's.
        Assert.True(
            page.Bounds.Width > 1000,
            $"the page was {page.Bounds.Width:0} wide in a panel {Quad.Width} wide");

        window.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void BackLeavesThePageForWhereItWasOpened()
    {
        var (window, panel, view) = Desktop();

        Press(Named(view, "OpenCoverage"));

        var page = panel.GetVisualDescendants().OfType<CoveragePage>().Single();

        Assert.True(panel.GoBack());
        Jobs();

        Assert.True(page.Left.IsCompleted);
        Assert.Equal(["Settings"], panel.Nav.Trail.Select(crumb => crumb.Word));
        Assert.Empty(panel.GetVisualDescendants().OfType<CoveragePage>());

        window.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void ThePagesOwnCloseButtonGoesBackToo()
    {
        var (window, panel, view) = Desktop();

        Press(Named(view, "OpenCoverage"));

        var page = panel.GetVisualDescendants().OfType<CoveragePage>().Single();

        Press(Named(page, "CoverageClose"));

        Assert.True(page.Left.IsCompleted);
        Assert.Equal(["Settings"], panel.Nav.Trail.Select(crumb => crumb.Word));

        window.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void OpenedAgainItIsANewPage()
    {
        var (window, panel, view) = Desktop();

        Press(Named(view, "OpenCoverage"));
        var first = panel.GetVisualDescendants().OfType<CoveragePage>().Single();

        panel.GoBack();
        Jobs();

        Press(Named(view, "OpenCoverage"));
        var second = panel.GetVisualDescendants().OfType<CoveragePage>().Single();

        Assert.NotSame(first, second);

        window.Close();
    }

    [AvaloniaFact]
    public void OnTheTranscriptTabThePageTakesThePlaceOfTheTranscript()
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };
        var window = new Window { Content = panel, Width = Quad.Width, Height = Quad.Height };

        window.Show();
        Jobs();

        Assert.Equal(PanelTab.Transcript, panel.Tab);

        var page = new ChangelogPage("- Added a thing");
        var left = panel.Open(page);
        Jobs();

        Assert.Equal("What changed", panel.Nav.Trail[^1].Word);
        Assert.Contains(page, panel.GetVisualDescendants());
        Assert.True(page.IsEffectivelyVisible, "the page is on screen");

        Assert.True(panel.GoBack());
        Jobs();

        Assert.True(left.IsCompleted);
        Assert.DoesNotContain(page, panel.GetVisualDescendants());

        window.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void TheHeadsetRayOpensItOnTheHeadsetPanel()
    {
        var view = Attached();

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableSettings(() => view);
        panel.Tab = PanelTab.Settings;

        using var surface = new OffscreenSurface(panel, Quad);

        surface.Render();
        Jobs();
        surface.Render();

        view.ShowPlaceOf(DiagnosticsCapability.CoverageKey);
        Jobs();
        surface.Render();

        var button = Named(view, "OpenCoverage");

        Assert.DoesNotContain(OffscreenSurface.DesktopOnly, button.Classes);

        var at = button.TranslatePoint(
            new Point(button.Bounds.Width / 2, button.Bounds.Height / 2),
            surface.View);

        Assert.NotNull(at);
        Assert.True(surface.Click(at!.Value), "the ray pressed the row");
        Jobs();

        Assert.Equal("Coverage", panel.Nav.Trail[^1].Word);
        Assert.Empty(((Window)surface.Root).OwnedWindows);
    }

    /// <summary>What the page looks like inside the panel, saved for a person to check.</summary>
    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void ThePageInThePanelIsCaptured()
    {
        var (window, panel, view) = Desktop();

        Press(Named(view, "OpenCoverage"));

        var frame = window.CaptureRenderedFrame();

        Assert.NotNull(frame);
        frame!.SaveCapture("wide-dialog-coverage-page.png");

        panel.GoBack();
        Jobs();

        panel.Tab = PanelTab.Transcript;
        Jobs();

        _ = panel.Open(new HelpImprovePage(
            new DateTimeOffset(2026, 9, 1, 21, 0, 0, TimeSpan.Zero),
            TestSurface.Excerpt("a line"),
            destination: "donations.example"));
        Jobs();

        window.CaptureRenderedFrame()!.SaveCapture("wide-dialog-help-improve-page.png");

        window.Close();
    }
}

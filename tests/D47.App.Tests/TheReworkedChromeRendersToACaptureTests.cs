using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Pictures of the reworked chrome, for looking at rather than for asserting on
/// (remediation.md 10, items 1 to 4).
/// <para>
/// The assertions live in <c>TheTabStripFitsAnyWidthTests</c>. This is the other half of checking
/// a layout, and the repo's own convention for it: an overlap, a label hanging low and a glyph
/// off-centre in its highlight are all things a test can be written to miss and an eye cannot.
/// </para>
/// </summary>
public class TheReworkedChromeRendersToACaptureTests
{
    /// <summary>A short route with the two things a hop can carry: a hazard, and no scoop.</summary>
    private static NavRoute Plotted => new()
    {
        Hops =
        [
            new RouteHop("Sol", "G") { Position = (0, 0, 0) },
            new RouteHop("Alpha Centauri", "K") { Position = (3, 2, 1) },
            new RouteHop("Jackson's Lighthouse", "N") { Position = (9, 4, 2) },
            new RouteHop("Wolf 359", "T") { Position = (14, 6, 3) },
            new RouteHop("Shinrarta Dezhra", "K") { Position = (22, 9, 5) },
        ],
    };

    private static RoutePlanBook Plans()
    {
        var folder = Path.Combine(Path.GetTempPath(), "d47-capture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        var book = new RoutePlanBook(
            Path.Combine(folder, "route-plans.json"),
            NullLogger<RoutePlanBook>.Instance);

        book.Record(
            new PlottedRoute(
                "Sol",
                "Colonia",
                22_000,
                168,
                [
                    new RouteWaypoint("PSR J1752-2806", 10, 21_629, true),
                    new RouteWaypoint("Nova Aquila No 3", 6, 21_350, true),
                    new RouteWaypoint("Skaudai AM-B d14-138", 8, 18_902, false),
                ]),
            "Sol to Colonia",
            new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.Zero));

        return book;
    }

    private static (Window Window, PanelView Panel) Open(double width, PanelTab tab = PanelTab.Transcript)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var model = new PanelViewModel();

        model.Append("Fixture One, docked. Fuel at 82 percent.\n");
        model.Append("\n> what is the beacon at Shinrarta Dezhra\n");
        model.Append("The beacon is quiet. Nothing on the board worth the detour.");

        var panel = new PanelView { DataContext = model };

        panel.EnableRouting(new RoutingSurface(
            () => Plotted,
            () => "Alpha Centauri",
            CapabilityRegistry.Build([]),
            Plans(),
            () => true));

        panel.Furnish(PanelTab.Checklist, _ => new TextBlock { Text = "checklist" }, new NavCrumb("checklist", "Checklist"));
        panel.Furnish(
            PanelTab.Loadout,
            crumb => new TextBlock { Text = crumb.Word },
            new NavCrumb("fleet", "Ships"),
            new NavCrumb("locker", "Suits and weapons"));
        panel.Furnish(PanelTab.Engineers, _ => new TextBlock { Text = "engineers" }, new NavCrumb("engineers", "Engineers"));
        panel.Furnish(PanelTab.Utilities, _ => new TextBlock { Text = "utilities" }, new NavCrumb("utilities", "Utilities"));
        panel.EnableSettings(() => new TextBlock { Text = "settings" });
        panel.EnableSearch();

        var window = new Window { Content = panel, Width = width, Height = 620 };

        window.Show();
        panel.Tab = tab;
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    private static void Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();

        window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, name),
            new PngBitmapEncoderOptions());
    }

    [AvaloniaFact]
    public void TheWideStrip()
    {
        var (window, _) = Open(1200);

        Save(window, "chrome-wide.png");
        window.Close();
    }

    /// <summary>The width the overlap was reported at.</summary>
    [AvaloniaFact]
    public void TheNarrowStrip()
    {
        var (window, _) = Open(620);

        Save(window, "chrome-narrow.png");
        window.Close();
    }

    /// <summary>Narrow enough that the steppers are carrying the strip on their own.</summary>
    [AvaloniaFact]
    public void TheVeryNarrowStrip()
    {
        var (window, _) = Open(380);

        Save(window, "chrome-tiny.png");
        window.Close();
    }

    /// <summary>The mode chooser open, which is where the readings live now.</summary>
    [AvaloniaFact]
    public void TheModeChooser()
    {
        var (window, panel) = Open(1200);

        // The box as it sits, not opened (#231). A ComboBox drops its list into a popup, and a
        // popup is its own top level — so it is not in this window's capture whether it is open
        // or not. What the picture is for is the control in the row beside the search box.
        Assert.True(
            panel.GetControl<StackPanel>("ModePicker").IsVisible,
            "there was no mode control to photograph");

        Save(window, "chrome-modes.png");
        window.Close();
    }

    /// <summary>
    /// The search row with matches in it, which is the state the gap was reported in
    /// (remediation.md 11, item 2).
    /// </summary>
    [AvaloniaFact]
    public void TheSearchRowWithMatches()
    {
        var (window, panel) = Open(1900);

        panel.GetControl<TextBox>("SearchInput").Text = "beacon";
        Dispatcher.UIThread.RunJobs();

        Save(window, "chrome-search.png");
        window.Close();
    }

    /// <summary>A furnished page, where Copy All must not be.</summary>
    [AvaloniaFact]
    public void AFurnishedPage()
    {
        var (window, _) = Open(1200, PanelTab.Checklist);

        Save(window, "chrome-checklist.png");
        window.Close();
    }

    /// <summary>
    /// Progress: the route being flown, all of it. The eye is checking that a hazard badge and a
    /// scoop badge sit on their row rather than colliding, and that the hop you are on is
    /// findable at a glance.
    /// </summary>
    [AvaloniaFact]
    public void TheRouteBeingFlown()
    {
        var (window, panel) = Open(1180, PanelTab.Routing);

        panel.Nav.SelectRoot(RoutingPages.ProgressRoot);
        Save(window, "routing-progress.png");
    }

    /// <summary>Plan: three cards of form, and whether they read as three things or one mess.</summary>
    [AvaloniaFact]
    public void ThePlannersAsForms()
    {
        var (window, panel) = Open(1180, PanelTab.Routing);

        panel.Nav.SelectRoot(RoutingPages.PlanRoot);
        Save(window, "routing-plan.png");
    }

    /// <summary>A plan drawn whole, as a level under Plan.</summary>
    [AvaloniaFact]
    public void APlanThatWasMade()
    {
        var (window, panel) = Open(1180, PanelTab.Routing);

        panel.Nav.SelectRoot(RoutingPages.PlanRoot);
        panel.Nav.Drill(RoutingPages.ResultCrumb(RoutePlanKind.Jump, "Sol to Colonia"));
        Save(window, "routing-result.png");
    }

    /// <summary>Course: the clipboard, and the drive that can fail.</summary>
    [AvaloniaFact]
    public void SettingACourse()
    {
        var (window, panel) = Open(1180, PanelTab.Routing);

        panel.Nav.SelectRoot(RoutingPages.CourseRoot);
        Save(window, "routing-course.png");
    }
}

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Capabilities;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Routing tab (list.md Phase 37): where the Commander is going, in three readings of one
/// journey.
/// <para>
/// The tab is furnished by the desktop window and by nothing else, so most of what is asserted
/// here is that it behaves like every other furnished tab — and that Progress draws the whole
/// route, which is the thing the spoken form structurally cannot do.
/// </para>
/// </summary>
public class TheRoutingTabTests
{
    private static RouteHop Hop(string system, double x = 0, string? starClass = "G") =>
        new(system, starClass) { Position = (x, 0, 0) };

    private static NavRoute Route(params RouteHop[] hops) => new() { Hops = hops };

    private static PanelView Laid(PanelView panel, double width = 900)
    {
        var window = new Window { Content = panel, Width = width, Height = 700 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return panel;
    }

    /// <summary>
    /// The tab with Progress alone — which is also the shape the headset would be given if it
    /// ever got this tab, and is the reason the roots are flags.
    /// </summary>
    private static PanelView Furnished(NavRoute route, string? here = null)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableRouting(
            new RoutingSurface(() => route, () => here),
            plan: false,
            course: false);

        return Laid(panel);
    }

    private static IEnumerable<string> TextOf(PanelView panel) =>
        panel.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .Where(text => text.Length > 0);

    /// <summary>
    /// A tab nobody furnished is not drawn, and the headset furnishes nothing — which is the
    /// whole of "desktop only" (CLAUDE.md: a tab is withdrawn from a surface by not making the
    /// call).
    /// </summary>
    [AvaloniaFact]
    public void TheTabIsAbsentUntilAHostFurnishesIt()
    {
        var panel = Laid(new PanelView { DataContext = new PanelViewModel() });

        Assert.False(panel.GetControl<RadioButton>("RoutingTab").IsVisible);

        panel.Tab = PanelTab.Routing;

        // Not merely undrawn — unreachable. Selecting a tab nobody built has to fall back rather
        // than leave the panel on an empty page.
        Assert.Equal(PanelTab.Transcript, panel.Tab);
    }

    [AvaloniaFact]
    public void FurnishingItShowsTheTabAndSelectingItSticks()
    {
        var panel = Furnished(Route(Hop("Sol"), Hop("Alpha Centauri", 4)));

        Assert.True(panel.GetControl<RadioButton>("RoutingTab").IsVisible);

        panel.Tab = PanelTab.Routing;

        Assert.Equal(PanelTab.Routing, panel.Tab);

        // The root is the first crumb, and it is a word the Commander can say as well as press.
        Assert.Equal("Progress", panel.Nav.Root.Word);
    }

    /// <summary>
    /// The item that justifies the tab: every hop, not the next handful. A spoken answer is
    /// capped because reading 131 waypoints aloud is not an answer; a screen has no such cap.
    /// </summary>
    [AvaloniaFact]
    public void ProgressDrawsEveryHopRatherThanTheNextFew()
    {
        var hops = Enumerable.Range(0, 40).Select(index => Hop($"Waypoint {index}", index)).ToArray();
        var panel = Furnished(Route(hops), "Waypoint 0");

        panel.Tab = PanelTab.Routing;
        Dispatcher.UIThread.RunJobs();

        var drawn = TextOf(panel).ToArray();

        Assert.Contains("Waypoint 0", drawn);
        Assert.Contains("Waypoint 39", drawn);
        Assert.Contains(drawn, text => text.Contains("39 jumps left", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void NothingPlottedSaysSoRatherThanDrawingAnEmptyList()
    {
        var panel = Furnished(NavRoute.None);

        panel.Tab = PanelTab.Routing;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(TextOf(panel), text => text.Contains("No route plotted", StringComparison.Ordinal));
    }

    /// <summary>
    /// A hazard rides on the hop rather than in a preamble, because it is the only line that
    /// changes what the Commander does on arrival — the same call routes.md records for the
    /// spoken form.
    /// </summary>
    [AvaloniaFact]
    public void AHazardousHopCarriesItsWarningOnItsOwnRow()
    {
        var panel = Furnished(
            Route(Hop("Sol"), Hop("Jackson's Lighthouse", 4, "N"), Hop("Colonia", 9)),
            "Sol");

        panel.Tab = PanelTab.Routing;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(TextOf(panel), text => text.Contains("neutron", StringComparison.Ordinal));
    }

    /// <summary>
    /// A class d47 does not recognise is drawn as not knowing, never as "no". A Commander told a
    /// star is unscoopable routes around one that would have refuelled them.
    /// </summary>
    [AvaloniaFact]
    public void AnUnknownStarClassIsDrawnAsUnknownRatherThanAsUnscoopable()
    {
        var panel = Furnished(Route(Hop("Sol"), Hop("Somewhere", 4, "ZZ")), "Sol");

        panel.Tab = PanelTab.Routing;
        Dispatcher.UIThread.RunJobs();

        var drawn = TextOf(panel).ToArray();

        Assert.Contains(drawn, text => text.Contains("scoop unknown", StringComparison.Ordinal));
        Assert.DoesNotContain(drawn, text => text.Contains("no scoop", StringComparison.Ordinal));
    }

    /// <summary>
    /// Being somewhere the route does not mention is said out loud, because the alternative is a
    /// jumps-remaining figure that quietly means something else.
    /// </summary>
    [AvaloniaFact]
    public void BeingOffTheRouteIsStatedRatherThanImplied()
    {
        var panel = Furnished(Route(Hop("Sol"), Hop("Alpha Centauri", 4)), "Shinrarta Dezhra");

        panel.Tab = PanelTab.Routing;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(
            TextOf(panel),
            text => text.Contains("not on this route", StringComparison.Ordinal));
    }

    private static RoutePlanBook Book(string folder)
    {
        Directory.CreateDirectory(folder);

        return new RoutePlanBook(
            Path.Combine(folder, "route-plans.json"),
            NullLogger<RoutePlanBook>.Instance);
    }

    private static string Scratch() =>
        Path.Combine(Path.GetTempPath(), "d47-routing-" + Guid.NewGuid().ToString("N"));

    private static PanelView FullyFurnished(
        NavRoute route,
        RoutePlanBook plans,
        bool lookups = true,
        Action? openSettings = null)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableRouting(new RoutingSurface(
            () => route,
            () => null,
            CapabilityRegistry.Build([]),
            plans,
            () => lookups,
            openSettings));

        return Laid(panel);
    }

    /// <summary>
    /// Three readings of one journey, in one tab and one mode control — the same collapse
    /// Transcript makes for Conversation, Technical and the log file.
    /// </summary>
    [AvaloniaFact]
    public void TheTabCarriesThreeModesRatherThanThreeTabs()
    {
        var folder = Scratch();

        try
        {
            var panel = FullyFurnished(NavRoute.None, Book(folder));

            var words = panel.Nav.Roots(PanelTab.Routing).Select(root => root.Word).ToArray();

            Assert.Equal(["Plan", "Progress", "Course"], words);

            // And exactly one tab was spent on them.
            Assert.True(panel.GetControl<RadioButton>("RoutingTab").IsVisible);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// Plotting off is a capability that is off rather than an error, and it says which setting
    /// turns it on — the same answer the tool gives.
    /// </summary>
    [AvaloniaFact]
    public void PlanSaysPlottingIsOffRatherThanFailing()
    {
        var folder = Scratch();

        try
        {
            var opened = 0;
            var panel = FullyFurnished(NavRoute.None, Book(folder), lookups: false, () => opened++);

            panel.Tab = PanelTab.Routing;
            panel.Nav.SelectRoot(RoutingPages.PlanRoot);
            Dispatcher.UIThread.RunJobs();

            var drawn = TextOf(panel).ToArray();

            Assert.Contains(drawn, text => text.Contains("switched off", StringComparison.Ordinal));
            Assert.Contains(drawn, text => text.Contains("Look things up in the galaxy", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [AvaloniaFact]
    public void PlanOffersTheThreePlannersWhenLookupsAreOn()
    {
        var folder = Scratch();

        try
        {
            var panel = FullyFurnished(NavRoute.None, Book(folder));

            panel.Tab = PanelTab.Routing;
            panel.Nav.SelectRoot(RoutingPages.PlanRoot);
            Dispatcher.UIThread.RunJobs();

            var drawn = TextOf(panel).ToArray();

            Assert.Contains("Jump route", drawn);
            Assert.Contains("Road to Riches", drawn);
            Assert.Contains("Trade run", drawn);

            // The one figure that is about the Commander rather than their ship.
            Assert.Contains(drawn, text => text.Contains("never read from the journal", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// A stored plan opens as a level and is drawn whole. The capability speaks five waypoints
    /// because 131 read aloud is not an answer; that cap belongs to speech and not to the plan.
    /// </summary>
    [AvaloniaFact]
    public void AStoredPlanIsDrawnWholeRatherThanCutToWhatWasSpoken()
    {
        var folder = Scratch();

        try
        {
            var plans = Book(folder);

            var waypoints = Enumerable
                .Range(0, 30)
                .Select(index => new RouteWaypoint($"Waypoint {index}", 3, 1000 - index, index % 7 == 0))
                .ToArray();

            plans.Record(
                new PlottedRoute("Sol", "Colonia", 22_000, 168, waypoints),
                "Sol to Colonia",
                new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.Zero));

            var panel = FullyFurnished(NavRoute.None, plans);

            panel.Tab = PanelTab.Routing;
            panel.Nav.SelectRoot(RoutingPages.PlanRoot);
            panel.Nav.Drill(RoutingPages.ResultCrumb(RoutePlanKind.Jump, "Sol to Colonia"));
            Dispatcher.UIThread.RunJobs();

            var drawn = TextOf(panel).ToArray();

            Assert.Contains("Waypoint 0", drawn);
            Assert.Contains("Waypoint 29", drawn);
            Assert.Contains(drawn, text => text.Contains("168 jumps", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// A plan can go away between the button being drawn and being pressed: the file is
    /// hand-editable and another plot replaces what was there.
    /// </summary>
    [AvaloniaFact]
    public void AResultLevelForAPlanThatIsGoneSaysSo()
    {
        var folder = Scratch();

        try
        {
            var panel = FullyFurnished(NavRoute.None, Book(folder));

            panel.Tab = PanelTab.Routing;
            panel.Nav.SelectRoot(RoutingPages.PlanRoot);
            panel.Nav.Drill(RoutingPages.ResultCrumb(RoutePlanKind.Trade, "gone"));
            Dispatcher.UIThread.RunJobs();

            Assert.Contains(
                TextOf(panel),
                text => text.Contains("no longer here", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// Course opens on the system the Commander is already going to, which is the one they
    /// usually want, and says that the clipboard is the half that always works.
    /// </summary>
    [AvaloniaFact]
    public void CourseStartsFromWhereTheRouteEnds()
    {
        var folder = Scratch();

        try
        {
            var panel = FullyFurnished(Route(Hop("Sol"), Hop("Colonia", 4)), Book(folder));

            panel.Tab = PanelTab.Routing;
            panel.Nav.SelectRoot(RoutingPages.CourseRoot);
            Dispatcher.UIThread.RunJobs();

            var boxes = panel.GetVisualDescendants().OfType<TextBox>().Select(box => box.Text).ToArray();

            Assert.Contains("Colonia", boxes);
            Assert.Contains(
                TextOf(panel),
                text => text.Contains("clipboard first", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Capabilities;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Routing tab: where the Commander is going, in three readings of one journey.</summary>
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
    /// The tab with Progress alone: a surface that furnishes one root and not the others, which is
    /// what the flags are for.
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
    /// A tab nobody furnished is not drawn, and the headset furnishes nothing — which is the whole of
    /// "desktop only" (CLAUDE.md: a tab is withdrawn from a surface by not making the call).
    /// </summary>
    [AvaloniaFact]
    public void TheTabIsAbsentUntilAHostFurnishesIt()
    {
        var panel = Laid(new PanelView { DataContext = new PanelViewModel() });

        Assert.False(panel.GetControl<RadioButton>("RoutingTab").IsVisible);

        panel.Tab = PanelTab.Routing;

        // Not merely undrawn — unreachable.
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

    /// <summary>The item that justifies the tab: every hop, not the next handful.</summary>
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
    /// A hazard rides on the hop rather than in a preamble, because it is the only line that changes
    /// what the Commander does on arrival — the same call routes.md records for the spoken form.
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

    /// <summary>A class d47 does not recognise is drawn as not knowing, never as "no".</summary>
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
    /// jumps-remaining figure that silently means something else.
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
        Action? openSettings = null,
        D47.Core.Capabilities.Builtin.IClipboard? clipboard = null,
        string? here = null)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableRouting(new RoutingSurface(
            () => route,
            () => here,
            CapabilityRegistry.Build([]),
            plans,
            () => lookups,
            openSettings,
            Clipboard: clipboard));

        return Laid(panel);
    }

    /// <summary>Opens the tab's Plan root already at mini's size.</summary>
    private static PanelView MiniOnPlan(NavRoute route, RoutePlanBook plans, string? here = null)
    {
        var panel = FullyFurnished(route, plans, here: here);

        panel.Tab = PanelTab.Routing;
        panel.Nav.SelectRoot(RoutingPages.PlanRoot);
        panel.Mode = PanelMode.Mini;
        Dispatcher.UIThread.RunJobs();

        return panel;
    }

    /// <summary>
    /// Three readings of one journey, in one tab and one mode control — the same collapse Transcript
    /// makes for Conversation, Technical and the log file.
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
    /// Plotting off is a capability that is off rather than an error, and it says which setting turns
    /// it on — the same answer the tool gives.
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

            Assert.Contains("NEUTRON PLOTTER", drawn);
            Assert.Contains("ROAD TO RICHES", drawn);
            Assert.Contains("TRADE RUN", drawn);

            // The one figure that is about the Commander rather than their ship.
            Assert.Contains(drawn, text => text.Contains("never read from the journal", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

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

    /// <summary>Every waypoint on a stored plan carries a copy glyph, wired to the surface's clipboard (#157).</summary>
    [AvaloniaFact]
    public void AStoredPlansWaypointsEachCarryACopyGlyph()
    {
        var folder = Scratch();

        try
        {
            var plans = Book(folder);

            plans.Record(
                new PlottedRoute(
                    "Sol", "Colonia", 22_000, 168,
                    [new RouteWaypoint("Waypoint 0", 3, 1000, IsNeutron: false)]),
                "Sol to Colonia",
                new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.Zero));

            var clipboard = new D47.Core.Capabilities.Builtin.RecordingClipboard();
            var panel = FullyFurnished(NavRoute.None, plans, clipboard: clipboard);

            panel.Tab = PanelTab.Routing;
            panel.Nav.SelectRoot(RoutingPages.PlanRoot);
            panel.Nav.Drill(RoutingPages.ResultCrumb(RoutePlanKind.Jump, "Sol to Colonia"));
            Dispatcher.UIThread.RunJobs();

            var glyph = panel.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => AutomationProperties.GetName(button) == "Copy Waypoint 0");

            glyph.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(["Waypoint 0"], clipboard.Written);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// A plan can go away between the button being drawn and being pressed: the file is hand-editable
    /// and another plot replaces what was there.
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

    /// <summary>A three-waypoint Jump plan, recorded into the given book (#197).</summary>
    private static void RecordJumpPlan(RoutePlanBook plans) =>
        plans.Record(
            new PlottedRoute(
                "Sol",
                "Colonia",
                1_000,
                3,
                [
                    new RouteWaypoint("Waypoint 0", 1, 700, IsNeutron: true),
                    new RouteWaypoint("Waypoint 1", 1, 300, IsNeutron: false),
                    new RouteWaypoint("Colonia", 1, 0, IsNeutron: false),
                ]),
            "Sol to Colonia",
            new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero));

    /// <summary>
    /// Mini's Plan root shows the stored plan as a list of waypoints, not the three planner forms
    /// (#197).
    /// </summary>
    [AvaloniaFact]
    public void MiniOnThePlanRootShowsTheStoredPlanRatherThanTheForms()
    {
        var folder = Scratch();

        try
        {
            var plans = Book(folder);
            RecordJumpPlan(plans);

            var panel = MiniOnPlan(NavRoute.None, plans);

            var drawn = TextOf(panel).ToArray();

            Assert.Contains("Sol", drawn);
            Assert.Contains("Waypoint 0", drawn);
            Assert.Contains("Waypoint 1", drawn);
            Assert.Contains("Colonia", drawn);

            Assert.DoesNotContain("Neutron Plotter", drawn);
            Assert.DoesNotContain("Road to Riches", drawn);
            Assert.DoesNotContain("Trade run", drawn);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>With no stored plan, mini says so rather than drawing the forms (#197).</summary>
    [AvaloniaFact]
    public void MiniWithNoStoredPlanSaysSo()
    {
        var folder = Scratch();

        try
        {
            var panel = MiniOnPlan(NavRoute.None, Book(folder));

            var drawn = TextOf(panel).ToArray();

            Assert.Contains(drawn, text => text.Contains("No neutron route is plotted", StringComparison.Ordinal));
            Assert.DoesNotContain("Neutron Plotter", drawn);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>NavRoute.json ending at a waypoint marks that one next, ahead of position (#197).</summary>
    [AvaloniaFact]
    public void MiniMarksTheWaypointNavRouteEndsAt()
    {
        var folder = Scratch();

        try
        {
            var plans = Book(folder);
            RecordJumpPlan(plans);

            var route = Route(Hop("Sol"), Hop("Waypoint 0", 10));
            var panel = MiniOnPlan(route, plans, here: "Waypoint 1");

            var drawn = TextOf(panel).ToArray();

            Assert.Equal(1, drawn.Count(text => text == "next"));

            var next = panel.GetVisualDescendants()
                .OfType<StackPanel>()
                .First(row => row.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "next"));

            Assert.Contains(
                next.GetVisualDescendants().OfType<TextBlock>(),
                text => text.Text == "Waypoint 0");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>With no route plotted, being at waypoint k marks waypoint k+1 next (#197).</summary>
    [AvaloniaFact]
    public void MiniMarksTheWaypointAfterWhereTheCommanderIs()
    {
        var folder = Scratch();

        try
        {
            var plans = Book(folder);
            RecordJumpPlan(plans);

            var panel = MiniOnPlan(NavRoute.None, plans, here: "Waypoint 0");

            var next = panel.GetVisualDescendants()
                .OfType<StackPanel>()
                .First(row => row.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "next"));

            Assert.Contains(
                next.GetVisualDescendants().OfType<TextBlock>(),
                text => text.Text == "Waypoint 1");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>Standing on the final waypoint says the destination is reached, with nothing marked next (#197).</summary>
    [AvaloniaFact]
    public void MiniSaysTheDestinationIsReachedAtTheFinalWaypoint()
    {
        var folder = Scratch();

        try
        {
            var plans = Book(folder);
            RecordJumpPlan(plans);

            var panel = MiniOnPlan(NavRoute.None, plans, here: "Colonia");

            var drawn = TextOf(panel).ToArray();

            Assert.Contains(drawn, text => text.Contains("destination is reached", StringComparison.Ordinal));
            Assert.DoesNotContain(drawn, text => text == "next");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>Neither the route file nor the current system matching a waypoint marks nothing (#197).</summary>
    [AvaloniaFact]
    public void MiniMarksNothingWhenNeitherTheRouteNorThePositionMatch()
    {
        var folder = Scratch();

        try
        {
            var plans = Book(folder);
            RecordJumpPlan(plans);

            var panel = MiniOnPlan(NavRoute.None, plans, here: "Shinrarta Dezhra");

            var drawn = TextOf(panel).ToArray();

            Assert.Contains("Waypoint 0", drawn);
            Assert.DoesNotContain(drawn, text => text == "next");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>Galaxy search being off does not hide a plan already stored (#197).</summary>
    [AvaloniaFact]
    public void MiniShowsAStoredPlanEvenWithGalaxySearchOff()
    {
        var folder = Scratch();

        try
        {
            var plans = Book(folder);
            RecordJumpPlan(plans);

            var panel = FullyFurnished(NavRoute.None, plans, lookups: false);

            panel.Tab = PanelTab.Routing;
            panel.Nav.SelectRoot(RoutingPages.PlanRoot);
            panel.Mode = PanelMode.Mini;
            Dispatcher.UIThread.RunJobs();

            Assert.Contains("Waypoint 0", TextOf(panel));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>The mark follows the Commander without leaving mini, on the same tick every other reading uses (#197).</summary>
    [AvaloniaFact]
    public void MiniMovesTheMarkOnATickAsThePositionChanges()
    {
        var folder = Scratch();

        try
        {
            var plans = Book(folder);
            RecordJumpPlan(plans);

            var here = "Waypoint 0";
            var panel = new PanelView { DataContext = new PanelViewModel() };

            panel.EnableRouting(new RoutingSurface(
                () => NavRoute.None,
                () => here,
                CapabilityRegistry.Build([]),
                plans));

            Laid(panel);
            panel.Tab = PanelTab.Routing;
            panel.Nav.SelectRoot(RoutingPages.PlanRoot);
            panel.Mode = PanelMode.Mini;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(1, TextOf(panel).Count(text => text == "next"));

            here = "Colonia";
            panel.TickRouting();
            Dispatcher.UIThread.RunJobs();

            var drawn = TextOf(panel).ToArray();

            Assert.Contains(drawn, text => text.Contains("destination is reached", StringComparison.Ordinal));
            Assert.DoesNotContain(drawn, text => text == "next");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>Switching back to full shows RoutePlanPage as it does today, not mini's list (#197).</summary>
    [AvaloniaFact]
    public void ReturningToFullShowsRoutePlanPage()
    {
        var folder = Scratch();

        try
        {
            var plans = Book(folder);
            RecordJumpPlan(plans);

            var panel = MiniOnPlan(NavRoute.None, plans);

            panel.Mode = PanelMode.Full;
            Dispatcher.UIThread.RunJobs();

            Assert.Contains("NEUTRON PLOTTER", TextOf(panel));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// Course opens on the system the Commander is already going to, which is the one they usually
    /// want, and says that the clipboard is the half that always works.
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

    /// <summary>The mark inside the card whose heading says this — upper case and tracked (#289).</summary>
    private static Button PlannerMark(Control page, string heading)
    {
        // Up from the heading rather than down from a card.
        var title = page.GetVisualDescendants().OfType<TextBlock>()
            .First(text => text.Text == heading.ToUpperInvariant());

        return ((StackPanel)title.Parent!).Children
            .OfType<Button>()
            .First(button => button.Content as string == "?");
    }

    /// <summary>Three planners, three pages.</summary>
    [Fact]
    public void EachPlannersPageIsItsOwnSubject()
    {
        var pages = new[]
        {
            (RoutePlanPage.NeutronPlotterHelp, "Neutron Plotter"),
            (RoutePlanPage.RichesHelp, "Road to Riches"),
            (RoutePlanPage.TradeHelp, "Trade run"),
        };

        foreach (var (id, title) in pages)
        {
            var article = D47.Core.Help.HelpLibrary.For(id);

            Assert.True(article is not null, $"{id} has no band");
            Assert.Equal(title, article!.Title);
            Assert.NotEmpty(article.Sections);
        }

        // And they are not three copies of one page, which is what the tab's single mark was.
        Assert.Equal(
            3,
            pages.Select(page => D47.Core.Help.HelpLibrary.For(page.Item1)!.Intro).Distinct().Count());
    }

    /// <summary>
    /// Each card's mark opens that card's page — the thing one mark for the whole tab could not do —
    /// and Back returns to the forms, so it is not a trip out of the tab.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("Neutron Plotter", "general-neutron-plotter")]
    [InlineData("Road to Riches", "general-road-to-riches")]
    [InlineData("Trade run", "general-trade-run")]
    public void EachPlannersMarkOpensThatPlannersPage(string heading, string page)
    {
        var folder = Scratch();

        try
        {
            var panel = FullyFurnished(NavRoute.None, Book(folder));

            panel.Tab = PanelTab.Routing;
            panel.Nav.SelectRoot(RoutingPages.PlanRoot);
            Dispatcher.UIThread.RunJobs();

            PlannerMark(panel, heading).RaiseEvent(
                new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.True(panel.Nav.Modal, "help took the panel");
            Assert.Equal(D47.Core.Help.HelpLevel.Prefix + page, panel.Nav.Trail[^1].Key);

            Assert.True(panel.GoBack());
            Dispatcher.UIThread.RunJobs();

            Assert.False(panel.Nav.Modal);
            Assert.Equal(RoutingPages.PlanRoot, panel.Nav.RootKeyOf(PanelTab.Routing));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

 /// <summary>Hover and click are two ways to the same words.</summary>
    [AvaloniaTheory]
    [InlineData("Neutron Plotter", "general-neutron-plotter")]
    [InlineData("Road to Riches", "general-road-to-riches")]
    [InlineData("Trade run", "general-trade-run")]
    public void EachPlannersMarkShowsThatPlannersHelpOnHover(string heading, string page)
    {
        var folder = Scratch();

        try
        {
            var panel = FullyFurnished(NavRoute.None, Book(folder));

            panel.Tab = PanelTab.Routing;
            panel.Nav.SelectRoot(RoutingPages.PlanRoot);
            Dispatcher.UIThread.RunJobs();

            var mark = PlannerMark(panel, heading);

            Assert.Equal($"About {heading}", AutomationProperties.GetName(mark));

            var intro = D47.Core.Help.HelpLibrary.For(page)!.Intro;
            var tip = Assert.IsType<TextBlock>(ToolTip.GetTip(mark));

            Assert.Equal(intro, tip.Text);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// No planner card's mark echoes an invented click description — the sweep #341 asks for, done here
    /// rather than by a build-time grep so it runs where the marks are actually drawn.
    /// </summary>
    [AvaloniaFact]
    public void NoPlannerMarkDescribesTheClickInsteadOfShowingTheHelp()
    {
        var folder = Scratch();

        try
        {
            var panel = FullyFurnished(NavRoute.None, Book(folder));

            panel.Tab = PanelTab.Routing;
            panel.Nav.SelectRoot(RoutingPages.PlanRoot);
            Dispatcher.UIThread.RunJobs();

            var offenders = panel.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.Content as string == "?")
                .Where(mark => ToolTip.GetTip(mark) is string tip
                               && System.Text.RegularExpressions.Regex.IsMatch(
                                   tip, @"^What .+ does$"))
                .ToArray();

            Assert.Empty(offenders);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static JournalEvent Arrival(string kind, string system, DateTimeOffset at) =>
        JournalEvent.TryParse(
            $$"""{"timestamp":"{{at:O}}","event":"{{kind}}","StarSystem":"{{system}}"}""",
            NullLogger.Instance,
            out var parsed)
            ? parsed!
            : throw new InvalidOperationException("bad journal fixture");

    private static Color? Colour(IBrush? brush) => (brush as ISolidColorBrush)?.Color;

    /// <summary>The row whose text block reads the given name, on the plan's result page.</summary>
    private static Border ResultRow(PanelView panel, string text) =>
        (Border)panel.GetVisualDescendants()
            .OfType<TextBlock>()
            .First(block => block.Text == text)
            .FindAncestorOfType<Border>()!;

    /// <summary>
    /// Reached rows on the result page carry a tick and read muted; the row after the reached stop is
    /// not reached, and the one further on is untouched (#200).
    /// </summary>
    [AvaloniaFact]
    public void ReachedRowsOnTheResultPageAreTickedAndTheNextRowIsNot()
    {
        var folder = Scratch();

        try
        {
            var plans = Book(folder);
            RecordJumpPlan(plans);
            plans.Apply([Arrival("FSDJump", "Waypoint 0", new DateTimeOffset(2026, 9, 1, 9, 5, 0, TimeSpan.Zero))]);

            var panel = FullyFurnished(NavRoute.None, plans);

            panel.Tab = PanelTab.Routing;
            panel.Nav.SelectRoot(RoutingPages.PlanRoot);
            panel.Nav.Drill(RoutingPages.ResultCrumb(RoutePlanKind.Jump, "Sol to Colonia"));
            Dispatcher.UIThread.RunJobs();

            var drawn = TextOf(panel).ToArray();

            Assert.Contains("✓ Waypoint 0", drawn);
            Assert.Contains("Waypoint 1", drawn);
            Assert.DoesNotContain("✓ Waypoint 1", drawn);
            Assert.DoesNotContain("✓ Colonia", drawn);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// The row after the reached stop gets the current-row treatment RouteProgressPage.Row uses; with
    /// nothing reached, that is the first row (#200).
    /// </summary>
    [AvaloniaFact]
    public void TheRowAfterTheReachedStopGetsTheCurrentRowTreatment()
    {
        var folder = Scratch();

        try
        {
            new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(themeId: null);

            var plans = Book(folder);
            RecordJumpPlan(plans);
            plans.Apply([Arrival("FSDJump", "Waypoint 0", new DateTimeOffset(2026, 9, 1, 9, 5, 0, TimeSpan.Zero))]);

            var panel = FullyFurnished(NavRoute.None, plans);

            panel.Tab = PanelTab.Routing;
            panel.Nav.SelectRoot(RoutingPages.PlanRoot);
            panel.Nav.Drill(RoutingPages.ResultCrumb(RoutePlanKind.Jump, "Sol to Colonia"));
            Dispatcher.UIThread.RunJobs();

            var fill = Colour(panel.FindResource(ThemeManager.FillHighKey) as IBrush);

            Assert.Equal(fill, Colour(ResultRow(panel, "Waypoint 1").Background));
            Assert.NotEqual(fill, Colour(ResultRow(panel, "Colonia").Background));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>The result page redraws when the reached stop moves, without being reopened (#200).</summary>
    [AvaloniaFact]
    public void TheResultPageRedrawsWhenTheReachedStopMovesWithoutReopening()
    {
        var folder = Scratch();

        try
        {
            var plans = Book(folder);
            RecordJumpPlan(plans);

            var panel = FullyFurnished(NavRoute.None, plans);

            panel.Tab = PanelTab.Routing;
            panel.Nav.SelectRoot(RoutingPages.PlanRoot);
            panel.Nav.Drill(RoutingPages.ResultCrumb(RoutePlanKind.Jump, "Sol to Colonia"));
            Dispatcher.UIThread.RunJobs();

            Assert.DoesNotContain("✓ Waypoint 0", TextOf(panel));

            plans.Apply([Arrival("FSDJump", "Waypoint 0", new DateTimeOffset(2026, 9, 1, 9, 5, 0, TimeSpan.Zero))]);
            Dispatcher.UIThread.RunJobs();

            Assert.Contains("✓ Waypoint 0", TextOf(panel));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>A route service that echoes the destination asked for, so two plots are told apart (#212).</summary>
    private sealed class FakeRoutes : D47.Core.Knowledge.IRouteService
    {
        public PlottedRoute? Route { get; set; } =
            new("Sol", string.Empty, 22_000, 168, [new RouteWaypoint("PSR J1752-2806", 10, 21_629, true)]);

        public Task<PlottedRoute?> PlotAsync(RouteQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(Route is { } route ? route with { Destination = query.To } : null);

        public Task<RichesRoute?> PlotRichesAsync(RichesQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<RichesRoute?>(null);

        public Task<ExobiologyRoute?> PlotExobiologyAsync(
            ExobiologyQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult<ExobiologyRoute?>(null);
    }

    /// <summary>
    /// A surface whose Plan card can actually plot, through a fake service rather than a stub tool. The
    /// commander is standing in Sol, which is what <see cref="D47.Core.Capabilities.Builtin.RouteCapability"/>
    /// falls back to for "from" — the card's own "where you are now" placeholder is a separate hint drawn
    /// from the surface's <c>Here</c> function and never fills the argument itself.
    /// </summary>
    private static (PanelView Panel, FakeRoutes Routes, RoutePlanBook Plans) Plottable(string folder)
    {
        var settings = TestSurface.Settings();

        settings.Apply(
            D47.Core.Capabilities.Builtin.GalaxyCapability.EnabledKey,
            "true",
            D47.Core.Configuration.SettingsCaller.Panel);

        var gameState = new D47.Core.Journal.GameStateStore();

        Assert.True(D47.Core.Journal.JournalEvent.TryParse(
            """{"timestamp":"2026-09-14T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
            NullLogger.Instance,
            out var commanderEvent));
        gameState.Apply(commanderEvent!);

        Assert.True(D47.Core.Journal.JournalEvent.TryParse(
            """{"timestamp":"2026-09-14T00:00:01Z","event":"FSDJump","StarSystem":"Sol"}""",
            NullLogger.Instance,
            out var jumpEvent));
        gameState.Apply(jumpEvent!);

        var routes = new FakeRoutes();
        var plans = Book(folder);

        var registry = CapabilityRegistry.Build(
        [
            D47.Core.Capabilities.Builtin.RouteCapability.Create(
                routes,
                trade: null,
                commander: () => gameState.Active,
                settings,
                plans),
        ]);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableRouting(new RoutingSurface(() => NavRoute.None, () => "Sol", registry, plans, () => true));

        Laid(panel);
        panel.Tab = PanelTab.Routing;
        panel.Nav.SelectRoot(RoutingPages.PlanRoot);
        Dispatcher.UIThread.RunJobs();

        return (panel, routes, plans);
    }

    private static TextBox DestinationBox(PanelView panel) =>
        panel.GetVisualDescendants()
            .OfType<TextBox>()
            .First(box => Avalonia.Automation.AutomationProperties.GetName(box) == "Destination, required");

    private static Button PlotButton(PanelView panel) =>
        panel.GetVisualDescendants().OfType<Button>().First(button => button.Content as string == "Plot");

    private static void Plot(PanelView panel, string destination)
    {
        DestinationBox(panel).Text = destination;
        PlotButton(panel).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Pressing Plot, when it records a plan, leaves the surface on the new plan's result page with the
    /// new headline in the breadcrumb — the button used to just redraw the form and leave the Commander to
    /// find "Show most recent" themselves (#212).
    /// </summary>
    [AvaloniaFact]
    public void PressingPlotOpensTheNewPlansResultPage()
    {
        var folder = Scratch();

        try
        {
            var (panel, _, _) = Plottable(folder);

            Plot(panel, "Colonia");

            Assert.Equal("Sol to Colonia", panel.Nav.Trail[^1].Word);
            Assert.Contains("PSR J1752-2806", TextOf(panel));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// A second plot while the first plan's result page is still the surface's own trail top used to be
    /// refused — <see cref="PanelNavigator"/> would not push a crumb whose key was already on top, so the
    /// breadcrumb kept the first plan's headline (#212).
    /// </summary>
    [AvaloniaFact]
    public void PressingPlotAgainMovesTheBreadcrumbAndThePageToTheNewPlan()
    {
        var folder = Scratch();

        try
        {
            var (panel, _, _) = Plottable(folder);

            Plot(panel, "Colonia");
            Assert.Equal("Sol to Colonia", panel.Nav.Trail[^1].Word);

            panel.Nav.SelectRoot(RoutingPages.PlanRoot);
            Dispatcher.UIThread.RunJobs();

            Plot(panel, "Procyon");

            Assert.Equal("Sol to Procyon", panel.Nav.Trail[^1].Word);
            Assert.Contains("Sol to Procyon", TextOf(panel));
            Assert.DoesNotContain("Sol to Colonia", TextOf(panel));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// A plot that records no plan — no route found, refused, switched off — stays on the form rather
    /// than opening a result page for a plan that was never made (#212).
    /// </summary>
    [AvaloniaFact]
    public void APlotThatRecordsNoPlanStaysOnTheForm()
    {
        var folder = Scratch();

        try
        {
            var (panel, routes, plans) = Plottable(folder);
            routes.Route = null;

            Plot(panel, "Colonia");

            Assert.Equal(RoutingPages.PlanRoot, panel.Nav.Trail[^1].Key);
            Assert.Null(plans.Last(RoutePlanKind.Jump));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}

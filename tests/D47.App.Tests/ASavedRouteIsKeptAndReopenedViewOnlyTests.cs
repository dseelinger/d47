using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Save this route keeps the plan on screen; a saved route opens view only (#2623).</summary>
public class ASavedRouteIsKeptAndReopenedViewOnlyTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);

    private sealed class Bench
    {
        public SavedRouteBook Saved { get; } =
            new("saved-routes.json", new MemoryFileSystem(), NullLogger<SavedRouteBook>.Instance, () => "F1");

        public PanelNavigator Nav { get; } = new();
    }

    private static StoredRoutePlan Plan(int? reached = null) =>
        new()
        {
            Kind = RoutePlanKind.Jump,
            FrontierId = "F1",
            PlottedAt = At,
            Headline = "Sol to Colonia",
            Reached = reached,
            Jump = new PlottedRoute(
                "Sol",
                "Colonia",
                22_000,
                168,
                [new RouteWaypoint("Wolf 359", 8, 50, true), new RouteWaypoint("Colonia", 8, 0, false)]),
        };

    private static T Show<T>(T page)
        where T : Control
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);
        new Window { Content = page, Width = 1000, Height = 800 }.Show();
        Dispatcher.UIThread.RunJobs();

        return page;
    }

    private static Button Button(Control page, string name) =>
        page.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static List<string> Texts(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    [AvaloniaFact]
    public void SavingUnderANameAddsItToTheList()
    {
        using var kit = AppLook.ControlKit();
        var bench = new Bench();
        var page = Show(new RoutePlanResultPage(Plan(), saved: bench.Saved));

        Press(Button(page, RoutePlanResultPage.SaveTileName));

        var box = page.GetVisualDescendants().OfType<TextBox>().Single();

        Assert.Equal("Sol to Colonia", box.Text);

        box.Text = "Colonia run";
        Press(Button(page, RoutePlanResultPage.SaveGoName));

        var kept = Assert.Single(bench.Saved.For());

        Assert.Equal("Colonia run", kept.Name);
        Assert.Contains("As Colonia run, in Saved routes.", Texts(page));
        Assert.Contains("✓ SAVED", Texts(page));
    }

    [AvaloniaFact]
    public void AnEmptyNameCannotBeSaved()
    {
        using var kit = AppLook.ControlKit();
        var bench = new Bench();
        var page = Show(new RoutePlanResultPage(Plan(), saved: bench.Saved));

        Press(Button(page, RoutePlanResultPage.SaveTileName));
        page.GetVisualDescendants().OfType<TextBox>().Single().Text = string.Empty;
        Dispatcher.UIThread.RunJobs();

        Assert.False(Button(page, RoutePlanResultPage.SaveGoName).IsEnabled);
        Assert.Empty(bench.Saved.For());
    }

    [AvaloniaFact]
    public void OnTheHeadsetTheTileSavesAtOnceUnderTheHeadline()
    {
        using var kit = AppLook.ControlKit();
        var bench = new Bench();
        var page = Show(new RoutePlanResultPage(Plan(), saved: bench.Saved, headset: true));

        Press(Button(page, RoutePlanResultPage.SaveTileName));

        Assert.Equal("Sol to Colonia", Assert.Single(bench.Saved.For()).Name);
        Assert.DoesNotContain(page.GetVisualDescendants().OfType<TextBox>(), box => box.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void ASavedRouteDrawsViewOnlyWithoutTicksOrNext()
    {
        using var kit = AppLook.ControlKit();
        var bench = new Bench();
        var saved = bench.Saved.Save(Plan(reached: 0), "Run", At);
        var page = Show(new RoutePlanResultPage(saved.Plan, nav: bench.Nav, saved: bench.Saved, opened: saved));
        var texts = Texts(page);

        Assert.True(page.ViewOnly);
        Assert.Contains("SAVED ROUTE · VIEW ONLY", texts);
        Assert.DoesNotContain("NEXT", texts);
        Assert.DoesNotContain("✓", texts);
        Assert.DoesNotContain(page.GetVisualDescendants().OfType<Button>(), button => button.Name == RoutePlanResultPage.SaveTileName);
        Assert.DoesNotContain(
            page.GetVisualDescendants().OfType<Button>(),
            button => button.IsEffectivelyVisible
                && Avalonia.Automation.AutomationProperties.GetName(button) == RoutePlanResultPage.ShowOnGalaxyMapWords);
    }

    [AvaloniaFact]
    public void ASavedRouteIsNotRedrawnFromTheLivePlan()
    {
        using var kit = AppLook.ControlKit();
        var bench = new Bench();
        var plans = new RoutePlanBook("route-plans.json", new MemoryFileSystem(), NullLogger<RoutePlanBook>.Instance, () => "F1");
        plans.Record(Plan().Jump!, "Sol to Colonia", At);
        var saved = bench.Saved.Save(plans.Last(RoutePlanKind.Jump)!, "Run", At);
        var page = Show(new RoutePlanResultPage(saved.Plan, nav: bench.Nav, saved: bench.Saved, opened: saved));

        plans.Record(new PlottedRoute("Sol", "Beagle Point", 65_000, 400, [new RouteWaypoint("Beagle Point", 400, 0, false)]), "Sol to Beagle Point", At.AddHours(1));

        Assert.False(page.Refresh());
        Assert.DoesNotContain(Texts(page), text => text.Contains("Beagle Point", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void DeletingFromTheViewAsksThenReturnsToTheList()
    {
        using var kit = AppLook.ControlKit();
        var bench = new Bench();
        bench.Nav.Register(PanelTab.Astrometrics, new NavCrumb(RoutingPages.PlanRoot, "Plan"));
        bench.Nav.Select(PanelTab.Astrometrics);
        bench.Nav.Drill(RoutingPages.SavedRoutesCrumb);
        var saved = bench.Saved.Save(Plan(), "Run", At);
        bench.Nav.Drill(RoutingPages.SavedRouteCrumb(saved.Id, saved.Name));
        var page = Show(new RoutePlanResultPage(saved.Plan, nav: bench.Nav, saved: bench.Saved, opened: saved));

        Press(Button(page, RoutePlanResultPage.DeleteSavedName));

        Assert.Contains(SavedRoutesPage.ConfirmText, Texts(page));
        Assert.Single(bench.Saved.For());

        Press(Button(page, RoutePlanResultPage.ConfirmDeleteName));

        Assert.Empty(bench.Saved.For());
        Assert.Equal(RoutingPages.SavedRoutesKey, bench.Nav.Trail[^1].Key);
    }

    [AvaloniaFact]
    public void TheListShowsEachRouteAndDeletesAfterAsking()
    {
        using var kit = AppLook.ControlKit();
        var bench = new Bench();
        var older = bench.Saved.Save(Plan(), "Older", At);
        var newer = bench.Saved.Save(Plan() with { PlottedAt = At.AddDays(1), Kind = RoutePlanKind.Jump }, "Newer", At);
        var page = Show(new SavedRoutesPage(bench.Saved, bench.Nav));
        var texts = Texts(page);

        Assert.Contains("2 saved routes", texts);
        Assert.Contains("Neutron Plotter", texts);
        Assert.True(texts.IndexOf("Newer") < texts.IndexOf("Older"));

        Press(Button(page, SavedRoutesPage.DeletePrefix + older.Id));
        Assert.Contains(SavedRoutesPage.ConfirmText, Texts(page));
        Press(Button(page, SavedRoutesPage.KeepPrefix + older.Id));
        Assert.Equal(2, bench.Saved.For().Count);

        Press(Button(page, SavedRoutesPage.DeletePrefix + older.Id));
        Press(Button(page, SavedRoutesPage.ConfirmPrefix + older.Id));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([newer.Id], bench.Saved.For().Select(route => route.Id));
    }

    [AvaloniaFact]
    public void AnEmptyListSaysHowToFillIt()
    {
        using var kit = AppLook.ControlKit();
        var page = Show(new SavedRoutesPage(new Bench().Saved, new PanelNavigator()));

        Assert.Contains(SavedRoutesPage.EmptyText, Texts(page));
    }

    [AvaloniaFact]
    public void TheHeadsetListPagesAtTwentyFiveRows()
    {
        using var kit = AppLook.ControlKit();
        var bench = new Bench();

        for (var i = 0; i < 30; i++)
        {
            bench.Saved.Save(Plan() with { PlottedAt = At.AddMinutes(i) }, $"Route {i}", At);
        }

        var page = Show(new SavedRoutesPage(bench.Saved, bench.Nav, headset: true));

        Assert.Equal(25, page.GetVisualDescendants().OfType<Border>().Count(row => row.Name?.StartsWith("SavedRoute.", StringComparison.Ordinal) == true));
        Press(Button(page, SavedRoutesPage.NextName));
        Assert.Equal(5, page.GetVisualDescendants().OfType<Border>().Count(row => row.Name?.StartsWith("SavedRoute.", StringComparison.Ordinal) == true));
    }

    [AvaloniaFact]
    public void ASavedRouteHasItsSummary()
    {
        using var kit = AppLook.ControlKit();
        var bench = new Bench();
        var saved = bench.Saved.Save(Plan(), "Run", At);

        Assert.Equal(
            "A saved route, as it was plotted.",
            new RoutePlanResultPage(saved.Plan, nav: bench.Nav, saved: bench.Saved, opened: saved).Summary);
        Assert.Equal(string.Empty, new RoutePlanResultPage(Plan(), saved: bench.Saved).Summary);
    }

    [AvaloniaFact]
    public void SavedRoutesFromATradeResultOpensUnderPlan()
    {
        using var kit = AppLook.ControlKit();
        var bench = new Bench();
        bench.Nav.Register(PanelTab.Astrometrics, new NavCrumb(RoutingPages.PlanRoot, "Plan"));
        bench.Nav.Register(PanelTab.Astrometrics, new NavCrumb(RoutingPages.TradeRoot, "Trade route"));
        bench.Nav.Select(PanelTab.Astrometrics);
        bench.Nav.SelectRoot(RoutingPages.TradeRoot);
        bench.Nav.Drill(RoutingPages.ResultCrumb(RoutePlanKind.Trade, "Sol to Alpha Centauri"));
        var trade = Plan() with { Kind = RoutePlanKind.Trade, Jump = null, Trade = new TradeRoute([new TradeStop("Sol", "Abraham Lincoln")]) };
        var page = Show(new RoutePlanResultPage(trade, nav: bench.Nav, saved: bench.Saved));

        Press(Button(page, RoutePlanResultPage.SavedTileName));

        Assert.Equal([RoutingPages.PlanRoot, RoutingPages.SavedRoutesKey], bench.Nav.Trail.Select(crumb => crumb.Key));
    }

    [AvaloniaTheory]
    [InlineData(RoutePlanKind.Jump)]
    [InlineData(RoutePlanKind.Riches)]
    [InlineData(RoutePlanKind.Exobiology)]
    [InlineData(RoutePlanKind.Carrier)]
    [InlineData(RoutePlanKind.Trade)]
    [InlineData(RoutePlanKind.Outfitting)]
    public void EveryKindOfResultCanBeSaved(RoutePlanKind kind)
    {
        using var kit = AppLook.ControlKit();
        var bench = new Bench();
        var plan = new StoredRoutePlan
        {
            Kind = kind,
            FrontierId = "F1",
            PlottedAt = At,
            Headline = "A plan",
            Riches = kind == RoutePlanKind.Riches ? new RichesRoute([new RichesStop("Stop", 1, [])]) : null,
            Trade = kind == RoutePlanKind.Trade ? new TradeRoute([new TradeStop("Sol", "Abraham Lincoln")]) : null,
        };
        var page = Show(new RoutePlanResultPage(plan, saved: bench.Saved, headset: true));

        Press(Button(page, RoutePlanResultPage.SaveTileName));

        var kept = Assert.Single(bench.Saved.For());

        Assert.Equal(kind, kept.Kind);
        Assert.Equal("A plan", kept.Name);
    }
}

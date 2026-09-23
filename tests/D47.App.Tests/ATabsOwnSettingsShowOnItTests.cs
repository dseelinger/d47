using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Settings;
using D47.Core;
using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A tab's own settings, drawn from its <see cref="SettingsLayout"/> tab place (#218): the strip
/// SettingsView draws in place mode, and where it lands on Fleet › Ships, Routing › Community Goal
/// and Adventures.
/// </summary>
public class ATabsOwnSettingsShowOnItTests
{
    private static (SettingsView View, Window Window) OpenStrip(
        SettingsService settings, ViewStateStore viewState, AppPaths paths, string tabPlaceId, double width = 400)
    {
        var view = new SettingsView();
        view.Attach(settings, viewState, paths, tabPlaceId: tabPlaceId);

        var window = new Window { Content = view, Width = width, Height = 400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (view, window);
    }

    [AvaloniaFact]
    public void TheFleetShipsStripDrawsWhatIsFittedAndHullPictures()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var (view, window) = OpenStrip(settings, viewState, paths, "fleet-ships");

        var texts = view.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text)
            .ToList();

        Assert.Contains("What is fitted, remembered", texts);
        Assert.Contains("Hull pictures", texts);

        Assert.Contains(
            view.GetVisualDescendants().OfType<Button>(),
            button => (button.Content as string) == "Rescan my journals");

        window.Close();
    }

    /// <summary>The captain and tower's names and voices are on Fleet › Carrier, not the settings window (#305).</summary>
    [AvaloniaFact]
    public void TheFleetCarrierStripDrawsTheNamesAndVoices()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var (view, window) = OpenStrip(settings, viewState, paths, "fleet-carrier");

        var texts = view.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text)
            .ToList();

        Assert.Contains("Captain name", texts);
        Assert.Contains("Carrier captain voice", texts);
        Assert.Contains("Tower name", texts);
        Assert.Contains("Carrier tower voice", texts);

        window.Close();
    }

    /// <summary>No nav, no page-top strip, no card header, no width floor — at any width.</summary>
    [AvaloniaTheory]
    [InlineData(300)]
    [InlineData(1400)]
    public void TheStripHasNoNavAndNoWidthFloorAtAnyWidth(double width)
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var (view, window) = OpenStrip(settings, viewState, paths, "fleet-ships", width);

        var nav = (Control)view.GetVisualDescendants().First(c => c.Name == "Nav");
        var root = (Grid)view.GetVisualDescendants().First(c => c.Name == "Root");

        Assert.False(nav.IsVisible);
        Assert.Equal(0, root.ColumnDefinitions[0].Width.Value);
        Assert.Equal(0, root.MinWidth);

        window.Close();
    }

    /// <summary>An open strip at the narrowest a pane can be fits it, with no sideways scrolling.</summary>
    [AvaloniaFact]
    public void AnOpenStripAtTheNarrowestPaneDoesNotScrollSideways()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        viewState.Save(viewState.Load().With("fleet-ships", expanded: true));

        var (view, window) = OpenStrip(settings, viewState, paths, "fleet-ships", DrillView.MinimumPaneWidth);

        var scroller = (ScrollViewer)view.GetVisualDescendants().First(c => c.Name == "Scroller");

        Assert.Equal(ScrollBarVisibility.Disabled, scroller.HorizontalScrollBarVisibility);
        Assert.True(scroller.Extent.Width <= scroller.Viewport.Width);

        window.Close();
    }

    /// <summary>
    /// Every row on the Community Goal tab place is Advanced, so this is also the fold test: none of
    /// them are hidden even though "Show every setting" is off.
    /// </summary>
    [AvaloniaFact]
    public void TheStripsRowsAreNeverFolded()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        Assert.False(settings.Current.Ui.ShowEverySetting);

        var (view, window) = OpenStrip(settings, viewState, paths, "routing-community-goal");

        var texts = view.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text)
            .ToList();

        Assert.Contains("Inara API key", texts);
        Assert.Contains("The week turns on", texts);
        Assert.Contains("…at this hour, UTC", texts);

        window.Close();
    }

    /// <summary>The cap tracks the host page's height, not a fixed pixel value (#340).</summary>
    [AvaloniaFact]
    public void AnOpenStripIsNeverTallerThanHalfThePage()
    {
        var host = new DockPanel();
        var strip = new Border { Height = 1000 };

        var window = new Window { Content = host, Width = 300, Height = 400 };
        window.Show();

        host.Children.Add(strip);
        host.CapStripHeight(strip);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(200, strip.MaxHeight);

        window.Height = 600;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(300, strip.MaxHeight);

        window.Close();
    }

    [AvaloniaFact]
    public void TheStripIsClosedByDefaultAndOpensOnClick()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var (view, window) = OpenStrip(settings, viewState, paths, "adventures");

        var strip = (StackPanel)view.GetVisualDescendants().First(c => c.Name == SettingsView.TabStripName);
        var content = (StackPanel)strip.Children[1];
        var header = (Button)strip.Children[0];

        Assert.False(content.IsVisible);

        header.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.True(content.IsVisible);
        Assert.StartsWith("▾ Settings for this page (", header.Content as string, StringComparison.Ordinal);

        window.Close();
    }

    /// <summary>A strip left open is open after the panel is rebuilt.</summary>
    [AvaloniaFact]
    public void AStripLeftOpenStaysOpenOnTheNextInstance()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        viewState.Save(viewState.Load().With("adventures", expanded: true));

        var (view, window) = OpenStrip(settings, viewState, paths, "adventures");

        var strip = (StackPanel)view.GetVisualDescendants().First(c => c.Name == SettingsView.TabStripName);
        var content = (StackPanel)strip.Children[1];

        Assert.True(content.IsVisible);

        window.Close();
    }

    /// <summary>The strip sits at the very bottom of the page, below the index (#340).</summary>
    [AvaloniaFact]
    public void TheFleetShipsPageDrawsTheGivenStripAtTheBottom()
    {
        var root = TempFolders.Create("d47-tab-settings-strip-tests");

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"), NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => null);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        var marker = new TextBlock { Name = "StripMarker", Text = "strip" };

        panel.EnableLoadout(ships, checklists, () => null, settingsStrip: () => marker);

        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();

        panel.Tab = PanelTab.Loadout;
        Dispatcher.UIThread.RunJobs();

        var host = (Control)marker.GetVisualParent()!;

        Assert.Equal(Dock.Bottom, DockPanel.GetDock(marker));
        Assert.True(
            Math.Abs(host.Bounds.Height - marker.Bounds.Bottom) < 1.0,
            $"expected the strip flush with the bottom of its {host.GetType().Name}, "
            + $"got host height {host.Bounds.Height} and strip bottom {marker.Bounds.Bottom}");

        window.Close();
    }

    [AvaloniaFact]
    public void TheAdventuresPageDrawsTheGivenStrip()
    {
        var surface = new Panel.AdventureSurface(
            new D47.Core.Adventures.AdventureBook(
                new D47.Core.Adventures.AdventureStore(
                    Path.Combine(TempFolders.Create("d47-tab-settings-strip-tests"), "adventures.json"),
                    NullLogger<D47.Core.Adventures.AdventureStore>.Instance),
                NullLogger<D47.Core.Adventures.AdventureBook>.Instance),
            new D47.Core.Adventures.AdventureGenerator(
                () => null, () => null, () => null, () => null, () => null, () => null,
                () => null, () => null, null, null, NullLogger.Instance),
            () => null,
            () => null,
            () => DateTimeOffset.UtcNow,
            _ => { },
            () => false,
            () => false,
            () => null,
            () => { });

        var panel = new PanelView { DataContext = new PanelViewModel() };
        var marker = new TextBlock { Name = "StripMarker", Text = "strip" };

        panel.EnableAdventures(surface, settingsStrip: () => marker);

        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();

        panel.Tab = PanelTab.Adventures;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(panel.GetVisualDescendants(), c => c.Name == "StripMarker");

        window.Close();
    }

    [AvaloniaFact]
    public void TheCommunityGoalPageDrawsTheGivenStripAndKeepsItAcrossARefresh()
    {
        var board = new CommodityBoard();

        var goal = new CommunityGoalSurface(
            new CommunityGoalSearch(),
            new CommodityLedger(),
            () => null,
            () => DateTimeOffset.UtcNow,
            at => CommodityLedger.Week(at, DayOfWeek.Thursday, 7));

        var routing = new RoutingSurface(
            () => new NavRoute(),
            () => "Ega",
            D47.Core.Capabilities.CapabilityRegistry.Build([]),
            Plans: null,
            LookupsEnabled: () => false,
            OpenSettings: null,
            Commodities: board,
            CommunityGoal: goal);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        var marker = new TextBlock { Name = "StripMarker", Text = "strip" };

        panel.EnableRouting(
            routing, plan: false, progress: false, course: false, market: false, settingsStrip: () => marker);

        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();

        panel.Tab = PanelTab.Routing;
        Dispatcher.UIThread.RunJobs();

        // Outside the page's ScrollViewer, so scrolling the results does not move it (#340).
        Assert.Null(marker.FindAncestorOfType<ScrollViewer>());

        var host = (Control)marker.GetVisualParent()!;

        Assert.Equal(Dock.Bottom, DockPanel.GetDock(marker));
        Assert.True(
            Math.Abs(host.Bounds.Height - marker.Bounds.Bottom) < 1.0,
            $"expected the strip flush with the bottom of its {host.GetType().Name}, "
            + $"got host height {host.Bounds.Height} and strip bottom {marker.Bounds.Bottom}");

        // The page's own Refresh() redraws its results and ledger; the strip is not among them.
        board.Post(new CommodityPosting(
            new CommodityQuery("Palladium", MaxDistance: 250, OrderBy: CommodityOrder.Distance, Limit: 10),
            CommodityAnswer.Empty,
            "Shinrarta Dezhra",
            DateTimeOffset.UtcNow));
        board.Announce();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(panel.GetVisualDescendants(), c => c.Name == "StripMarker");

        window.Close();
    }

    /// <summary>Every SettingsTabPlace.RootKey is a root furnished on a tab.</summary>
    [AvaloniaFact]
    public void EveryTabPlaceRootKeyIsARootThisAppFurnishes()
    {
        var known = new[]
        {
            LoadoutPages.FleetRoot, LoadoutPages.CarrierRoot, RoutingPages.CommunityGoalRoot, AdventuresPage.RootKey,
            "checklist", PanelView.LogRoot,
        };

        foreach (var tab in SettingsLayout.Tabs)
        {
            Assert.Contains(tab.RootKey, known);
        }
    }

    /// <summary>The Checklist tab place draws no strip.</summary>
    [AvaloniaFact]
    public void TheChecklistTabPlaceHasNoStrip()
    {
        var tab = SettingsLayout.Tabs.First(t => t.RootKey == "checklist");

        Assert.False(tab.Strip);
    }
}

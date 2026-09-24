using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Checklists;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A query on the settings page reaches beyond a row's own words: an area's title, a place's title or
/// one of its search terms, a named group's title or help, and — for a row that lives on a tab rather
/// than a settings page — a match under "On other tabs" that opens the tab and root it belongs to (#222).
/// </summary>
public class SearchSettingsByAreaAndSectionNamesTests
{
    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static TextBox Box(SettingsHost host) => (TextBox)host.Panel.FindControl<Control>("SearchInput")!;

    private static string Words(TextBlock block) =>
        block.Inlines is { Count: > 0 } inlines
            ? string.Concat(inlines.OfType<Run>().Select(run => run.Text))
            : block.Text ?? string.Empty;

    private static List<string> VisibleRowLabels(SettingsHost host) =>
        [.. host.View.GetVisualDescendants().OfType<Grid>()
            .Where(grid => grid.Classes.Contains(SettingsView.CompactRowClass) && grid.IsEffectivelyVisible)
            .Select(grid => grid.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault())
            .Where(label => label is not null)
            .Select(label => Words(label!))
            .Where(label => !string.IsNullOrEmpty(label))];

    private static Control? OtherTabsSection(SettingsHost host) =>
        host.View.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == SettingsView.OtherTabsName);

    private static List<Button> OtherTabsButtons(SettingsHost host) =>
        [.. OtherTabsSection(host)?.GetVisualDescendants().OfType<Button>() ?? []];

    [AvaloniaFact]
    public void AnAreaTitleMarksEveryPlaceInIt()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        var area = SettingsLayout.Areas.Single(a => a.Title == "Voice and hearing");

        Box(host).Text = "Voice and hearing";
        Jobs();

        Assert.Equal(
            area.Places.Select(p => p.Title).ToHashSet(),
            SettingsPageReading.Counted(host.View).Keys.ToHashSet());

        host.Close();
    }

    [AvaloniaFact]
    public void APlaceTermShowsItsWholePlace()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        Box(host).Text = "ptt";
        Jobs();

        Assert.Equal(["Voice Input"], SettingsPageReading.Counted(host.View).Keys);

        // A row whose own words say nothing about "ptt" is still on the page, because the match is on the
        // place's term rather than on any one row.
        Assert.Contains("Cancel", VisibleRowLabels(host));

        host.Close();
    }

    [AvaloniaFact]
    public void ANamedGroupsHelpRevealsEveryRowUnderIt()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        // Unique to the "Levels" group's own help sentence — no row under it says "for every channel".
        Box(host).Text = "for every channel";
        Jobs();

        Assert.Equal(["Sounds and levels"], SettingsPageReading.Counted(host.View).Keys);

        SettingsPageReading.Open(host.View, "sounds");

        Assert.Contains("Level", VisibleRowLabels(host));
        Assert.Contains("Mute", VisibleRowLabels(host));

        host.Close();
    }

    [AvaloniaFact]
    public void AMatchOnAStripFalseTabPlaceOffersToOpenItsTab()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        Box(host).Text = "checklist";
        Jobs();

        var section = OtherTabsSection(host);

        Assert.NotNull(section);
        Assert.True(section!.IsVisible);
        Assert.Contains(OtherTabsButtons(host), b => (b.Content as string) == "Open the Checklist tab");

        host.Close();
    }

    [AvaloniaFact]
    public void EmptyingTheQueryHidesOnOtherTabs()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        Box(host).Text = "checklist";
        Jobs();

        Assert.True(OtherTabsSection(host)!.IsVisible);

        Box(host).Text = string.Empty;
        Jobs();

        // Gone from the query's own results, not merely hidden behind them — nothing to search is nothing to
        // draw a section for.
        var afterClear = OtherTabsSection(host);
        Assert.True(afterClear is null || !afterClear.IsVisible);

        host.Close();
    }

    /// <summary>
    /// A match on a Strip = true tab place's row offers a button that puts the panel on that tab and
    /// root, with its own strip open — the "hull" acceptance case (#222).
    /// </summary>
    [AvaloniaFact]
    public void AMatchOnAStripTruePlaceOpensItsTabWithTheStripOpen()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        var root = TempFolders.Create("d47-search-other-tabs-tests");

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"), NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => null);

        var view = new SettingsView();
        var panel = new PanelView { DataContext = new PanelViewModel() };

        SettingsView? fleetStrip = null;

        panel.EnableSettings(() =>
        {
            view.Attach(settings, viewState, paths);
            return view;
        });

        panel.EnableLoadout(
            ships,
            checklists,
            () => null,
            settingsStrip: () =>
            {
                var strip = new SettingsView();
                strip.Attach(settings, viewState, paths, tabPlaceId: "fleet-ships");
                fleetStrip = strip;
                return strip;
            });

        // As MainWindow wires it: this view cannot change tab itself, so a match hands the root key to
        // whoever can, and that caller opens the destination's own strip once it is on screen.
        view.EnableTabJump(rootKey =>
        {
            panel.Nav.Show(rootKey);

            if (rootKey == LoadoutPages.FleetRoot)
            {
                fleetStrip?.ExpandTabStrip();
            }
        });

        panel.EnableSearch();
        panel.Tab = PanelTab.Settings;

        var window = new Window { Content = panel, Width = 1180, Height = 880 };
        window.Show();
        Jobs();

        Box2(panel).Text = "hull";
        Jobs();

        var button = OtherTabsButtons2(view).Single(b => (b.Content as string) == "Open Fleet › Ships");

        Assert.Equal(PanelTab.Settings, panel.Tab);

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();

        Assert.Equal(PanelTab.Loadout, panel.Tab);
        Assert.Equal(LoadoutPages.FleetRoot, panel.Nav.RootKeyOf(PanelTab.Loadout));

        var strip = (StackPanel)fleetStrip!.GetVisualDescendants().First(c => c.Name == SettingsView.TabStripName);
        var content = (StackPanel)strip.Children[1];

        Assert.True(content.IsVisible);

        window.Close();
    }

    private static TextBox Box2(PanelView panel) => (TextBox)panel.FindControl<Control>("SearchInput")!;

    private static List<Button> OtherTabsButtons2(SettingsView view) =>
        [.. (view.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == SettingsView.OtherTabsName)
            ?.GetVisualDescendants().OfType<Button>() ?? [])];
}

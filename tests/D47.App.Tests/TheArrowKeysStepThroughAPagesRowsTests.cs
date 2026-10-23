using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Up, Down, Left and Right with no modifiers move focus between a page's rows (#1514).</summary>
[Trait("Category", "Integration")]
public class TheArrowKeysStepThroughAPagesRowsTests
{
    private static (SettingsHost Host, ChecklistService Checklists) Open()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths, width: 1500, height: 800);
        var root = TempFolders.Create("d47-row-keys");
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Location","StarSystem":"Sol","StarPos":[0.0,0.0,0.0],"Docked":true,"StationName":"Abraham Lincoln"}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        var state = store.Active!;

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(Path.Combine(root, "checklist-proposals.json"), new MemoryFileSystem(), NullLogger<ChecklistProposalStore>.Instance),
            () => state);

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        checklists.AddNote(ChecklistScope.System("Sol"), "scan the beacon");
        checklists.AddNote(ChecklistScope.Ship(51), "fit a shield");

        host.Panel.EnableChecklist(checklists);
        host.Panel.Tab = PanelTab.Commander;
        Dispatcher.UIThread.RunJobs();

        return (host, checklists);
    }

    private static void Press(SettingsHost host, Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        host.Window.KeyPress(key, (RawInputModifiers)(int)modifiers, PhysicalKey.None, null);
        Dispatcher.UIThread.RunJobs();
    }

    private static List<Control> Rows(SettingsHost host) =>
        [.. host.Panel.GetVisualDescendants().OfType<Control>()
            .Where(c => c.Focusable && (c.Classes.Contains(ListRow.Class) || c.Classes.Contains(ListRow.OpensClass)) && c.IsEffectivelyVisible)
            .OrderBy(c => c.TranslatePoint(default, host.Panel)!.Value.Y)
            .ThenBy(c => c.TranslatePoint(default, host.Panel)!.Value.X)];

    private static Control? Focused(SettingsHost host) => host.Window.FocusManager?.GetFocusedElement() as Control;

    [AvaloniaFact]
    public void DownWithNothingFocusedFocusesTheFirstRowAndEnterOpensIt()
    {
        var (host, _) = Open();
        var rows = Rows(host);

        Assert.True(rows.Count >= 2, $"rows: {rows.Count}");

        var before = host.Panel.Nav.Trail.Count;

        Press(host, Key.Down);
        Assert.Same(rows[0], Focused(host));

        Press(host, Key.Enter);
        Assert.True(host.Panel.Nav.Trail.Count > before);

        host.Close();
    }

    [AvaloniaFact]
    public void DownAndUpStepOneRowAndStopAtTheEnds()
    {
        var (host, _) = Open();
        var rows = Rows(host);

        rows[0].Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();

        Press(host, Key.Up);
        Assert.Same(rows[0], Focused(host));

        Press(host, Key.Down);
        Assert.Same(rows[1], Focused(host));

        rows[^1].Focus(NavigationMethod.Tab);
        Press(host, Key.Down);
        Assert.Same(rows[^1], Focused(host));

        Press(host, Key.Up);
        Assert.Same(rows[^2], Focused(host));

        host.Close();
    }

    [AvaloniaFact]
    public void DownFromTheLastRowOfTheLeftPaneStaysInTheLeftPane()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths, width: 1500, height: 800);
        var panes = new Dictionary<string, List<Border>>();

        host.Panel.Furnish(
            PanelTab.Navigation,
            crumb =>
            {
                var column = new StackPanel { Spacing = 2 };
                var rows = new List<Border>();

                for (var i = 0; i < 3; i++)
                {
                    var row = ListRow.Dress(new Border { Height = 50 });
                    ListRow.Opens(row, () => { });
                    rows.Add(row);
                    column.Children.Add(row);
                }

                panes[crumb.Key] = rows;
                return column;
            },
            new NavCrumb("left", "Left"));

        host.Panel.Tab = PanelTab.Navigation;
        Dispatcher.UIThread.RunJobs();
        Assert.True(host.Panel.Nav.Drill(new NavCrumb("right", "Right")));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, panes.Count);

        panes["left"][2].Focus(NavigationMethod.Tab);
        Press(host, Key.Down);
        Assert.Same(panes["left"][2], Focused(host));

        Press(host, Key.Up);
        Press(host, Key.Up);
        Press(host, Key.Up);
        Assert.Same(panes["left"][0], Focused(host));

        host.Close();
    }

    [AvaloniaFact]
    public void UpWithNothingFocusedFocusesTheLastRow()
    {
        var (host, _) = Open();
        var rows = Rows(host);

        Press(host, Key.Up);

        Assert.Same(rows[^1], Focused(host));

        host.Close();
    }

    [AvaloniaFact]
    public void AKeyboardFocusedRowDrawsTile2AndAClickedRowDoesNot()
    {
        var (host, _) = Open();
        var rows = Rows(host);

        Press(host, Key.Down);

        Assert.Same(rows[0], Focused(host));
        Assert.True(rows[0].Classes.Contains(":focus-visible") || rows[0].IsFocused);

        host.Close();
    }

    [AvaloniaFact]
    public void ModifiedArrowsAndASegmentKeepTheirOwnKeys()
    {
        var (host, _) = Open();
        var rows = Rows(host);

        Press(host, Key.Down, KeyModifiers.Shift);
        Assert.Null(Focused(host) is { } f && rows.Contains(f) ? f : null);

        host.Panel.Tab = PanelTab.Settings;
        Dispatcher.UIThread.RunJobs();
        host.View.Reveal(D47.Core.Capabilities.Builtin.ConversationCapability.Id);
        Dispatcher.UIThread.RunJobs();

        var segs = host.View.GetVisualDescendants().OfType<D47.App.Controls.Segment>().ToList();
        Assert.True(segs.Count > 0, $"tab {host.Panel.Tab} segs {segs.Count}");
        var segment = segs.First(s => s.Bounds.Height > 0);
        segment.Focus();
        var before = Focused(host);
        Assert.NotNull(before);
        Press(host, Key.Down);
        Press(host, Key.Up);

        Assert.Same(before, Focused(host));

        host.Close();
    }

    [AvaloniaFact]
    public void DownFromThePageSearchBoxFocusesTheFirstMatchingRow()
    {
        var (host, _) = Open();

        var search = host.Panel.GetControl<TextBox>("SearchInput");
        search.Focus();
        search.Text = "limpets";
        Dispatcher.UIThread.RunJobs();

        Press(host, Key.Down);

        var focused = Focused(host);
        Assert.NotNull(focused);
        Assert.Contains(ListRow.Class, focused!.Classes);

        host.Close();
    }

    [AvaloniaFact]
    public void RowsInAGridStepDownAColumnAndAlongALine()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);
        using var kit = AppLook.ControlKit();

        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths, width: 1500, height: 800);
        var tiles = new List<Border>();

        host.Panel.Furnish(
            PanelTab.Navigation,
            _ =>
            {
                var grid = new WrapPanel { Width = 330, HorizontalAlignment = HorizontalAlignment.Left };

                for (var i = 0; i < 6; i++)
                {
                    var tile = ListRow.Dress(new Border { Width = 100, Height = 50, Margin = new Thickness(0, 0, 5, 5) });
                    ListRow.Opens(tile, () => { });
                    tiles.Add(tile);
                    grid.Children.Add(tile);
                }

                return grid;
            },
            new NavCrumb("grid", "Grid"));

        host.Panel.Tab = PanelTab.Navigation;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(6, tiles.Count);

        tiles[1].Focus(NavigationMethod.Tab);
        Press(host, Key.Down);
        Assert.Same(tiles[4], Focused(host));

        var tile2 = Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(Application.Current!.FindResource(D47.App.Theming.ThemeManager.Tile2Key));
        Assert.Equal(tile2.Color, ((Avalonia.Media.ISolidColorBrush)tiles[4].Background!).Color);
        Assert.NotEqual(tile2.Color, ((Avalonia.Media.ISolidColorBrush)tiles[3].Background!).Color);

        Press(host, Key.Up);
        Assert.Same(tiles[1], Focused(host));

        Press(host, Key.Left);
        Assert.Same(tiles[0], Focused(host));

        Press(host, Key.Left);
        Assert.Same(tiles[0], Focused(host));

        Press(host, Key.Right);
        Press(host, Key.Right);
        Press(host, Key.Right);
        Assert.Same(tiles[2], Focused(host));

        host.Close();
    }
}

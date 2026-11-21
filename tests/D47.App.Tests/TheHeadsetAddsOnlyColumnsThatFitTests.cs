using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The headset does not scroll a table sideways: a column that does not fit at its narrowest cannot be added, and a
/// stored arrangement that no longer fits is drawn without its columns at the right (#2517).
/// </summary>
public class TheHeadsetAddsOnlyColumnsThatFitTests
{
    private const string Id = StoredModulesPage.TableId;

    private static (Window Window, ViewStateStore Views) Panel(Action<ColumnArrangementMemory>? arrange = null)
    {
        var views = ATableWiderThanItsPageScrollsInItsOwnBoxTests.ViewState();
        var memory = new ColumnArrangementMemory(views, headset: true);
        arrange?.Invoke(memory);

        var root = TestSurface.MemoryFolder("d47-headset-columns-fit-tests");
        var store = ATableWiderThanItsPageScrollsInItsOwnBoxTests.Store();
        CommanderGameState? State() => store.Active;
        var (checklists, _, _, ships, kit, _) = PlanBench.Over(root, State);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.Classes.Add("headset");
        panel.EnableColumnArrangements(memory);
        panel.EnableLoadout(ships, checklists, State, kit);

        var look = AppLook.Put();
        var window = new Window { Content = panel, Width = 800, Height = 500 };
        window.Closed += (_, _) => look.Dispose();
        window.Show();
        panel.Tab = PanelTab.Assets;
        Dispatcher.UIThread.RunJobs();
        panel.Nav.SelectRoot(StoredModulesPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        return (window, views);
    }

    private static T? Named<T>(Visual root, string name)
        where T : Control =>
        root.GetVisualDescendants().OfType<T>().FirstOrDefault(control => control.Name == name);

    private static void Press(Visual root, string name)
    {
        var button = Named<Button>(root, name)!;
        Assert.True(button.IsEffectivelyEnabled, $"{name} is disabled.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static string Part(string part) => ColumnArranger.NameOf(Id, part);

    private static TableGrid Header(Visual root) =>
        root.GetVisualDescendants().OfType<TableGrid>().First(grid => grid.Layout.Id == Id && grid.IsHeader);

    private static string[] Keys(Visual root) => [.. Header(root).Layout.Columns.Select(column => column.Key)];

    private static List<CheckBox> Boxes(Visual root) =>
        [.. root.GetVisualDescendants().OfType<CheckBox>().Where(box => box.Name?.StartsWith(Part("Show."), StringComparison.Ordinal) == true)];

    private static void Fits(Visual root)
    {
        var header = Header(root);
        var need = Enumerable.Range(0, header.Layout.Columns.Count).Sum(header.Layout.MinimumOf);

        Assert.True(need <= header.Bounds.Width + 0.5, $"the columns need {need} in {header.Bounds.Width}");
    }

    [AvaloniaFact]
    public void ColumnsAreAddedUntilNoneFitsThenRemovingOneMakesRoom()
    {
        var (window, _) = Panel();

        Press(window, Part("Tile"));
        Press(window, Part("Add"));

        while (Named<Notice>(window, Part("NoRoom")) is null)
        {
            var hidden = Boxes(window).Where(box => box.IsChecked != true).ToList();
            Assert.NotEmpty(hidden);

            var next = hidden.FirstOrDefault(box => box.IsEnabled);
            Assert.NotNull(next);
            next.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            Fits(window);
        }

        var notice = Named<Notice>(window, Part("NoRoom"))!;
        Assert.Equal(ColumnArranger.NoRoomText, notice.Text);
        Assert.Equal(ColumnArranger.NoRoomLabel, notice.Label);
        Assert.Equal(NoticeLevel.Warning, notice.Level);
        Assert.False(Named<Button>(window, Part("Add"))!.IsEnabled);
        Assert.Null(Named<StackPanel>(window, Part("Panel")));

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("headset-columns-no-room.png");
        }

        Press(window, Part("Remove"));

        Assert.Null(Named<Notice>(window, Part("NoRoom")));
        Assert.True(Named<Button>(window, Part("Add"))!.IsEnabled);
        window.Close();
    }

    [AvaloniaFact]
    public void AStoredArrangementThatNoLongerFitsIsDrawnWithoutItsColumnsAtTheRight()
    {
        var (window, views) = Panel(memory => memory.Remember(Id, ATableWiderThanItsPageScrollsInItsOwnBoxTests.Every));
        var drawn = Keys(window);

        Assert.InRange(drawn.Length, 1, 9);
        Assert.Equal(ATableWiderThanItsPageScrollsInItsOwnBoxTests.Every.Take(drawn.Length), drawn);
        Fits(window);
        Assert.Empty(Header(window).GetVisualAncestors().OfType<TableScroll>());
        Assert.Equal(ATableWiderThanItsPageScrollsInItsOwnBoxTests.Every, views.Load().ColumnArrangements[ColumnArrangement.IdFor(Id, headset: true)]);
        window.Close();
    }

    [AvaloniaFact]
    public void AColumnTooWideForTheTableHasItsCheckboxDisabled()
    {
        IReadOnlyList<TableField<string>> fields =
        [
            new("Name", new TableColumn("Name") { Min = 100 }, row => new TextBlock { Text = row }) { Label = "Name", Filters = [], Sort = SortColumn<string>.Text("Name", row => row) },
            new("Wide", new TableColumn("Wide") { Min = 450 }, row => new TextBlock { Text = row }) { Label = "Wide", Filters = [], Sort = SortColumn<string>.Text("Wide", row => row), ByDefault = false },
            new("Narrow", new TableColumn("Narrow") { Min = 50 }, row => new TextBlock { Text = row }) { Label = "Narrow", Filters = [], Sort = SortColumn<string>.Text("Narrow", row => row), ByDefault = false },
        ];
        var table = new ArrangedTable<string>("Lines", fields, new ColumnArrangementMemory(ATableWiderThanItsPageScrollsInItsOwnBoxTests.ViewState(), headset: true), null);
        var body = new StackPanel();

        void Draw()
        {
            table.Refresh();
            body.Children.Clear();
            body.Children.Add(table.Header());

            if (ColumnArranger.Bar(table) is { } bar)
            {
                body.Children.Add(bar);
            }
        }

        table.Changed += Draw;
        Draw();

        var look = AppLook.Put();
        var window = new Window { Content = body, Width = 500, Height = 500 };
        window.Closed += (_, _) => look.Dispose();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        table.ToggleArranging();
        table.ToggleAdding();
        Dispatcher.UIThread.RunJobs();

        Assert.False(table.CanAdd("Wide"));
        Assert.True(table.CanAdd("Narrow"));
        Assert.False(Named<CheckBox>(window, ColumnArranger.NameOf("Lines", "Show.Wide"))!.IsEnabled);
        Assert.True(Named<CheckBox>(window, ColumnArranger.NameOf("Lines", "Show.Narrow"))!.IsEnabled);
        Assert.Null(Named<Notice>(window, ColumnArranger.NameOf("Lines", "NoRoom")));

        table.Show("Wide");
        Assert.Equal(["Name"], Keys(table));

        table.Show("Narrow");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(["Name", "Narrow"], Keys(table));
        Assert.True(table.NoRoom);
        Assert.False(table.Adding);
        Assert.NotNull(Named<Notice>(window, ColumnArranger.NameOf("Lines", "NoRoom")));
        window.Close();
    }

    private static string[] Keys(ArrangedTable<string> table) => [.. table.ShownFields.Select(field => field.Key)];
}

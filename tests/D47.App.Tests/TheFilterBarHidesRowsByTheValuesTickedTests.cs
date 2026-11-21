using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The FILTER tile opens a bar over a table whose values hide its rows, on the desktop and in the headset (#2631).</summary>
public class TheFilterBarHidesRowsByTheValuesTickedTests
{
    private const string Id = "FilterBarTest";

    private sealed record Place(string Name, string Economy, string? Security, long Population, string Faction);

    private static readonly string[] Economies = ["Industrial", "High Tech", "Refinery", "Agriculture", "Extraction", "Service", "Tourism"];

    private static readonly Place[] Places =
    [
        .. Enumerable.Range(0, 21).Select(i => new Place(
            $"Place {i:00}",
            Economies[i % Economies.Length],
            i % 4 == 3 ? null : i % 2 == 0 ? "High" : "Low",
            1000 * i,
            $"Faction {i:00}")),
    ];

    private static ArrangedTable<Place> Table(ColumnArrangementMemory? memory = null, bool values = true) => new(
        Id,
        [
            new TableField<Place>("Name", new TableColumn("Name"), place => new TextBlock { Text = place.Name })
            {
                Label = "Name",
                Sort = SortColumn<Place>.Text("Name", place => place.Name),
                Filters = [],
                Required = true,
            },
            new TableField<Place>("Economy", new TableColumn("Economy"), place => new TextBlock { Text = place.Economy })
            {
                Label = "Economy",
                Second = "Security",
                Sort = SortColumn<Place>.Text("Economy", place => place.Economy),
                Filters = values
                    ?
                    [
                        new ValueList<Place>("Economy", place => [place.Economy], "No economy reported"),
                        new ValueList<Place>("Security", place => place.Security is { } security ? [security] : [], "No security reported"),
                    ]
                    : [],
            },
            new TableField<Place>("Population", new TableColumn("Population"), place => new TextBlock { Text = place.Population.ToString() })
            {
                Label = "Population",
                Sort = SortColumn<Place>.Figure("Population", place => (long?)place.Population, SortDirection.Descending),
                Filters = [],
            },
            new TableField<Place>("Faction", new TableColumn("Faction"), place => new TextBlock { Text = place.Faction })
            {
                Label = "Faction",
                Sort = SortColumn<Place>.Text("Faction", place => place.Faction),
                Filters = values ? [new ValueList<Place>("Faction", place => [place.Faction], "No faction reported")] : [],
            },
        ],
        memory,
        null,
        spacing: 16);

    /// <summary>A table page as the panel draws one: the arranger and the filter over the header and the rows.</summary>
    private sealed class Page : UserControl
    {
        private readonly ArrangedTable<Place> _table;
        private readonly ArrangedList _list;
        private readonly PanelPrompts? _prompts;

        public Page(ArrangedTable<Place> table, bool headset, PanelPrompts? prompts)
        {
            _table = table;
            _prompts = prompts;
            _list = new ArrangedList(headset, Id + "Rows", item => item is Place place ? table.Row(place) : null);
            table.Changed += () =>
            {
                table.Refresh();
                Draw();
            };
            Content = _list.View;
            Draw();
        }

        private void Draw()
        {
            _list.Top.Children.Clear();
            _list.Top.Children.Add(LedgerTables.Arranger(_table, Places, prompts: _prompts));

            if (ColumnFilter.Empty(_table, Places) is { } empty)
            {
                _list.Top.Children.Add(empty);
            }

            _list.Show(_table, [.. _table.Shown(Places).Cast<object>()], new Thickness(LedgerTables.RowInset, 10, LedgerTables.RowInset, 0));
        }
    }

    private static ViewStateStore ViewState() =>
        new(new AppPaths("memory"), new MemoryFileSystem(), NullLogger<ViewStateStore>.Instance);

    private static Window Show(Control page)
    {
        var look = AppLook.Put();
        var window = new Window
        {
            Content = page,
            Width = 1200,
            Height = 1400,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Closed += (_, _) => look.Dispose();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static T Named<T>(Visual root, string name)
        where T : Control =>
        root.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    private static bool Has(Visual root, string name) =>
        root.GetVisualDescendants().OfType<Control>().Any(control => control.Name == name && control.IsEffectivelyVisible);

    private static void Press(Visual root, string name)
    {
        var button = Named<Button>(root, name);
        Assert.True(button.IsEffectivelyEnabled, $"{name} is disabled.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Tick(Visual root, string key, int block, string value, bool on)
    {
        var box = Named<CheckBox>(root, ColumnFilter.ValueName(Id, key, block, value));
        box.IsChecked = on;
        Dispatcher.UIThread.RunJobs();
    }

    private static string Part(string part) => ColumnFilter.NameOf(Id, part);

    private static string Cell(string key) => Id + "Head.Filter." + key;

    private static string Text(TextBlock block) =>
        block.Inlines is { Count: > 0 } inlines ? string.Concat(inlines.OfType<Run>().Select(run => run.Text)) : block.Text ?? string.Empty;

    private static string[] Shown(ArrangedTable<Place> table) => [.. table.Shown(Places).Select(place => place.Name)];

    [AvaloniaFact]
    public void FilterOpensTheBarAHeaderChoosesAndDoneKeepsTheRowsFiltered()
    {
        var table = Table();
        var window = Show(new Page(table, headset: false, prompts: null));

        Assert.Equal("Filter", Named<Button>(window, Part("Tile")).Content);
        Press(window, Part("Tile"));
        Assert.True(Has(window, Part("Done")));
        Assert.Equal(ColumnFilter.Choosing, Named<TextBlock>(window, Part("Hint")).Text);
        Assert.False(Named<Button>(window, Part("TickAll")).IsEffectivelyEnabled);
        Assert.False(Named<Button>(window, Part("Clear")).IsEffectivelyEnabled);

        Press(window, Cell("Economy"));
        Assert.Equal("ECONOMY" + ColumnFilter.ChosenTail, Text(Named<TextBlock>(window, Part("Hint"))));
        Assert.True(Has(window, ColumnFilter.ValueName(Id, "Economy", 1, "No security reported")));
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "VALUES IN THE 21 ROWS");
        Assert.Equal(ColumnFilter.Kept, Named<TextBlock>(window, Part("Kept")).Text);

        Tick(window, "Economy", 0, "Industrial", on: false);
        Tick(window, "Economy", 0, "Tourism", on: false);
        Assert.DoesNotContain(Shown(table), name => Places.Single(place => place.Name == name).Economy is "Industrial" or "Tourism");
        Assert.Equal(15, Shown(table).Length);
        Assert.Equal("Filter · 1", Named<Button>(window, Part("Tile")).Content);

        using (var open = window.CaptureRenderedFrame()!)
        {
            open.SaveCapture("filter-bar-open.png");
        }

        Press(window, Part("Done"));
        Assert.False(Has(window, Part("Done")));
        Assert.Equal(15, Shown(table).Length);

        var head = Named<Button>(window, Id + "Head.Economy");
        Assert.Equal("Economy, filtered: 5 of 7 values shown", AutomationProperties.GetName(head));
        Assert.Contains(head.GetVisualDescendants().OfType<TextBlock>(), text => Text(text) == "ECONOMY 5/7");

        using var closed = window.CaptureRenderedFrame()!;
        closed.SaveCapture("filter-bar-closed.png");

        double Left(Control control) => control.TranslatePoint(default, window)!.Value.X;
        var words = Named<TextBlock>(window, Part("Words"));
        Assert.Equal("THESE 15 OF 21, FILTERED BY ECONOMY", words.Text);
        var order = new Control[] { words, Named<Button>(window, Part("HeadClear")), Named<Button>(window, Part("Tile")), Named<Button>(window, ColumnArranger.NameOf(Id, "Tile")) };
        Assert.Equal(order, order.OrderBy(Left));
        Assert.True(Left(order[1]) > window.Bounds.Width / 2, "CLEAR FILTERS is not with FILTER and COLUMNS at the right.");
        window.Close();
    }

    [AvaloniaFact]
    public void UntickAllHidesEveryRowAndTheEmptyLineNamesTheColumn()
    {
        var table = Table();
        var window = Show(new Page(table, headset: false, prompts: null));

        Press(window, Part("Tile"));
        Press(window, Cell("Economy"));
        Press(window, Part("UntickAll"));

        Assert.Empty(Shown(table));
        Assert.Equal("No row has a ticked Economy.", Named<TextBlock>(window, Part("Empty")).Text);

        Press(window, Part("EmptyClear"));
        Assert.Equal(Places.Length, Shown(table).Length);
        Assert.False(Has(window, Part("Empty")));
        Assert.Equal(Economies, table.FilterBlocks[0].Values.Select(value => value.Value));
        window.Close();
    }

    [AvaloniaFact]
    public void OpeningFilterClosesColumnsAndTheReverse()
    {
        var table = Table();
        var window = Show(new Page(table, headset: false, prompts: null));

        Press(window, ColumnArranger.NameOf(Id, "Tile"));
        Assert.True(table.Arranging);

        Press(window, Part("Tile"));
        Assert.True(table.Filtering);
        Assert.False(table.Arranging);
        Assert.False(Has(window, ColumnArranger.NameOf(Id, "Done")));

        Press(window, ColumnArranger.NameOf(Id, "Tile"));
        Assert.True(table.Arranging);
        Assert.False(table.Filtering);
        Assert.False(Has(window, Part("Done")));
        window.Close();
    }

    [AvaloniaFact]
    public void AFieldWithNoValueListDrawsADisabledCellAndNoHeaderSorts()
    {
        var table = Table();
        var window = Show(new Page(table, headset: false, prompts: null));

        Press(window, Part("Tile"));

        var population = Named<Button>(window, Cell("Population"));
        Assert.False(population.IsEnabled);
        Assert.Equal("Population: not filtered", AutomationProperties.GetName(population));
        Assert.False(Named<Button>(window, Cell("Name")).IsEnabled);
        Assert.False(Has(window, Id + "Head.Economy"));

        Press(window, Cell("Economy"));
        Assert.Null(table.SortedBy);
        Assert.Equal("Economy", table.FilterChosen);

        Press(window, Cell("Economy"));
        Assert.Null(table.FilterChosen);
        window.Close();
    }

    [AvaloniaFact]
    public void ATableWithNoValueListShowsNoFilterTile()
    {
        var table = Table(values: false);
        var window = Show(new Page(table, headset: false, prompts: null));

        Assert.Null(ColumnFilter.Tile(table));
        Assert.False(Has(window, Part("Tile")));
        Assert.True(Has(window, ColumnArranger.NameOf(Id, "Tile")));
        window.Close();
    }

    [AvaloniaFact]
    public void TwentyOneValuesOpenAPickerPageWhoseUseTheseAppliesTheTicks()
    {
        var nav = new PanelNavigator();
        nav.Register(PanelTab.Transcript, new NavCrumb("transcript", "Transcript"));
        var prompts = new PanelPrompts(nav, new Avalonia.Controls.Panel());
        var table = Table();
        var window = Show(new Page(table, headset: false, prompts));

        Press(window, Part("Tile"));
        Press(window, Cell("Faction"));

        Assert.Null(table.FilterChosen);
        Assert.Equal("Filter by Faction", nav.Trail[^1].Word);

        var picker = (PickerPage)prompts.Build(nav.Trail[^1])!;
        var page = Show(picker);
        var boxes = picker.GetVisualDescendants().OfType<CheckBox>().ToList();
        Assert.Equal(21, boxes.Count);
        Assert.Equal("21 of 21 ticked.", picker.GetControl<TextBlock>("TickedLine").Text);

        picker.GetControl<TextBox>("FilterBox").Text = "0";
        Dispatcher.UIThread.RunJobs();
        picker.GetControl<Button>("UntickAllButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("11 of 21 ticked.", picker.GetControl<TextBlock>("TickedLine").Text);
        Assert.Equal("Use these · 11", picker.GetControl<Button>("AcceptButton").Content);
        Assert.Equal(Places.Length, Shown(table).Length);

        picker.GetControl<Button>("AcceptButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        page.Close();

        Assert.Equal(11, Shown(table).Length);
        Assert.All(Shown(table), name => Assert.DoesNotContain("Faction 0", Places.Single(place => place.Name == name).Faction, StringComparison.Ordinal));
        Assert.Equal((11, 21), table.Mark("Faction"));

        Press(window, Cell("Faction"));
        var again = (PickerPage)prompts.Build(nav.Trail[^1])!;
        var second = Show(again);
        again.GetControl<Button>("TickAllButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        again.GetControl<Button>("BackButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        second.Close();

        Assert.Equal(11, Shown(table).Length);
        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadLineSaysWhatIsHiddenAndBy()
    {
        using var look = AppLook.Put();
        var table = Table();
        table.Observe(Places);

        Assert.Null(ColumnFilter.Words(table, Places, null));

        table.Untick("Economy", 0, "Industrial");
        Assert.Equal("these 18 of 21, filtered by Economy", ColumnFilter.Words(table, Places, null));

        table.Untick("Faction", 0, "Faction 01");
        Assert.Equal("these 17 of 21 sorted by Population, filtered by Economy and Faction", ColumnFilter.Words(table, Places, "Population"));

        Assert.Equal("No row fetched has both a ticked Economy and a ticked Power.", ColumnFilter.EmptyWords(["Economy", "Power"], search: true));
        Assert.Equal("No row has a ticked Location.", ColumnFilter.EmptyWords(["Location"], search: false));
        Assert.Equal(
            "No row has a ticked Economy, a ticked Power and a ticked Services.",
            ColumnFilter.EmptyWords(["Economy", "Power", "Services"], search: false));

        Assert.Equal("THESE 17 OF 21, FILTERED BY ECONOMY AND FACTION", ColumnFilter.Head(table, Places, null)!.Text);
    }

    [AvaloniaFact]
    public void TheHeaderCountsOnlyTheBlocksThatAreFiltered()
    {
        using var look = AppLook.Put();
        var table = Table();
        table.Observe(Places);

        table.Untick("Economy", 0, "Refinery");
        Assert.Equal((6, 7), table.Mark("Economy"));

        table.Untick("Economy", 1, "Low");
        Assert.Equal((8, 10), table.Mark("Economy"));
    }

    [AvaloniaFact]
    public void TheHeadsetHidesTheRowsWhileTheBarIsOpenWith44pxTargets()
    {
        var table = Table(new ColumnArrangementMemory(ViewState(), headset: true));
        var window = Show(new Page(table, headset: true, prompts: null));
        var rows = window.GetVisualDescendants().OfType<ScrollViewer>().First();
        Assert.True(rows.IsVisible);

        Press(window, Part("Tile"));
        Assert.False(window.GetVisualDescendants().OfType<ScrollViewer>().First().IsVisible);
        Assert.Equal(ColumnFilter.HeadsetChoosing, Named<TextBlock>(window, Part("Hint")).Text);

        Press(window, Cell("Economy"));
        Assert.Equal("ECONOMY" + ColumnFilter.HeadsetChosenTail, Text(Named<TextBlock>(window, Part("Hint"))));

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("filter-bar-headset.png");
        }

        foreach (var target in new[] { Part("Tile"), Part("TickAll"), Part("UntickAll"), Part("Clear"), Part("Done"), Cell("Economy"), Cell("Population") })
        {
            Assert.True(Named<Button>(window, target).Bounds.Height >= TypeScale.MinimumTarget, $"{target} is under 44px.");
        }

        Assert.True(Named<CheckBox>(window, ColumnFilter.ValueName(Id, "Economy", 0, "Industrial")).Bounds.Height >= TypeScale.MinimumTarget);

        Press(window, Part("Done"));
        Assert.True(window.GetVisualDescendants().OfType<ScrollViewer>().First().IsVisible);
        window.Close();
    }
}

using Avalonia;
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
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The COLUMNS bar adds, moves and removes a table's columns in the table itself, storing each change (#2516).</summary>
public class ColumnsAreArrangedInTheTableTests
{
    private const string Id = StoredModulesPage.TableId;

    private static readonly string[] Journal =
    [
        """{"timestamp":"2026-10-04T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
        """{"timestamp":"2026-10-04T09:00:00Z","event":"Location","StarSystem":"Shinrarta Dezhra","SystemAddress":3932277478106,"StarPos":[55.7,17.6,27.2],"Docked":true,"StationName":"Jameson Memorial"}""",
        """
        {"timestamp":"2026-10-04T09:01:00Z","event":"StoredModules","MarketID":128666762,"StationName":"Jameson Memorial","StarSystem":"Shinrarta Dezhra","Items":[
         {"Name":"$int_shieldgenerator_size6_class5_name;","Name_Localised":"Shield Generator","StorageSlot":1,"BuyPrice":16179531,"Hot":false},
         {"Name":"$int_powerdistributor_size5_class5_name;","Name_Localised":"Power Distributor","StorageSlot":3,"BuyPrice":3475688,"Hot":true},
         {"Name":"$int_cargorack_size7_class1_name;","Name_Localised":"Cargo Rack","StorageSlot":4,"BuyPrice":1178420,"Hot":false}
        ]}
        """,
    ];

    private static GameStateStore Store()
    {
        var store = new GameStateStore();

        foreach (var line in Journal)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store;
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
        root.GetVisualDescendants().OfType<Control>().Any(control => control.Name == name);

    private static void Press(Visual root, string name)
    {
        var button = Named<Button>(root, name);
        Assert.True(button.IsEffectivelyEnabled, $"{name} is disabled.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Tick(Visual root, string key, bool on)
    {
        var box = Named<CheckBox>(root, ColumnArranger.NameOf(Id, "Show." + key));
        Assert.True(box.IsEffectivelyEnabled, $"{key} is disabled.");
        box.IsChecked = on;
        Dispatcher.UIThread.RunJobs();
    }

    private static string Part(string part) => ColumnArranger.NameOf(Id, part);

    private static string Choose(string key) => "StoredHead.Choose." + key;

    private static List<TableGrid> Headers(Visual root) =>
        [.. root.GetVisualDescendants().OfType<TableGrid>().Where(grid => grid.Layout.Id == Id && grid.IsHeader)];

    private static string[] Keys(Visual root) =>
        [.. Headers(root).First().Layout.Columns.Select(column => column.Key)];

    private static string[] Modules(Visual root)
    {
        var rows = root.GetVisualDescendants().OfType<TableGrid>().Where(grid => grid.Layout.Id == Id && !grid.IsHeader).ToList();
        var at = rows[0].Layout.Columns.ToList().FindIndex(column => column.Key == "Module");

        return [.. rows.Select(row => row.Children.OfType<TextBlock>().Single(cell => Grid.GetColumn(cell) == at).Text!)];
    }

    private static string HeadLabel(Visual root, string name) =>
        Named<Button>(root, name).Content is TextBlock label ? label.Text! : string.Empty;

    private static IBrush? Ground(Button button) => button.Background;

    private static IBrush Brush(string key) => (IBrush)Application.Current!.Resources[key]!;

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddingMovingAndRemovingIsKeptWhenThePageOpensAgain(bool headset)
    {
        var views = ViewState();
        var store = Store();
        var window = Show(new StoredModulesPage(() => store.Active, columnArrangements: new ColumnArrangementMemory(views, headset)));

        Press(window, Part("Tile"));
        Press(window, Part("Add"));
        Tick(window, "Hot", on: true);
        Assert.Equal(["Module", "Class", "TransferCost", "TransferTime", "Hot"], Keys(window));

        Press(window, Part("MoveLeft"));
        Press(window, Part("MoveLeft"));
        Press(window, Part("MoveLeft"));
        Assert.Equal(["Module", "Hot", "Class", "TransferCost", "TransferTime"], Keys(window));

        Press(window, Choose("TransferTime"));
        Press(window, Part("Remove"));
        Press(window, Part("Done"));
        Assert.Equal(["Module", "Hot", "Class", "TransferCost"], Keys(window));
        window.Close();

        var again = Show(new StoredModulesPage(() => store.Active, columnArrangements: new ColumnArrangementMemory(views, headset)));
        Assert.Equal(["Module", "Hot", "Class", "TransferCost"], Keys(again));
        Assert.False(Has(again, Part("Done")));

        var stored = views.Load().ColumnArrangements;
        Assert.Equal(["Module", "Hot", "Class", "TransferCost"], stored[ColumnArrangement.IdFor(Id, headset)]);
        Assert.False(stored.ContainsKey(ColumnArrangement.IdFor(Id, !headset)));
        again.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetsColumnsAndTheDesktopsAreStoredApart()
    {
        var views = ViewState();
        var store = Store();
        var desktop = Show(new StoredModulesPage(() => store.Active, columnArrangements: new ColumnArrangementMemory(views)));

        Press(desktop, Part("Tile"));
        Press(desktop, Choose("Class"));
        Press(desktop, Part("Remove"));
        desktop.Close();

        var headset = Show(new StoredModulesPage(() => store.Active, columnArrangements: new ColumnArrangementMemory(views, headset: true)));
        Assert.Equal(["Module", "Class", "TransferCost", "TransferTime"], Keys(headset));
        headset.Close();
    }

    [AvaloniaFact]
    public void SortedByClassThenClassRemovedTheRowsAreInTheDefaultOrder()
    {
        var store = Store();
        var window = Show(new StoredModulesPage(() => store.Active, columnArrangements: new ColumnArrangementMemory(ViewState())));
        Assert.Equal(["Cargo Rack", "Power Distributor", "Shield Generator"], Modules(window));

        Press(window, "StoredHead.Class");
        Assert.Equal(["Cargo Rack", "Shield Generator", "Power Distributor"], Modules(window));

        Press(window, Part("Tile"));
        Press(window, Choose("Class"));
        Press(window, Part("Remove"));

        Assert.Equal(["Cargo Rack", "Power Distributor", "Shield Generator"], Modules(window));

        Press(window, Part("Done"));
        Assert.Contains("▲", HeadLabel(window, "StoredHead.Module"));
        window.Close();
    }

    [AvaloniaFact]
    public void WithTheBarOpenAHeaderPressChoosesAndDoesNotSort()
    {
        var store = Store();
        var window = Show(new StoredModulesPage(() => store.Active, columnArrangements: new ColumnArrangementMemory(ViewState())));

        Press(window, Part("Tile"));
        Assert.Contains("selected", Named<Button>(window, Part("Tile")).Classes);
        Assert.Equal(ColumnArranger.Choosing, Named<TextBlock>(window, Part("Hint")).Text);
        Assert.False(Has(window, "StoredHead.Class"));

        Press(window, Choose("Class"));

        Assert.Equal(["Cargo Rack", "Power Distributor", "Shield Generator"], Modules(window));
        Assert.Equal(Brush(ThemeManager.AKey), Ground(Named<Button>(window, Choose("Class"))));
        Assert.Equal(Brush(ThemeManager.TileKey), Ground(Named<Button>(window, Choose("Module"))));
        Assert.DoesNotContain("▲", HeadLabel(window, Choose("Module")));

        var hint = Named<TextBlock>(window, Part("Hint"));
        Assert.Equal("CLASS" + ColumnArranger.ChosenTail, string.Concat(hint.Inlines!.OfType<Run>().Select(run => run.Text)));

        Press(window, Choose("Class"));
        Assert.Equal(Brush(ThemeManager.TileKey), Ground(Named<Button>(window, Choose("Class"))));
        Assert.Equal(ColumnArranger.Choosing, Named<TextBlock>(window, Part("Hint")).Text);
        window.Close();
    }

    [AvaloniaFact]
    public void TheChoiceShowsOnEveryHeaderOfThePage()
    {
        var store = new GameStateStore();
        string[] journal =
        [
            Journal[0],
            Journal[1],
            """
            {"timestamp":"2026-10-04T09:01:00Z","event":"StoredModules","MarketID":128666762,"StationName":"Jameson Memorial","StarSystem":"Shinrarta Dezhra","Items":[
             {"Name":"$int_shieldgenerator_size6_class5_name;","Name_Localised":"Shield Generator","StorageSlot":1,"BuyPrice":16179531,"Hot":false},
             {"Name":"$int_cargorack_size7_class1_name;","Name_Localised":"Cargo Rack","StorageSlot":4,"StarSystem":"LHS 20","MarketID":3230448384,"TransferCost":38800,"TransferTime":1260,"BuyPrice":1178420,"Hot":false}
            ]}
            """,
        ];

        foreach (var line in journal)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        var window = Show(new StoredModulesPage(() => store.Active, columnArrangements: new ColumnArrangementMemory(ViewState())));
        Press(window, Part("Tile"));
        Press(window, Choose("TransferCost"));

        var heads = Headers(window);
        Assert.Equal(2, heads.Count);

        foreach (var head in heads)
        {
            var cell = head.Children.OfType<Button>().Single(button => button.Name == Choose("TransferCost"));
            Assert.Equal(Brush(ThemeManager.AKey), Ground(cell));
        }

        window.Close();
    }

    [AvaloniaFact]
    public void TheBarRefusesWhatTheArrangementRefuses()
    {
        var store = Store();
        var window = Show(new StoredModulesPage(() => store.Active, columnArrangements: new ColumnArrangementMemory(ViewState())));
        Press(window, Part("Tile"));

        Assert.False(Named<Button>(window, Part("MoveLeft")).IsEnabled);
        Assert.False(Named<Button>(window, Part("MoveRight")).IsEnabled);
        Assert.False(Named<Button>(window, Part("Remove")).IsEnabled);
        Assert.False(Named<Button>(window, Part("Default")).IsEnabled);

        Press(window, Choose("Module"));
        Assert.False(Named<Button>(window, Part("MoveLeft")).IsEnabled);
        Assert.True(Named<Button>(window, Part("MoveRight")).IsEnabled);
        Assert.True(Named<Button>(window, Part("Remove")).IsEnabled);

        Press(window, Choose("TransferTime"));
        Assert.True(Named<Button>(window, Part("MoveLeft")).IsEnabled);
        Assert.False(Named<Button>(window, Part("MoveRight")).IsEnabled);

        Press(window, Part("Add"));
        Assert.Contains("selected", Named<Button>(window, Part("Add")).Classes);

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("columns-bar-open.png");
        }

        var labels = Named<StackPanel>(window, Part("Panel")).GetVisualDescendants().OfType<CheckBox>()
            .Select(box => box.Name!.Replace(Part("Show."), string.Empty, StringComparison.Ordinal))
            .ToList();
        Assert.Equal(["Module", "Class", "TransferCost", "TransferTime", "Engineering", "Grade", "Hot", "InTransit", "Station", "System"], labels);

        Tick(window, "Class", on: false);
        Tick(window, "TransferCost", on: false);
        Tick(window, "TransferTime", on: false);
        Assert.Equal(["Module"], Keys(window));

        var last = Named<CheckBox>(window, Part("Show.Module"));
        Assert.True(last.IsChecked);
        Assert.False(last.IsEnabled);

        Press(window, Choose("Module"));
        Assert.False(Named<Button>(window, Part("Remove")).IsEnabled);
        Assert.True(Named<Button>(window, Part("Default")).IsEnabled);
        window.Close();
    }

    [AvaloniaFact]
    public void DefaultColumnsForgetsTheArrangementAndKeepsTheWidths()
    {
        var views = ViewState();
        var widths = new ColumnWidthMemory(views);
        widths.Remember(Id, "Module", 300);
        var memory = new ColumnArrangementMemory(views);
        memory.Remember(Id, ["Hot", "Module"]);
        var store = Store();
        var window = Show(new StoredModulesPage(() => store.Active, widths, columnArrangements: memory));

        Press(window, Part("Tile"));
        Press(window, Part("Default"));

        Assert.Equal(["Module", "Class", "TransferCost", "TransferTime"], Keys(window));
        Assert.False(views.Load().ColumnArrangements.ContainsKey(Id));
        Assert.Equal(300, views.Load().ColumnWidths[Id]["Module"]);
        Assert.False(Named<Button>(window, Part("Default")).IsEnabled);
        window.Close();
    }

    [AvaloniaFact]
    public void ARequiredColumnCannotBeRemoved()
    {
        IReadOnlyList<TableField<string>> fields =
        [
            new("Name", new TableColumn("Name"), row => new TextBlock { Text = row }) { Label = "Name", Required = true },
            new("Size", new TableColumn("Size"), row => new TextBlock { Text = row }) { Label = "Size" },
        ];
        var table = new ArrangedTable<string>("Lines", fields, null, null);

        table.ToggleArranging();
        table.Choose("Name");

        Assert.False(table.CanRemove);
        Assert.Contains(table.Choices, choice => choice is { Key: "Name", Locked: true });
        Assert.Contains(table.Choices, choice => choice is { Key: "Size", Locked: false });
    }

    [AvaloniaFact]
    public void ATableOfOneColumnShowsNoColumnsTile()
    {
        IReadOnlyList<TableField<string>> fields =
        [
            new("Name", new TableColumn("Name"), row => new TextBlock { Text = row }) { Label = "Name" },
            new("Go", new TableColumn("Go"), row => new TextBlock { Text = row }),
        ];

        Assert.Null(ColumnArranger.Line(new ArrangedTable<string>("Lines", fields, null, null)));
    }
}

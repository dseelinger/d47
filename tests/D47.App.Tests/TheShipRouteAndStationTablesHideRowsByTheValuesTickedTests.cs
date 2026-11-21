using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The ship, module and station tables list the values of their columns in the FILTER bar, and unticking one hides its rows (#2634).</summary>
[Trait("Category", "Integration")]
public class TheShipRouteAndStationTablesHideRowsByTheValuesTickedTests
{
    private const long Address = 7786;
    private const long Market = 3700001;

    private static readonly string[] FleetLines =
    [
        """{"timestamp":"2026-10-05T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
        """{"timestamp":"2026-08-18T09:01:00Z","event":"Loadout","Ship":"mandalay","ShipID":1,"ShipName":"Far Walker","ShipIdent":"FW-22","HullValue":200000000,"ModulesValue":88120000,"Rebuy":14406000,"UnladenMass":318.2,"CargoCapacity":32,"MaxJumpRange":71.8,"FuelCapacity":{"Main":32.0,"Reserve":0.5},"Modules":[]}""",
        """{"timestamp":"2026-08-18T09:02:00Z","event":"Loadout","Ship":"type9","ShipID":2,"ShipName":"Deep Hold","ShipIdent":"DH-09","HullValue":76555842,"ModulesValue":335884158,"Rebuy":20622000,"UnladenMass":1120.6,"CargoCapacity":400,"MaxJumpRange":18.4,"FuelCapacity":{"Main":64.0,"Reserve":0.77},"Modules":[]}""",
    ];

    private static readonly string[] StoredLines =
    [
        """{"timestamp":"2026-10-04T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
        """{"timestamp":"2026-10-04T09:00:00Z","event":"Location","StarSystem":"Shinrarta Dezhra","SystemAddress":3932277478106,"StarPos":[55.7,17.6,27.2],"Docked":true,"StationName":"Jameson Memorial"}""",
        """
        {"timestamp":"2026-10-04T09:01:00Z","event":"StoredModules","MarketID":128666762,"StationName":"Jameson Memorial","StarSystem":"Shinrarta Dezhra","Items":[
         {"Name":"$int_shieldgenerator_size6_class5_name;","Name_Localised":"Shield Generator","StorageSlot":1,"BuyPrice":16179531,"Hot":false},
         {"Name":"$int_cargorack_size7_class1_name;","Name_Localised":"Cargo Rack","StorageSlot":4,"BuyPrice":1178420,"Hot":false}
        ]}
        """,
    ];

    private static readonly DateTimeOffset AskedAt = new(2026, 10, 10, 19, 42, 0, TimeSpan.Zero);

    private static readonly StarSystemProfile Profile = new()
    {
        Name = "LTT 7786",
        SystemAddress = Address,
        Stations =
        [
            new StationProfile
            {
                Name = "Smith Port",
                MarketId = Market,
                Kind = StationKind.Starport,
                Type = "Orbis Starport",
                Market = new StationMarket(
                    [
                        new MarketCommodity("Gold", "gold", "Metals", 9000, 0, 12, 0),
                        new MarketCommodity("Algae", "algae", "Foods", 100, 80, 500, 40),
                    ],
                    AskedAt),
            },
        ],
    };

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

    private static void Untick(Visual root, string table, string key, string value)
    {
        Named<CheckBox>(root, ColumnFilter.ValueName(table, key, 0, value)).IsChecked = false;
        Dispatcher.UIThread.RunJobs();
    }

    private static bool Shows(Visual root, string text) =>
        root.GetVisualDescendants().OfType<TextBlock>().Any(block => string.Equals(block.Text, text, StringComparison.OrdinalIgnoreCase));

    private static void Reattach(Window window, Control page)
    {
        window.Content = null;
        Dispatcher.UIThread.RunJobs();
        window.Content = page;
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void AShipsLocationIsListedAndUntickingItHidesTheShipsThere()
    {
        var state = JournalScript.State(FleetLines);
        var (window, page) = PageHost.Open(() => new ComparePage(() => state));
        var table = ComparePage.TableId;

        Assert.True(Shows(page, "Far Walker"));
        Assert.True(Shows(page, "Deep Hold"));

        Press(page, ColumnFilter.NameOf(table, "Tile"));
        Press(page, "CompareHead.Filter.Where");
        Untick(page, table, "Where", "Location not known");

        Assert.False(Shows(page, "Far Walker"));
        Assert.True(Shows(page, "Deep Hold"));
        Assert.Contains(
            page.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text?.StartsWith("THESE 1 OF 2", StringComparison.Ordinal) == true);

        Reattach(window, page);

        Assert.True(Shows(page, "Far Walker"));
        Assert.False(Has(page, ColumnFilter.NameOf(table, "HeadClear")));
        window.Close();
    }

    [AvaloniaFact]
    public void AModulesClassIsListedAndUntickingItHidesTheModulesOfThatClass()
    {
        var store = new GameStateStore();

        foreach (var line in StoredLines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        var (window, page) = PageHost.Open(() => new StoredModulesPage(() => store.Active));
        var table = StoredModulesPage.TableId;

        Assert.True(Shows(page, "Shield Generator"));
        Assert.True(Shows(page, "Cargo Rack"));

        Press(page, ColumnFilter.NameOf(table, "Tile"));
        Press(page, "StoredHead.Filter.Class");
        Untick(page, table, "Class", "7E");

        Assert.True(Shows(page, "Shield Generator"));
        Assert.False(Shows(page, "Cargo Rack"));
        window.Close();
    }

    [AvaloniaFact]
    public void AStationMarketsCategoryIsListedAndUntickingItHidesItsCommodities()
    {
        var surface = new StarSystemSurface(new StubStarSystemService(), () => null);
        var (window, page) = PageHost.Open(() => new SystemStationPage(surface, Address, Market, "Smith Port", Profile));
        var table = SystemStationPage.MarketTable;

        page.Show(SystemStationPage.Section.Market);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Shows(page, "Gold"));
        Assert.True(Shows(page, "Algae"));

        Press(page, ColumnFilter.NameOf(table, "Tile"));
        Press(page, "MarketHead.Filter.Category");
        Untick(page, table, "Category", "Foods");

        Assert.True(Shows(page, "Gold"));
        Assert.False(Shows(page, "Algae"));
        window.Close();
    }
}

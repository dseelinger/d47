using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Crew, Stored modules, Materials, Construction, Compare, Phrases and the HOTAS positions draw through a TableLayout (#1615).</summary>
public class TheShipMaterialAndPhraseTablesFitTheirColumnsTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private static GameStateStore Store(params string[] lines)
    {
        var store = new GameStateStore();

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store;
    }

    private const string Commander = """{"timestamp":"2026-10-05T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""";

    private static Window Show(Control page, double width = 1000)
    {
        var look = AppLook.Put();
        var window = new Window
        {
            Content = page,
            Width = width,
            Height = 1400,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Closed += (_, _) => look.Dispose();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static double Natural(Control child)
    {
        var supplied = TableGrid.GetNaturalWidth(child);

        return double.IsNaN(supplied) ? child.DesiredSize.Width : Math.Max(supplied, child.DesiredSize.Width);
    }

    private static List<TableGrid> Grids(Visual root, string id) =>
        [.. root.GetVisualDescendants().OfType<TableGrid>().Where(grid => grid.Layout.Id == id)];

    /// <summary>The header and every row share column edges, and no Content column is wider than its widest cell.</summary>
    private static void Fits(Visual root, string id, int rowsAtLeast = 1, int headers = 1)
    {
        var grids = Grids(root, id);
        var heads = grids.Where(grid => grid.IsHeader).ToList();
        var rows = grids.Where(grid => !grid.IsHeader).ToList();

        Assert.Equal(headers, heads.Count);
        Assert.True(rows.Count >= rowsAtLeast, $"{id}: {rows.Count} rows drawn");

        var edges = heads[0].ColumnDefinitions.Select(definition => definition.ActualWidth).ToList();

        foreach (var grid in grids)
        {
            Assert.Equal(edges, grid.ColumnDefinitions.Select(definition => definition.ActualWidth));
        }

        var columns = heads[0].Layout.Columns;
        var spacing = heads[0].Layout.Spacing;

        for (var column = 0; column < columns.Count; column++)
        {
            if (columns[column].Fit != ColumnFit.Content)
            {
                continue;
            }

            var widest = grids
                .SelectMany(grid => grid.Children.Where(child => Grid.GetColumn(child) == column && Grid.GetColumnSpan(child) == 1 && child is not ColumnGrip))
                .Select(Natural).DefaultIfEmpty(0).Max();

            Assert.True(edges[column] - spacing <= widest + 1, $"{id}.{columns[column].Key} is {edges[column]} wide; its widest cell is {widest} (spacing {spacing})");
        }
    }

    private static ColumnWidthMemory Memory() =>
        new(new ViewStateStore(new AppPaths("memory"), new MemoryFileSystem(), NullLogger<ViewStateStore>.Instance));

    private static void Remembered(Visual root, string id, string key, double width)
    {
        var header = Grids(root, id).First(grid => grid.IsHeader);
        var column = header.Layout.Columns.ToList().FindIndex(c => c.Key == key);

        Assert.Equal(width + header.Layout.Spacing, header.ColumnDefinitions[column].ActualWidth, 1.5);
        Assert.NotEmpty(header.Children.OfType<ColumnGrip>());
    }

    private static string Hire(string name, int id, int rank) =>
        $$"""{"timestamp":"2026-10-05T09:01:00Z","event":"CrewHire","Name":"{{name}}","CrewID":{{id}},"CombatRank":{{rank}}}""";

    [AvaloniaFact]
    public void TheCrewFitsItsColumns()
    {
        var store = Store(Commander, Hire("Samira Voss", 1, 6), Hire("Ed", 2, 8));
        var window = Show(new CrewPage(() => store.Active));

        Fits(window, CrewPage.TableId, 2);
        window.Close();
    }

    [AvaloniaFact]
    public void ADraggedCrewWidthHoldsWhenThePageIsReopened()
    {
        var memory = Memory();
        memory.Remember(CrewPage.TableId, "CombatRank", 300);
        var store = Store(Commander, Hire("Samira Voss", 1, 6));
        var window = Show(new CrewPage(() => store.Active, columnWidths: memory));

        Remembered(window, CrewPage.TableId, "CombatRank", 300);
        window.Close();
    }

    private static readonly string[] StoredJournal =
    [
        Commander,
        """{"timestamp":"2026-10-04T09:00:00Z","event":"Location","StarSystem":"Shinrarta Dezhra","SystemAddress":3932277478106,"StarPos":[55.7,17.6,27.2],"Docked":true,"StationName":"Jameson Memorial"}""",
        """
        {"timestamp":"2026-10-04T09:01:00Z","event":"StoredModules","MarketID":128666762,"StationName":"Jameson Memorial","StarSystem":"Shinrarta Dezhra","Items":[
         {"Name":"$int_shieldgenerator_size6_class5_name;","Name_Localised":"Shield Generator","StorageSlot":1,"BuyPrice":16179531,"Hot":false},
         {"Name":"$int_powerdistributor_size5_class5_name;","Name_Localised":"Power Distributor","StorageSlot":3,"StarSystem":"Deciat","MarketID":3221636096,"TransferCost":212400,"TransferTime":840,"BuyPrice":3475688,"Hot":false},
         {"Name":"$int_fuelscoop_size6_class5_name;","Name_Localised":"Fuel Scoop","StorageSlot":5,"StarSystem":"Colonia","MarketID":3228342528,"TransferCost":4880200,"TransferTime":7920,"BuyPrice":28763610,"Hot":false},
         {"Name":"$int_fighterbay_size5_class1_name;","Name_Localised":"Fighter Hangar","StorageSlot":6,"StarSystem":"Colonia","MarketID":3228342528,"TransferCost":402100,"TransferTime":7920,"BuyPrice":575015,"Hot":false}
        ]}
        """,
    ];

    [AvaloniaFact]
    public void TheStoredModulesFitTheirColumnsAndEveryGroupShareThem()
    {
        var store = Store(StoredJournal);
        var window = Show(new StoredModulesPage(() => store.Active));

        var grids = Grids(window, StoredModulesPage.TableId);
        Assert.Equal(3, grids.Count(grid => grid.IsHeader));

        Fits(window, StoredModulesPage.TableId, 4, headers: 3);
        window.Close();
    }

    [AvaloniaFact]
    public void ADraggedStoredModuleWidthHoldsInEveryGroup()
    {
        var memory = Memory();
        memory.Remember(StoredModulesPage.TableId, "TransferCost", 260);
        var store = Store(StoredJournal);
        var window = Show(new StoredModulesPage(() => store.Active, memory));

        foreach (var header in Grids(window, StoredModulesPage.TableId).Where(grid => grid.IsHeader))
        {
            Assert.Equal(260, header.ColumnDefinitions[2].ActualWidth, 0.5);
        }

        window.Close();
    }

    private static readonly string[] CompareJournal =
    [
        Commander,
        """{"timestamp":"2026-08-18T09:01:00Z","event":"Loadout","Ship":"mandalay","ShipID":1,"ShipName":"Far Walker","ShipIdent":"FW-22","HullValue":200000000,"ModulesValue":88120000,"Rebuy":14406000,"UnladenMass":318.2,"CargoCapacity":32,"MaxJumpRange":71.8,"FuelCapacity":{"Main":32.0,"Reserve":0.5},"Modules":[]}""",
        """{"timestamp":"2026-08-18T09:02:00Z","event":"Loadout","Ship":"type9","ShipID":2,"ShipName":"Deep Hold","ShipIdent":"DH-09","HullValue":76555842,"ModulesValue":335884158,"Rebuy":20622000,"UnladenMass":1120.6,"CargoCapacity":400,"MaxJumpRange":18.4,"FuelCapacity":{"Main":64.0,"Reserve":0.77},"Modules":[]}""",
    ];

    [AvaloniaFact]
    public void TheCompareTableFitsItsColumns()
    {
        var store = Store(CompareJournal);
        var window = Show(new ComparePage(() => store.Active), 1200);

        Fits(window, ComparePage.TableId, 2);
        window.Close();
    }

    [AvaloniaFact]
    public void ADraggedCompareWidthHoldsWhenThePageIsReopened()
    {
        var memory = Memory();
        memory.Remember(ComparePage.TableId, "Value", 240);
        var store = Store(CompareJournal);
        var window = Show(new ComparePage(() => store.Active, memory), 1200);

        Remembered(window, ComparePage.TableId, "Value", 240);
        window.Close();
    }

    private static (Window Window, PanelView Panel, ShipPlanService Ships, OnFootPlanService Kit) Fleet(GameStateStore store, ColumnWidthMemory? memory = null)
    {
        var root = TestSurface.MemoryFolder("d47-ship-material-tables");

        CommanderGameState? State() => store.Active;

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(Path.Combine(root, "checklist-proposals.json"), new MemoryFileSystem(), NullLogger<ChecklistProposalStore>.Instance),
            State);
        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(root, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance), checklists, State);
        var kit = new OnFootPlanService(
            new OnFootBuildStore(Path.Combine(root, "on-foot.json"), new MemoryFileSystem(), NullLogger<OnFootBuildStore>.Instance), checklists, State);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (memory is not null)
        {
            panel.EnableResizableColumns(memory);
        }

        panel.EnableLoadout(ships, checklists, State, kit);

        var window = new Window { Content = panel, Width = 1280, Height = 1400 };
        window.Show();

        panel.Tab = PanelTab.Assets;
        Dispatcher.UIThread.RunJobs();

        return (window, panel, ships, kit);
    }

    [AvaloniaFact]
    public void TheMaterialsNeededByPlansFitTheirColumnsAndEveryPlanShareThem()
    {
        using var look = AppLook.Put();
        var store = Store(
            Commander,
            """{"timestamp":"2026-08-18T09:00:00Z","event":"SuitLoadout","SuitID":7,"SuitName":"utilitysuit_class3","LoadoutName":"Ground","SuitMods":[],"Modules":[]}""",
            """{"timestamp":"2026-08-18T09:00:00Z","event":"Materials","Raw":[],"Manufactured":[],"Encoded":[]}""");
        var (window, panel, ships, kit) = Fleet(store, Memory());

        var ship = ships.BuildFor(12, "anaconda", "Sacred Wings");
        ships.Plan(ship.Id, new SlotPlan("MainEngines") { Blueprint = "Dirty Drive Tuning", Grade = 5, Module = "Thrusters" });
        var suit = kit.BuildFor(OnFootKind.Suit, 7, "Maverick Suit");
        kit.Plan(suit.Id, new KitPlan(OnFootBuild.GradeSlot, 5));

        Assert.True(panel.Nav.SelectRoot(LoadoutPages.GapRoot));
        Dispatcher.UIThread.RunJobs();

        var heads = Grids(panel, MaterialsPage.NeededTable).Count(grid => grid.IsHeader);
        Assert.True(heads >= 2, $"{heads} plans drawn");

        Fits(panel, MaterialsPage.NeededTable, 2, headers: heads);
        window.Close();
    }

    [AvaloniaFact]
    public void ThePhrasesTablesFitTheirColumns()
    {
        var root = TestSurface.MemoryFolder("d47-phrases-fit");
        var learned = new LearnedPhrasesStore(Path.Combine(root, "phrases.json"), new MemoryFileSystem(), NullLogger<LearnedPhrasesStore>.Instance);
        learned.Learn("F1", "[gear|wheels] out", "new phrase", At);
        learned.Learn("F1", "lights on", "new phrase", At);
        var store = Store(Commander);

        CapabilityRegistry? registry = null;
        registry = CapabilityRegistry.Build(
        [
            LearnedPhrasesCapability.Create(
                learned,
                () => store.Active?.Identity.FrontierId ?? string.Empty,
                () => PhraseBook.From(registry!, [])),
            InterfaceCapability.Create(),
            .. ActionCapabilities.All(ActionSurface.Inert),
        ]);

        var memory = Memory();
        memory.Remember(PhrasesPage.OwnTable, "Phrase", 300);
        var window = Show(new PhrasesPage(registry, learned, () => store.Active, columnWidths: memory), 1100);

        Fits(window, PhrasesPage.OwnTable, 2);
        Remembered(window, PhrasesPage.OwnTable, "Phrase", 300);

        var groups = PhrasesPage.Groups(PhraseBook.From(registry, []), registry, string.Empty).Count;
        Assert.True(groups >= 2, $"{groups} built-in groups");
        Fits(window, PhrasesPage.BuiltInTable, groups);
        window.Close();
    }

    [AvaloniaFact]
    public void TheHotasPositionsFitTheirColumns()
    {
        var root = TestSurface.MemoryFolder("d47-switch-fit");
        var switches = new SwitchStore(Path.Combine(root, "switches.json"), new MemoryFileSystem(), NullLogger<SwitchStore>.Instance);

        SwitchMapping Mapping(string name, params SwitchPosition[] positions) => new()
        {
            Name = name,
            DeviceId = "stick-1",
            Device = "VID 0x4098 PID 0xBD65, 32 buttons, 0 hats, 0 axes",
            Positions = positions,
        };

        switches.Save(
        [
            Mapping("LDG GEAR", new SwitchPosition(9, "landing_gear", DesiredState.On), new SwitchPosition(8, "landing_gear", DesiredState.Off)),
            Mapping("HOOK", new SwitchPosition(3), new SwitchPosition(2)),
        ]);
        switches.Poll();

        var page = new SwitchPage(
            switches,
            new FakeHotasReader(),
            new SwitchReconciler(NullLogger<SwitchReconciler>.Instance),
            () => At,
            Path.Combine(root, "capture.txt"),
            [],
            columnWidths: Memory());
        var window = Show(page, 900);

        var grids = Grids(window, SwitchPage.PositionsTable);
        Assert.Equal(2, grids.Count(grid => grid.IsHeader));
        Assert.Equal(4, grids.Count(grid => !grid.IsHeader));

        var edges = grids[0].ColumnDefinitions.Select(definition => definition.ActualWidth).ToList();

        foreach (var grid in grids)
        {
            Assert.Equal(edges, grid.ColumnDefinitions.Select(definition => definition.ActualWidth));
        }

        window.Close();
    }

    [AvaloniaFact]
    public void TheConstructionNeedsFitTheirColumns()
    {
        using var look = AppLook.Put();
        var now = new DateTimeOffset(2026, 10, 5, 18, 0, 0, TimeSpan.Zero);
        var state = new CommanderGameState(new CommanderIdentity("F735466", "John Deparagon"));

        void Apply(string json)
        {
            var root = System.Text.Json.JsonDocument.Parse(json).RootElement;
            state.Apply(new JournalEvent(now - TimeSpan.FromHours(2), root.GetProperty("event").GetString()!, root));
        }

        Apply("""{"event":"Docked","StationName":"Vogel Relay","StationType":"SpaceConstructionDepot","StarSystem":"HIP 47126","MarketID":3900000001}""");
        Apply("""{"event":"ColonisationConstructionDepot","MarketID":3900000001,"ConstructionProgress":0.62,"ConstructionComplete":false,"ConstructionFailed":false,"ResourcesRequired":[{"Name":"$steel_name;","Name_Localised":"Steel","RequiredAmount":20000,"ProvidedAmount":5180,"Payment":1000},{"Name":"$aluminium_name;","Name_Localised":"Aluminium","RequiredAmount":9410,"ProvidedAmount":0,"Payment":1000},{"Name":"$titanium_name;","Name_Localised":"Titanium","RequiredAmount":1150,"ProvidedAmount":0,"Payment":1000}]}""");

        var root = TestSurface.MemoryFolder("d47-construction-fit");
        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(Path.Combine(root, "checklist-proposals.json"), new MemoryFileSystem(), NullLogger<ChecklistProposalStore>.Instance),
            () => null);
        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(root, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance), checklists, () => null);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableResizableColumns(Memory());
        panel.EnableLoadout(ships, checklists, () => state, null, construction: new ConstructionSurface(() => state, () => now, null, new SourcingBoard(), () => true));

        var window = new Window { Content = panel, Width = 1280, Height = 900 };
        window.Show();
        panel.Tab = PanelTab.Assets;
        panel.Nav.SelectRoot(PanelTab.Assets, ConstructionPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        Fits(panel, ConstructionPage.NeedsTable, 3);
        window.Close();
    }
}

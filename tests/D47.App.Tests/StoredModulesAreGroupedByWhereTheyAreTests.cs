using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Loadout;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Fleet › Stored modules: every stored module, grouped by system, filtered by name (#563).</summary>
[Trait("Category", "Integration")]
public class StoredModulesAreGroupedByWhereTheyAreTests
{
    private sealed record Surface(Window Window, PanelView Panel, GameStateStore Store);

    private const string Location =
        """{"timestamp":"2026-10-04T09:00:00Z","event":"Location","StarSystem":"Shinrarta Dezhra","SystemAddress":3932277478106,"StarPos":[55.7,17.6,27.2],"Docked":true,"StationName":"Jameson Memorial"}""";

    private const string Stored =
        """
        {"timestamp":"2026-10-04T09:01:00Z","event":"StoredModules","MarketID":128666762,"StationName":"Jameson Memorial","StarSystem":"Shinrarta Dezhra","Items":[
         {"Name":"$int_shieldgenerator_size6_class5_name;","Name_Localised":"Shield Generator","StorageSlot":1,"BuyPrice":16179531,"Hot":false},
         {"Name":"$int_guardianfsdbooster_size3_name;","Name_Localised":"Guardian FSD Booster","StorageSlot":2,"BuyPrice":1620430,"Hot":false},
         {"Name":"$int_powerdistributor_size5_class5_name;","Name_Localised":"Power Distributor","StorageSlot":3,"StarSystem":"Deciat","MarketID":3221636096,"TransferCost":212400,"TransferTime":840,"BuyPrice":3475688,"Hot":false},
         {"Name":"$int_cargorack_size7_class1_name;","Name_Localised":"Cargo Rack","StorageSlot":4,"StarSystem":"LHS 20","MarketID":3230448384,"TransferCost":38800,"TransferTime":1260,"BuyPrice":1178420,"Hot":false},
         {"Name":"$int_fuelscoop_size6_class5_name;","Name_Localised":"Fuel Scoop","StorageSlot":5,"StarSystem":"Colonia","MarketID":3228342528,"TransferCost":4880200,"TransferTime":7920,"BuyPrice":28763610,"Hot":false},
         {"Name":"$int_fighterbay_size5_class1_name;","Name_Localised":"Fighter Hangar","StorageSlot":6,"StarSystem":"Colonia","MarketID":3228342528,"TransferCost":402100,"TransferTime":7920,"BuyPrice":575015,"Hot":false}
        ]}
        """;

    private static readonly string[] Journal =
    [
        """{"timestamp":"2026-10-04T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
        Location,
        Stored,
    ];

    private static Surface Open(IEnumerable<string> journal)
    {
        var root = TempFolders.Create("d47-stored-modules-tests");
        var store = new GameStateStore();

        foreach (var line in journal)
        {
            Apply(store, line);
        }

        CommanderGameState? State() => store.Active;

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(Path.Combine(root, "checklist-proposals.json"), new MemoryFileSystem(), NullLogger<ChecklistProposalStore>.Instance),
            State);
        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance), checklists, State);
        var kit = new OnFootPlanService(
            new OnFootBuildStore(Path.Combine(root, "on-foot.json"), NullLogger<OnFootBuildStore>.Instance), checklists, State);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableLoadout(ships, checklists, State, kit);
        panel.EnableSearch();

        var window = new Window { Content = panel, Width = 1280, Height = 1100 };
        window.Show();

        panel.Tab = PanelTab.Assets;
        Dispatcher.UIThread.RunJobs();
        panel.Nav.SelectRoot(StoredModulesPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, store);
    }

    private static void Apply(GameStateStore store, string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        store.Apply(parsed!);
    }

    private static StoredModulesPage Page(PanelView panel) =>
        panel.GetVisualDescendants().OfType<StoredModulesPage>().Single();

    private static List<string> Text(Control page) =>
        [.. page.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    private static TextBox Search(PanelView panel) => panel.FindControl<TextBox>("SearchInput")!;

    [AvaloniaFact]
    public void ModulesAreGroupedBySystemWithHereFirstAndNearestNext()
    {
        var surface = Open(Journal);
        var groups = StoredModulesPage.Groups(surface.Store.Active);

        Assert.Equal(["Shinrarta Dezhra", "Deciat", "LHS 20", "Colonia"], groups.Select(group => group.System));
        Assert.Equal("Jameson Memorial · here · 2 MODULES", StoredModulesPage.Detail(groups[0]));
        Assert.Equal("1 MODULE", StoredModulesPage.Detail(groups[1]));
        Assert.Equal("2 MODULES", StoredModulesPage.Detail(groups[3]));

        var text = Text(Page(surface.Panel));
        Assert.Contains("STORED MODULES", text);
        Assert.Contains("6", text);
        Assert.Contains("SHINRARTA DEZHRA", text);
        Assert.DoesNotContain(text, line => line.Contains(" LY", StringComparison.Ordinal));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void EachRowShowsClassCostAndTime()
    {
        var surface = Open(Journal);
        var text = Text(Page(surface.Panel));

        Assert.Contains("6A", text);
        Assert.Contains("7E", text);
        Assert.Contains(StoredModulesPage.Free, text);
        Assert.Contains("212,400 CR", text);
        Assert.Contains("00:14", text);
        Assert.Contains("02:12", text);

        var groups = StoredModulesPage.Groups(surface.Store.Active);
        var booster = groups[0].Modules.Single(module => module.Name == "Guardian FSD Booster");
        Assert.Null(booster.Class);
        Assert.Equal("—", StoredModulesPage.Time(booster));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheCurrentSystemIsCyanAndOthersOrange()
    {
        using var look = AppLook.Put();

        var surface = Open(Journal);
        var page = Page(surface.Panel);

        Assert.Equal(Brush(ThemeManager.CyanKey), Ink(page, "SHINRARTA DEZHRA"));
        Assert.Equal(Brush(ThemeManager.AKey), Ink(page, "COLONIA"));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheSearchFiltersByNameAndHidesEmptyGroups()
    {
        var surface = Open(Journal);

        Assert.Equal("Filter by module name", Search(surface.Panel).PlaceholderText);
        Assert.True(Search(surface.Panel).IsVisible);

        Search(surface.Panel).Text = "fuel";
        Dispatcher.UIThread.RunJobs();

        var text = Text(Page(surface.Panel));
        Assert.Contains("COLONIA", text);
        Assert.Contains("Fuel Scoop", text);
        Assert.DoesNotContain("Fighter Hangar", text);
        Assert.DoesNotContain("DECIAT", text);
        Assert.Contains("1", text);

        Search(surface.Panel).Text = "plasma";
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(StoredModulesPage.NoMatch("plasma"), Text(Page(surface.Panel)));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void ANewSnapshotRedrawsTheOpenPage()
    {
        var surface = Open(Journal.Take(2));

        Assert.Contains(StoredModulesPage.Unseen, Text(Page(surface.Panel)));

        Apply(surface.Store, Stored);
        Assert.True(surface.Panel.TickLoadout());
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Fuel Scoop", Text(Page(surface.Panel)));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheStoredModulesPageIsCapturedWholeAndFiltered()
    {
        using var look = AppLook.Put();

        var surface = Open(Journal);

        foreach (var (query, name) in new[] { ("", "all"), ("fuel", "filtered") })
        {
            Search(surface.Panel).Text = query;
            Dispatcher.UIThread.RunJobs();

            var path = $"fleet-stored-modules-{name}.png";

            using (var frame = surface.Window.CaptureRenderedFrame()!)
            {
                frame.SaveCapture(path);
            }
        }

        surface.Window.Close();
    }

    private static IBrush? Ink(StoredModulesPage page, string text) =>
        page.GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == text).Foreground;

    private static IBrush? Brush(string key) =>
        Avalonia.Application.Current!.Resources.TryGetResource(key, null, out var brush) ? brush as IBrush : null;
}

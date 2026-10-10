using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Fleet › Materials › Backpack and Ship locker, and the on-foot resource detail they open (#560).</summary>
[Trait("Category", "Integration")]
public class WhatYouCarryOnFootIsCountedByKindTests
{
    private sealed record Surface(Window Window, PanelView Panel, OnFootPlanService Kit, GameStateStore Store);

    private const string BackpackJson = """
        {"timestamp":"2026-08-18T09:00:00Z","event":"Backpack",
         "Items":[{"Name":"suitschematic","Name_Localised":"Suit Schematic","OwnerID":0,"Count":1}],
         "Components":[{"Name":"graphene","OwnerID":0,"Count":6},{"Name":"graphene","OwnerID":0,"MissionID":5,"Count":2}],
         "Consumables":[{"Name":"energycell","Name_Localised":"Energy Cell","OwnerID":0,"Count":4}],
         "Data":[]}
        """;

    private const string LockerJson = """
        {"timestamp":"2026-08-18T09:00:00Z","event":"ShipLocker",
         "Items":[{"Name":"suitschematic","Name_Localised":"Suit Schematic","OwnerID":0,"Count":4}],
         "Components":[{"Name":"graphene","OwnerID":0,"Count":212},{"Name":"carbonfibreplating","Name_Localised":"Carbon Fibre Plating","OwnerID":0,"Count":180}],
         "Consumables":[{"Name":"healthpack","Name_Localised":"Medkit","OwnerID":0,"Count":100}],
         "Data":[]}
        """;

    private static Surface Open()
    {
        const string root = @"C:\d47-test";
        var files = new MemoryFileSystem();
        var store = new GameStateStore();

        files.WriteText(
            Path.Combine(root, "Journal.2026-08-18T090000.01.log"),
            """
            {"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}
            {"timestamp":"2026-08-18T09:00:00Z","event":"SuitLoadout","SuitID":7,"SuitName":"utilitysuit_class3","LoadoutName":"Ground","SuitMods":[],"Modules":[]}

            """);
        files.WriteText(Path.Combine(root, SuitInventoryReader.BackpackFile), BackpackJson);
        files.WriteText(Path.Combine(root, SuitInventoryReader.ShipLockerFile), LockerJson);
        new JournalSpine(root, files, store, NullLoggerFactory.Instance).Poll();
        Assert.Equal(4, store.Active!.Suit.Backpack.Count);

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
        panel.EnableLoadout(ships, checklists, State, kit);

        var window = new Window { Content = panel, Width = 1280, Height = 1000 };
        window.Show();

        panel.Tab = PanelTab.Assets;
        Dispatcher.UIThread.RunJobs();
        Assert.True(panel.Nav.SelectRoot(LoadoutPages.GapRoot));
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, kit, store);
    }

    private static SuitInventory Carried(Surface surface) => surface.Store.Active!.Suit;

    private static MaterialsPage Materials(PanelView panel) =>
        panel.GetVisualDescendants().OfType<MaterialsPage>().Single();

    private static List<string> Text(Control page) =>
        [.. page.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    private static MaterialsPage Show(Surface surface, string view)
    {
        var page = Materials(surface.Panel);
        page.Select(view);
        Dispatcher.UIThread.RunJobs();
        return page;
    }

    private static OnFootDetailPage Drill(Surface surface, string view, string symbol)
    {
        Show(surface, view).Open(MaterialCatalogue.Find(symbol)!);
        Dispatcher.UIThread.RunJobs();
        return surface.Panel.GetVisualDescendants().OfType<OnFootDetailPage>().Single();
    }

    private static void PlanGradeFive(Surface surface)
    {
        var suit = surface.Kit.BuildFor(OnFootKind.Suit, 7, "Maverick Suit");
        surface.Kit.Plan(suit.Id, new KitPlan(OnFootBuild.GradeSlot, 5));
        surface.Panel.TickLoadout();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TheSidebarCountsTheResourcesInEach()
    {
        var surface = Open();
        var sidebar = Text(Materials(surface.Panel).GetVisualDescendants().OfType<Sidebar>().Single());
        var empty = new Dictionary<string, int>();

        Assert.Equal(3, MaterialsPage.OnFootGroups(Carried(surface).Backpack, empty).Sum(group => group.Tiles.Count));
        Assert.Equal(4, MaterialsPage.OnFootGroups(Carried(surface).ShipLocker, empty).Sum(group => group.Tiles.Count));
        Assert.Equal("3", sidebar[sidebar.IndexOf("BACKPACK") + 1]);
        Assert.Equal("4", sidebar[sidebar.IndexOf("SHIP LOCKER") + 1]);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheBackpackGroupsByKindAndShowsCountsWithNoCap()
    {
        var surface = Open();
        var shown = Text(Show(surface, MaterialsPage.BackpackView));

        Assert.Contains(MaterialsPage.BackpackHead, shown);
        Assert.Contains("1 HELD", shown);
        Assert.Contains("8 HELD", shown);
        Assert.Contains("4 HELD", shown);
        Assert.DoesNotContain(shown, line => line.Contains("1,000", StringComparison.Ordinal));
        Assert.True(
            shown.IndexOf("ITEMS") < shown.IndexOf("COMPONENTS") && shown.IndexOf("COMPONENTS") < shown.IndexOf("CONSUMABLES"),
            "Kinds run Items, Components, Data, Consumables.");
        Assert.DoesNotContain("DATA", shown);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheShipLockerHoldsEachKindAgainstItsCap()
    {
        var surface = Open();
        var shown = Text(Show(surface, MaterialsPage.LockerView));

        Assert.Contains(MaterialsPage.LockerHead, shown);
        Assert.Contains("4 / 1,000", shown);
        Assert.Contains("392 / 1,000", shown);
        Assert.Contains("100 HELD · CAP 100 EACH", shown);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void ALockerTileAPlanNeedsShowsHowMany()
    {
        var surface = Open();
        PlanGradeFive(surface);

        var needs = MaterialsPage.NeedsOf(MaterialTracker.Of(
            surface.Store.Active,
            PlanGap.Of([], surface.Kit.Store.Builds, surface.Store.Active, includeIntended: true)));
        var (symbol, needed) = needs.First(pair => Carried(surface).ShipLocker.Any(item => item.Name == pair.Key));

        var shown = Text(Show(surface, MaterialsPage.LockerView));

        Assert.Contains($"NEED {needed}", shown);
        Assert.Contains(MaterialCatalogue.Find(symbol)!.Name.ToUpperInvariant(), shown);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void ALockerTileDrillsToADetailPageUnderTheShipLockerCrumb()
    {
        var surface = Open();
        var detail = Drill(surface, MaterialsPage.LockerView, "suitschematic");

        Assert.Equal(MaterialsPage.OnFootPrefix + "suitschematic", surface.Panel.Nav.Trail[^1].Key);

        var shown = Text(detail);

        Assert.Contains("MATERIALS", shown);
        Assert.Contains("SHIP LOCKER", shown);
        Assert.Contains("Items", shown);
        Assert.Contains("1", shown);
        Assert.Contains("4 / 1,000", shown);
        var record = MicroResourceGuide.For("suitschematic", Carried(surface))!;
        Assert.Contains(OnFootDetailPage.Joined(record.Settlements), shown);
        Assert.Contains(OnFootDetailPage.Joined(record.Buildings), shown);
        Assert.Contains("WHERE IT'S FOUND", shown.Select(line => line.ToUpperInvariant()));

        detail.Back(MaterialsPage.LockerView);
        Dispatcher.UIThread.RunJobs();

        Assert.True(surface.Panel.Nav.AtRoot);
        Assert.Equal(MaterialsPage.LockerView, Materials(surface.Panel).GetVisualDescendants().OfType<Sidebar>().Single().Selected);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheDetailListsWhatPlansNeed()
    {
        var surface = Open();
        PlanGradeFive(surface);

        var shown = Text(Drill(surface, MaterialsPage.BackpackView, "suitschematic"));

        Assert.Contains("BACKPACK", shown);
        Assert.Contains(MaterialDetailPage.NeededHint, shown);
        Assert.Contains(OnFootDetailPage.NoBartender(MaterialCatalogue.Find("suitschematic")!), shown);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void ABartenderSwapIsPaidForWithWhatYouHold()
    {
        var surface = Open();
        var exchange = MicroResourceGuide.For("graphene", Carried(surface))!.Exchanges.Single();

        var shown = Text(Drill(surface, MaterialsPage.LockerView, "graphene"));

        Assert.Equal("carbonfibreplating", exchange.Offered.Symbol);
        Assert.Contains("SWAP", shown);
        Assert.Contains("Carbon Fibre Plating", shown);
        Assert.Contains(exchange.Give.ToString(System.Globalization.CultureInfo.InvariantCulture), shown);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void AnOnFootLineOnNeededByPlansOpensTheOnFootDetail()
    {
        var surface = Open();
        PlanGradeFive(surface);

        Materials(surface.Panel).Open(MaterialCatalogue.Find("suitschematic")!);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(surface.Panel.GetVisualDescendants().OfType<OnFootDetailPage>());
        Assert.Contains("ON FOOT", Text(surface.Panel.GetVisualDescendants().OfType<OnFootDetailPage>().Single()));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheOnFootPagesAreCaptured()
    {
        using var look = AppLook.Put();

        foreach (var (name, view, symbol) in new[]
                 {
                     ("materials-backpack", MaterialsPage.BackpackView, (string?)null),
                     ("materials-locker", MaterialsPage.LockerView, null),
                     ("materials-onfoot-detail", MaterialsPage.LockerView, "suitschematic"),
                     ("materials-onfoot-detail-bartender", MaterialsPage.LockerView, "graphene"),
                 })
        {
            var surface = Open();
            PlanGradeFive(surface);

            if (symbol is null)
            {
                Show(surface, view);
            }
            else
            {
                Drill(surface, view, symbol);
            }

            var path = $"{name}.png";

            using (var frame = surface.Window.CaptureRenderedFrame()!)
            {
                frame.SaveCapture(path);
            }

            surface.Window.Close();
        }
    }
}

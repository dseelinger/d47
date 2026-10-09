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

/// <summary>Fleet › Materials › one material: a page with a breadcrumb, not a dialog (#559).</summary>
[Trait("Category", "Integration")]
public class AMaterialOpensOnItsOwnPageTests
{
    private sealed class Surface(Window window, PanelView panel, ShipPlanService ships, GameStateStore store, Galaxy galaxy)
    {
        public Window Window { get; } = window;

        public PanelView Panel { get; } = panel;

        public ShipPlanService Ships { get; } = ships;

        public GameStateStore Store { get; } = store;

        public Galaxy Galaxy { get; } = galaxy;

        public bool SearchOn { get; set; }
    }

    private static Surface Open(bool searchOn = false)
    {
        var root = TempFolders.Create("d47-material-detail-tests");
        var store = new GameStateStore();

        Apply(store, """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""");
        Apply(store, """{"timestamp":"2026-08-18T09:00:00Z","event":"Location","StarSystem":"Sol","SystemAddress":10477373803,"StarPos":[0,0,0],"Docked":false}""");
        Apply(store, """{"timestamp":"2026-08-18T09:00:00Z","event":"SuitLoadout","SuitID":7,"SuitName":"utilitysuit_class3","LoadoutName":"Ground","SuitMods":[],"Modules":[]}""");
        Apply(store, """
            {"timestamp":"2026-08-18T09:00:00Z","event":"Materials",
             "Raw":[{"Name":"polonium","Count":38},{"Name":"mercury","Count":20}],
             "Manufactured":[{"Name":"guardian_powercell","Name_Localised":"Guardian Power Cell","Count":33}],
             "Encoded":[]}
            """.ReplaceLineEndings(string.Empty));

        CommanderGameState? State() => store.Active;

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(Path.Combine(root, "checklist-proposals.json"), new MemoryFileSystem(), NullLogger<ChecklistProposalStore>.Instance),
            State);
        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(root, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance), checklists, State);
        var kit = new OnFootPlanService(
            new OnFootBuildStore(Path.Combine(root, "on-foot.json"), NullLogger<OnFootBuildStore>.Instance), checklists, State);

        var galaxy = new Galaxy();
        Surface? surface = null;

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableLoadout(ships, checklists, State, kit, galaxy: () => surface!.SearchOn ? galaxy : null);

        var window = new Window { Content = panel, Width = 1280, Height = 1000 };
        surface = new Surface(window, panel, ships, store, galaxy) { SearchOn = searchOn };
        window.Show();

        panel.Tab = PanelTab.Assets;
        Dispatcher.UIThread.RunJobs();
        Assert.True(panel.Nav.SelectRoot(LoadoutPages.GapRoot));
        Dispatcher.UIThread.RunJobs();

        return surface;
    }

    private static void Apply(GameStateStore store, string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        store.Apply(parsed!);
    }

    private static MaterialsPage Materials(PanelView panel) =>
        panel.GetVisualDescendants().OfType<MaterialsPage>().Single();

    private static MaterialDetailPage? Detail(PanelView panel) =>
        panel.GetVisualDescendants().OfType<MaterialDetailPage>().SingleOrDefault();

    private static List<string> Text(Control page) =>
        [.. page.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    private static MaterialDetailPage Drill(Surface surface, string symbol)
    {
        Materials(surface.Panel).Open(MaterialCatalogue.Find(symbol)!);
        Dispatcher.UIThread.RunJobs();
        return Detail(surface.Panel)!;
    }

    /// <summary>Lets a search on the pool post its answer back.</summary>
    private static void Settle(Func<bool> done)
    {
        var until = DateTime.UtcNow.AddSeconds(10);

        while (!done() && DateTime.UtcNow < until)
        {
            Thread.Sleep(10);
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void PressingALedgerTileDrillsToAPageWithTheLedgerInItsBreadcrumb()
    {
        var surface = Open();
        Materials(surface.Panel).Select("raw");
        Dispatcher.UIThread.RunJobs();

        var detail = Drill(surface, "polonium");

        Assert.NotNull(detail);
        Assert.Equal(MaterialsPage.DetailPrefix + "polonium", surface.Panel.Nav.Trail[^1].Key);
        Assert.Empty(surface.Panel.GetVisualDescendants().OfType<MaterialsPage>());

        var shown = Text(detail);

        Assert.Contains("MATERIALS", shown);
        Assert.Contains("RAW", shown);
        Assert.Contains("POLONIUM", shown.Select(line => line.ToUpperInvariant()));
        Assert.Contains("Category 6", shown);
        Assert.Contains("G4", shown);
        Assert.Contains("38 / 150", shown);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheLedgerCrumbGoesBackToThatLedger()
    {
        var surface = Open();
        var detail = Drill(surface, "polonium");

        detail.Back("raw");
        Dispatcher.UIThread.RunJobs();

        Assert.True(surface.Panel.Nav.AtRoot);
        Assert.Equal("raw", Materials(surface.Panel).GetVisualDescendants().OfType<Sidebar>().Single().Selected);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void NeededByPlansAppearsOnlyWhenAPlanNeedsIt()
    {
        var surface = Open();

        Assert.DoesNotContain(MaterialDetailPage.NeededHint, Text(Drill(surface, "polonium")));

        surface.Panel.Nav.Back();
        var ship = surface.Ships.BuildFor(12, "anaconda", "Sacred Wings");
        surface.Ships.Plan(ship.Id, new SlotPlan("MainEngines") { Blueprint = "Dirty Drive Tuning", Grade = 5, Module = "Thrusters" });
        Dispatcher.UIThread.RunJobs();

        var gap = PlanGap.Of(surface.Ships.Store.Builds, [], surface.Store.Active, includeIntended: true);
        var needed = gap.Builds.Single().Lines.First(line => line.Material.Ledger == MaterialLedger.Material);

        var shown = Text(Drill(surface, needed.Material.Symbol));

        Assert.Contains(MaterialDetailPage.NeededHint, shown);
        Assert.Contains($"NEED {needed.Needed}", shown);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void WithGalaxySearchOffTheNearestSectionSaysHowToTurnItOn()
    {
        var surface = Open(searchOn: false);
        var shown = Text(Drill(surface, "polonium"));

        Assert.Contains(MaterialDetailPage.TurnItOn, shown);
        Assert.Equal(0, surface.Galaxy.Calls);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void WithGalaxySearchOnTheNearestPlacesAreListedNearestFirst()
    {
        var surface = Open(searchOn: true);
        var detail = Drill(surface, "polonium");

        Settle(() => Text(detail).Contains("FAR 1"));

        var shown = Text(detail);
        Assert.Equal(1, surface.Galaxy.Calls);
        Assert.True(shown.IndexOf("NEAR 2") < shown.IndexOf("FAR 1"), "The nearer body is listed first.");
        Assert.Contains("12.5 LY", shown);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TurningTheSettingOnWhileThePageIsOpenStartsTheSearch()
    {
        var surface = Open(searchOn: false);
        var detail = Drill(surface, "polonium");

        surface.SearchOn = true;
        surface.Panel.TickLoadout();
        Dispatcher.UIThread.RunJobs();

        Settle(() => Text(detail).Contains("FAR 1"));

        Assert.Equal(1, surface.Galaxy.Calls);
        Assert.DoesNotContain(MaterialDetailPage.TurnItOn, Text(detail));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void ChangingSystemSearchesAgain()
    {
        var surface = Open(searchOn: true);
        var detail = Drill(surface, "polonium");
        Settle(() => surface.Galaxy.Calls == 1 && Text(detail).Contains("FAR 1"));

        Apply(surface.Store, """{"timestamp":"2026-08-18T09:05:00Z","event":"FSDJump","StarSystem":"Alpha Centauri","SystemAddress":1,"StarPos":[3,0,0],"JumpDist":4.4}""");
        surface.Panel.TickLoadout();
        Dispatcher.UIThread.RunJobs();
        Settle(() => surface.Galaxy.Calls == 2 && Text(detail).Contains("FAR 1"));

        Assert.Equal(2, surface.Galaxy.Calls);
        Assert.Equal("Alpha Centauri", surface.Galaxy.LastFrom);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void AGuardianMaterialHasNoTraderSection()
    {
        var surface = Open();
        var shown = Text(Drill(surface, "guardian_powercell"));

        Assert.Contains(MaterialDetailPage.NoTrader("Guardian"), shown);
        Assert.DoesNotContain("AT A MATERIAL TRADER", shown.Select(line => line.ToUpperInvariant()));
        Assert.Contains("GUARDIAN", shown);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheTraderSectionTradesUpDownAndAcrossIntoTheMaterial()
    {
        var surface = Open();
        var shown = Text(Drill(surface, "mercury"));

        Assert.Contains("TRADE UP", shown);
        Assert.Contains("TRADE DOWN", shown);
        Assert.Contains("ACROSS CATEGORIES", shown);
        Assert.Contains("Arsenic", shown);
        Assert.Contains("Polonium", shown);

        surface.Panel.Nav.Back();
        Dispatcher.UIThread.RunJobs();

        var top = Text(Drill(surface, "polonium"));
        Assert.DoesNotContain("TRADE DOWN", top);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void TheDetailPagesAreCaptured()
    {
        using var look = AppLook.Put();

        foreach (var (name, symbol, on) in new[]
                 {
                     ("materials-detail", "polonium", true),
                     ("materials-detail-galaxy-off", "polonium", false),
                     ("materials-guardian-detail", "guardian_powercell", true),
                 })
        {
            var surface = Open(searchOn: on);
            Materials(surface.Panel).Select(symbol == "polonium" ? "raw" : "guardian");
            Dispatcher.UIThread.RunJobs();

            var detail = Drill(surface, symbol);

            if (on && MaterialGuide.Searchable(MaterialCatalogue.Find(symbol)!))
            {
                Settle(() => Text(detail).Contains("FAR 1"));
            }

            var path = $"{name}.png";

            using (var frame = surface.Window.CaptureRenderedFrame()!)
            {
                frame.SaveCapture(path);
            }

            surface.Window.Close();
        }
    }

    private sealed class Galaxy : IGalaxyService
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public string? LastFrom { get; private set; }

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            LastFrom = query.ReferenceSystem;

            BodySummary[] bodies =
            [
                new() { Name = "Far 1", SystemName = "Far", Distance = 1040.8, Materials = [("Polonium", 5.1)] },
                new() { Name = "Near 2", SystemName = "Near", Distance = 12.5, Materials = [("Polonium", 3.1)] },
            ];

            return Task.FromResult(new BodySearchResult(query.ReferenceSystem, bodies.Length, bodies));
        }

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}

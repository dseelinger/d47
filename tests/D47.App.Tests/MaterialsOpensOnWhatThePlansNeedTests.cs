using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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

/// <summary>Fleet › Materials opens on Needed by plans: one group per live build, from the gap (#557).</summary>
public class MaterialsOpensOnWhatThePlansNeedTests
{
    private sealed record Surface(Window Window, PanelView Panel, ShipPlanService Ships, OnFootPlanService Kit, Func<CommanderGameState> State, Action<string> Apply);

    private static Surface Open()
    {
        var root = TempFolders.Create("d47-materials-needed-tests");
        var store = new GameStateStore();

        void Apply(string line)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        Apply("""{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""");
        Apply("""{"timestamp":"2026-08-18T09:00:00Z","event":"SuitLoadout","SuitID":7,"SuitName":"utilitysuit_class3","LoadoutName":"Ground","SuitMods":[],"Modules":[]}""");
        Apply("""{"timestamp":"2026-08-18T09:00:00Z","event":"Materials","Raw":[],"Manufactured":[],"Encoded":[]}""");

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

        var window = new Window { Content = panel, Width = 1280, Height = 900 };
        window.Show();

        panel.Tab = PanelTab.Assets;
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, ships, kit, () => store.Active!, Apply);
    }

    private static IReadOnlyList<string> Text(Control page) =>
        [.. page.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    private static MaterialsPage Page(PanelView panel) =>
        panel.GetVisualDescendants().OfType<MaterialsPage>().Single();

    private static void Plan(Surface surface)
    {
        var ship = surface.Ships.BuildFor(12, "anaconda", "Sacred Wings");
        surface.Ships.Plan(ship.Id, new SlotPlan("MainEngines") { Blueprint = "Dirty Drive Tuning", Grade = 5, Module = "Thrusters" });

        var suit = surface.Kit.BuildFor(OnFootKind.Suit, 7, "Maverick Suit");
        surface.Kit.Plan(suit.Id, new KitPlan(OnFootBuild.GradeSlot, 5));
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void ThePageOpensOnNeededByPlansWithOneGroupPerBuild()
    {
        var surface = Open();
        Plan(surface);

        Assert.True(surface.Panel.Nav.SelectRoot(LoadoutPages.GapRoot));
        Dispatcher.UIThread.RunJobs();

        var page = Page(surface.Panel);
        var shown = Text(page);
        var report = PlanGap.Of(surface.Ships.Store.Builds, surface.Kit.Store.Builds, surface.State());

        Assert.Contains("PLANS", shown);
        Assert.Contains("NEEDED BY PLANS", shown);
        Assert.Contains(MaterialsPage.NeededHead, shown);
        Assert.Contains($"Say: “{MaterialsPage.Phrase}”", shown);

        var sidebar = page.GetVisualDescendants().OfType<Sidebar>().Single();
        Assert.Equal(MaterialsPage.NeededView, sidebar.Selected);

        var distinct = report.Builds.SelectMany(build => build.Lines).Select(line => line.Material.Symbol).Distinct().Count();
        Assert.Contains(distinct.ToString(System.Globalization.CultureInfo.InvariantCulture), shown);

        Assert.Contains("SACRED WINGS", shown);
        Assert.Contains(shown, line => line.StartsWith("Anaconda · 1 plan · ", StringComparison.Ordinal) && line.EndsWith(" SHORT", StringComparison.Ordinal));
        Assert.Contains("MAVERICK SUIT", shown);
        Assert.Contains(shown, line => line.StartsWith("Suit · grade 5 · ", StringComparison.Ordinal));

        Assert.Equal(2, shown.Count(line => line == "HELD / NEED"));
        Assert.Contains("ON FOOT", shown);
        Assert.Contains(shown, line => line is "RAW" or "MANUFACTURED" or "ENCODED");

        foreach (var line in report.Builds.SelectMany(build => build.Lines))
        {
            Assert.Contains(line.Material.Name, shown);
        }

        surface.Window.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void AMaterialHeldInFullReadsMet()
    {
        var surface = Open();
        Plan(surface);

        surface.Panel.Nav.SelectRoot(LoadoutPages.GapRoot);
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(MaterialsPage.Met, Text(Page(surface.Panel)));

        var report = PlanGap.Of(surface.Ships.Store.Builds, surface.Kit.Store.Builds, surface.State());
        var line = report.Builds[0].Lines.First(line => line.Material.Ledger == MaterialLedger.Material);

        surface.Apply(
            $$"""{"timestamp":"2026-08-18T09:01:00Z","event":"MaterialCollected","Category":"{{line.Material.Category}}","Name":"{{line.Material.Symbol}}","Count":{{line.Needed}}}""");

        surface.Panel.TickLoadout();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(MaterialsPage.Met, Text(Page(surface.Panel)));

        surface.Window.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void WithNoPlanThePageSaysSo()
    {
        var surface = Open();

        surface.Panel.Nav.SelectRoot(LoadoutPages.GapRoot);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(MaterialsPage.NoPlans, Text(Page(surface.Panel)));

        surface.Window.Close();
    }

    [Fact]
    public void TheGroupHeadNamesTheKindAndWhatIsShort()
    {
        var material = MaterialCatalogue.Find("iron")!;
        var lines = new[] { new GapBuildLine(material, 10, 2), new GapBuildLine(material, 4, 9) };

        Assert.Equal(
            "Anaconda · 2 plans · 1 SHORT",
            MaterialsPage.KindLine(new GapBuild(new GapBuildKey(GapBuildKind.Ship, "s"), "Sacred Wings", lines) { Hull = "Anaconda", Plans = 2 }));
        Assert.Equal(
            "Weapon · grade 5 · 1 SHORT",
            MaterialsPage.KindLine(new GapBuild(new GapBuildKey(GapBuildKind.Weapon, "w"), "Karma C-44", lines) { Grade = 5 }));
        Assert.Equal(
            "Suit · 1 SHORT",
            MaterialsPage.KindLine(new GapBuild(new GapBuildKey(GapBuildKind.Suit, "k"), "Dominator Suit", lines)));
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
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

/// <summary>Fleet › Materials › Raw to Thargoid: one group per grade, a tile per material held against its cap (#558).</summary>
public class EachLedgerHoldsItsGradesAgainstTheCapTests
{
    private sealed record Surface(Window Window, PanelView Panel, ShipPlanService Ships, OnFootPlanService Kit, Func<CommanderGameState> State);

    private static Surface Open()
    {
        var root = TestSurface.MemoryFolder("d47-materials-ledger-tests");
        var store = new GameStateStore();

        void Apply(string line)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        Apply("""{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""");
        Apply("""{"timestamp":"2026-08-18T09:00:00Z","event":"SuitLoadout","SuitID":7,"SuitName":"utilitysuit_class3","LoadoutName":"Ground","SuitMods":[],"Modules":[]}""");
        Apply("""
            {"timestamp":"2026-08-18T09:00:00Z","event":"Materials",
             "Raw":[{"Name":"iron","Count":300},{"Name":"carbon","Count":45},{"Name":"polonium","Count":38}],
             "Manufactured":[{"Name":"guardian_powercell","Name_Localised":"Guardian Power Cell","Count":33}],
             "Encoded":[{"Name":"ancientbiologicaldata","Name_Localised":"Pattern Alpha Obelisk Data","Count":150}]}
            """.ReplaceLineEndings(string.Empty));

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
        Assert.True(panel.Nav.SelectRoot(LoadoutPages.GapRoot));
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, ships, kit, () => store.Active!);
    }

    private static MaterialsPage Page(PanelView panel) =>
        panel.GetVisualDescendants().OfType<MaterialsPage>().Single();

    private static IReadOnlyList<TextBlock> Blocks(Control page) =>
        [.. page.GetVisualDescendants().OfType<TextBlock>()];

    private static IReadOnlyList<string> Text(Control page) => [.. Blocks(page).Select(block => block.Text ?? string.Empty)];

    private static void Show(Surface surface, string view)
    {
        Page(surface.Panel).Select(view);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TheSidebarListsTheFiveShipLedgersWithTheirCounts()
    {
        var surface = Open();
        var page = Page(surface.Panel);
        var shown = Text(page);

        Assert.Contains("SHIP MATERIALS", shown);

        var ledgers = MaterialTracker.Of(surface.State(), PlanGap.Of([], [], surface.State())).Ship;

        foreach (var card in ledgers)
        {
            Assert.Contains(card.Name.ToUpperInvariant(), shown);
        }

        Assert.Equal(["raw", "manufactured", "encoded", "guardian", "thargoid"], ledgers.Select(MaterialsPage.ViewOf));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void RawIsOneGroupPerGradeWithItsCapAndNoGuardianMaterial()
    {
        var surface = Open();
        Show(surface, "raw");

        var page = Page(surface.Panel);
        var shown = Text(page);

        Assert.Equal("raw", page.GetVisualDescendants().OfType<Sidebar>().Single().Selected);
        Assert.Contains("RAW", shown);
        Assert.Contains(MaterialsPage.LedgerHead, shown);

        for (var grade = 1; grade <= 4; grade++)
        {
            Assert.Contains($"GRADE {grade}", shown);
            Assert.Contains($"CAP {MaterialGrades.CapacityOfGrade(grade)}", shown);
        }

        Assert.Contains("IRON", shown);
        Assert.Contains("CARBON", shown);
        Assert.DoesNotContain("GUARDIAN POWER CELL", shown);
        Assert.DoesNotContain(shown, line => line.Contains('·', StringComparison.Ordinal) && line.StartsWith("RAW", StringComparison.Ordinal));

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void AMaterialAtItsCapIsYellowAndOneBelowIsNot()
    {
        using var look = AppLook.Put();

        var surface = Open();
        Show(surface, "raw");

        var blocks = Blocks(Page(surface.Panel));
        var yellow = ((ISolidColorBrush)Application.Current!.FindResource(ThemeManager.YellowKey)!).Color;

        var iron = blocks.Single(block => block.Text == "300");
        var carbon = blocks.Single(block => block.Text == "45");

        Assert.Equal(yellow, ((ISolidColorBrush)iron.Foreground!).Color);
        Assert.NotEqual(yellow, ((ISolidColorBrush)carbon.Foreground!).Color);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void GuardianIsGroupedByJournalCategoryThenGrade()
    {
        var surface = Open();
        Show(surface, "guardian");

        var shown = Text(Page(surface.Panel));

        Assert.Contains("MANUFACTURED · GRADE 1", shown);
        Assert.Contains("MANUFACTURED · GRADE 3", shown);
        Assert.Contains("ENCODED · GRADE 4", shown);
        Assert.Contains("GUARDIAN POWER CELL", shown);
        Assert.Contains("33", shown);
        Assert.DoesNotContain("IRON", shown);
        Assert.True(
            shown.ToList().IndexOf("MANUFACTURED · GRADE 3") < shown.ToList().IndexOf("ENCODED · GRADE 4"),
            "Manufactured groups come before Encoded ones.");

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void ATilePlansNeedCarriesTheTotalNeed()
    {
        var surface = Open();

        var ship = surface.Ships.BuildFor(12, "anaconda", "Sacred Wings");
        surface.Ships.Plan(ship.Id, new SlotPlan("MainEngines") { Blueprint = "Dirty Drive Tuning", Grade = 5, Module = "Thrusters" });
        Dispatcher.UIThread.RunJobs();

        var gap = PlanGap.Of(surface.Ships.Store.Builds, surface.Kit.Store.Builds, surface.State(), includeIntended: true);
        var row = MaterialTracker.Of(surface.State(), gap).Ship
            .SelectMany(card => card.Rows.Select(row => (card, row)))
            .First(pair => pair.row.Needed > 0);

        Show(surface, MaterialsPage.ViewOf(row.card));

        var shown = Text(Page(surface.Panel));

        Assert.Contains(row.row.Material.Name.ToUpperInvariant(), shown);
        Assert.Contains($"NEED {row.row.Needed}", shown);

        surface.Window.Close();
    }

    [Fact]
    public void EveryShipMaterialIsInExactlyOneLedger()
    {
        var ledgers = MaterialTracker.Of(null, PlanGap.Of([], [], null)).Ship;
        var symbols = ledgers.SelectMany(card => card.Rows).Select(row => row.Material.Symbol).ToList();

        Assert.Equal(symbols.Count, symbols.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(
            ledgers.Where(card => card.Name is "Raw" or "Manufactured" or "Encoded").SelectMany(card => card.Rows),
            row => Assert.NotNull(row.Material.Line));
        Assert.All(
            ledgers.Where(card => card.Name is "Guardian" or "Thargoid").SelectMany(card => card.Rows),
            row => Assert.Null(row.Material.Line));
    }

    [AvaloniaFact]
    public void TheRawAndGuardianLedgersAreCaptured()
    {
        using var look = AppLook.Put();

        var surface = Open();

        foreach (var view in new[] { "raw", "guardian" })
        {
            Show(surface, view);

            var path = $"materials-ledger-{view}.png";

            using (var frame = surface.Window.CaptureRenderedFrame()!)
            {
                frame.SaveCapture(path);
            }
        }

        surface.Window.Close();
    }
}

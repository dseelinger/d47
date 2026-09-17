using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Configuration;
using D47.Core.Engineers;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The "Add to checklist" control on an engineer's own page adds every unmet prerequisite line in one
/// press, and goes away once there is nothing left to add (#257).
/// </summary>
public class EngineerPrerequisitesCanBeAddedToTheChecklistTests
{
    private sealed record Surface(Window Window, PanelView Panel, ChecklistService Checklists);

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CommanderGameState State()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Location","StarSystem":"Sol","StarPos":[0.0,0.0,0.0],"Docked":true,"StationName":"Abraham Lincoln"}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Loadout","Ship":"python","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","MaxJumpRange":30.0,"Modules":[]}""",
                 })
        {
            store.Apply(Event(line));
        }

        return store.Active!;
    }

    private static Surface Open()
    {
        var root = TempFolders.Create("d47-engineer-prerequisites-tests");
        var state = State();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => state);

        var builds = new ShipBuildStore(Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance);
        var kit = new OnFootBuildStore(Path.Combine(root, "on-foot.json"), NullLogger<OnFootBuildStore>.Instance);
        var ships = new ShipPlanService(builds, checklists, () => state);
        var onFoot = new OnFootPlanService(kit, checklists, () => state);
        var unlocks = new EngineerPlanService(builds, kit, checklists, () => state);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableEngineers(unlocks, ships, () => state, onFoot, checklists: checklists);

        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();

        panel.Tab = PanelTab.Engineers;

        // Felicity Farseer needs no referral: an unmet exploration-rank invitation and an unmet Meta
        // Alloys tribute, the same pair the issue's own example uses.
        panel.Nav.Drill(EngineersPages.Crumb(EngineerDirectory.All.First(e => e.Name == "Felicity Farseer")));
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, checklists);
    }

    private static Button Press(PanelView panel, string label) =>
        panel.GetVisualDescendants().OfType<Button>()
            .First(button => button.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.Text is { } said && said.Contains(label, StringComparison.Ordinal)));

    [AvaloniaFact]
    public void PressingItAddsEveryUnmetLineOnce()
    {
        var surface = Open();

        Assert.Empty(surface.Checklists.Document.Items);

        Press(surface.Panel, "Add to checklist").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var items = surface.Checklists.Document.Items;

        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.Equal(ChecklistItemKind.Derived, item.Kind));
        Assert.All(items, item => Assert.Equal(ChecklistState.Open, item.State));
        Assert.Contains(items, item => item.Text == "Gain exploration rank Scout or higher.");
        Assert.Contains(items, item => item.Text == "Provide 1 unit of Meta Alloys.");

        // Pressed again, nothing already on the list is duplicated (#257).
        Press(surface.Panel, "Add to checklist").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, surface.Checklists.Document.Items.Count);

        surface.Window.Close();
    }
}

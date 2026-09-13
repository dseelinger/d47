using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The pinned-blueprint filter, on the page, from nowhere near the engineer (#113).</summary>
public class ThePinnedFilterShowsWorkFromAnywhereTests
{
    private const int LeiCheung = 300120;

    /// <summary>Docked at Jameson Memorial — nowhere Lei Cheung is based.</summary>
    private static CommanderGameState State()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-09-01T08:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-09-01T08:00:01Z","event":"Location","StarSystem":"Shinrarta Dezhra","Docked":true,"StationName":"Jameson Memorial"}""",
                     $$"""{"timestamp":"2026-09-01T08:00:02Z","event":"EngineerProgress","Engineers":[{"Engineer":"Lei Cheung","EngineerID":{{LeiCheung}},"Progress":"Unlocked","Rank":5}]}""",
                     """{"timestamp":"2026-09-01T08:00:03Z","event":"Loadout","Ship":"anaconda","ShipID":51,"ShipName":"Flamebrand","ShipIdent":"FB-01","Modules":[{"Slot":"TinyHardpoint5","Item":"hpt_shieldbooster_size0_class5","On":true,"Priority":0,"Health":1.0}]}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    private static ChecklistService Checklists(CommanderGameState state)
    {
        var paths = new AppPaths(TempFolders.Create("d47-pinned-filter-tests"));
        paths.EnsureCreated();

        var checklists = new ChecklistService(
            new ChecklistStore(
                Path.Combine(paths.Data, "checklist.json"),
                NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => state);

        checklists.AdoptPlan(
            ChecklistScope.Ship(51),
            ChecklistSource.EngineeringPlan,
            EngineeringPlan.Items(
                ChecklistScope.Ship(51),
                "anaconda",
                [new BuildRequest("TinyHardpoint5", "Heavy Duty", 3)]),
            ["TinyHardpoint5"]);

        return checklists;
    }

    private static IReadOnlyList<string> Drawn(ChecklistService checklists)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableChecklist(checklists);

        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();

        panel.Tab = PanelTab.Checklist;
        Dispatcher.UIThread.RunJobs();

        return [.. panel.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];
    }

    /// <summary>Nowhere near Lei Cheung, so the location-bound row is not even on offer.</summary>
    [AvaloniaFact]
    public void NowhereNearTheEngineerTheHereRowIsAbsent()
    {
        var checklists = Checklists(State());

        Assert.DoesNotContain(
            checklists.FilterAxes(),
            filter => filter.Key == ChecklistService.HereKey);
    }

    /// <summary>The whole point: a pin reaches the line from wherever the Commander is standing.</summary>
    [AvaloniaFact]
    public void APinnedLineShowsUpFromAnywhere()
    {
        var checklists = Checklists(State());
        checklists.Pin(LeiCheung, pinned: true);

        checklists.Choose(ChecklistService.PinnedKey);

        var drawn = Drawn(checklists);

        Assert.Contains(drawn, text => text.Contains("Heavy Duty", StringComparison.Ordinal));
    }

    /// <summary>And a line nobody has pinned anything for is still filtered out.</summary>
    [AvaloniaFact]
    public void ALineNothingIsPinnedForIsStillFilteredOut()
    {
        var checklists = Checklists(State());
        checklists.Pin(LeiCheung, pinned: true);
        checklists.AddNote(ChecklistScope.Universal, "buy limpets");

        checklists.Choose(ChecklistService.PinnedKey);

        var drawn = Drawn(checklists);

        Assert.DoesNotContain(drawn, text => text.Contains("buy limpets", StringComparison.Ordinal));
    }
}

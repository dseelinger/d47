using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The one-engineer checklist filter, drawn on the page (#265).</summary>
public class TheEngineerFilterFocusesOnOneEngineersUnlockTests
{
    private static ChecklistService Checklists()
    {
        var paths = new AppPaths(TempFolders.Create("d47-engineer-focus-filter-tests"));
        paths.EnsureCreated();

        var checklists = new ChecklistService(
            new ChecklistStore(
                Path.Combine(paths.Data, "checklist.json"),
                NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        // Felicity Farseer needs no referral, so pressing this adds only her own invitation and
        // tribute lines — nothing else to tell apart from the note added beside it.
        var farseer = EngineerDirectory.All.First(engineer => engineer.Name == "Felicity Farseer");
        checklists.AddPrerequisites(farseer);
        checklists.AddNote(ChecklistScope.Universal, "buy limpets");

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

    [AvaloniaFact]
    public void ChoosingTheEngineerKeepsOnlyHerLines()
    {
        var checklists = Checklists();
        var farseer = EngineerDirectory.All.First(engineer => engineer.Name == "Felicity Farseer");

        checklists.Choose(ChecklistService.EngineerFilterKey(farseer.Id));

        var drawn = Drawn(checklists);

        Assert.Contains(drawn, text => text.Contains("exploration rank", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(drawn, text => text.Contains("buy limpets", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void TheRowIsOfferedUnderItsOwnHeadingWordedWithHerName()
    {
        var checklists = Checklists();
        var farseer = EngineerDirectory.All.First(engineer => engineer.Name == "Felicity Farseer");

        var row = Assert.Single(
            checklists.FilterAxes(),
            filter => filter.Key == ChecklistService.EngineerFilterKey(farseer.Id));

        Assert.Equal("Felicity Farseer", row.Word);
        Assert.Equal("Unlocking an engineer", row.Heading);
    }
}

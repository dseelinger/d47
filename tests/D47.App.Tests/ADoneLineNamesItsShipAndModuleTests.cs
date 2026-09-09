using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
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

/// <summary>Three finished rolls reading Grade 5 Reinforced Shields on Slot01_Size7 over a caption saying ship
/// 51.</summary>
public class ADoneLineNamesItsShipAndModuleTests
{
    private const string Flamebrand =
        """
        { "timestamp":"2026-08-21T10:00:00Z", "event":"Loadout", "Ship":"anaconda",
          "Ship_Localised":"Anaconda", "ShipID":51, "ShipName":"Flamebrand", "ShipIdent":"FB-01",
          "Modules":[
            { "Slot":"Slot01_Size7", "Item":"int_shieldgenerator_size7_class5", "On":true,
              "Engineering":{"BlueprintName":"ShieldGenerator_Reinforced","Level":5,"Quality":1.0,
                             "Engineer":"Didi Vatermann","EngineerID":300000} } ]
        }
        """;

    private static CommanderGameState State()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-08-21T08:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                     Flamebrand,
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    private static ChecklistService Checklists(CommanderGameState? state)
    {
        var paths = new AppPaths(TempFolders.Create("d47-checklist-naming-tests"));
        paths.EnsureCreated();

        return new ChecklistService(
            new ChecklistStore(
                Path.Combine(paths.Data, "checklist.json"),
                NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => state);
    }

    private static (Window Window, PanelView Panel) Open(ChecklistService checklists)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableChecklist(checklists);

        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();

        panel.Tab = PanelTab.Checklist;
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    /// <summary>The roll from the screenshot, put on the list the way a promoted build puts it.</summary>
    private static void Roll(ChecklistService checklists)
    {
        checklists.AdoptPlan(
            ChecklistScope.Ship(51),
            ChecklistSource.EngineeringPlan,
            EngineeringPlan.Items(
                ChecklistScope.Ship(51),
                "anaconda",
                [new BuildRequest("Slot01_Size7", "Reinforced Shields", 5)]),
            ["Slot01_Size7"]);
    }

    private static IReadOnlyList<string> Drawn(Visual panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    [AvaloniaFact]
    public void TheLineNamesTheModuleAndTheCaptionNamesTheShip()
    {
        var checklists = Checklists(State());
        Roll(checklists);

        var (window, panel) = Open(checklists);
        var drawn = Drawn(panel);

        Assert.Contains(
            drawn, text => text.Contains("Grade 5 Reinforced Shields on 7A Shield Generator", StringComparison.Ordinal));

        Assert.Contains(drawn, text => text.StartsWith("Flamebrand (Anaconda)", StringComparison.Ordinal));

        // The two spellings that were reported, and neither of them is drawn any more.
        Assert.DoesNotContain(drawn, text => text.Contains("Slot01_Size7", StringComparison.Ordinal));
        Assert.DoesNotContain(drawn, text => text.StartsWith("ship 51", StringComparison.Ordinal));

        window.Close();
    }

    /// <summary>And it is findable by what is on it.</summary>
    [AvaloniaFact]
    public void TheShipAndTheModuleAreBothSearchable()
    {
        var checklists = Checklists(State());
        Roll(checklists);

        var (window, panel) = Open(checklists);
        var page = panel.GetVisualDescendants().OfType<ChecklistPage>().Single();

        foreach (var query in new[] { "Flamebrand", "Shield Generator", "Slot01_Size7" })
        {
            page.Filter(query);
            Dispatcher.UIThread.RunJobs();

            Assert.Contains(
                Drawn(page),
                text => text.Contains("7A Shield Generator", StringComparison.Ordinal));
        }

        window.Close();
    }

    /// <summary>The reported page, at the size the headset renders it, for a human to look at.</summary>
    [AvaloniaFact]
    public void TheNamedLineRendersToACapture()
    {
        var checklists = Checklists(State());
        Roll(checklists);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableChecklist(checklists);

        var window = new Window { Content = panel, Width = 1024, Height = 640 };
        window.Show();

        panel.Tab = PanelTab.Checklist;
        Dispatcher.UIThread.RunJobs();

        window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "checklist-named-line.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        window.Close();
    }
}

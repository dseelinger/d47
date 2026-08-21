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

/// <summary>
/// The drawn line, as it was reported on 2026-08-21: three finished rolls reading
/// <i>Grade 5 Reinforced Shields on Slot01_Size7</i> over a caption saying <i>ship 51</i>.
/// <para>
/// Both halves of that are d47's own keys. The <c>ShipID</c> is what makes a build follow a hull
/// through a swap and the journal's slot name is what makes two conversations about one slot the
/// same item — so neither is going anywhere, and neither is a thing a Commander calls the thing.
/// </para>
/// <para>
/// Under the <b>Done</b> heading the page is flat: no scope headings, one caption per line. So the
/// line and its caption together are the only place the ship can be said, which is why this is
/// tested on the surface rather than only on the wording.
/// </para>
/// </summary>
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

    /// <summary>
    /// And it is findable by what is on it. A search box that cannot match the name on the caption
    /// is a filter that disagrees with the page it filters.
    /// </summary>
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

    /// <summary>
    /// The reported page, at the size the headset renders it, for a human to look at. The line is
    /// longer than it was — a wrap that reads badly is the one failure the assertions above cannot
    /// see.
    /// </summary>
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

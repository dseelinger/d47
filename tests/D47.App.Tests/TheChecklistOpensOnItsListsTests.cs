using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Checklists;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Checklist opens on the Commander's lists, and drilling into one is the normal way to use it (#830).</summary>
[Trait("Category", "Integration")]
public class TheChecklistOpensOnItsListsTests
{
    private static CommanderGameState State()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-10-01T08:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                     """
                     { "timestamp":"2026-10-01T09:00:00Z", "event":"Loadout", "Ship":"anaconda",
                       "Ship_Localised":"Anaconda", "ShipID":7, "ShipName":"Big Iron", "ShipIdent":"BI-07",
                       "Modules":[] }
                     """,
                     """
                     { "timestamp":"2026-10-01T10:00:00Z", "event":"Loadout", "Ship":"krait_mkii",
                       "Ship_Localised":"Krait Mk II", "ShipID":12, "ShipName":"Gonzo", "ShipIdent":"GZ-12",
                       "Modules":[] }
                     """,
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    /// <summary>Six lists: notes, engineer unlocks, two ships and two systems, with done lines on three.</summary>
    private static ChecklistService Checklists()
    {
        var paths = new AppPaths(TempFolders.Create("d47-checklist-lists"));
        paths.EnsureCreated();

        var state = State();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => state);

        checklists.AddNote(ChecklistScope.Universal, "Buy limpets before the Colonia run");
        checklists.AddNote(ChecklistScope.Universal, "Sell the exploration data");
        checklists.AddNote(ChecklistScope.Ship(12), "Refit the shield boosters");
        checklists.AddNote(ChecklistScope.Ship(12), "Fit a fuel scoop");
        checklists.AddNote(ChecklistScope.Ship(7), "Buy a bigger cargo rack");
        checklists.AddNote(ChecklistScope.System("HIP 47126"), "Build a Coriolis starport at Vogel Relay");
        checklists.AddNote(ChecklistScope.System("Col 285 Sector KT-Q b5-4"), "Build an outpost at Tamm Outpost");

        checklists.List.Save(
        [
            checklists.Document with
            {
                Items =
                [
                    .. checklists.Document.Items,
                    Prerequisite(),
                ],
            },
        ]);

        Complete(checklists, "Sell the exploration data");
        Complete(checklists, "Fit a fuel scoop");
        Complete(checklists, "Buy a bigger cargo rack");

        return checklists;
    }

    private static ChecklistItem Prerequisite()
    {
        var intent = new ChecklistIntent(ChecklistIntentKind.EngineerPrerequisite, "Broo Tarquin") { Detail = "tribute" };

        return new ChecklistItem
        {
            Key = ChecklistKeys.For(intent),
            Scope = ChecklistScope.Universal,
            Kind = ChecklistItemKind.Derived,
            Source = ChecklistSource.EngineerPrerequisite,
            Text = "Provide 50 units of Fujin Tea to Broo Tarquin",
            Intent = intent,
            Provenance = ChecklistProvenance.Asserted,
        };
    }

    private static void Complete(ChecklistService checklists, string text) =>
        checklists.Complete(checklists.Document.Items.Single(item => item.Text == text).Id);

    private static (Window Window, PanelView Panel) Open(ChecklistService checklists, double width = 1280, double height = 900)
    {
        var panel = new PanelView
        {
            DataContext = new PanelViewModel(),
            Mode = height < 400 ? PanelMode.Mini : PanelMode.Full,
        };

        panel.EnableChecklist(checklists);

        var window = new Window { Content = panel, Width = width, Height = height };
        window.Show();

        panel.Tab = PanelTab.Commander;
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    private static IReadOnlyList<string> Words(PanelView panel)
    {
        Dispatcher.UIThread.RunJobs();

        return
        [
            .. panel.GetVisualDescendants().OfType<TextBlock>()
                .Where(block => block.IsEffectivelyVisible)
                .Select(block => block.Text ?? string.Empty)
                .Where(text => text.Length > 0),
        ];
    }

    /// <summary>A list row by the start of its accessible name.</summary>
    private static Button Row(PanelView panel, string name) =>
        panel.GetVisualDescendants().OfType<Button>()
            .Single(button => button.IsEffectivelyVisible
                              && AutomationProperties.GetName(button)?.StartsWith(name + ",", StringComparison.Ordinal) == true);

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static bool Shown(PanelView panel, string accessibleName) =>
        panel.GetVisualDescendants().OfType<Control>()
            .Any(control => control.IsEffectivelyVisible && AutomationProperties.GetName(control) == accessibleName);

    private static CheckBox? HereOnly(PanelView panel) =>
        panel.GetVisualDescendants().OfType<CheckBox>()
            .SingleOrDefault(box => box.IsEffectivelyVisible && box.Content as string == "Only what engineers here do");

    [AvaloniaFact]
    public void ThePageOpensOnTheListOfLists()
    {
        var (window, panel) = Open(Checklists());

        var words = Words(panel);

        Assert.Contains("YOURS", words);
        Assert.Contains("SHIPS", words);
        Assert.Contains("SYSTEMS", words);
        Assert.Contains("NOTHING OPEN", words);

        Assert.Contains("GONZO", words);
        Assert.Contains("ENGINEER UNLOCKS", words);

        // Big Iron's one line is done, so it sits under Nothing open.
        Assert.Contains("✓ 1 done", words);

        var all = panel.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "ChecklistAllLists");

        Assert.Equal("All lists, 5 open, 3 done", AutomationProperties.GetName(all));
        Assert.True(Row(panel, "Gonzo").Bounds.Height >= TypeScale.MinimumTarget);

        // No line is drawn at this level.
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<CheckBox>(), box => box.Content as string == "completed");

        window.Close();
    }

    [AvaloniaFact]
    public void ChoosingAShipShowsItsLinesOnlyAndBackReturns()
    {
        var (window, panel) = Open(Checklists());

        Assert.Equal("Gonzo, 1 open, 1 done", AutomationProperties.GetName(Row(panel, "Gonzo")));

        Press(Row(panel, "Gonzo"));

        Assert.Equal(["Checklist", "Gonzo"], panel.Nav.Trail.Select(crumb => crumb.Word));

        var words = Words(panel);

        Assert.Contains("Refit the shield boosters", words);
        Assert.Contains("Fit a fuel scoop", words);
        Assert.Contains("DONE (1)", words);
        Assert.Contains("KRAIT MK II · FLYING NOW", words);

        Assert.DoesNotContain("Buy limpets before the Colonia run", words);
        Assert.DoesNotContain("Buy a bigger cargo rack", words);

        Assert.True(panel.Nav.Back());
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Checklist"], panel.Nav.Trail.Select(crumb => crumb.Word));
        Assert.Contains("SHIPS", Words(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void EachListOffersOnlyTheControlsThatFitIt()
    {
        var (window, panel) = Open(Checklists());

        Press(Row(panel, "Gonzo"));

        Assert.NotNull(HereOnly(panel));
        Assert.True(Shown(panel, "Add a line"));

        panel.Nav.Back();
        Press(Row(panel, "Your notes"));

        Assert.Null(HereOnly(panel));
        Assert.True(Shown(panel, "Add a line"));

        panel.Nav.Back();
        Press(Row(panel, "Engineer unlocks"));

        Assert.False(Shown(panel, "Add a line"));

        panel.Nav.Back();
        Press(panel.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "ChecklistAllLists"));

        Assert.False(Shown(panel, "Add a line"));
        Assert.Null(HereOnly(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void DeleteCompletedOnOneListLeavesTheOthers()
    {
        var checklists = Checklists();
        var (window, panel) = Open(checklists);

        Press(Row(panel, "Gonzo"));

        var delete = panel.GetVisualDescendants().OfType<Button>()
            .Single(button => button.IsEffectivelyVisible && button.Content as string == "Delete completed");

        Press(delete);

        // The question names the list, and says the others keep theirs.
        Assert.Contains(Words(panel), text => text.Contains("would come off Gonzo", StringComparison.Ordinal));

        Press(panel.GetVisualDescendants().OfType<Button>()
            .First(button => button.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Delete them")));

        var done = checklists.Document.Items.Where(item => item.IsComplete).Select(item => item.Text).ToList();

        Assert.DoesNotContain("Fit a fuel scoop", done);
        Assert.Contains("Sell the exploration data", done);
        Assert.Contains("Buy a bigger cargo rack", done);

        window.Close();
    }

    [AvaloniaFact]
    public void AllListsDrawsEveryLineWithItsListsNameAndKeepsTheFilter()
    {
        var (window, panel) = Open(Checklists());

        Press(panel.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "ChecklistAllLists"));

        Assert.Equal(["Checklist", "All lists"], panel.Nav.Trail.Select(crumb => crumb.Word));

        var words = Words(panel);

        foreach (var text in new[]
                 {
                     "Buy limpets before the Colonia run", "Refit the shield boosters", "Buy a bigger cargo rack",
                     "Build a Coriolis starport at Vogel Relay", "Provide 50 units of Fujin Tea to Broo Tarquin",
                 })
        {
            Assert.Contains(text, words);
        }

        foreach (var lead in new[] { "YOUR NOTES", "GONZO", "BIG IRON", "HIP 47126", "ENGINEER UNLOCKS" })
        {
            Assert.Contains(lead, words);
        }

        Assert.Contains(
            panel.GetVisualDescendants().OfType<Stepper>(),
            stepper => stepper.IsEffectivelyVisible && AutomationProperties.GetName(stepper) == "Checklist scope");

        window.Close();
    }

    [AvaloniaFact]
    public void MiniShowsFourListsAndCountsTheRest()
    {
        var (window, panel) = Open(Checklists(), 792, 280);

        Dispatcher.UIThread.RunJobs();

        var rows = panel.GetVisualDescendants().OfType<Border>()
            .Where(border => border.Classes.Contains(ListRow.Class) && AutomationProperties.GetName(border) is not null)
            .ToList();

        Assert.Equal(4, rows.Count);

        var more = panel.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "ChecklistMoreLists");

        Assert.Equal("2 more lists", more.Text);

        window.Close();
    }

    /// <summary>The three window boards and the mini panel, for comparison with the Checklist Lists canvas.</summary>
    [AvaloniaFact]
    public void TheBoardsAreCaptured()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var (window, panel) = Open(Checklists());

        var lists = Save(window, "checklist-830-lists.png");

        Press(Row(panel, "Gonzo"));
        var one = Save(window, "checklist-830-one.png");

        panel.Nav.Back();
        Press(panel.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "ChecklistAllLists"));
        var all = Save(window, "checklist-830-all.png");

        window.Close();

        var (miniWindow, _) = Open(Checklists(), 792, 280);
        var mini = Save(miniWindow, "checklist-830-mini.png");

        miniWindow.Close();
    }

    private static string Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();

        var path = name;

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture(path);
        }

        return path;
    }
}

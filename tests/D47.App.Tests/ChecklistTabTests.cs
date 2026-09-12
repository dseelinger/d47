using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core;
using D47.Core.Checklists;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The one surface, now a tab of the panel rather than a window over it.</summary>
public class ChecklistTabTests
{
    private static ChecklistService Checklists(string root) => Checklists(root, () => null);

    private static ChecklistService Checklists(
        string root, Func<D47.Core.Journal.CommanderGameState?> state)
    {
        var paths = new AppPaths(root);
        paths.EnsureCreated();

        return new ChecklistService(
            new ChecklistStore(
                Path.Combine(paths.Data, "checklist.json"),
                NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            state);
    }

    /// <summary>The panel on the Checklist tab, shown, which is what makes its controls measurable.</summary>
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

    /// <summary>The movers on screen, by the name a screen reader reads.</summary>
    private static IReadOnlyList<Button> Movers(PanelView panel, string name) =>
        [.. panel.GetVisualDescendants().OfType<Button>()
            .Where(button => AutomationProperties.GetName(button) == name)];

    private static ChecklistItem Derived(bool done = false)
    {
        var intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, "MainEngines")
        {
            Detail = "Engine_Dirty",
            Grade = 5,
        };

        return new ChecklistItem
        {
            Key = ChecklistKeys.For(intent),
            Scope = ChecklistScope.Ship(12),
            Kind = ChecklistItemKind.Derived,
            Source = ChecklistSource.EngineeringPlan,
            Text = "Grade 5 dirty drives",
            Intent = intent,
            State = done ? ChecklistState.Done : ChecklistState.Open,
        };
    }

    /// <summary>
    /// The tab is on both surfaces, unlike Settings — and that asymmetry is deliberate rather than
    /// an oversight.
    /// </summary>
    [AvaloniaFact]
    public void TheTabIsThereOnceTheHostGivesIt()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));
        var panel = new PanelView { DataContext = new PanelViewModel() };

        Assert.False(panel.FindControl<Control>("ChecklistTab")!.IsVisible);

        panel.EnableChecklist(checklists);

        Assert.True(panel.FindControl<Control>("ChecklistTab")!.IsVisible);
    }

    /// <summary>And the headset's own instantiation has it too.</summary>
    [AvaloniaFact]
    public void TheHeadsetCopyHasTheChecklist()
    {
        var (settings, _, _) = TestSurface.Create();
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));

        using var surface = new Headset.VrPanelSurface(
            new PanelViewModel(),
            settings,
            _ => null,
            checklists: checklists);

        var view = (PanelView)surface.GetType()
            .GetField("_view", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(surface)!;

        Assert.True(view.FindControl<Control>("ChecklistTab")!.IsVisible);
    }

    /// <summary>
    /// The window keeps both, which is the half of "one widget tree renders to both surfaces" that
    /// never moved: the difference between the surfaces is which host calls which <c>Enable</c>, and
    /// nothing in the view knows a headset from a window.
    /// </summary>
    [AvaloniaFact]
    public void TheWindowKeepsBoth()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));
        var (window, panel) = Open(checklists);

        Assert.True(panel.FindControl<Control>("ChecklistTab")!.IsVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void ADerivedItemHasNoCheckboxAtAll()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        checklists.List.Save(
        [
            checklists.Document with { Items = [.. checklists.Document.Items, Derived()] },
        ]);

        var (window, panel) = Open(checklists);

        var ticks = Ticks.On(panel);

        // One authored item and one derived one, and exactly one checkbox.
        Assert.Single(ticks);
        Assert.Equal("buy limpets", ticks[0].Content);

        window.Close();
    }

    [AvaloniaFact]
    public void FinishedItemsSitBelowTheLineWithTheirCountShowing()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        checklists.Complete(checklists.Document.Items[0].Id);
        checklists.AddNote(ChecklistScope.Universal, "fit a fuel scoop");

        var (window, panel) = Open(checklists);

        var text = panel.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .ToList();

        // Kept, counted, and out of the way.
        Assert.Contains("Done (1)", text);

        window.Close();
    }

    /// <summary>One list: scope rides each line, and the page is not carved into one list per ship.</summary>
    [AvaloniaFact]
    public void ScopeIsALabelOnTheLineRatherThanAHeadingOverAGroup()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        checklists.AddNote(ChecklistScope.Ship(12), "fit a fuel scoop");
        checklists.AddNote(ChecklistScope.Universal, "sell the cargo");

        var (window, panel) = Open(checklists);

        var lines = Ticks.Words(panel);

 // Grouped by project in first-appearance order, no headings between them, and nothing
        // lost.
        Assert.Equal(["buy limpets", "sell the cargo", "fit a fuel scoop"], lines);

        window.Close();
    }

    [AvaloniaFact]
    public void TheSelectedLineGrowsMoversAndMovingReordersTheList()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        checklists.AddNote(ChecklistScope.Universal, "fit a fuel scoop");

        // Adding selects, and this test is about a press selecting.
        checklists.Select(null);

        var (window, panel) = Open(checklists);

        // Nothing is selected, so no line is carrying movers.
        Assert.Empty(Movers(panel, "Move up"));

        // The innermost border that holds the line, which is the card.
        var second = panel.GetVisualDescendants().OfType<Border>()
            .Last(border => border.GetVisualDescendants().OfType<CheckBox>()
                .Any(tick => tick.Content as string == "fit a fuel scoop"));

        second.RaiseEvent(new Avalonia.Input.PointerPressedEventArgs(
            second,
            new Avalonia.Input.Pointer(0, Avalonia.Input.PointerType.Mouse, true),
            second,
            default,
            0,
            new Avalonia.Input.PointerPointProperties(
                Avalonia.Input.RawInputModifiers.LeftMouseButton,
                Avalonia.Input.PointerUpdateKind.LeftButtonPressed),
            Avalonia.Input.KeyModifiers.None));

        Dispatcher.UIThread.RunJobs();

        Assert.Single(Movers(panel, "Move up")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            ["fit a fuel scoop", "buy limpets"],
            checklists.Document.Items.Select(item => item.Text));

        window.Close();
    }

    /// <summary>
    /// Suggestions are a page rather than an interruption: they wait in one place, reached by drilling,
    /// and the list itself is not interleaved with them.
    /// </summary>
    [AvaloniaFact]
    public void SuggestionsWaitOnTheirOwnPage()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));

        checklists.ProposeAdd(ChecklistScope.Universal, ["buy limpets"]);

        var (window, panel) = Open(checklists);

        var open = panel.GetVisualDescendants().OfType<Button>()
            .Single(button => (button.Content as string)?.StartsWith("Suggestions", StringComparison.Ordinal) == true);

        Assert.Equal("Suggestions (1)", open.Content);

        // Not on the list itself.
        Assert.DoesNotContain(
            panel.GetVisualDescendants().OfType<Button>(),
            button => button.Content as string == "Accept");

        open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        // A level of the stack, so the breadcrumb says where the Commander is and offers the way back — which
        // is the thing a headset has no title bar for.
        Assert.Equal(["Checklist", "Suggestions"], panel.Nav.Trail.Select(crumb => crumb.Word));

        var buttons = panel.GetVisualDescendants().OfType<Button>()
            .Select(button => button.Content as string ?? string.Empty)
            .ToList();

        Assert.Contains("Accept", buttons);
        Assert.Contains("Decline", buttons);

        // Nothing has moved yet — the page is where the Commander decides, and the AI cannot.
        Assert.Empty(checklists.Document.Items);

        window.Close();
    }

    [AvaloniaFact]
    public void AcceptingFromThePageCommitsIt()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));

        checklists.ProposeAdd(ChecklistScope.Universal, ["buy limpets"]);

        var (window, panel) = Open(checklists);

        panel.Nav.Drill(new NavCrumb(ChecklistPage.SuggestionsKey, "Suggestions"));
        Dispatcher.UIThread.RunJobs();

        var accept = panel.GetVisualDescendants().OfType<Button>()
            .First(button => button.Content as string == "Accept");

        accept.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("buy limpets", checklists.Document.Items.Single().Text);
        Assert.Empty(checklists.Proposals.Pending);

        window.Close();
    }

    [AvaloniaFact]
    public void ThePageFollowsAChangeMadeFromSomewhereElse()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));

        var (window, panel) = Open(checklists);

        // A voice command, or a text editor.
        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(
            panel.GetVisualDescendants().OfType<CheckBox>(),
            tick => tick.Content as string == "buy limpets");

        window.Close();
    }

    /// <summary>Four movers on the selected line, not two.</summary>
    [AvaloniaFact]
    public void TheSelectedLineCarriesBothStepsAndBothEnds()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        checklists.AddNote(ChecklistScope.Universal, "sell the cargo");
        checklists.AddNote(ChecklistScope.Universal, "fit a fuel scoop");

        // The last line added is the selected one and what makes this test need no press of its own.
        var (window, panel) = Open(checklists);

        foreach (var name in new[] { "Move to the top", "Move up", "Move down", "Move to the bottom" })
        {
            Assert.Single(Movers(panel, name));
        }

        Assert.Single(Movers(panel, "Move to the top")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            ["fit a fuel scoop", "buy limpets", "sell the cargo"],
            checklists.Document.Items.Select(item => item.Text));

        window.Close();
    }

    /// <summary>The page at the size the headset renders it, for a human to look at.</summary>
    [AvaloniaFact]
    public void TheChecklistTabRendersToACapture()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        checklists.AddNote(ChecklistScope.Ship(12), "fit a fuel scoop");
        checklists.ProposeAdd(ChecklistScope.Universal, ["grind for grade 5 dirty drives"]);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableChecklist(checklists);

        var window = new Window { Content = panel, Width = 1024, Height = 640 };
        window.Show();

        panel.Tab = PanelTab.Checklist;
        Dispatcher.UIThread.RunJobs();

        window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "checklist-tab.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        window.Close();
    }

    /// <summary>Every mover glyph is actually painted.</summary>
    [AvaloniaFact]
    public void NoMoverGlyphIsBlank()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");

        var (window, panel) = Open(checklists);

        var glyphs = panel.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => AutomationProperties.GetName(button)?.StartsWith("Move", StringComparison.Ordinal) == true)
            .Select(button => button.Content)
            .OfType<Avalonia.Controls.Shapes.Path>()
            .ToList();

        Assert.Equal(4, glyphs.Count);

        foreach (var glyph in glyphs)
        {
            Assert.NotNull(glyph.Fill);
            Assert.NotNull(glyph.Data);
            Assert.True(glyph.Data!.Bounds.Width > 0, "the glyph has no geometry to draw");
        }

        window.Close();
    }

    /// <summary>The four movers, drawn, for a human to look at.</summary>
    [AvaloniaFact]
    public void TheMoversRenderToACapture()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        checklists.AddNote(ChecklistScope.Universal, "refill manufactured materials");

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableChecklist(checklists);

        var window = new Window { Content = panel, Width = 1024, Height = 400 };
        window.Show();

        panel.Tab = PanelTab.Checklist;
        Dispatcher.UIThread.RunJobs();

        window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "checklist-movers.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        window.Close();
    }

    /// <summary> The engineer filter, driven through the page rather than through the join under it. </summary>
    [AvaloniaFact]
    public void TheEngineerFilterDrawsOnlyWorkThatEngineerCanActuallyDo()
    {
        var state = InLaksakWithAShieldBooster();
        var checklists = Checklists(TempFolders.Create("d47-engineer-filter"), () => state);

        checklists.List.Save(
        [
            checklists.Document with
            {
                Items =
                [
                    Roll("TinyHardpoint1", grade: 3),
                    Roll("TinyHardpoint2", grade: 5),
                ],
            },
        ]);

        var (window, panel) = Open(checklists);

        var drawn = Lines(panel);
        Assert.Contains(drawn, line => line.Contains("Grade 3", StringComparison.Ordinal));
        Assert.Contains(drawn, line => line.Contains("Grade 5", StringComparison.Ordinal));

        // Take the filter the way the Commander does: press the chooser, press the option.
        var word = checklists.FilterAxes()
            .Single(filter => filter.Key == ChecklistService.HereKey).Word;

        Press(panel, content => content.StartsWith("Showing", StringComparison.Ordinal));
        Press(panel, content => content == word);

        drawn = Lines(panel);

        Assert.Contains(drawn, line => line.Contains("Grade 3", StringComparison.Ordinal));
        Assert.DoesNotContain(drawn, line => line.Contains("Grade 5", StringComparison.Ordinal));

        window.Close();
    }

    /// <summary>Presses the first button whose text content matches, then lets the UI settle.</summary>
    private static void Press(PanelView panel, Func<string, bool> matching)
    {
        // Matched on the text a Commander can see, not on Content: a chooser's rows are Buttons wrapping a
        // StackPanel, so only the plain toolbar buttons carry a string.
        var button = panel.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b =>
                (b.Content is string text && matching(text))
                || b.GetVisualDescendants().OfType<TextBlock>()
                    .Any(block => block.Text is { Length: > 0 } shown && matching(shown)));

        Assert.NotNull(button);
        button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Every line of text the checklist page is drawing right now.</summary>
    private static IReadOnlyList<string> Lines(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .Where(text => text.Length > 0)];

    /// <summary>A rank gate says why each line is blocked, and teaches no lesson about how rank works.</summary>
    [AvaloniaFact]
    public void ARankGateNamesTheBlockAndTeachesNothing()
    {
        var state = InLaksakWithAShieldBooster(rank: 1);
        var checklists = Checklists(TempFolders.Create("d47-one-explanation"), () => state);

        checklists.List.Save(
        [
            checklists.Document with
            {
                Items = [Gated("TinyHardpoint1"), Gated("TinyHardpoint2")],
            },
        ]);

        var (window, panel) = Open(checklists);

        var drawn = Lines(panel);

        // Each line keeps its own verdict.
        Assert.Equal(
            2,
            drawn.Count(line => line.Contains("cannot be crafted at rank 1", StringComparison.Ordinal)));

        Assert.DoesNotContain(drawn, line => line.Contains("compounds", StringComparison.Ordinal));

        window.Close();
    }

    /// <summary>
    /// A gated roll: grade 3 wanted, the engineer named so the rank can be looked up at all, and the
    /// Commander at rank 1 — which is no route rather than a slow one.
    /// </summary>
    private static ChecklistItem Gated(string slot)
    {
        var intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, slot)
        {
            Detail = "Heavy Duty",
            Grade = 3,
            Engineer = "Lei Cheung",
        };

        return new ChecklistItem
        {
            Key = ChecklistKeys.For(intent),
            Scope = ChecklistScope.Ship(51),
            Kind = ChecklistItemKind.Derived,
            Source = ChecklistSource.EngineeringPlan,
            Text = $"Grade 3 Heavy Duty on {slot}",
            Intent = intent,
        };
    }

    private static ChecklistItem Roll(string slot, int grade)
    {
        var intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, slot)
        {
            Detail = "Heavy Duty",
            Grade = grade,
        };

        return new ChecklistItem
        {
            Key = ChecklistKeys.For(intent),
            Scope = ChecklistScope.Ship(51),
            Kind = ChecklistItemKind.Derived,
            Source = ChecklistSource.EngineeringPlan,
            Text = $"Grade {grade} Heavy Duty on {slot}",
            Intent = intent,
        };
    }

    /// <summary>In Lei Cheung's system, in a ship with a shield booster in both utility slots.</summary>
    /// <param name="rank">The Commander's standing with Lei Cheung.</param>
    private static D47.Core.Journal.CommanderGameState InLaksakWithAShieldBooster(int rank = 5)
    {
        var store = new D47.Core.Journal.GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-23T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-23T09:00:01Z","event":"Location","StarSystem":"Laksak","Docked":true,"StationName":"Trader's Rest"}""",
                     """{"timestamp":"2026-08-23T09:00:02Z","event":"EngineerProgress","Engineers":[{"Engineer":"Lei Cheung","EngineerID":300120,"Progress":"Unlocked","Rank":""" + rank.ToString(CultureInfo.InvariantCulture) + """}]}""",
                     """{"timestamp":"2026-08-23T09:00:03Z","event":"Loadout","Ship":"anaconda","ShipID":51,"ShipName":"Flamebrand","ShipIdent":"FB-01","Modules":[{"Slot":"TinyHardpoint1","Item":"hpt_shieldbooster_size0_class5","On":true,"Priority":0,"Health":1.0},{"Slot":"TinyHardpoint2","Item":"hpt_shieldbooster_size0_class5","On":true,"Priority":0,"Health":1.0}]}""",
                 })
        {
            Assert.True(D47.Core.Journal.JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    /// <summary>Include Partial Grades.</summary>
    [AvaloniaFact]
    public void PartialGradesAreOfferedBesideTheEngineerFilterAndSayHowFarTheyGo()
    {
        var state = InLaksakWithAShieldBooster();
        var checklists = Checklists(TempFolders.Create("d47-partial-grades"), () => state);

        checklists.List.Save(
        [
            checklists.Document with
            {
                Items = [Roll("TinyHardpoint1", grade: 3), Roll("TinyHardpoint2", grade: 5)],
            },
        ]);

        var (window, panel) = Open(checklists);

        var word = checklists.FilterAxes()
            .Single(filter => filter.Key == ChecklistService.HereKey).Word;

        Press(panel, content => content.StartsWith("Showing", StringComparison.Ordinal));
        Press(panel, content => content == word);

        // Unchecked stays exactly what shipped.
        Assert.DoesNotContain(Lines(panel), line => line.Contains("Grade 5", StringComparison.Ordinal));

        var box = panel.GetVisualDescendants().OfType<CheckBox>()
            .Single(check => check.Content?.ToString() == "Include Partial Grades");

        box.IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        var drawn = Lines(panel);

        Assert.Contains(drawn, line => line.Contains("Grade 5", StringComparison.Ordinal));
        Assert.Contains(drawn, line => line.Contains("Lei Cheung takes this to 3 of 5", StringComparison.Ordinal));

        window.Close();
    }

    /// <summary>
    /// The control is absent rather than hidden anywhere it means nothing — the phrase says nothing
    /// about a list filtered by ship, and the bar is already crowded.
    /// </summary>
    [AvaloniaFact]
    public void TheCheckboxIsNotThereWhenTheEngineerFilterIsNot()
    {
        var state = InLaksakWithAShieldBooster();
        var checklists = Checklists(TempFolders.Create("d47-partial-grades"), () => state);

        checklists.List.Save(
        [
            checklists.Document with { Items = [Roll("TinyHardpoint2", grade: 5)] },
        ]);

        var (window, panel) = Open(checklists);

        Assert.DoesNotContain(
            panel.GetVisualDescendants().OfType<CheckBox>(),
            check => check.Content?.ToString() == "Include Partial Grades");

        window.Close();
    }
}

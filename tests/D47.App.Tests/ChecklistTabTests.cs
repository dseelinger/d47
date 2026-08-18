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

/// <summary>
/// The one surface (list.md Phase 17), now a tab of the panel rather than a window over it
/// (list.md Phase 25, "The checklist leaves its window").
/// <para>
/// <b>The headline is not tidiness.</b> A <c>Window</c> cannot appear in the headset, so until
/// this moved a Commander in VR could not see their checklist at all — which is why these tests
/// build the panel rather than a dialog, and why the headset's copy gets the tab too.
/// </para>
/// <para>
/// The distinction the whole phase turns on is unchanged: some ticks are computed and some are
/// opinions, and a Commander must be able to tell which at a glance.
/// </para>
/// </summary>
public class ChecklistTabTests
{
    private static ChecklistService Checklists(string root)
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
            () => null);
    }

    /// <summary>
    /// The panel on the Checklist tab, shown, which is what makes its controls measurable.
    /// </summary>
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
    /// The tab is on both surfaces, unlike Settings — and that asymmetry is the item's whole
    /// point rather than an oversight.
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

    /// <summary>And the headset's own instantiation has it, which is what a Window could not do.</summary>
    [AvaloniaFact]
    public void TheHeadsetCopyHasItToo()
    {
        var (settings, _, _) = TestSurface.Create();
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));

        using var surface = new Headset.VrPanelSurface(
            new PanelViewModel(), settings, _ => null, checklists: checklists);

        var view = (PanelView)surface.GetType()
            .GetField("_view", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(surface)!;

        Assert.True(view.FindControl<Control>("ChecklistTab")!.IsVisible);
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

        var ticks = panel.GetVisualDescendants().OfType<CheckBox>().ToList();

        // One authored item and one derived one, and exactly one checkbox. Not a disabled tick:
        // a greyed-out control still asserts that ticking is the mechanism here, and it is not.
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

        // Kept, counted, and out of the way. Forty finished items must not bury the six still open.
        Assert.Contains("Done (1)", text);

        window.Close();
    }

    /// <summary>
    /// One list in the Commander's order: scope rides each line, and the page is not carved into
    /// one list per ship. What gets reordered on a whim is everything being worked on.
    /// </summary>
    [AvaloniaFact]
    public void ScopeIsALabelOnTheLineRatherThanAHeadingOverAGroup()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        checklists.AddNote(ChecklistScope.Ship(12), "fit a fuel scoop");
        checklists.AddNote(ChecklistScope.Universal, "sell the cargo");

        var (window, panel) = Open(checklists);

        var lines = panel.GetVisualDescendants().OfType<CheckBox>()
            .Select(tick => tick.Content as string ?? string.Empty)
            .ToList();

        // In the order they were added, across scopes, rather than gathered into two lists.
        Assert.Equal(["buy limpets", "fit a fuel scoop", "sell the cargo"], lines);

        window.Close();
    }

    /// <summary>
    /// Selecting a line grows the movers, and moving reorders the file. Not a drag: a drag is the
    /// worst gesture available to a ray at a metre, and it has no spoken form at all.
    /// </summary>
    [AvaloniaFact]
    public void TheSelectedLineGrowsMoversAndMovingReordersTheList()
    {
        var checklists = Checklists(TempFolders.Create("d47-checklist-tests"));

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        checklists.AddNote(ChecklistScope.Universal, "fit a fuel scoop");

        var (window, panel) = Open(checklists);

        // Nothing is selected, so no line is carrying arrows. Several hundred rows each with a
        // permanent pair is several hundred controls a ray can hit by accident.
        Assert.DoesNotContain(
            panel.GetVisualDescendants().OfType<Button>(),
            button => button.Content as string == "▲");

        // The innermost border that holds the line, which is the card. Descendants come out
        // outermost first, so Last is the card rather than the pane or the scroller around it.
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

        var up = panel.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Content as string == "▲");

        up.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            ["fit a fuel scoop", "buy limpets"],
            checklists.Document.Items.Select(item => item.Text));

        window.Close();
    }

    /// <summary>
    /// Suggestions are a page rather than an interruption: they wait in one place, reached by
    /// drilling, and the list itself is not interleaved with them.
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

        // Not on the list itself. A proposal is not something the Commander is working on.
        Assert.DoesNotContain(
            panel.GetVisualDescendants().OfType<Button>(),
            button => button.Content as string == "Accept");

        open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        // A level of the stack, so the breadcrumb says where the Commander is and offers the way
        // back — which is the thing a headset has no title bar for.
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

        // A voice command, or a text editor. The page is a view of the file rather than a second
        // copy of it, so it has to follow whoever wrote.
        checklists.AddNote(ChecklistScope.Universal, "buy limpets");
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(
            panel.GetVisualDescendants().OfType<CheckBox>(),
            tick => tick.Content as string == "buy limpets");

        window.Close();
    }

    /// <summary>
    /// The page at the size the headset renders it, for a human to look at.
    /// </summary>
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
}

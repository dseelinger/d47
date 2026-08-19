using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The checklist page's own hands: rewording a line, taking one off, and typing into the box at
/// all (remediation.md 10, items 11, 13 and 14).
/// </summary>
public class TheChecklistTakesTheKeyboardTests
{
    private static (PanelView Panel, ChecklistService Checklists) Page(params string[] lines)
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-checklist-keyboard-tests"));

        paths.EnsureCreated();

        var checklists = new ChecklistService(
            new ChecklistStore(
                Path.Combine(paths.Data, "checklist.json"),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        foreach (var line in lines)
        {
            checklists.AddNote(ChecklistScope.Universal, line);
        }

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableChecklist(checklists);

        var window = new Window { Content = panel, Width = 1100, Height = 800 };

        window.Show();

        panel.Tab = PanelTab.Checklist;
        Dispatcher.UIThread.RunJobs();

        return (panel, checklists);
    }

    private static Button? Named(PanelView panel, string name) =>
        panel.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => (button.Content as string) == name);

    /// <summary>
    /// One answer in a chooser. Its content is a label and an optional detail rather than a
    /// string, so it is found by what is written in it.
    /// </summary>
    private static Button Option(PanelView panel, string label) =>
        panel.GetVisualDescendants()
            .OfType<Button>()
            .First(button => button.GetVisualDescendants()
                .OfType<TextBlock>()
                .Any(text => text.Text == label));

    /// <summary>Selecting a line is what grows its controls, so the card takes a press first.</summary>
    private static void Select(PanelView panel, string text)
    {
        // Descendants come out outermost first, so Last is the card rather than the pane or the
        // scroller around it.
        var card = panel.GetVisualDescendants()
            .OfType<Border>()
            .Last(border => border.GetVisualDescendants()
                .OfType<CheckBox>()
                .Any(tick => (tick.Content as string) == text));

        card.RaiseEvent(new Avalonia.Input.PointerPressedEventArgs(
            card,
            new Avalonia.Input.Pointer(0, Avalonia.Input.PointerType.Mouse, true),
            card,
            default,
            0,
            new Avalonia.Input.PointerPointProperties(
                Avalonia.Input.RawInputModifiers.LeftMouseButton,
                Avalonia.Input.PointerUpdateKind.LeftButtonPressed),
            Avalonia.Input.KeyModifiers.None));

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// The defect, as a Commander meets it: there is no way to change what a line says.
    /// </summary>
    [AvaloniaFact]
    public void ASelectedLineOffersEditAndDelete()
    {
        var (panel, _) = Page("Unlockly Chung");

        Assert.Null(Named(panel, "Edit"));
        Assert.Null(Named(panel, "Delete"));

        Select(panel, "Unlockly Chung");

        Assert.NotNull(Named(panel, "Edit"));
        Assert.NotNull(Named(panel, "Delete"));
    }

    /// <summary>
    /// And the prompt it opens is writable, which is the whole of item 11: it was read-only, so
    /// on the one surface with a keyboard and a clipboard in front of it the obvious thing to do
    /// was the one thing that did not work.
    /// </summary>
    [AvaloniaFact]
    public void TheEditPromptCanBeTypedInto()
    {
        var (panel, checklists) = Page("Unlockly Chung");

        Select(panel, "Unlockly Chung");
        Named(panel, "Edit")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var box = panel.GetVisualDescendants()
            .OfType<TextBox>()
            .First(candidate => candidate.Text == "Unlockly Chung");

        Assert.False(box.IsReadOnly, "the box could not be typed into");

        // What a keyboard or a paste does, which is all either of them does.
        box.Text = "Unlock Lei Cheung";
        Dispatcher.UIThread.RunJobs();

        Named(panel, "Done")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            "Unlock Lei Cheung",
            Assert.Single(checklists.Document.In(ChecklistScope.Universal)).Text);
    }

    /// <summary>
    /// Deleting asks first, and keeping is the answer that costs nothing. Deleting is the one
    /// control on this page with no way back, which is exactly the gap a ray at a metre falls
    /// into.
    /// </summary>
    [AvaloniaFact]
    public void DeletingAsksBeforeItActs()
    {
        var (panel, checklists) = Page("buy limpets");

        Select(panel, "buy limpets");
        Named(panel, "Delete")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        // Still there while the question is open.
        Assert.Single(checklists.Document.In(ChecklistScope.Universal));

        Option(panel, "Keep it").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Single(checklists.Document.In(ChecklistScope.Universal));
    }

    [AvaloniaFact]
    public void AndDeletesWhenTheAnswerIsYes()
    {
        var (panel, checklists) = Page("buy limpets");

        Select(panel, "buy limpets");
        Named(panel, "Delete")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Option(panel, "Delete it").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(checklists.Document.In(ChecklistScope.Universal));
    }

    /// <summary>
    /// Import and export are one button, because they are the same rare errand in two directions
    /// and two permanent controls on a working bar is two things to read past every session.
    /// </summary>
    [AvaloniaFact]
    public void TheBarOffersTheTransfer()
    {
        var (panel, _) = Page("buy limpets");

        // "Import/Export" rather than "Transfer" (remediation.md 13, item 3): transfer is what
        // Elite calls moving a ship between stations, which is not this.
        Assert.NotNull(Named(panel, "Import/Export"));
    }
}

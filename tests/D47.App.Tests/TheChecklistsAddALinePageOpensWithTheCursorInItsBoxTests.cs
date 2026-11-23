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
using D47.Core.Storage;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Checklist's entry pages open with the cursor in their text box, so typing needs no click (#2726).</summary>
public class TheChecklistsAddALinePageOpensWithTheCursorInItsBoxTests
{
    private static void Jobs()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static (Window Window, PanelView Panel) Open(ChecklistService checklists)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableChecklist(checklists);

        var window = new Window { Content = panel, Width = 1280, Height = 900 };
        window.Show();

        panel.Tab = PanelTab.Commander;
        Jobs();

        return (window, panel);
    }

    private static void Press(PanelView panel, string name)
    {
        var button = panel.GetVisualDescendants().OfType<Button>()
            .First(found => found.IsEffectivelyVisible && AutomationProperties.GetName(found) == name);

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();
    }

    private static void AssertBoxFocused(Window window, PanelView panel)
    {
        var box = panel.GetVisualDescendants().OfType<TextBox>().First(found => found.IsEffectivelyVisible);

        Assert.Same(box, window.FocusManager!.GetFocusedElement());
    }

    [AvaloniaFact]
    public void AddALineOpensWithTheCursorInItsBox()
    {
        var paths = new AppPaths(TestSurface.MemoryFolder("d47-checklist-add-focus"));
        var (window, panel) = Open(TestSurface.Checklists(paths.Data));

        Press(panel, "Add a line");

        AssertBoxFocused(window, panel);

        window.Close();
    }

    [AvaloniaFact]
    public void EditThisLineOpensWithTheCursorInItsBox()
    {
        var paths = new AppPaths(TestSurface.MemoryFolder("d47-checklist-edit-focus"));
        var checklists = TestSurface.Checklists(paths.Data);
        checklists.AddNote(ChecklistScope.Universal, "Buy limpets");
        var (window, panel) = Open(checklists);

        panel.Nav.Drill(ChecklistPage.AllLists);
        Jobs();

        Press(panel, "Edit this line");

        AssertBoxFocused(window, panel);

        window.Close();
    }
}

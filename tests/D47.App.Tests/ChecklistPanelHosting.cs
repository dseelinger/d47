using Avalonia.Controls;
using Avalonia.Threading;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Checklists;

namespace D47.App.Tests;

/// <summary>Opens a panel in a 900 by 700 window on the checklist's All lists page.</summary>
public static class ChecklistPanelHosting
{
    public static (Window Window, PanelView Panel) Show(ChecklistService checklists)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableChecklist(checklists);

        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();

        panel.Tab = PanelTab.Commander;
        panel.Nav.Drill(ChecklistPage.AllLists);
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }
}

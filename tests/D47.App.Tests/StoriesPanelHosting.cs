using Avalonia.Controls;
using Avalonia.Threading;
using D47.App.Panel;
using D47.Core.Interface;

namespace D47.App.Tests;

/// <summary>Opens a full panel in a 1280 by 860 window on the Stories tab's list page.</summary>
public static class StoriesPanelHosting
{
    public static (Window Window, PanelView Panel) Show(AdventureSurface surface)
    {
        var panel = new PanelView { DataContext = new PanelViewModel(), Mode = PanelMode.Full };
        panel.EnableAdventures(surface);

        var window = new Window { Content = panel, Width = 1280, Height = 860 };
        window.Show();

        panel.Tab = PanelTab.Stories;
        panel.Nav.SelectRoot(StoriesView.RootKey);
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }
}

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Catalogue is a root of the Stories tab, so a story drilled below it is drawn beside it and nothing sits above.</summary>
[Trait("Category", "Integration")]
public sealed class TheCatalogueIsDrawnWithoutTheStoriesRootTests
{
    private static NavCrumb Root => new("adventures", "Stories");

    private static PanelView Wide()
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.Furnish(PanelTab.Stories, crumb => new TextBlock { Text = crumb.Word }, Root, StoriesView.Crumb);

        var window = new Window { Content = panel, Width = 1400, Height = 700 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return panel;
    }

    private static IReadOnlyList<string> Panes(PanelView panel)
    {
        var strip = panel.GetVisualDescendants().OfType<DrillView>().Single();
        var grid = strip.GetVisualDescendants().OfType<Grid>().First();

        return grid.Children.OfType<Border>()
            .Select(host => (host.Child as TextBlock)?.Text ?? string.Empty)
            .Where(text => text.Length > 0)
            .ToList();
    }

    [AvaloniaFact]
    public void TheCatalogueAndAStoryShareTheStripWithoutTheRoot()
    {
        var panel = Wide();
        panel.Tab = PanelTab.Stories;
        panel.Nav.SelectRoot(StoriesView.RootKey);
        panel.Nav.GoTo(StoriesView.Crumb, new NavCrumb("read:mayday", "My Own Mayday"));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Catalogue", "My Own Mayday"], Panes(panel));
    }

    [AvaloniaFact]
    public void TheCatalogueAloneIsDrawnAlone()
    {
        var panel = Wide();
        panel.Tab = PanelTab.Stories;
        panel.Nav.SelectRoot(StoriesView.RootKey);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Catalogue"], Panes(panel));
    }
}

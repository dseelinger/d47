using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>A level marked Leads is the first one drawn, so the levels above it stay behind the breadcrumb.</summary>
[Trait("Category", "Integration")]
public sealed class TheCatalogueLeadsTheStripTests
{
    private static NavCrumb Root => new("adventures", "Stories");

    private static PanelView Wide()
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.Furnish(PanelTab.Stories, crumb => new TextBlock { Text = crumb.Word }, Root);

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
        panel.Nav.GoTo(Root, StoriesView.Crumb, new NavCrumb("read:mayday", "My Own Mayday"));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Catalogue", "My Own Mayday"], Panes(panel));
    }

    [AvaloniaFact]
    public void TheCatalogueAloneIsDrawnAlone()
    {
        var panel = Wide();
        panel.Tab = PanelTab.Stories;
        panel.Nav.GoTo(Root, StoriesView.Crumb);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Catalogue"], Panes(panel));
    }
}

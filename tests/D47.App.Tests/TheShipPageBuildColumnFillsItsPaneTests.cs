using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Storage;
using D47.TestSupport;
using Xunit;

namespace D47.App.Tests;

/// <summary>A ship's page build column takes its pane's width, less a 400 side column beside it where there is room (#2403).</summary>
[Trait("Category", "Integration")]
[Collection(nameof(ShipArtCollection))]
public class TheShipPageBuildColumnFillsItsPaneTests
{
    private static (Window Window, PanelView Panel) Page(int width, bool pictures = true, bool headset = false)
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-ship-columns"));

        paths.EnsureCreated();
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ships", "corsair.4k.png"), Path.Combine(paths.Ships, "corsair.4k.png"));

        ShipArt.Files = new DiskFileSystem();
        ShipArt.Shipped = null;
        ShipArt.Folder = paths.Ships;

        var checklists = TestSurface.Checklists(paths.Data);
        var ships = TestSurface.ShipPlans(checklists, paths.Data, () => null);

        ships.BuildFor(12, "Corsair", "Reaper");

        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        panel.EnableLoadout(ships, checklists, () => null, null, null, () => pictures);

        var window = new Window { Content = panel, Width = width, Height = 900 };

        window.Show();
        panel.Tab = PanelTab.Assets;
        Dispatcher.UIThread.RunJobs();

        panel.GetVisualDescendants()
            .OfType<Button>()
            .First(button => button.GetVisualDescendants()
                .OfType<TextBlock>()
                .Any(block => (block.Text ?? string.Empty).Contains("Reaper", StringComparison.OrdinalIgnoreCase)))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    private static ScrollViewer Scroller(PanelView panel) =>
        panel.GetVisualDescendants().OfType<ItemPage>().Single().GetVisualDescendants().OfType<ScrollViewer>().First();

    private static Grid? Columns(PanelView panel) =>
        panel.GetVisualDescendants().OfType<HullPicture>().SingleOrDefault()?.GetVisualParent() as Grid;

    private static void Press(PanelView panel, string size)
    {
        panel.GetVisualDescendants().OfType<HullPicture>().Single().GetVisualDescendants().OfType<RadioButton>()
            .Single(choice => choice.Content as string == size).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void AWideScrollerGetsABuildColumnOfItsWidthLessTheSideColumnAndTheSlotTableInsideTheBuild()
    {
        var (window, panel) = Page(3400);

        Assert.True(Scroller(panel).Bounds.Width >= 1000, $"scroller {Scroller(panel).Bounds.Width} panel {panel.Bounds.Width} window {window.Bounds.Width} scrollers {string.Join(",", panel.GetVisualDescendants().OfType<ScrollViewer>().Select(s => s.Bounds.Width))}");

        var grid = Columns(panel);

        Assert.NotNull(grid);
        var buildWidth = grid.Bounds.Width - 400 - 32;

        Assert.True(buildWidth > 900);
        Assert.Equal(buildWidth, grid.Children[0].Bounds.Width, 1.5);
        Assert.Equal(400, grid.Children[1].Bounds.Width, 1.5);
        Assert.Equal(buildWidth + 32, grid.Children[1].Bounds.X, 2.5);

        var build = grid.Children[0];

        Assert.All(
            panel.GetVisualDescendants().OfType<TableGrid>(),
            table => Assert.True(build.IsVisualAncestorOf(table) && table.Bounds.Width > 900));

        window.Close();
    }

    [AvaloniaFact]
    public void ANarrowScrollerHasNoSideColumn()
    {
        var (window, panel) = Page(2000);

        Assert.True(Scroller(panel).Bounds.Width < 1000);

        var picture = panel.GetVisualDescendants().OfType<HullPicture>().Single();

        Assert.IsNotType<Grid>(picture.GetVisualParent() is Grid { ColumnDefinitions.Count: 3 } ? picture.GetVisualParent() : new Border());

        window.Close();
    }

    [AvaloniaFact]
    public void WideIsNoWiderThan900AndPicturesOffFillTheBuildColumn()
    {
        var (window, panel) = Page(3400);

        try
        {
            Press(panel, "WIDE");

            var picture = panel.GetVisualDescendants().OfType<HullPicture>().Single();

            Assert.False(picture.GetVisualParent() is Grid { ColumnDefinitions.Count: 3 });
            Assert.True(picture.Bounds.Width <= 901.5);
        }
        finally
        {
            Press(panel, "BESIDE");
        }

        window.Close();

        var (off, bare) = Page(3400, pictures: false);

        Assert.Empty(bare.GetVisualDescendants().OfType<HullPicture>());
        Assert.All(bare.GetVisualDescendants().OfType<TableGrid>(), table => Assert.True(table.Bounds.Width > 900));

        off.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetPageHasNoSideColumn()
    {
        var (window, panel) = Page(3400, headset: true);

        Assert.False(panel.GetVisualDescendants().OfType<HullPicture>().Single().GetVisualParent() is Grid { ColumnDefinitions.Count: 3 });

        window.Close();
    }
}

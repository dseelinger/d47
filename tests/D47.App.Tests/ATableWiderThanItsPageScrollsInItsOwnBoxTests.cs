using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>On the desktop, a table whose columns do not fit at their narrowest scrolls sideways in its own box (#2517).</summary>
public class ATableWiderThanItsPageScrollsInItsOwnBoxTests
{
    private const string Id = StoredModulesPage.TableId;

    internal static readonly string[] Every =
        ["Module", "Class", "TransferCost", "TransferTime", "System", "Station", "InTransit", "Hot", "Engineering", "Grade"];

    private static readonly string[] Journal =
    [
        """{"timestamp":"2026-10-04T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
        """{"timestamp":"2026-10-04T09:00:00Z","event":"Location","StarSystem":"Shinrarta Dezhra","SystemAddress":3932277478106,"StarPos":[55.7,17.6,27.2],"Docked":true,"StationName":"Jameson Memorial"}""",
        """
        {"timestamp":"2026-10-04T09:01:00Z","event":"StoredModules","MarketID":128666762,"StationName":"Jameson Memorial","StarSystem":"Shinrarta Dezhra","Items":[
         {"Name":"$int_shieldgenerator_size6_class5_name;","Name_Localised":"Shield Generator","StorageSlot":1,"BuyPrice":16179531,"Hot":false},
         {"Name":"$int_powerdistributor_size5_class5_name;","Name_Localised":"Power Distributor","StorageSlot":3,"BuyPrice":3475688,"Hot":true},
         {"Name":"$int_cargorack_size7_class1_name;","Name_Localised":"Cargo Rack","StorageSlot":4,"BuyPrice":1178420,"Hot":false}
        ]}
        """,
    ];

    internal static GameStateStore Store()
    {
        var store = new GameStateStore();

        foreach (var line in Journal)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store;
    }

    internal static ViewStateStore ViewState() =>
        new(new AppPaths("memory"), new MemoryFileSystem(), NullLogger<ViewStateStore>.Instance);

    private static Window Show(Control page, double width, double height)
    {
        var look = AppLook.Put();
        var window = new Window
        {
            Content = page,
            Width = width,
            Height = height,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Closed += (_, _) => look.Dispose();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static List<TableGrid> Grids(Visual root) =>
        [.. root.GetVisualDescendants().OfType<TableGrid>().Where(grid => grid.Layout.Id == Id)];

    private static ScrollViewer Box(TableGrid grid) => grid.FindAncestorOfType<ScrollViewer>()!;

    private static ScrollViewer PageScroller(Visual root) =>
        root.GetVisualDescendants().OfType<ScrollViewer>().First(viewer => viewer.FindAncestorOfType<TableScroll>() is null);

    [AvaloniaFact]
    public void TenColumnsInANarrowWindowScrollSidewaysTogether()
    {
        var views = ViewState();
        var memory = new ColumnArrangementMemory(views);
        memory.Remember(Id, Every);
        var store = Store();
        var window = Show(new StoredModulesPage(() => store.Active, columnArrangements: memory), 600, 300);

        var grids = Grids(window);
        var header = grids.Single(grid => grid.IsHeader);
        var box = Box(header);

        Assert.Equal(Every, header.Layout.Columns.Select(column => column.Key));
        Assert.True(box.Extent.Width > box.Viewport.Width + 1, $"extent {box.Extent.Width}, viewport {box.Viewport.Width}");
        Assert.All(grids, grid => Assert.Same(box, Box(grid)));
        Assert.All(grids, grid => Assert.Equal(header.Bounds.Width, grid.Bounds.Width, 1));

        for (var column = 0; column < header.Layout.Columns.Count; column++)
        {
            Assert.True(header.Layout.Widths[column] >= header.Layout.MinimumOf(column) - 0.5, header.Layout.Columns[column].Key);
        }

        var page = PageScroller(window);
        Assert.True(page.Extent.Width <= page.Viewport.Width + 0.5, $"the page is {page.Extent.Width} wide in {page.Viewport.Width}");
        Assert.True(page.Extent.Height > page.Viewport.Height, "the page must scroll down for the wheel check");

        box.Offset = new Vector(100, 0);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(100, box.Offset.X, 1);

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("stored-modules-ten-columns-scrolled.png");
        }

        var over = box.TranslatePoint(new Point(40, 20), window)!.Value;
        Assert.True(over.Y < page.TranslatePoint(new Point(0, page.Viewport.Height), window)!.Value.Y, "the point is below the page");
        Assert.Same(box, window.GetVisualAt(over)!.FindAncestorOfType<ScrollViewer>(includeSelf: true));

        window.MouseWheel(over, new Vector(0, -1));
        Dispatcher.UIThread.RunJobs();

        Assert.True(page.Offset.Y > 0, "a wheel over the table no longer scrolls the page down");
        Assert.Equal(100, box.Offset.X, 1);
        window.Close();
    }

    [AvaloniaFact]
    public void ATableThatFitsFillsItsBoxAndDoesNotScroll()
    {
        var store = Store();
        var window = Show(new StoredModulesPage(() => store.Active, columnArrangements: new ColumnArrangementMemory(ViewState())), 1200, 900);

        var header = Grids(window).Single(grid => grid.IsHeader);
        var box = Box(header);

        Assert.Equal(box.Viewport.Width, box.Extent.Width, 0.5);
        Assert.Equal(box.Viewport.Width - 28, header.Bounds.Width, 1);
        window.Close();
    }
}

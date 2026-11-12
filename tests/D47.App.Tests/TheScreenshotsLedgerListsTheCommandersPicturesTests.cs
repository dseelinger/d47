using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Storage;
using Xunit;

namespace D47.App.Tests;

/// <summary>Commander › Ledgers › Screenshots: the grid, its sort and thumbnails, and the viewer over the window.</summary>
[Trait("Category", "Integration")]
public sealed class TheScreenshotsLedgerListsTheCommandersPicturesTests
{
    private const string Folder = @"C:\Pictures\D47";

    private const string Thumbs = @"C:\d47\data\screenshot-thumbnails";

    private static readonly string[] Names =
    [
        "2026-11-01 10-00-00 Sol Earth.jpg",
        "2026-11-03 10-00-00 Colonia.jpg",
        "2026-11-02 10-00-00 Deciat 6 a.jpg",
    ];

    private static byte[] Jpeg(int width, int height, byte shade)
    {
        using var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);

        using (var buffer = bitmap.Lock())
        {
            var row = new byte[buffer.RowBytes];

            for (var x = 0; x < width; x++)
            {
                row[(x * 4) + 0] = shade;
                row[(x * 4) + 1] = shade;
                row[(x * 4) + 2] = shade;
                row[(x * 4) + 3] = 255;
            }

            for (var y = 0; y < height; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(row, 0, buffer.Address + (y * buffer.RowBytes), row.Length);
            }
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, new JpegBitmapEncoderOptions());

        return stream.ToArray();
    }

    private static MemoryFileSystem Stocked(int width = 64, int height = 36)
    {
        var files = new MemoryFileSystem();

        for (var i = 0; i < Names.Length; i++)
        {
            files.WriteBytes(Path.Combine(Folder, Names[i]), Jpeg(width, height, (byte)(60 + (i * 40))));
        }

        return files;
    }

    private static ScreenshotSet SetOf(MemoryFileSystem files, IReadOnlySet<string>? cut = null, Action<string?>? show = null) =>
        new(files, Folder, Thumbs, () => cut ?? new HashSet<string>(), show);

    private static void Until(Func<bool> done, string what)
    {
        var until = DateTime.UtcNow.AddSeconds(20);

        while (!done() && DateTime.UtcNow < until)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }

        Dispatcher.UIThread.RunJobs();
        Assert.True(done(), what);
    }

    private static bool Drawn(ScreenshotsPage page, int tiles) =>
        tiles == 0
            ? In<TextBlock>(page).Any(block => block.Text == ScreenshotsPage.EmptyText)
            : Tiles(page).Length == tiles && In<Image>(page).Count(image => image.Source is not null) == tiles;

    private static (Window Window, ScreenshotsPage Page) Open(ScreenshotSet set, int tiles, string? current = null)
    {
        var page = new ScreenshotsPage(set, () => current);
        var window = new Window { Content = page, Width = 1280, Height = 800 };
        window.Show();
        Until(() => Drawn(page, tiles), "the page did not finish drawing");

        return (window, page);
    }

    private static List<T> In<T>(Visual root) where T : Visual => [.. root.GetVisualDescendants().OfType<T>()];

    private static string[] Tiles(Visual root) =>
        [.. In<Button>(root).Where(button => button.Name?.StartsWith("Screenshot.", StringComparison.Ordinal) == true).Select(button => button.Name![11..])];

    private static void Press(Button button)
    {
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public void TheLedgerListsAfterCodexAndBeforeThisSession()
    {
        var nav = new PanelNavigator();
        nav.Register(PanelTab.Commander, LedgersPage.Root);

        foreach (var (key, word) in new[] { (SessionPage.RootKey, "This session"), (ScreenshotsPage.PageKey, "Screenshots"), (CodexPage.PageKey, "Codex") })
        {
            Assert.True(nav.Declare(LedgersPage.RootKey, new NavCrumb(key, word)));
        }

        var rows = LedgersPage.Rows(nav);

        Assert.Equal(["Codex", "Screenshots", "This session"], rows.Select(row => row.Name));
        Assert.Equal("The pictures you have taken in Elite.", rows[1].Description);
    }

    [AvaloniaFact]
    public void TheGridListsEveryPictureNewestFirstAndOldestFirstOnRequest()
    {
        var (_, page) = Open(SetOf(Stocked()), 3);

        Assert.Equal(["2026-11-03 10-00-00 Colonia", "2026-11-02 10-00-00 Deciat 6 a", "2026-11-01 10-00-00 Sol Earth"], Tiles(page));

        var oldest = In<RadioButton>(page).Single(button => Equals(button.Content, "Oldest first"));
        oldest.IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["2026-11-01 10-00-00 Sol Earth", "2026-11-02 10-00-00 Deciat 6 a", "2026-11-03 10-00-00 Colonia"], Tiles(page));
        Assert.True(In<RadioButton>(page).All(button => button.Bounds.Height >= 44));
    }

    [AvaloniaFact]
    public void ThePageCountsThePicturesAndTheirSize()
    {
        var (_, page) = Open(SetOf(Stocked()), 3);

        Assert.Contains(In<TextBlock>(page), block => block.Text is { } text && text.StartsWith("3 screenshots · ", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void ThePageIsListedAgainEachTimeItIsShown()
    {
        var files = Stocked();
        var (window, page) = Open(SetOf(files), 3);
        window.Content = null;
        files.WriteBytes(Path.Combine(Folder, "2026-11-05 10-00-00 Maia.jpg"), Jpeg(64, 36, 200));

        window.Content = page;
        Until(() => Drawn(page, 4), "the page was not listed again");

        Assert.Equal(4, Tiles(page).Length);
    }

    [AvaloniaFact]
    public void WithNoPicturesThePageSaysHowToTakeOne()
    {
        var (_, page) = Open(SetOf(new MemoryFileSystem()), 0);

        Assert.Contains(In<TextBlock>(page), block => block.Text == ScreenshotsPage.EmptyText);
        Assert.Contains("screenshot key", ScreenshotsPage.EmptyText, StringComparison.Ordinal);
        Assert.Contains("take a picture", ScreenshotsPage.EmptyText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void ATileNamesTheSystemInCyanOnlyForTheCurrentOneAndTagsACodexPicture()
    {
        using var look = AppLook.Put();

        var cut = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Names[1] };
        var (_, page) = Open(SetOf(Stocked(), cut), 3, current: "Colonia");

        Assert.Single(In<TextBlock>(page), block => block.Text == ScreenshotsPage.CodexTag);

        var colonia = In<TextBlock>(page).Single(block => block.Text == "Colonia");
        var sol = In<TextBlock>(page).Single(block => block.Text == "Sol Earth");

        Assert.Equal(((ISolidColorBrush)Application.Current!.Resources[D47.App.Theming.ThemeManager.CyanKey]!).Color, ((ISolidColorBrush)colonia.Foreground!).Color);
        Assert.Equal(((ISolidColorBrush)Application.Current!.Resources[D47.App.Theming.ThemeManager.AKey]!).Color, ((ISolidColorBrush)sol.Foreground!).Color);
    }

    [AvaloniaFact]
    public void ShowInFolderOpensTheFolderFromTheGrid()
    {
        var shown = new List<string?>();
        var (_, page) = Open(SetOf(Stocked(), show: shown.Add), 3);

        Press(In<Button>(page).Single(button => button.Name == "ScreenshotsShowInFolder"));

        Assert.Equal([(string?)null], shown);
    }

    [AvaloniaFact]
    public void ATileDrawsFromACachedThumbnailRemadeWhenItsFileChangesAndDeletedWhenItsFileIsGone()
    {
        var files = Stocked();
        var set = SetOf(files);
        var (_, page) = Open(set, 3);

        Until(() => files.Enumerate(Thumbs, "*.png").Count == 3, "thumbnails were not cached");
        var first = files.Enumerate(Thumbs, "*.png");
        Assert.Equal(3, first.Count);
        Assert.All(In<Image>(page).Where(image => image.Source is not null), image => Assert.True(image.Source!.Size.Width <= ScreenshotSet.ThumbnailWidth));
        Assert.Equal(3, In<Image>(page).Count(image => image.Source is not null));

        files.WriteBytes(Path.Combine(Folder, Names[0]), Jpeg(80, 45, 10));
        files.Delete(Path.Combine(Folder, Names[1]));
        var again = SetOf(files);
        var (_, second) = Open(again, 2);

        Until(() => files.Enumerate(Thumbs, "*.png").Count == 2, "stale thumbnails were not deleted");
        var now = files.Enumerate(Thumbs, "*.png");
        Assert.Equal(2, now.Count);
        Assert.Single(now.Except(first));
        Assert.Equal(2, Tiles(second).Length);
    }

    [AvaloniaFact]
    public void APictureIsDecodedNoLargerThanTheSizeAskedFor()
    {
        var files = new MemoryFileSystem();
        var path = Path.Combine(Folder, Names[0]);
        files.WriteBytes(path, Jpeg(2000, 1000, 90));
        var set = SetOf(files);

        using var small = set.Decode(path, 520, 292);

        Assert.NotNull(small);
        Assert.InRange(small!.PixelSize.Width, 1, 520);
        Assert.InRange(small.PixelSize.Height, 1, 292);
        Assert.Equal((2000, 1000), set.PixelSize(path));
    }

    [AvaloniaFact]
    public void PressingATileOpensTheViewerAtThatPictureAndTheKeysMoveAndClose()
    {
        var shown = new List<string?>();
        var (window, page) = Open(SetOf(Stocked(), show: shown.Add), 3);
        var layer = OverlayLayer.GetOverlayLayer(page)!;

        Press(In<Button>(page).Single(button => button.Name == "Screenshot.2026-11-02 10-00-00 Deciat 6 a"));

        var viewer = Assert.Single(layer.Children.OfType<Grid>(), grid => grid.GetType().Name == "ScreenshotViewer");
        string Text(string name) => In<TextBlock>(viewer).Single(block => block.Name == name).Text!;
        Button Named(string name) => In<Button>(viewer).Single(button => button.Name == name);

        Assert.Equal("2 of 3", Text("ScreenshotPosition"));
        Assert.True(Named("PreviousScreenshot").IsEnabled);
        Assert.True(Named("NextScreenshot").IsEnabled);
        Assert.All(In<Button>(viewer), button => Assert.True(button.MinHeight >= 44));

        window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("3 of 3", Text("ScreenshotPosition"));
        Assert.False(Named("NextScreenshot").IsEnabled);

        window.KeyPress(Key.Left, RawInputModifiers.None, PhysicalKey.ArrowLeft, null);
        window.KeyPress(Key.Left, RawInputModifiers.None, PhysicalKey.ArrowLeft, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("1 of 3", Text("ScreenshotPosition"));
        Assert.False(Named("PreviousScreenshot").IsEnabled);

        Press(Named("ShowScreenshotInFolder"));
        Assert.Equal([Path.Combine(Folder, Names[1])], shown);

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(layer.Children.OfType<Grid>(), grid => grid.GetType().Name == "ScreenshotViewer");
    }

    [AvaloniaFact]
    public void TheViewerShowsThePictureAndItsPixelAndFileSize()
    {
        var files = Stocked(640, 360);
        var (_, page) = Open(SetOf(files), 3);
        var layer = OverlayLayer.GetOverlayLayer(page)!;

        Press(In<Button>(page).First(button => button.Name?.StartsWith("Screenshot.", StringComparison.Ordinal) == true));

        var viewer = layer.Children.OfType<Grid>().Single(grid => grid.GetType().Name == "ScreenshotViewer");
        Until(() => In<Image>(viewer).Single(i => i.Name == "ViewedScreenshot").Source is not null, "the viewer did not draw");

        var image = In<Image>(viewer).Single(i => i.Name == "ViewedScreenshot");
        var facts = In<TextBlock>(viewer).Single(block => block.Name == "ScreenshotFacts").Text!;

        Assert.NotNull(image.Source);
        Assert.Contains("640 × 360", facts, StringComparison.Ordinal);
        Assert.Contains("KB", facts, StringComparison.Ordinal);
    }
}

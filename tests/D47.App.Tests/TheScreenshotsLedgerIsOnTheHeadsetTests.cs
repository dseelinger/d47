using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Storage;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Screenshots ledger on the headset big panel: 25 tiles a page, a picture page, and nothing that needs typing or a window.</summary>
[Trait("Category", "Integration")]
public sealed class TheScreenshotsLedgerIsOnTheHeadsetTests
{
    private const string Folder = @"C:\Pictures\D47";

    private const string Thumbs = @"C:\d47\data\screenshot-thumbnails";

    private const int Pictures = 30;

    private static byte[] Jpeg(int width, int height)
    {
        using var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);

        using (var buffer = bitmap.Lock())
        {
            var row = new byte[buffer.RowBytes];
            Array.Fill(row, (byte)120);

            for (var y = 0; y < height; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(row, 0, buffer.Address + (y * buffer.RowBytes), row.Length);
            }
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, new JpegBitmapEncoderOptions());

        return stream.ToArray();
    }

    private static string NameOf(int day) => $"2026-10-{day:00} 10-00-00 Sol Earth.jpg";

    private static ScreenshotSet Stocked(int count = Pictures, int width = 640, int height = 360)
    {
        var files = new MemoryFileSystem();
        var bytes = Jpeg(width, height);

        for (var day = 1; day <= count; day++)
        {
            files.WriteBytes(Path.Combine(Folder, NameOf(day)), bytes);
        }

        return new ScreenshotSet(files, Folder, Thumbs, () => new HashSet<string>(), null);
    }

    private static PanelView BigPanel(ScreenshotSet set)
    {
        var state = JournalScript.Commander("F1000001", "Test Pilot");
        var view = new PanelView { DataContext = new PanelViewModel() };
        view.Classes.Add("headset");
        new MiniPanelServices
        {
            Checklists = TestSurface.Checklists(TestSurface.MemoryFolder("d47-screenshots-headset-tests")),
            GameState = () => state,
            HistoryState = () => HistoryState.Done,
            Commanders = new CommanderRoster(() => null, () => 0, () => null, () => false, _ => { }),
            Screenshots = set,
        }.Furnish(view);

        return view;
    }

    private static Window Show(PanelView view)
    {
        var window = new Window
        {
            Content = view,
            Width = 1024,
            Height = 900,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static List<T> In<T>(Visual root) where T : Visual => [.. root.GetVisualDescendants().OfType<T>()];

    private static string[] Tiles(Visual root) =>
        [.. In<Button>(root).Where(button => button.Name?.StartsWith("Screenshot.", StringComparison.Ordinal) == true).Select(button => button.Name![11..])];

    private static Button Named(Visual root, string name) => In<Button>(root).Single(button => button.Name == name);

    private static string Readout(Visual root) =>
        In<TextBlock>(root).Single(block => block.Text?.Contains(" of ", StringComparison.Ordinal) == true && block.Text.Contains('–', StringComparison.Ordinal)).Text!;

    private static void Press(Button button)
    {
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

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

    private static (PanelView View, Window Window) Opened(ScreenshotSet set)
    {
        var view = BigPanel(set);
        var window = Show(view);

        Assert.True(view.Nav.Show(ScreenshotsPage.PageKey));
        Until(() => Tiles(view).Length > 0, "the ledger did not draw");

        return (view, window);
    }

    [AvaloniaFact]
    public void TheBigPanelListsTheLedgerWith25TilesAndAPagerRow()
    {
        using var look = AppLook.Put();
        var (view, window) = Opened(Stocked());

        Assert.Equal(25, Tiles(view).Length);
        Assert.Equal("1–25 of 30", Readout(view));
        Assert.False(Named(view, ScreenshotsPage.PreviousName).IsEnabled);
        Assert.True(Named(view, ScreenshotsPage.NextName).IsEnabled);
        Assert.DoesNotContain(In<Button>(view), button => button.Name == "ScreenshotsShowInFolder");
        Assert.All(In<Button>(view).Where(button => button.Name is ScreenshotsPage.PreviousName or ScreenshotsPage.NextName), button => Assert.True(button.Bounds.Height >= TypeScale.MinimumTarget));

        Press(Named(view, ScreenshotsPage.NextName));

        Assert.Equal(5, Tiles(view).Length);
        Assert.Equal("26–30 of 30", Readout(view));
        Assert.True(Named(view, ScreenshotsPage.PreviousName).IsEnabled);
        Assert.False(Named(view, ScreenshotsPage.NextName).IsEnabled);

        window.Close();
    }

    [AvaloniaFact]
    public void ChangingTheSortReturnsToTheFirstPage()
    {
        using var look = AppLook.Put();
        var (view, window) = Opened(Stocked());

        Press(Named(view, ScreenshotsPage.NextName));
        Assert.Equal("26–30 of 30", Readout(view));

        var oldest = In<RadioButton>(view).Single(button => Equals(button.Content, "Oldest first"));
        oldest.IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("1–25 of 30", Readout(view));
        Assert.Equal(NameOf(1)[..^4], Tiles(view)[0]);

        window.Close();
    }

    [AvaloniaFact]
    public void ALedgerOfOnePageHasNoPagerRow()
    {
        using var look = AppLook.Put();
        var (view, window) = Opened(Stocked(count: 3));

        Assert.Equal(3, Tiles(view).Length);
        Assert.DoesNotContain(In<Button>(view), button => button.Name == ScreenshotsPage.NextName);

        window.Close();
    }

    [AvaloniaFact]
    public void PressingATileDrillsToAPageThatShowsTheWholePicture()
    {
        using var look = AppLook.Put();
        var (view, window) = Opened(Stocked());

        Press(In<Button>(view).First(button => button.Name?.StartsWith("Screenshot.", StringComparison.Ordinal) == true));

        Assert.Equal(["Ledgers", "Screenshots", "Screenshot"], view.Nav.Trail.Select(crumb => crumb.Word));

        var page = In<ScreenshotPicturePage>(view).Single();
        Until(() => In<Image>(page).Single(image => image.Name == ScreenshotPicturePage.ImageName).Source is not null, "the picture did not draw");

        var shown = (Bitmap)In<Image>(page).Single(image => image.Name == ScreenshotPicturePage.ImageName).Source!;
        var frame = ((Control)In<Image>(page).Single(image => image.Name == ScreenshotPicturePage.ImageName).Parent!).Bounds;

        Assert.True(shown.PixelSize.Width <= Math.Max(1, (int)Math.Round(frame.Width)));
        Assert.True(shown.PixelSize.Height <= Math.Max(1, (int)Math.Round(frame.Height)));
        Assert.True(shown.PixelSize.Width <= 640);
        Assert.Equal("1 of 30", In<TextBlock>(page).Single(block => block.Name == ScreenshotPicturePage.PositionName).Text);
        Assert.Contains("2026-10-30 10:00 · 640 × 360", In<TextBlock>(page).Single(block => block.Name == ScreenshotPicturePage.FactsName).Text, StringComparison.Ordinal);
        Assert.False(Named(page, "PreviousScreenshotPicture").IsEnabled);
        Assert.True(Named(page, "NextScreenshotPicture").IsEnabled);

        Press(Named(page, "NextScreenshotPicture"));

        Assert.Equal("2 of 30", In<TextBlock>(page).Single(block => block.Name == ScreenshotPicturePage.PositionName).Text);
        Assert.Contains("2026-10-29", In<TextBlock>(page).Single(block => block.Name == ScreenshotPicturePage.FactsName).Text, StringComparison.Ordinal);

        var labels = In<Button>(page).Select(button => button.Content as string).ToList();
        Assert.Equal(["◄ PREVIOUS", "NEXT ►"], labels);
        Assert.All(In<Button>(page), button => Assert.True(button.Bounds.Height >= TypeScale.MinimumTarget));

        view.Nav.Back();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Ledgers", "Screenshots"], view.Nav.Trail.Select(crumb => crumb.Word));

        window.Close();
    }

    [AvaloniaFact]
    public void ThePicturePageNeverDecodesAboveThePanel()
    {
        using var look = AppLook.Put();
        var (view, window) = Opened(Stocked(count: 3, width: 4000, height: 2250));

        Press(In<Button>(view).First(button => button.Name?.StartsWith("Screenshot.", StringComparison.Ordinal) == true));

        var page = In<ScreenshotPicturePage>(view).Single();
        Until(() => In<Image>(page).Single(image => image.Name == ScreenshotPicturePage.ImageName).Source is not null, "the picture did not draw");

        var image = In<Image>(page).Single(candidate => candidate.Name == ScreenshotPicturePage.ImageName);
        var shown = (Bitmap)image.Source!;
        var frame = ((Control)image.Parent!).Bounds;

        Assert.True(shown.PixelSize.Width <= Math.Ceiling(frame.Width));
        Assert.True(shown.PixelSize.Height <= Math.Ceiling(frame.Height) + 1);
        Assert.True(image.Bounds.Width <= frame.Width && image.Bounds.Height <= frame.Height);

        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetPicturePageOffersNothingToRenameDeleteOrOpenInAFolder()
    {
        using var look = AppLook.Put();
        var (view, window) = Opened(Stocked(count: 3));

        Press(In<Button>(view).First(button => button.Name?.StartsWith("Screenshot.", StringComparison.Ordinal) == true));

        var labels = In<Button>(In<ScreenshotPicturePage>(view).Single()).Select(button => (string)button.Content!).ToList();
        Assert.DoesNotContain(labels, label => label.Contains("Rename", StringComparison.OrdinalIgnoreCase)
            || label.Contains("Delete", StringComparison.OrdinalIgnoreCase)
            || label.Contains("folder", StringComparison.OrdinalIgnoreCase));

        window.Close();
    }
}

using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Audio;
using D47.Core.Books;
using D47.Core.Interface;
using D47.Core.Storage;
using Xunit;

namespace D47.App.Tests.Books;

/// <summary>A book opened while its download is arriving lists and reads the chapters that arrive later (#2714).</summary>
public sealed class APartlyDownloadedBookTakesTheTextThatArrivesTests
{
    private const string One = "CHAPTER I\r\n\r\nThe lamp was lit.\r\n\r\n";
    private const string Two = "CHAPTER II\r\n\r\nNight fell.\r\n\r\n";
    private const string Three = "CHAPTER III\r\n\r\nThe tide was out.\r\n\r\n";
    private const int Declared = 4;

    private static BookRig HalfArrived(out long written)
    {
        var rig = BookRig.OnShelf(new MemoryFileSystem(), @"C:\d47\data\books");
        var first = GutenbergText.Header + One + Two;

        rig.Shelf.AddDownload(GutenbergText.Source, "The Night Watch", "H. G. Wellstone");
        written = Encoding.UTF8.GetByteCount(first);
        rig.Shelf.Arrived(GutenbergText.Source, first, replace: false, new BookDownload(false, written, "\"v1\""), Declared);

        return rig;
    }

    private static void ArriveChapterThree(BookRig rig, long written)
    {
        rig.Shelf.Arrived(
            GutenbergText.Source,
            Three,
            replace: false,
            new BookDownload(false, written + Encoding.UTF8.GetByteCount(Three), "\"v1\""),
            Declared);
    }

    private static BooksHost HostOf(BookRig rig) =>
        new(
            rig.Shelf,
            rig.Player,
            () => rig.Settings,
            change => rig.Settings = change(rig.Settings),
            _ => rig.Installed,
            (_, _) => Task.FromResult(VoiceCatalogue.Silent),
            rig.BookFiles);

    private static (Window Window, PanelView Panel) OpenPage(BookRig rig)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableMedia(books: HostOf(rig));

        var window = new Window { Content = panel, Width = 1280, Height = 1000 };

        window.Show();
        panel.Tab = PanelTab.Media;
        Assert.True(panel.Nav.SelectRoot(MediaFurnishing.BooksRoot));
        Dispatcher.UIThread.RunJobs();

        var row = panel.GetVisualDescendants().OfType<Button>().First(button => button.Name == BooksPage.RowName);
        row.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    private static List<string> Titles(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Name == BookPage.ChapterName)
            .Select(button => button.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty).First(text => text.Length > 1))];

    private static void Redraw(PanelView panel)
    {
        panel.GetVisualDescendants().OfType<BookPage>().Single().Draw();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void ThePageListsChaptersThatArriveWhileItIsOpenByTheirOwnTitles()
    {
        var rig = HalfArrived(out var written);
        var (window, panel) = OpenPage(rig);

        var before = Titles(panel);
        Assert.Equal(Declared, before.Count);
        Assert.Equal("CHAPTER 3", before[2]);
        Assert.Equal("CHAPTER 4", before[3]);

        ArriveChapterThree(rig, written);
        Redraw(panel);

        var after = Titles(panel);
        Assert.Equal(Declared, after.Count);
        Assert.Equal(before[..2], after[..2]);
        Assert.NotEqual("CHAPTER 3", after[2]);
        Assert.Contains("III", after[2], StringComparison.OrdinalIgnoreCase);
        Assert.Equal("CHAPTER 4", after[3]);

        window.Close();
    }

    [Fact]
    public void ThePlayersBookTakesTheChaptersThatArriveAndNextChapterReachesThem()
    {
        var rig = HalfArrived(out var written);
        var host = HostOf(rig);

        Assert.True(host.ReadFrom(GutenbergText.Source, 1));
        rig.Settle();

        Assert.Equal(2, rig.Player.Now!.Book.Chapters.Count);
        Assert.False(host.HasNextChapter);

        ArriveChapterThree(rig, written);

        Assert.True(host.HasNextChapter);
        Assert.Equal(3, rig.Player.Now!.Book.Chapters.Count);

        Assert.True(host.NextChapter());
        rig.Settle();

        Assert.Equal(2, rig.Player.Now!.Place.Chapter);
        Assert.Equal("The tide was out.", rig.Player.Now.Text);
    }
}

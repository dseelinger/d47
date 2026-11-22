using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Audio;
using D47.Core.Books;
using D47.Core.Interface;
using D47.Core.Storage;
using Xunit;

namespace D47.App.Tests.Books;

/// <summary>Media › Books › Add a book lists the .txt and .epub files in the books folder (#2665).</summary>
public class TheAddABookPageListsTheBooksFolderTests
{
    private const string Folder = @"C:\d47\data\books";

    private static (Window Window, PanelView Panel, BookRig Rig, BooksHost Host) Open(bool headset = false)
    {
        var rig = BookRig.OnShelf(new MemoryFileSystem(), Folder);
        var host = new BooksHost(
            rig.Shelf,
            rig.Player,
            () => rig.Settings,
            change => rig.Settings = change(rig.Settings),
            _ => true,
            (_, _) => Task.FromResult(VoiceCatalogue.Silent),
            rig.BookFiles);
        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        panel.EnableMedia(books: host);

        var window = new Window { Content = panel, Width = headset ? 1024 : 1280, Height = headset ? 640 : 1000 };

        window.Show();
        panel.Tab = PanelTab.Media;
        Assert.True(panel.Nav.SelectRoot(MediaFurnishing.BooksRoot));
        Dispatcher.UIThread.RunJobs();

        return (window, panel, rig, host);
    }

    private static T Find<T>(Control root, string name)
        where T : Control =>
        root.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    private static Button? Maybe(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Name == name);

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task OpenAddPage(PanelView panel)
    {
        Click(Find<Button>(panel, BooksPage.AddName));
        await Refresh(panel);
    }

    private static async Task Refresh(PanelView panel)
    {
        await panel.GetVisualDescendants().OfType<BookAddPage>().First().RefreshAsync();
        Dispatcher.UIThread.RunJobs();
    }

    private static List<string> Drawn(Control root) =>
        [.. root.GetVisualDescendants().OfType<BookAddPage>().First().GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AFileCopiedIntoTheFolderAppearsWithoutARestart(bool headset)
    {
        var (window, panel, rig, host) = Open(headset);
        rig.Files.WriteText(@"D:\Downloads\the-raven.txt", "Once upon a midnight dreary.");

        await OpenAddPage(panel);

        Assert.Contains(BookAddPage.NothingToAdd, Drawn(panel));

        Assert.Equal("the-raven.txt", rig.BookFiles.CopyIn(@"D:\Downloads\the-raven.txt"));
        host.NotifyFilesChanged();
        Dispatcher.UIThread.RunJobs();
        await Refresh(panel);
        Assert.Contains("THE-RAVEN.TXT", Drawn(panel));
        Assert.Contains("1 PART", Drawn(panel));
        Assert.Contains(Folder, Drawn(panel));

        window.Close();
    }

    [AvaloniaFact]
    public async Task ChooseAFileAndOpenTheFolderAreOnTheDesktopOnly()
    {
        var (desktop, desktopPanel, _, _) = Open();
        await OpenAddPage(desktopPanel);

        Assert.NotNull(Maybe(desktopPanel, BookAddPage.ChooseName));
        Assert.NotNull(Maybe(desktopPanel, BookAddPage.OpenFolderName));
        desktop.Close();

        var (headset, headsetPanel, _, _) = Open(headset: true);
        await OpenAddPage(headsetPanel);

        Assert.Null(Maybe(headsetPanel, BookAddPage.ChooseName));
        Assert.Null(Maybe(headsetPanel, BookAddPage.OpenFolderName));
        Assert.NotNull(Maybe(headsetPanel, BookAddPage.AddName));
        headset.Close();
    }

    [AvaloniaFact]
    public async Task AddAndReadPutsTheChosenFileOnTheShelfAndStartsReadingIt()
    {
        var (window, panel, rig, _) = Open();
        rig.Files.WriteText(Path.Combine(Folder, "the-raven.txt"), "Once upon a midnight dreary.");

        await OpenAddPage(panel);

        var add = Find<Button>(panel, BookAddPage.AddName);
        Assert.False(add.IsEnabled);

        Click(Find<Button>(panel, BookAddPage.RowName));
        Assert.True(add.IsEnabled);

        Click(add);
        rig.Settle();
        Dispatcher.UIThread.RunJobs();

        var source = BookSource.FromFile("the-raven.txt");

        Assert.NotNull(rig.Shelf.Find(source));
        Assert.Equal(source, rig.Player.Now?.Book.Source);
        Assert.Empty(rig.BookFiles.List().Books);

        window.Close();
    }

    [AvaloniaFact]
    public async Task AFileWithNoTextShowsTheNoticeAndIsNotListed()
    {
        var (window, panel, rig, _) = Open();
        rig.Files.WriteText(Path.Combine(Folder, "empty.txt"), string.Empty);

        await OpenAddPage(panel);

        var notice = Find<Notice>(panel, BookAddPage.RefusalName);

        Assert.Equal("empty.txt has no text", notice.Label);
        Assert.Equal(NoticeLevel.Warning, notice.Level);
        Assert.Null(Maybe(panel, BookAddPage.RowName));
        Assert.DoesNotContain(rig.Shelf.Books, book => book.Source.File == "empty.txt");

        window.Close();
    }
}

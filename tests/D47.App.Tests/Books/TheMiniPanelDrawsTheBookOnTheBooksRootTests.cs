using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests.Books;

/// <summary>On Media › Books the mini panel draws the book being read; elsewhere it draws what it did before (#2664).</summary>
public class TheMiniPanelDrawsTheBookOnTheBooksRootTests
{
    private static (Window Window, PanelView Panel) Open(BookRig rig)
    {
        var host = new BooksHost(
            rig.Shelf,
            rig.Player,
            () => rig.Settings,
            change => rig.Settings = change(rig.Settings),
            _ => rig.Installed,
            (_, _) => Task.FromResult(VoiceCatalogue.Silent));
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableMedia(books: host);

        var window = new Window { Content = panel, Width = 792, Height = 400 };

        window.Show();
        panel.Mode = PanelMode.Mini;
        panel.Tab = PanelTab.Media;
        Assert.True(panel.Nav.SelectRoot(MediaFurnishing.BooksRoot));
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    private static List<string> Drawn(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<BookMini>().Single().GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    [AvaloniaFact]
    public void TheBookIsDrawnWithItsPlaceTheSentenceAndTheHint()
    {
        var rig = BookRig.InMemory();
        var (window, panel) = Open(rig);

        rig.Play();
        rig.Settle();
        Dispatcher.UIThread.RunJobs();

        var mini = panel.GetVisualDescendants().OfType<BookMini>().Single();

        Assert.True(mini.IsEffectivelyVisible);
        Assert.Contains("The Night Watch · CH 1 OF 2", Drawn(panel));
        Assert.Contains(BookRig.First, Drawn(panel));
        Assert.Contains(BookMini.ReadingHint, Drawn(panel));
        Assert.Empty(mini.GetVisualDescendants().OfType<Button>());

        window.Close();
    }

    [AvaloniaFact]
    public void ACommanderPauseNamesResume()
    {
        var rig = BookRig.InMemory();
        var (window, panel) = Open(rig);

        rig.Play();
        rig.Player.Pause(StoryRunPause.Commander);
        rig.Settle();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(BookMini.PausedHint, Drawn(panel));
        Assert.DoesNotContain(BookMini.ReadingHint, Drawn(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void NothingReadingSaysSo()
    {
        var (window, panel) = Open(BookRig.InMemory());

        Assert.Contains(BookMini.NoBook, Drawn(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void OtherMediaRootsDrawNoBook()
    {
        var (window, panel) = Open(BookRig.InMemory());

        Assert.True(panel.Nav.SelectRoot(MediaFurnishing.MusicRoot));
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<BookMini>(), mini => mini.IsEffectivelyVisible);

        window.Close();
    }
}

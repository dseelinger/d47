using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Books;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests.Books;

/// <summary>Media › Books reads the shelf and the player, on the window and on the big panel (#2662).</summary>
public class TheBooksPageReadsTheShelfTests
{
    private static BooksHost HostOf(BookRig rig, IReadOnlyList<VoiceInfo>? voices = null) =>
        new(
            rig.Shelf,
            rig.Player,
            () => rig.Settings,
            change => rig.Settings = change(rig.Settings),
            _ => rig.Installed,
            (_, _) => Task.FromResult(voices is null ? VoiceCatalogue.Silent : VoiceCatalogue.Of(voices)),
            rig.BookFiles);

    private static (Window Window, PanelView Panel) Open(BookRig rig, bool headset = false, BooksHost? host = null)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        panel.EnableMedia(books: host ?? HostOf(rig));

        var window = new Window { Content = panel, Width = headset ? 1024 : 1280, Height = headset ? 640 : 1000 };

        window.Show();
        panel.Tab = PanelTab.Media;
        Assert.True(panel.Nav.SelectRoot(MediaFurnishing.BooksRoot));
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    private static Control Page(Control root) =>
        root.GetVisualDescendants().OfType<Control>().First(control => control is BooksPage or BookPage);

    private static List<string> Drawn(Control root) =>
        [.. Page(root).GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    private static Button Named(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().First(button => button.Name == name);

    private static Button? MaybeNamed(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Name == name);

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static string? Status(Control root) =>
        Page(root).GetVisualDescendants().OfType<StatusLine>().FirstOrDefault(line => line.IsVisible)?.Text;

    private static void Settled(BookRig rig)
    {
        rig.Settle();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void BooksIsMediasThirdSubTabAfterPodcastsAndBeforeMovies(bool headset)
    {
        var (window, panel) = Open(BookRig.InMemory(), headset);

        Assert.Equal(["Music", "Podcasts", "Books", "Movies"], panel.Nav.Roots(PanelTab.Media).Select(root => root.Word));

        window.Close();
    }

    [AvaloniaFact]
    public void TheRootShowsTheBookBeingReadAndTheShelf()
    {
        var rig = BookRig.InMemory();
        rig.Play();

        var (window, panel) = Open(rig);
        var drawn = Drawn(panel);

        Assert.Contains("NOW READING", drawn);
        Assert.Contains("The Night Watch", drawn);
        Assert.Contains(BookRig.First, drawn);
        Assert.Contains("CH 1 OF 2 · 0% · KOKORO · DEFAULT VOICE", drawn);
        Assert.Contains("ON YOUR SHELF · 1 BOOK", drawn);
        Assert.Contains("H. G. WELLSTONE · FROM A FILE · THE NIGHT WATCH.TXT · READING NOW", drawn);
        Assert.Contains("CH 1 OF 2 · 0%", drawn);
        Assert.Equal("PAUSE", Named(panel, BooksKit.PauseName).Content);
        Assert.NotNull(MaybeNamed(panel, BooksPage.AddName));
        window.SaveFrame("books-desktop.png");

        window.Close();
    }

    [AvaloniaFact]
    public void ACommanderPauseSaysResumeAndEndsTheLineWithPaused()
    {
        var rig = BookRig.InMemory();
        rig.Play();
        var (window, panel) = Open(rig);

        Click(Named(panel, BooksKit.PauseName));
        Settled(rig);

        Assert.Equal(StoryRunPause.Commander, rig.Player.Now!.Pause);
        Assert.Equal("RESUME", Named(panel, BooksKit.PauseName).Content);
        Assert.Contains("CH 1 OF 2 · 0% · PAUSED", Drawn(panel));
        Assert.Equal(BooksKit.CommanderPaused, Status(panel));

        Click(Named(panel, BooksKit.PauseName));
        Settled(rig);

        Assert.False(rig.Player.Now!.IsPaused);
        Assert.Equal("PAUSE", Named(panel, BooksKit.PauseName).Content);

        window.Close();
    }

    [AvaloniaFact]
    public void APushToTalkPauseKeepsPauseOnTheTileAndSaysWhy()
    {
        var rig = BookRig.InMemory();
        rig.Play();
        var (window, panel) = Open(rig);

        rig.Audio.TurnInFlight = true;
        rig.Player.Pause(StoryRunPause.PushToTalk);
        Settled(rig);

        Assert.Equal("PAUSE", Named(panel, BooksKit.PauseName).Content);
        Assert.Contains("CH 1 OF 2 · 0% · KOKORO · DEFAULT VOICE", Drawn(panel));
        Assert.Equal(BooksKit.PushToTalkPaused, Status(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void ACalloutPauseKeepsPauseOnTheTileAndSaysWhy()
    {
        var rig = BookRig.InMemory();
        rig.Play();
        var (window, panel) = Open(rig);

        rig.Audio.Enqueue(new AudioRequest
        {
            Channel = AudioChannel.Alert,
            Clip = new AudioClip("alert", new byte[960], AudioFormat.Standard),
            Group = "alert",
        });
        Settled(rig);

        Assert.Equal(StoryRunPause.Interrupted, rig.Player.Now!.Pause);
        Assert.Equal("PAUSE", Named(panel, BooksKit.PauseName).Content);
        Assert.Equal(BooksKit.CalloutPaused, Status(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void ASentenceNotMadeYetShowsTheWaitMarkInTheStatusLine()
    {
        var rig = BookRig.InMemory();
        rig.Voices["kokoro"].Holds = true;
        rig.Play();
        var (window, panel) = Open(rig);

        Assert.True(rig.Player.Now!.Waiting);
        Assert.Equal("Kokoro is reading ahead…", Status(panel));

        rig.Voices["kokoro"].Release(BookRig.First);

        // The player's follow-up is queued from a pool thread; wait for it to be queued before running it.
        for (var wait = 0; wait < 400 && rig.Background.Count == 0; wait++)
        {
            Thread.Sleep(5);
        }

        Thread.Sleep(20);
        Settled(rig);

        Assert.False(rig.Player.Now.Waiting);
        Assert.Null(Status(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void AStoppedVoiceShowsTheRedNoticeAndRetryReadsTheSentenceAgain()
    {
        var rig = BookRig.InMemory();
        rig.Voices["kokoro"].Fails = BookRig.First;
        rig.Play();
        var (window, panel) = Open(rig);

        var notice = Page(panel).GetVisualDescendants().OfType<Notice>().Where(found => found.IsEffectivelyVisible).Single(found => found.Level == NoticeLevel.Error);

        Assert.Equal("Kokoro stopped", notice.Label);
        Assert.Equal(BooksKit.StoppedText, notice.Text);
        Assert.Equal("RESUME", Named(panel, BooksKit.PauseName).Content);
        Assert.NotNull(MaybeNamed(panel, BooksKit.VoiceSettingsName));
        Assert.Null(Status(panel));

        rig.Voices["kokoro"].Fails = null;
        Click(Named(panel, BooksKit.RetryName));
        Settled(rig);

        Assert.Null(rig.Player.Now!.Problem);
        Assert.DoesNotContain(Page(panel).GetVisualDescendants().OfType<Notice>().Where(found => found.IsEffectivelyVisible), found => found.Level == NoticeLevel.Error);

        window.Close();
    }

    [AvaloniaFact]
    public void ANoticeWithNoLocalVoiceShowsAboveTheShelfAndTheSegmentsAreDisabled()
    {
        var rig = BookRig.InMemory();
        rig.Installed = false;
        var (window, panel) = Open(rig);

        var notice = Page(panel).GetVisualDescendants().OfType<Notice>().Where(found => found.IsEffectivelyVisible).Single();

        Assert.Equal(NoticeLevel.Warning, notice.Level);
        Assert.Equal(BooksKit.NoVoiceLabel, notice.Label);
        Assert.NotNull(MaybeNamed(panel, BooksKit.VoiceSettingsName));
        Assert.Contains("ON YOUR SHELF · 1 BOOK", Drawn(panel));
        Assert.Contains("NOT STARTED", Drawn(panel));
        Assert.Equal(2, Drawn(panel).Count(text => string.Equals(text, BooksPage.NotInstalled, StringComparison.OrdinalIgnoreCase)));
        Assert.All(Page(panel).GetVisualDescendants().OfType<Segment>().Single().GetVisualDescendants().OfType<RadioButton>(), radio => Assert.False(radio.IsEnabled));
        Assert.False(Named(panel, BooksPage.ChangeVoiceName).IsEnabled);

        window.Close();
    }

    [AvaloniaFact]
    public void NextChapterAndBackAParagraphMoveTheReading()
    {
        var rig = BookRig.InMemory();
        rig.Play();
        var (window, panel) = Open(rig);

        Click(Named(panel, BooksKit.NextName));
        Settled(rig);

        Assert.Equal(new BookPlace(1, 0), rig.Player.Now!.Place);
        Assert.Equal(BookRig.Fourth, rig.Player.Now.Text);
        Assert.False(Named(panel, BooksKit.NextName).IsEnabled);
        Assert.Contains("CH 2 OF 2 · 66% · KOKORO · DEFAULT VOICE", Drawn(panel));

        Click(Named(panel, BooksKit.BackName));
        Settled(rig);

        Assert.Equal(new BookPlace(0, 1), rig.Player.Now!.Place);
        Assert.Equal(BookRig.Third, rig.Player.Now.Text);

        window.Close();
    }

    [AvaloniaFact]
    public void PressingABookOpensItsPageAndAChapterReadsFromItsStart()
    {
        var rig = BookRig.InMemory();
        var (window, panel) = Open(rig);

        Assert.Contains(BooksPage.NothingReading, Drawn(panel));

        Click(Named(panel, BooksPage.RowName));

        Assert.Equal("The Night Watch", panel.Nav.Trail[^1].Word);
        Assert.Contains("H. G. Wellstone · from a file · The Night Watch.txt · 2 chapters", Drawn(panel));
        Assert.Contains("CHAPTERS · 2", Drawn(panel));

        var chapters = panel.GetVisualDescendants().OfType<Button>().Where(button => button.Name == BookPage.ChapterName).ToList();

        Assert.Equal(2, chapters.Count);

        Click(chapters[1]);
        Settled(rig);

        Assert.Equal(new BookPlace(1, 0), rig.Player.Now!.Place);
        Assert.Equal(BookRig.Fourth, rig.Player.Now.Text);
        Assert.NotNull(MaybeNamed(panel, BooksKit.PauseName));

        Click(Named(panel, BookPage.StartName));
        Settled(rig);

        Assert.Equal(new BookPlace(0, 0), rig.Player.Now!.Place);
        Assert.Equal(BookRig.First, rig.Player.Now.Text);

        window.Close();
    }

    [AvaloniaFact]
    public void AFinishedBookSaysSoOnItsPageAndOnTheShelf()
    {
        var rig = BookRig.InMemory();
        rig.Shelf.MoveTo(BookRig.Source, new BookPlace(1, 0), finished: true);
        var (window, panel) = Open(rig);

        Assert.Contains("✓ FINISHED", Drawn(panel));

        Click(Named(panel, BooksPage.RowName));

        Assert.Contains("✓ FINISHED · 2 CHAPTERS", Drawn(panel));
        Assert.Null(MaybeNamed(panel, BookPage.ReadOnName));
        Assert.NotNull(MaybeNamed(panel, BookPage.StartName));

        window.Close();
    }

    [AvaloniaFact]
    public void TakingABookOffTheShelfStopsItAtOnceAndGoesBack()
    {
        var rig = BookRig.InMemory();
        rig.Play();
        var (window, panel) = Open(rig);

        Click(Named(panel, BooksPage.RowName));
        Click(Named(panel, BookPage.TakeOffName));
        Settled(rig);

        Assert.Empty(rig.Shelf.Books);
        Assert.Null(rig.Player.Now);
        Assert.Contains("ON YOUR SHELF · 0 BOOKS", Drawn(panel));
        Assert.Contains(BooksPage.EmptyShelf, Drawn(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void ChangeVoiceOpensAPickerOfTheProvidersVoices()
    {
        var rig = BookRig.InMemory();
        var host = HostOf(rig, [new VoiceInfo("bm_george", "George", "en-GB")]);
        var (window, panel) = Open(rig, host: host);

        Click(Named(panel, BooksPage.ChangeVoiceName));

        for (var wait = 0; wait < 300 && !panel.Prompts.IsOpen; wait++)
        {
            Thread.Sleep(10);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(panel.Prompts.IsOpen);

        window.Close();
    }

    [Fact]
    public void ChoosingAProviderClearsTheVoiceOfTheOldOne()
    {
        var rig = BookRig.InMemory();
        rig.Settings = rig.Settings with { Voice = "bm_george" };
        var host = HostOf(rig);

        host.Choose("chatterbox");

        Assert.Equal("chatterbox", rig.Settings.Provider);
        Assert.Null(rig.Settings.Voice);
    }

    [AvaloniaFact]
    public void TheBigPanelListsTheShelfInCompactRows()
    {
        var rig = BookRig.InMemory();
        rig.Play();
        var (window, panel) = Open(rig, headset: true);
        var drawn = Drawn(panel);

        Assert.Contains("The Night Watch", drawn);
        Assert.Contains("CH 1 OF 2 · 0%", drawn);
        Assert.DoesNotContain(drawn, text => text.Contains("from a file", StringComparison.Ordinal));
        Assert.NotNull(MaybeNamed(panel, BooksKit.ChaptersName));
        Assert.NotNull(MaybeNamed(panel, BooksPage.AddName));
        window.SaveFrame("books-headset.png");

        window.Close();
    }
}

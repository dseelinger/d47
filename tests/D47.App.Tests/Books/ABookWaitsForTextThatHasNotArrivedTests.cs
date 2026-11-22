using System.Text;
using D47.App.Panel;
using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Books;
using D47.Core.Storage;
using Xunit;

namespace D47.App.Tests.Books;

/// <summary>
/// In a downloaded book whose text is not whole, a paragraph not yet arrived is waited for while its download runs,
/// and at the end of the text with no download running the book pauses and says the rest has not arrived.
/// </summary>
public sealed class ABookWaitsForTextThatHasNotArrivedTests
{
    private const string Lamp = "The lamp was lit.";
    private const string Night = "Night fell.";
    private const string Tide = "The tide was out.";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private static readonly string Arrived = GutenbergText.Header + Lamp + "\r\n\r\n" + Night + "\r\n\r\n";

    private static readonly string Whole =
        Arrived + Tide + "\r\n\r\n*** END OF THE PROJECT GUTENBERG EBOOK THE NIGHT WATCH ***\r\n";

    private readonly FakeGutenbergMirror _mirror = new();

    private readonly Clock _clock = new();

    private BookRig OnShelfHalfArrived()
    {
        var rig = BookRig.OnShelf(new MemoryFileSystem(), @"C:\d47\data\books", mirror: _mirror, time: _clock);

        rig.Shelf.AddDownload(GutenbergText.Source, "The Night Watch", "H. G. Wellstone");
        rig.Shelf.Arrived(
            GutenbergText.Source,
            Arrived,
            replace: false,
            new BookDownload(false, Encoding.UTF8.GetByteCount(Arrived), "\"v1\""),
            chapters: 1);

        return rig;
    }

    [Fact]
    public async Task AParagraphNotYetArrivedIsWaitedForAndReadOnceItArrives()
    {
        var answer = new TaskCompletionSource<HttpResponseMessage>();
        _mirror.Answer = _ => answer.Task;
        var rig = OnShelfHalfArrived();
        var ended = Ended(rig);

        Assert.True(rig.Player.Play(GutenbergText.Source));
        rig.Settle();

        Assert.True(rig.Downloads!.Downloading(GutenbergText.Source));

        rig.Finish(Lamp);
        rig.Finish(Night);

        var waiting = rig.Player.Now!;
        Assert.Equal(Night, waiting.Text);
        Assert.True(waiting.Waiting);
        Assert.False(waiting.IsPaused);
        Assert.DoesNotContain(Tide, rig.Voices["kokoro"].Said);

        var whole = Encoding.UTF8.GetBytes(Whole);
        answer.SetResult(FakeGutenbergMirror.Partial(whole, Encoding.UTF8.GetByteCount(Arrived), "\"v1\""));
        await ended.WaitAsync(Patience, TestContext.Current.CancellationToken);
        rig.Settle();

        Assert.Equal(Tide, rig.Player.Now!.Text);
        Assert.False(rig.Player.Now.IsPaused);
        Assert.Contains(Tide, rig.Voices["kokoro"].Said);
        Assert.Equal(new BookPlace(0, 2), rig.Shelf.Find(GutenbergText.Source)!.Place);
        Assert.Equal($"bytes={Encoding.UTF8.GetByteCount(Arrived)}-", Assert.Single(_mirror.Requests).Range);
    }

    [Fact]
    public async Task AtTheEndOfTheTextWithNoDownloadItPausesAndSaysTheRestHasNotArrived()
    {
        var rig = OnShelfHalfArrived();
        var ended = Ended(rig);

        rig.Player.Play(GutenbergText.Source);
        rig.Settle();
        await ended.WaitAsync(Patience, TestContext.Current.CancellationToken);
        rig.Settle();

        rig.Finish(Lamp);
        rig.Finish(Night);

        var paused = rig.Player.Now!;
        Assert.Equal(StoryRunPause.Commander, paused.Pause);
        Assert.True(paused.NotArrived);
        Assert.Null(paused.Problem);
        Assert.Equal(BookPlayer.RestNotArrived, BooksKit.PauseSentence(paused));
        Assert.True(paused.ToBookNow().Paused);
        Assert.False(rig.Shelf.Find(GutenbergText.Source)!.Finished);
        Assert.Single(_mirror.Requests);

        rig.Player.Resume();
        rig.Settle();

        Assert.True(rig.Player.Now!.NotArrived);
        Assert.Single(_mirror.Requests);

        _clock.Now += GutenbergBookFetch.ResumeEvery;
        ended = Ended(rig);
        rig.Player.Resume();
        rig.Settle();
        await ended.WaitAsync(Patience, TestContext.Current.CancellationToken);
        rig.Settle();

        Assert.Equal(2, _mirror.Requests.Count);
        Assert.True(rig.Player.Now!.NotArrived);
        Assert.Equal(StoryRunPause.Commander, rig.Player.Now.Pause);
    }

    /// <summary>Completes when the book's download has ended.</summary>
    private static Task Ended(BookRig rig)
    {
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var downloads = rig.Downloads!;
        Action<BookSource>? handler = null;

        handler = source =>
        {
            if (!downloads.Downloading(source))
            {
                downloads.Arrived -= handler;
                ended.TrySetResult();
            }
        };
        downloads.Arrived += handler;

        return ended.Task;
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }
}

using System.Net;
using D47.App.Panel;
using D47.Core.Books;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Books;

/// <summary>
/// A Project Gutenberg book is fetched from gutenberg.pglaf.org onto the shelf as it arrives: whole lines only, ready
/// once its first paragraph is there, and a cut-off download resumed from the bytes written on the same ETag.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AGutenbergBookStreamsOntoTheShelfTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly string _folder = TempFolders.Create("d47-gutenberg-fetch");

    private readonly FakeGutenbergMirror _mirror = new();

    private readonly Clock _clock = new();

    private readonly BookShelf _shelf;

    private readonly GutenbergBookFetch _fetch;

    public AGutenbergBookStreamsOntoTheShelfTests()
    {
        _shelf = new BookShelf(new DiskFileSystem(), _folder, NullLogger.Instance);
        _fetch = new GutenbergBookFetch(_shelf, NullLogger.Instance, _mirror, _clock);
    }

    private string TextFile => Path.Combine(_folder, BookShelf.DownloadFileName(GutenbergText.Number));

    [Fact]
    public void TheTextIsAskedForFromProjectGutenbergsOwnMirrorAndNeverTheWebsite()
    {
        var address = GutenbergBookFetch.TextAddress(84);

        Assert.Equal("https://gutenberg.pglaf.org/cache/epub/84/pg84.txt", address.ToString());
        Assert.Equal(GutenbergBookFetch.Host, address.Host);
        Assert.DoesNotContain("www.gutenberg.org", address.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheFirstParagraphIsOnTheShelfBeforeTheLastPieceIsSentAndTheFinishedFileMatchesTheSource()
    {
        var source = GutenbergText.Bytes(GutenbergText.Text);
        var body = new PieceByPieceBody();
        _mirror.Answer = _ => Task.FromResult(FakeGutenbergMirror.Whole(new StreamContent(body), "\"v1\""));
        var done = Ended();

        var adding = _fetch.AddAsync(GutenbergText.Listed, TestContext.Current.CancellationToken);
        var cut = GutenbergText.Offset(GutenbergText.Text, "Night fell") + 3;
        var inCafe = GutenbergText.Offset(GutenbergText.Text, "café") + 4;
        body.Send(source[..cut]);

        var entry = await adding.WaitAsync(Patience, TestContext.Current.CancellationToken);

        Assert.NotNull(entry);
        Assert.Equal(GutenbergText.Source, entry.Source);
        Assert.Equal("The Night Watch", entry.Title);
        Assert.Equal("Wellstone, H. G.", entry.Author);
        Assert.False(entry.Download!.Complete);
        Assert.Equal(GutenbergText.FirstParagraph, Assert.Single(Assert.Single(_shelf.Open(GutenbergText.Source)!.Chapters).Paragraphs).Text);
        Assert.False(_shelf.IsDownloadText("pg1.txt"));
        Assert.True(_shelf.IsDownloadText(BookShelf.DownloadFileName(GutenbergText.Number)));

        body.Send(source[cut..inCafe]);
        body.Send(source[inCafe..]);
        body.End();
        await done.WaitAsync(Patience, TestContext.Current.CancellationToken);

        var written = File.ReadAllBytes(TextFile);
        var start = GutenbergText.Offset(GutenbergText.Text, GutenbergText.StartLine);

        Assert.Equal(source[start..], written[start..]);
        Assert.Equal(source, written);

        var whole = _shelf.Find(GutenbergText.Source)!;
        Assert.Equal(new BookDownload(true, source.Length, "\"v1\""), whole.Download);
        Assert.Equal(2, whole.Chapters);

        var book = _shelf.Open(GutenbergText.Source)!;
        Assert.Equal(
            [GutenbergText.FirstParagraph, GutenbergText.SecondParagraph, GutenbergText.ThirdParagraph],
            book.Chapters.SelectMany(chapter => chapter.Paragraphs).Select(paragraph => paragraph.Text));

        var asked = Assert.Single(_mirror.Requests);
        Assert.Equal(GutenbergBookFetch.TextAddress(GutenbergText.Number), asked.Uri);
        Assert.Equal("d47-books", asked.UserAgent);
        Assert.Null(asked.Range);
    }

    [Fact]
    public async Task ADownloadCutOffMidLineResumesFromTheLastWholeLineOnTheSameETag()
    {
        var source = GutenbergText.Bytes(GutenbergText.Text);
        var cut = GutenbergText.Offset(GutenbergText.Text, "The tide") + 4;
        var wholeLines = GutenbergText.Offset(GutenbergText.Text, "The tide");
        await CutOffAt(source, cut);

        Assert.Equal(new BookDownload(false, wholeLines, "\"v1\""), _shelf.Find(GutenbergText.Source)!.Download);
        Assert.Equal(source[..wholeLines], File.ReadAllBytes(TextFile));
        Assert.False(_fetch.TryResume(GutenbergText.Source));

        _clock.Now += GutenbergBookFetch.ResumeEvery;
        _mirror.Answer = _ => Task.FromResult(FakeGutenbergMirror.Partial(source, wholeLines, "\"v1\""));
        var done = Ended();

        Assert.True(_fetch.TryResume(GutenbergText.Source));
        await done.WaitAsync(Patience, TestContext.Current.CancellationToken);

        var resumed = _mirror.Requests[^1];
        Assert.Equal($"bytes={wholeLines}-", resumed.Range);
        Assert.Equal("\"v1\"", resumed.IfRange);
        Assert.Equal(source, File.ReadAllBytes(TextFile));
        Assert.Equal(new BookDownload(true, source.Length, "\"v1\""), _shelf.Find(GutenbergText.Source)!.Download);
    }

    [Fact]
    public async Task AWholeAnswerToTheResumeReplacesTheTextAndKeepsThePlace()
    {
        var source = GutenbergText.Bytes(GutenbergText.Text);
        await CutOffAt(source, GutenbergText.Offset(GutenbergText.Text, "CHAPTER II") + 3);
        _shelf.MoveTo(GutenbergText.Source, new BookPlace(0, 1));

        var changed = GutenbergText.Text.Replace("Night fell over the café.", "Night fell over the quay.", StringComparison.Ordinal);
        var revised = GutenbergText.Bytes(changed);
        _clock.Now += GutenbergBookFetch.ResumeEvery;
        _mirror.Answer = _ => Task.FromResult(FakeGutenbergMirror.Whole(new ByteArrayContent(revised), "\"v2\""));
        var done = Ended();

        Assert.True(_fetch.TryResume(GutenbergText.Source));
        await done.WaitAsync(Patience, TestContext.Current.CancellationToken);

        Assert.Equal("\"v1\"", _mirror.Requests[^1].IfRange);
        Assert.Equal(revised, File.ReadAllBytes(TextFile));

        var entry = _shelf.Find(GutenbergText.Source)!;
        Assert.Equal(new BookDownload(true, revised.Length, "\"v2\""), entry.Download);
        Assert.Equal(new BookPlace(0, 1), entry.Place);
        Assert.Equal("Night fell over the quay.", _shelf.Open(GutenbergText.Source)!.Chapters[0].Paragraphs[1].Text);
    }

    [Fact]
    public async Task ABookWithNoParagraphBeforeTheMirrorFailsIsTakenOffTheShelf()
    {
        _mirror.Answer = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

        Assert.Null(await _fetch.AddAsync(GutenbergText.Listed, TestContext.Current.CancellationToken).WaitAsync(Patience, TestContext.Current.CancellationToken));
        Assert.Null(_shelf.Find(GutenbergText.Source));
        Assert.False(File.Exists(TextFile));
    }

    [Fact]
    public async Task ARedirectIsNotFollowed()
    {
        _mirror.Answer = _ =>
        {
            var moved = new HttpResponseMessage(HttpStatusCode.Found);
            moved.Headers.Location = new Uri("https://www.gutenberg.org/cache/epub/4747/pg4747.txt");
            return Task.FromResult(moved);
        };

        Assert.Null(await _fetch.AddAsync(GutenbergText.Listed, TestContext.Current.CancellationToken).WaitAsync(Patience, TestContext.Current.CancellationToken));
        Assert.Single(_mirror.Requests);
    }

    /// <summary>Adds the book and lets the download run to <paramref name="cut"/>, where the connection drops.</summary>
    private async Task CutOffAt(byte[] source, int cut)
    {
        var body = new PieceByPieceBody();
        _mirror.Answer = _ => Task.FromResult(FakeGutenbergMirror.Whole(new StreamContent(body), "\"v1\""));
        var done = Ended();

        var adding = _fetch.AddAsync(GutenbergText.Listed, TestContext.Current.CancellationToken);
        body.Send(source[..cut]);
        body.Cut();

        Assert.NotNull(await adding.WaitAsync(Patience, TestContext.Current.CancellationToken));
        await done.WaitAsync(Patience, TestContext.Current.CancellationToken);
    }

    /// <summary>Completes when the book's download has ended.</summary>
    private Task Ended()
    {
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<BookSource>? handler = null;

        handler = source =>
        {
            if (source == GutenbergText.Source && !_fetch.Downloading(source))
            {
                _fetch.Arrived -= handler;
                ended.TrySetResult();
            }
        };
        _fetch.Arrived += handler;

        return ended.Task;
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }
}

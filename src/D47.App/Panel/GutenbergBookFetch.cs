using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using D47.Core.Books;
using Microsoft.Extensions.Logging;

namespace D47.App.Panel;

/// <summary>
/// Downloads a Project Gutenberg book's text from Project Gutenberg's own mirror onto the shelf as it arrives. Each
/// whole line is appended to the book's <c>pg&lt;n&gt;.txt</c>; a partial last line waits for the rest. A cut-off
/// download is resumed from the bytes written, on the same ETag, at most once a minute per book. One download per book
/// at a time, on the pool. <see cref="Arrived"/> is raised on the download's thread, never while holding a lock.
/// </summary>
public sealed class GutenbergBookFetch
{
    public const string Host = "gutenberg.pglaf.org";

    public const string UserAgent = "d47-books";

    /// <summary>The most bytes of one book's text written; a longer text stops there and stays incomplete.</summary>
    public const long MaxBytes = 32L * 1024 * 1024;

    private const int ChunkBytes = 65_536;

    /// <summary>The shortest time between two resumes of one book.</summary>
    public static readonly TimeSpan ResumeEvery = TimeSpan.FromMinutes(1);

    /// <summary>How long a download may go without a byte before it is treated as cut off.</summary>
    public static readonly TimeSpan Idle = TimeSpan.FromSeconds(60);

    private static readonly UTF8Encoding Utf8 = new(false);

    private readonly BookShelf _shelf;
    private readonly HttpClient _http;
    private readonly TimeProvider _time;
    private readonly Action<Action> _background;
    private readonly ILogger _logger;
    private readonly Lock _gate = new();
    private readonly Dictionary<int, TaskCompletionSource<bool>> _running = [];
    private readonly Dictionary<int, DateTimeOffset> _lastResume = [];
    private readonly HashSet<int> _tooLarge = [];

    /// <param name="handler">The HTTP handler; one that follows no redirect by default.</param>
    /// <param name="background">Runs a download off the calling thread; the thread pool when null.</param>
    public GutenbergBookFetch(
        BookShelf shelf,
        ILogger logger,
        HttpMessageHandler? handler = null,
        TimeProvider? time = null,
        Action<Action>? background = null)
    {
        ArgumentNullException.ThrowIfNull(shelf);
        ArgumentNullException.ThrowIfNull(logger);

        _shelf = shelf;
        _logger = logger;
        _time = time ?? TimeProvider.System;
        _background = background ?? (work => _ = Task.Run(work));
        _http = new HttpClient(handler ?? new SocketsHttpHandler { AllowAutoRedirect = false }, disposeHandler: handler is null)
        {
            Timeout = TimeSpan.FromSeconds(60),
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    /// <summary>Raised when more of a book's text is on the shelf, and when its download ends.</summary>
    public event Action<BookSource>? Arrived;

    /// <summary>Where book <paramref name="number"/>'s plain text is fetched from.</summary>
    public static Uri TextAddress(int number) => new($"https://{Host}/cache/epub/{number}/pg{number}.txt");

    public bool Downloading(BookSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        lock (_gate)
        {
            return source.Gutenberg is { } number && _running.ContainsKey(number);
        }
    }

    /// <summary>
    /// Puts the book on the shelf with the catalogue's title and author and fetches its text. Returns the shelf entry
    /// once its first paragraph is on the shelf, while the rest arrives; null, with the book taken off again, when no
    /// paragraph arrived. A book already on the shelf with text to read is returned at once, its download resumed when
    /// it is incomplete.
    /// </summary>
    public async Task<ShelfBook?> AddAsync(GutenbergBook book, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);

        var source = BookSource.FromGutenberg(book.Id);
        TaskCompletionSource<bool>? ready;

        lock (_gate)
        {
            _running.TryGetValue(book.Id, out ready);
        }

        if (ready is null && _shelf.Find(source) is { } held && _shelf.Open(source) is not null)
        {
            if (held.Download is { Complete: false })
            {
                TryResume(source);
            }

            return held;
        }

        var started = false;

        lock (_gate)
        {
            if (!_running.TryGetValue(book.Id, out ready))
            {
                if (_shelf.AddDownload(source, book.Title, book.Author) is null)
                {
                    return null;
                }

                ready = Register(book.Id);
                started = true;
            }
        }

        if (started)
        {
            Launch(source, ready, adding: true);
        }

        return await ready.Task.WaitAsync(cancellationToken).ConfigureAwait(false) ? _shelf.Find(source) : null;
    }

    /// <summary>
    /// Resumes an incomplete book's download unless one ran in the last minute or the text reached
    /// <see cref="MaxBytes"/>. Whether a download is running now.
    /// </summary>
    public bool TryResume(BookSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Gutenberg is not { } number)
        {
            return false;
        }

        TaskCompletionSource<bool> ready;

        lock (_gate)
        {
            if (_running.ContainsKey(number))
            {
                return true;
            }

            if (_shelf.Find(source)?.Download is not { Complete: false } || _tooLarge.Contains(number))
            {
                return false;
            }

            if (_lastResume.TryGetValue(number, out var last) && _time.GetUtcNow() - last < ResumeEvery)
            {
                return false;
            }

            ready = Register(number);
        }

        Launch(source, ready, adding: false);

        return true;
    }

    /// <summary>Marks a download of book <paramref name="number"/> as running. Called holding <see cref="_gate"/>.</summary>
    private TaskCompletionSource<bool> Register(int number)
    {
        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _running[number] = ready;
        _lastResume[number] = _time.GetUtcNow();

        return ready;
    }

    private void Launch(BookSource source, TaskCompletionSource<bool> ready, bool adding) =>
        _background(() => _ = RunAsync(source, ready, adding));

    private async Task RunAsync(BookSource source, TaskCompletionSource<bool> ready, bool adding)
    {
        try
        {
            await DownloadAsync(source, ready).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogInformation("Project Gutenberg book {Number} could not be fetched ({Error}).", source.Gutenberg, ex.GetType().Name);
        }
        finally
        {
            lock (_gate)
            {
                _running.Remove(source.Gutenberg!.Value);
            }

            if (adding && !ready.Task.IsCompleted)
            {
                _shelf.TakeOff(source);
            }

            ready.TrySetResult(false);

            Arrived?.Invoke(source);
        }
    }

    private async Task DownloadAsync(BookSource source, TaskCompletionSource<bool> ready)
    {
        var number = source.Gutenberg!.Value;

        if (_shelf.Find(source)?.Download is not { Complete: false } download)
        {
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, TextAddress(number));
        EntityTagHeaderValue? tag = null;
        var resuming = download.Written > 0 && EntityTagHeaderValue.TryParse(download.ETag, out tag);

        if (resuming)
        {
            request.Headers.Range = new RangeHeaderValue(download.Written, null);
            request.Headers.IfRange = new RangeConditionHeaderValue(tag!);
        }

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        var text = new Arrival(source, ready);

        switch (response.StatusCode)
        {
            case HttpStatusCode.PartialContent when resuming && response.Content.Headers.ContentRange?.From == download.Written:
                text.ContinueFrom(download, _shelf.DownloadedText(source));
                break;

            case HttpStatusCode.OK:
                text.Replace(response.Headers.ETag?.ToString());
                break;

            case HttpStatusCode.RequestedRangeNotSatisfiable when resuming:
                text.ContinueFrom(download, _shelf.DownloadedText(source));
                Write(text, [], whole: true);
                return;

            default:
                _logger.LogInformation("Project Gutenberg book {Number} was not fetched: the mirror answered {Status}.", number, response.StatusCode);
                return;
        }

        await using var body = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        var buffer = new byte[ChunkBytes];
        var carried = new MemoryStream();

        while (true)
        {
            int read;

            using (var idle = new CancellationTokenSource(Idle, _time))
            {
                read = await body.ReadAsync(buffer, idle.Token).ConfigureAwait(false);
            }

            if (read == 0)
            {
                Write(text, carried.ToArray(), whole: true);
                return;
            }

            carried.Write(buffer, 0, read);
            var held = carried.GetBuffer().AsSpan(0, (int)carried.Length);
            var end = held.LastIndexOf((byte)'\n');

            if (end < 0)
            {
                if (text.Written + held.Length > MaxBytes && TooLarge(number))
                {
                    return;
                }

                continue;
            }

            var lines = held[..(end + 1)].ToArray();
            var rest = held[(end + 1)..].ToArray();

            carried.SetLength(0);
            carried.Write(rest);

            if (!Write(text, lines, whole: false))
            {
                return;
            }
        }
    }

    /// <summary>
    /// Writes whole lines, or the end of the text when <paramref name="whole"/>, to the shelf and feeds them to the
    /// parser. False when the download must stop: the text would pass <see cref="MaxBytes"/>, or the book is off the
    /// shelf or cannot be written.
    /// </summary>
    private bool Write(Arrival text, byte[] bytes, bool whole)
    {
        if (text.Written + bytes.Length > MaxBytes)
        {
            return !TooLarge(text.Source.Gutenberg!.Value);
        }

        var lines = Utf8.GetString(bytes);
        text.Feed(lines, whole);

        var entry = _shelf.Arrived(
            text.Source,
            lines,
            text.Replacing,
            new BookDownload(whole, text.Written + bytes.Length, text.ETag),
            text.Chapters);

        if (entry is null)
        {
            return false;
        }

        text.Wrote(bytes.Length);

        if (text.HasParagraph)
        {
            text.Ready.TrySetResult(true);
        }

        Arrived?.Invoke(text.Source);

        return true;
    }

    /// <summary>Notes that book <paramref name="number"/> is longer than <see cref="MaxBytes"/>; always true.</summary>
    private bool TooLarge(int number)
    {
        _logger.LogInformation("Project Gutenberg book {Number} is larger than {Limit} bytes; the rest is not fetched.", number, MaxBytes);

        lock (_gate)
        {
            _tooLarge.Add(number);
        }

        return true;
    }

    /// <summary>One download's text: what was written, its ETag, and the parser that counts its chapters.</summary>
    private sealed class Arrival(BookSource source, TaskCompletionSource<bool> ready)
    {
        private readonly TextBookParser _parser = new();
        private int _chapters;
        private bool _preface;

        public BookSource Source => source;

        public TaskCompletionSource<bool> Ready => ready;

        public long Written { get; private set; }

        public string? ETag { get; private set; }

        /// <summary>The next write replaces the text on the shelf.</summary>
        public bool Replacing { get; private set; }

        public bool HasParagraph { get; private set; }

        public int Chapters => _chapters + (_preface ? 1 : 0);

        /// <summary>Carries on after <paramref name="sofar"/>, the text already on the shelf.</summary>
        public void ContinueFrom(BookDownload download, string? sofar)
        {
            Written = download.Written;
            ETag = download.ETag;
            Feed(sofar ?? "", whole: false);
        }

        public void Replace(string? etag)
        {
            ETag = etag;
            Replacing = true;
        }

        public void Feed(string lines, bool whole)
        {
            using var reader = new StringReader(lines);

            while (reader.ReadLine() is { } line)
            {
                Count(_parser.Feed(line));
            }

            if (whole)
            {
                Count(_parser.Finish());
            }
        }

        public void Wrote(int bytes)
        {
            Written += bytes;
            Replacing = false;
        }

        private void Count(IReadOnlyList<BookTextPiece> pieces)
        {
            foreach (var piece in pieces)
            {
                if (piece.Kind == BookTextPieceKind.Chapter)
                {
                    _chapters++;
                }
                else
                {
                    HasParagraph = true;
                    _preface |= _chapters == 0;
                }
            }
        }
    }
}

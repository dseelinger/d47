using System.Net;
using System.Net.Http.Headers;
using D47.Core.Audio;

namespace D47.App.Media;

/// <summary>
/// A podcast episode read over HTTP: read-only, each read served from a buffer of <see cref="BufferBytes"/> filled by
/// a GET with a Range header. The first request follows redirects and later ranges go to the URL it ended at; a 403
/// or 410 from that URL asks the enclosure again, once. A server that ignores Range gives a stream that cannot seek,
/// read from the start. A request that fails or is cut short is tried again with backoff for up to
/// <see cref="RetryFor"/>, then the read throws <see cref="IOException"/>. Reads must not overlap.
/// </summary>
internal sealed class HttpRangeStream : Stream
{
    internal const int BufferBytes = 1 << 20;

    internal static readonly TimeSpan RetryFor = TimeSpan.FromSeconds(20);

    internal static readonly TimeSpan FirstRetry = TimeSpan.FromMilliseconds(500);

    internal static readonly TimeSpan EpisodeReadAhead = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan LongestRetry = TimeSpan.FromSeconds(8);

    /// <summary>The longest wait for an answer's headers or for the next part of its body.</summary>
    private static readonly TimeSpan Stall = TimeSpan.FromSeconds(15);

    private readonly HttpClient _http;
    private readonly Uri _enclosure;
    private readonly TimeProvider _time;
    private readonly byte[] _buffer = new byte[BufferBytes];

    /// <summary>Where the last answer came from once redirects were followed; null until one has been read.</summary>
    private Uri? _target;

    private bool _opened;
    private bool _seekable;
    private long? _length;
    private long _position;
    private long _bufferStart;
    private int _bufferCount;

    /// <summary>The whole-file answer of a server that ignores Range, kept open and read in order.</summary>
    private HttpResponseMessage? _whole;
    private Stream? _wholeBody;
    private long _wholeAt;

    private bool _disposed;

    private HttpRangeStream(HttpClient http, Uri enclosure, TimeProvider time)
    {
        _http = http;
        _enclosure = enclosure;
        _time = time;
    }

    public override bool CanRead => !_disposed;

    public override bool CanSeek => _seekable && !_disposed;

    public override bool CanWrite => false;

    public override long Length => _length ?? throw new NotSupportedException("The server did not say how long the episode is.");

    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    /// <summary>Makes the first request, which settles the length, whether the stream can seek and where later ranges go.</summary>
    public static HttpRangeStream Open(HttpClient http, Uri enclosure, TimeProvider? time = null)
    {
        var stream = new HttpRangeStream(http, enclosure, time ?? TimeProvider.System);

        try
        {
            stream.FillWithRetry(0);
            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>An episode as a track. Nothing is requested until it starts to play.</summary>
    public static MusicTrack Episode(string name, Uri enclosure, HttpClient http, IAudioDecoder decoder) =>
        new(name, () => decoder.Open(Open(http, enclosure)), ReadAhead: EpisodeReadAhead);

    /// <summary>A client that keeps no cookies and has no overall timeout; a stalled request is bounded instead.</summary>
    internal static HttpClient CreateClient()
    {
        var client = new HttpClient(new SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = true })
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("d47-podcasts");

        return client;
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (buffer.IsEmpty || (_length is { } length && _position >= length))
        {
            return 0;
        }

        if (!Holds(_position))
        {
            FillWithRetry(_position);

            if (!Holds(_position))
            {
                return 0;
            }
        }

        var at = (int)(_position - _bufferStart);
        var count = Math.Min(buffer.Length, _bufferCount - at);

        _buffer.AsSpan(at, count).CopyTo(buffer);
        _position += count;

        return count;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_seekable)
        {
            throw new NotSupportedException("The server sends this episode only from the start.");
        }

        var target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };

        if (target < 0)
        {
            throw new IOException("A seek cannot go before the start of the episode.");
        }

        _position = target;
        return target;
    }

    public override void Flush()
    {
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _disposed = true;
            DropWhole();
        }

        base.Dispose(disposing);
    }

    private static bool IsTransient(Exception ex) =>
        ex is not RefusedException and (HttpRequestException or IOException or OperationCanceledException);

    private bool Holds(long position) => position >= _bufferStart && position < _bufferStart + _bufferCount;

    private void FillWithRetry(long from)
    {
        long? firstFailure = null;
        var delay = FirstRetry;

        while (true)
        {
            try
            {
                Fill(from);
                return;
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                firstFailure ??= _time.GetTimestamp();

                if (_time.GetElapsedTime(firstFailure.Value) + delay > RetryFor)
                {
                    throw new IOException($"The episode stopped arriving: {ex.Message}", ex);
                }

                Pause(delay);
                delay = delay + delay < LongestRetry ? delay + delay : LongestRetry;
            }
        }
    }

    private void Pause(TimeSpan delay)
    {
        using var done = new ManualResetEventSlim();
        using var timer = _time.CreateTimer(_ => done.Set(), null, delay, Timeout.InfiniteTimeSpan);

        done.Wait();
    }

    private void Fill(long from)
    {
        using var stall = new CancellationTokenSource(Stall);

        if (_wholeBody is { } open && _wholeAt == from)
        {
            try
            {
                Load(open, from, null, stall);
                _wholeAt = from + _bufferCount;
                return;
            }
            catch
            {
                DropWhole();
                throw;
            }
        }

        DropWhole();

        var response = Answer(from, stall.Token);
        var keep = false;

        try
        {
            var body = response.Content.ReadAsStreamAsync(stall.Token).GetAwaiter().GetResult();
            var range = response.StatusCode == HttpStatusCode.PartialContent ? response.Content.Headers.ContentRange : null;

            Skip(body, from - (range?.From ?? 0), stall);
            Load(body, from, range?.To + 1, stall);

            if (!_seekable)
            {
                _whole = response;
                _wholeBody = body;
                _wholeAt = from + _bufferCount;
                keep = true;
            }
        }
        finally
        {
            if (!keep)
            {
                response.Dispose();
            }
        }
    }

    /// <summary>A 200 or 206 answer for the range starting at <paramref name="from"/>, with the stream's length and target settled.</summary>
    private HttpResponseMessage Answer(long from, CancellationToken token)
    {
        var response = Send(_target ?? _enclosure, from, token);

        if (_target is not null && response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Gone)
        {
            response.Dispose();
            _target = null;
            response = Send(_enclosure, from, token);
        }

        var code = response.StatusCode;
        var status = (int)code;

        if (code is not (HttpStatusCode.OK or HttpStatusCode.PartialContent))
        {
            response.Dispose();

            if (status >= 500 || code is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests)
            {
                throw new HttpRequestException($"The podcast's server answered {status}.", null, code);
            }

            throw new RefusedException($"The podcast's server answered {status}.");
        }

        long? total;

        if (response.StatusCode == HttpStatusCode.PartialContent)
        {
            if (response.Content.Headers.ContentRange is not { From: { } start } range
                || start > from
                || range.To < from)
            {
                response.Dispose();
                throw new RefusedException("The podcast's server sent a part of the episode other than the one asked for.");
            }

            total = range.Length;
        }
        else
        {
            total = response.Content.Headers.ContentLength;
        }

        if (!_opened)
        {
            _opened = true;
            _seekable = response.StatusCode == HttpStatusCode.PartialContent && total is not null;
            _length = total;
        }

        _target = response.RequestMessage?.RequestUri ?? _target ?? _enclosure;
        return response;
    }

    private HttpResponseMessage Send(Uri uri, long from, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);

        if (_seekable || !_opened)
        {
            var last = from + BufferBytes - 1;

            if (_length is { } length && last >= length)
            {
                last = length - 1;
            }

            request.Headers.Range = new RangeHeaderValue(from, last);
        }

        return _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).GetAwaiter().GetResult();
    }

    /// <summary>Discards <paramref name="count"/> bytes, for an answer that starts before the range asked for.</summary>
    private void Skip(Stream body, long count, CancellationTokenSource stall)
    {
        while (count > 0)
        {
            var read = ReadPart(body, _buffer.AsMemory(0, (int)Math.Min(BufferBytes, count)), stall);

            if (read == 0)
            {
                throw new IOException("The podcast's server stopped sending before the part asked for.");
            }

            count -= read;
        }
    }

    /// <summary>
    /// Fills the buffer from <paramref name="from"/> up to <paramref name="sentEnd"/>, the end of the range the answer
    /// says it holds, or the episode's length. A body that ends before that is a dropped read.
    /// </summary>
    private void Load(Stream body, long from, long? sentEnd, CancellationTokenSource stall)
    {
        _bufferStart = from;
        _bufferCount = 0;

        var end = sentEnd ?? _length;
        var wanted = end is { } known ? (int)Math.Min(BufferBytes, known - from) : BufferBytes;

        while (_bufferCount < wanted)
        {
            var read = ReadPart(body, _buffer.AsMemory(_bufferCount, wanted - _bufferCount), stall);

            if (read == 0)
            {
                break;
            }

            _bufferCount += read;
        }

        if (end is not null && _bufferCount < wanted)
        {
            throw new IOException("The podcast's server stopped sending partway through.");
        }
    }

    private static int ReadPart(Stream body, Memory<byte> into, CancellationTokenSource stall)
    {
        var read = body.ReadAsync(into, stall.Token).AsTask().GetAwaiter().GetResult();
        stall.CancelAfter(Stall);
        return read;
    }

    private void DropWhole()
    {
        _wholeBody?.Dispose();
        _whole?.Dispose();
        _wholeBody = null;
        _whole = null;
    }

    /// <summary>An answer that trying again will not change.</summary>
    private sealed class RefusedException(string message) : IOException(message);
}

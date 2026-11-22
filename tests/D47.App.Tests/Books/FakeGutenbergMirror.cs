using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Channels;
using D47.Core.Books;

namespace D47.App.Tests.Books;

/// <summary>A request as the mirror saw it.</summary>
internal sealed record MirrorRequest(Uri Uri, string? Range, string? IfRange, string UserAgent);

/// <summary>Answers each request with what <see cref="Answer"/> gives for it, and records every request.</summary>
internal sealed class FakeGutenbergMirror : HttpMessageHandler
{
    public List<MirrorRequest> Requests { get; } = [];

    public Func<HttpRequestMessage, Task<HttpResponseMessage>> Answer { get; set; } =
        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));

    /// <summary>A 200 with <paramref name="etag"/> whose body is <paramref name="body"/>.</summary>
    public static HttpResponseMessage Whole(HttpContent body, string etag)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = body };
        response.Headers.ETag = EntityTagHeaderValue.Parse(etag);

        return response;
    }

    /// <summary>A 206 for the bytes of <paramref name="text"/> from <paramref name="from"/> on.</summary>
    public static HttpResponseMessage Partial(byte[] text, long from, string etag)
    {
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(text[(int)from..]) };
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(from, text.Length - 1, text.Length);
        response.Headers.ETag = EntityTagHeaderValue.Parse(etag);

        return response;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (Requests)
        {
            Requests.Add(new MirrorRequest(
                request.RequestUri!,
                request.Headers.Range?.ToString(),
                request.Headers.IfRange?.ToString(),
                request.Headers.UserAgent.ToString()));
        }

        return Answer(request);
    }
}

/// <summary>A response body the test sends piece by piece, then ends or cuts off.</summary>
internal sealed class PieceByPieceBody : Stream
{
    private static readonly object Ended = new();

    private readonly Channel<object> _pieces = Channel.CreateUnbounded<object>();

    private ReadOnlyMemory<byte> _current;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public void Send(byte[] piece) => _pieces.Writer.TryWrite(piece);

    public void End() => _pieces.Writer.TryWrite(Ended);

    /// <summary>The connection drops: the next read past what was sent throws.</summary>
    public void Cut() => _pieces.Writer.TryWrite(new IOException("The connection was reset."));

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_current.IsEmpty)
        {
            switch (await _pieces.Reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                case byte[] piece:
                    _current = piece;
                    break;

                case IOException cut:
                    throw cut;

                default:
                    return 0;
            }
        }

        var count = Math.Min(buffer.Length, _current.Length);
        _current[..count].CopyTo(buffer);
        _current = _current[count..];

        return count;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer, offset, count).GetAwaiter().GetResult();

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>A small Project Gutenberg text, with its licence header, CRLF line ends and a two-byte character.</summary>
internal static class GutenbergText
{
    public const int Number = 4747;

    public const string StartLine = "*** START OF THE PROJECT GUTENBERG EBOOK THE NIGHT WATCH ***";

    public const string FirstParagraph = "The lamp was lit. The door was shut.";

    public const string SecondParagraph = "Night fell over the café.";

    public const string ThirdParagraph = "The tide was out.";

    public static readonly BookSource Source = BookSource.FromGutenberg(Number);

    public static readonly GutenbergBook Listed = new(Number, "The Night Watch", "Wellstone, H. G.", "en", [], 10, null);

    public static string Header =>
        "The Project Gutenberg eBook of The Night Watch\r\n"
        + "\r\n"
        + "Title: The Night Watch\r\n"
        + "Author: H. G. Wellstone\r\n"
        + "\r\n"
        + StartLine + "\r\n"
        + "\r\n";

    public static string Text =>
        Header
        + "CHAPTER I\r\n\r\n"
        + "The lamp was lit.\r\nThe door was shut.\r\n\r\n"
        + "Night fell over the café.\r\n\r\n"
        + "CHAPTER II\r\n\r\n"
        + ThirdParagraph + "\r\n\r\n"
        + "*** END OF THE PROJECT GUTENBERG EBOOK THE NIGHT WATCH ***\r\n";

    public static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    /// <summary>Where <paramref name="marker"/> starts in the UTF-8 bytes of <paramref name="text"/>.</summary>
    public static int Offset(string text, string marker) => Encoding.UTF8.GetByteCount(text[..text.IndexOf(marker, StringComparison.Ordinal)]);
}

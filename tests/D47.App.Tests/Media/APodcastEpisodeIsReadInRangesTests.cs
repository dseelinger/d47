using System.Net;
using System.Net.Http.Headers;
using D47.App.Media;
using D47.Core.Audio;
using Xunit;

namespace D47.App.Tests.Media;

/// <summary>
/// An episode is read over HTTP in ranges of a megabyte: a seek past the buffer asks for a new range, a redirect is
/// followed once, an expired target is asked for again from the enclosure once, and a dropped read is tried again
/// with backoff before it fails.
/// </summary>
public class APodcastEpisodeIsReadInRangesTests
{
    private static readonly Uri Enclosure = new("https://feeds.example/episode.mp3");
    private static readonly Uri Cdn = new("https://cdn.example/signed/episode.mp3");
    private static readonly Uri FreshCdn = new("https://cdn.example/signed-again/episode.mp3");

    private readonly byte[] _episode = Episode(2_500_000);

    [Fact]
    public void SequentialReadsAreServedFromOneRangeEach()
    {
        var server = new FakeServer(request => Partial(request, _episode));
        using var stream = Open(server);

        Assert.True(stream.CanSeek);
        Assert.Equal(_episode.Length, stream.Length);
        Assert.Equal(_episode, ReadToEnd(stream, 65_536));
        Assert.Equal(["0-1048575", "1048576-2097151", "2097152-2499999"], server.Ranges);
    }

    [Fact]
    public void AServerThatSendsLessThanTheRangeAskedForIsReadToTheEnd()
    {
        var server = new FakeServer(request => Partial(request, _episode, halfRange: true));
        var time = new SkippingTime();
        using var stream = Open(server, time);

        Assert.Equal(_episode, ReadToEnd(stream, 65_536));
        Assert.Equal(["0-1048575", "524288-1572863", "1048576-2097151", "1572864-2499999"], server.Ranges[..4]);
        Assert.Empty(time.Pauses);
    }

    [Fact]
    public void ASeekPastTheBufferAsksForANewRange()
    {
        var server = new FakeServer(request => Partial(request, _episode));
        using var stream = Open(server);

        stream.Position = 1_000;
        Assert.Equal(_episode[1_000..1_010], ReadExactly(stream, 10));
        Assert.Single(server.Ranges);

        stream.Position = 2_000_000;
        Assert.Equal(_episode[2_000_000..2_000_010], ReadExactly(stream, 10));
        Assert.Equal(["0-1048575", "2000000-2499999"], server.Ranges);
    }

    [Fact]
    public void AServerThatIgnoresRangeGivesAStreamThatCannotSeek()
    {
        var server = new FakeServer(request => Whole(request, _episode));
        using var stream = Open(server);

        Assert.False(stream.CanSeek);
        Assert.Throws<NotSupportedException>(() => stream.Seek(10, SeekOrigin.Begin));
        Assert.Equal(_episode, ReadToEnd(stream, 65_536));
        Assert.Single(server.Requests);
    }

    [Fact]
    public void ARedirectIsFollowedOnceAndLaterRangesGoToItsTarget()
    {
        var server = new FakeServer(request => Partial(request, _episode, finalUri: request.RequestUri == Enclosure ? Cdn : null));
        using var stream = Open(server);

        Assert.Equal(_episode, ReadToEnd(stream, 65_536));
        Assert.Equal([Enclosure, Cdn, Cdn], server.Requests);
    }

    [Fact]
    public void AnExpiredTargetIsAskedForAgainFromTheEnclosureOnce()
    {
        var expired = false;
        var server = new FakeServer(request =>
        {
            if (request.RequestUri == Enclosure)
            {
                return Partial(request, _episode, finalUri: expired ? FreshCdn : Cdn);
            }

            if (request.RequestUri == Cdn)
            {
                expired = true;
                return new HttpResponseMessage(HttpStatusCode.Forbidden) { RequestMessage = request };
            }

            return Partial(request, _episode);
        });
        var time = new SkippingTime();
        using var stream = Open(server, time);

        Assert.Equal(_episode, ReadToEnd(stream, 65_536));
        Assert.Equal([Enclosure, Cdn, Enclosure, FreshCdn], server.Requests);
        Assert.Empty(time.Pauses);
    }

    [Fact]
    public void ATargetThatStaysForbiddenFailsAfterOneReResolve()
    {
        var server = new FakeServer(request => request.RequestUri == Enclosure && request.Headers.Range!.Ranges.First().From == 0
            ? Partial(request, _episode, finalUri: Cdn)
            : new HttpResponseMessage(HttpStatusCode.Gone) { RequestMessage = new HttpRequestMessage(HttpMethod.Get, Cdn) });
        var time = new SkippingTime();
        using var stream = Open(server, time);

        stream.Position = 2_000_000;

        Assert.ThrowsAny<IOException>(() => stream.ReadByte());
        Assert.Equal([Enclosure, Cdn, Enclosure], server.Requests);
        Assert.Empty(time.Pauses);
    }

    [Fact]
    public void ADroppedReadIsTriedAgainWithBackoff()
    {
        var failures = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>(
        [
            _ => throw new HttpRequestException("connection reset"),
            request => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { RequestMessage = request },
            request => Partial(request, _episode, cutShort: true),
        ]);
        var server = new FakeServer(request =>
            request.Headers.Range!.Ranges.First().From > 0 && failures.TryDequeue(out var fail) ? fail(request) : Partial(request, _episode));
        var time = new SkippingTime();
        using var stream = Open(server, time);

        Assert.Equal(_episode, ReadToEnd(stream, 65_536));
        Assert.Equal([TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)], time.Pauses);
    }

    [Fact]
    public void AReadThatKeepsFailingGivesUpAfterTwentySeconds()
    {
        var server = new FakeServer(request => request.Headers.Range!.Ranges.First().From > 0
            ? throw new HttpRequestException("connection reset")
            : Partial(request, _episode));
        var time = new SkippingTime();
        using var stream = Open(server, time);

        stream.Position = 2_000_000;

        Assert.Throws<IOException>(() => stream.ReadByte());
        Assert.InRange(time.Pauses.Aggregate(TimeSpan.Zero, (sum, pause) => sum + pause), TimeSpan.FromSeconds(10), HttpRangeStream.RetryFor);
        Assert.Equal(time.Pauses.Count + 2, server.Requests.Count);
    }

    [Fact]
    public void AnEpisodeTrackRequestsNothingUntilItPlaysAndKeepsThirtySecondsAhead()
    {
        var server = new FakeServer(request => Partial(request, _episode));
        using var http = new HttpClient(server);

        var track = HttpRangeStream.Episode("Episode 12", Enclosure, http, new UnusedDecoder());

        Assert.Equal(TimeSpan.FromSeconds(30), track.ReadAhead);
        Assert.Empty(server.Requests);
    }

    private static HttpRangeStream Open(FakeServer server, TimeProvider? time = null) =>
        HttpRangeStream.Open(new HttpClient(server), Enclosure, time ?? new SkippingTime());

    private static byte[] Episode(int length)
    {
        var bytes = new byte[length];
        new Random(2654).NextBytes(bytes);
        return bytes;
    }

    private static byte[] ReadToEnd(Stream stream, int chunk)
    {
        using var all = new MemoryStream();
        var buffer = new byte[chunk];
        int read;

        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            all.Write(buffer, 0, read);
        }

        return all.ToArray();
    }

    private static byte[] ReadExactly(Stream stream, int count)
    {
        var buffer = new byte[count];
        stream.ReadExactly(buffer);
        return buffer;
    }

    /// <summary>The asked range of <paramref name="content"/>, as the handler returns it after following any redirect.</summary>
    private static HttpResponseMessage Partial(
        HttpRequestMessage request, byte[] content, Uri? finalUri = null, bool cutShort = false, bool halfRange = false)
    {
        var range = request.Headers.Range!.Ranges.First();
        var from = range.From!.Value;
        var to = Math.Min(range.To!.Value, content.Length - 1);

        if (halfRange && to > from)
        {
            to = from + ((to - from + 1) / 2) - 1;
        }
        var body = content[(int)from..(int)(to + 1)];

        if (cutShort)
        {
            body = body[..(body.Length / 2)];
        }

        var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new ByteArrayContent(body),
            RequestMessage = finalUri is null ? request : new HttpRequestMessage(HttpMethod.Get, finalUri),
        };

        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(from, to, content.Length);
        return response;
    }

    private static HttpResponseMessage Whole(HttpRequestMessage request, byte[] content) =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent(content), RequestMessage = request };

    /// <summary>Answers each request with the given function and records what was asked.</summary>
    private sealed class FakeServer(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        public List<string> Ranges { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);

            if (request.Headers.Range?.Ranges.First() is { } range)
            {
                Ranges.Add($"{range.From}-{range.To}");
            }

            return Task.FromResult(answer(request));
        }
    }

    /// <summary>A clock that jumps forward by each wait as it starts, recording the waits.</summary>
    private sealed class SkippingTime : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UnixEpoch;

        public List<TimeSpan> Pauses { get; } = [];

        public override DateTimeOffset GetUtcNow() => _now;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _now.UtcTicks;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Pauses.Add(dueTime);
            _now += dueTime;
            callback(state);
            return new Spent();
        }

        private sealed class Spent : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => false;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class UnusedDecoder : IAudioDecoder
    {
        public IReadOnlySet<string> Extensions { get; } = new HashSet<string>();

        public AudioClip Decode(string path, string name) => throw new NotSupportedException();

        public IPcmStream Open(string path) => throw new NotSupportedException();

        public IPcmStream Open(Stream source) => throw new NotSupportedException();
    }
}

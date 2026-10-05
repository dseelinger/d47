using System.Net;
using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

/// <summary><see cref="ElevenLabsTtsProvider.StreamAsync"/> against a handler standing in for the service.</summary>
public class ElevenLabsStreamsTheSameRequestTests
{
    private static readonly byte[] Body = [.. Enumerable.Range(0, 2001).Select(i => (byte)(i * 37))];

    [Fact]
    public async Task TheRequestGoesToStreamWithTheSameBody()
    {
        var handler = new Answering(HttpStatusCode.OK, Body);
        using var provider = Provider(handler);
        var token = TestContext.Current.CancellationToken;

        await provider.SynthesizeAsync("Docking granted.", new VoiceSelection("voice-1"), token);
        var clip = await provider.StreamAsync("Docking granted.", new VoiceSelection("voice-1"), token);
        await clip.Whole;

        Assert.Equal("/v1/text-to-speech/voice-1", handler.Paths[0]);
        Assert.Equal("/v1/text-to-speech/voice-1/stream", handler.Paths[1]);
        Assert.Equal("?output_format=pcm_24000", handler.Queries[1]);
        Assert.Equal(handler.Bodies[0], handler.Bodies[1]);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task ARefusalRaisesTheSameExceptionAsAWholeRequest(HttpStatusCode status)
    {
        const string Said = """{"detail":{"status":"refused","message":"Something was refused."}}""";
        using var provider = Provider(new Answering(status, System.Text.Encoding.UTF8.GetBytes(Said)));
        var token = TestContext.Current.CancellationToken;

        var whole = await Assert.ThrowsAsync<TtsException>(
            () => provider.SynthesizeAsync("Docking granted.", new VoiceSelection("voice-1"), token));
        var streamed = await Assert.ThrowsAsync<TtsException>(
            () => provider.StreamAsync("Docking granted.", new VoiceSelection("voice-1"), token));

        Assert.Equal(whole.Message, streamed.Message);
        Assert.Equal(whole.Fault, streamed.Fault);
    }

    [Fact]
    public async Task ABodyInOddChunksUpsamplesToTheSameBytesAsTheWholeBody()
    {
        using var provider = Provider(new Answering(HttpStatusCode.OK, Body, chunks: [1, 3, 7, 2, 513, 5, 1]));

        var clip = await provider.StreamAsync(
            "Docking granted.", new VoiceSelection("voice-1"), TestContext.Current.CancellationToken);

        Assert.Equal(PcmUpsample.Double(Body), (await clip.Whole).Pcm.ToArray());
    }

    [Fact]
    public void TheUpsamplerMatchesTheWholeBodyForEveryChunkSize()
    {
        var expected = PcmUpsample.Double(Body);

        for (var size = 1; size <= 9; size++)
        {
            var upsampler = new PcmUpsampler();
            var output = new List<byte>();

            for (var at = 0; at < Body.Length; at += size)
            {
                output.AddRange(upsampler.Push(Body.AsSpan(at, Math.Min(size, Body.Length - at))));
            }

            output.AddRange(upsampler.Finish());

            Assert.Equal(expected, output.ToArray());
        }
    }

    [Fact]
    public async Task ABodyThatFailsPartWayFailsTheClip()
    {
        using var provider = Provider(new Answering(HttpStatusCode.OK, Body, chunks: [400], failAfter: 1));

        var clip = await provider.StreamAsync(
            "Docking granted.", new VoiceSelection("voice-1"), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<TtsException>(() => clip.Whole);
    }

    private static ElevenLabsTtsProvider Provider(HttpMessageHandler handler) =>
        new(() => "sk_test", NullLogger<ElevenLabsTtsProvider>.Instance, new HttpClient(handler));

    /// <summary>Answers every request with one status and body, sent in the chunk sizes given.</summary>
    private sealed class Answering(HttpStatusCode status, byte[] body, int[]? chunks = null, int? failAfter = null)
        : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        public List<string> Queries { get; } = [];

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            Queries.Add(request.RequestUri.Query);
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));

            return new HttpResponseMessage(status)
            {
                Content = new StreamContent(new Chunked(body, chunks ?? [body.Length], failAfter)),
            };
        }
    }

    /// <summary>A read stream that hands back at most the next chunk size on each read.</summary>
    private sealed class Chunked(byte[] body, int[] chunks, int? failAfter) : Stream
    {
        private int _position;
        private int _reads;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (failAfter is { } limit && _reads >= limit)
            {
                throw new IOException("the connection was reset");
            }

            var size = Math.Min(Math.Min(count, chunks[_reads % chunks.Length]), body.Length - _position);
            Array.Copy(body, _position, buffer, offset, size);
            _position += size;
            _reads++;
            return size;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

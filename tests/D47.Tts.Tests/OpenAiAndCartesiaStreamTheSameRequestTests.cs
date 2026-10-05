using System.Net;
using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

/// <summary><c>StreamAsync</c> on OpenAI and Cartesia against a handler standing in for the service.</summary>
public class OpenAiAndCartesiaStreamTheSameRequestTests
{
    private static readonly byte[] Body = [.. Enumerable.Range(0, 2001).Select(i => (byte)(i * 37))];

    public static TheoryData<string> Providers => ["openai", "cartesia"];

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task TheRequestIsTheSameAsAWholeRequest(string id)
    {
        var handler = new Answering(HttpStatusCode.OK, Body);
        using var provider = Provider(id, handler);
        var token = TestContext.Current.CancellationToken;

        await provider.SynthesizeAsync("Docking granted.", new VoiceSelection("voice-1"), token);
        var clip = await provider.StreamAsync("Docking granted.", new VoiceSelection("voice-1"), token);
        await clip.Whole;

        Assert.Equal(handler.Uris[0], handler.Uris[1]);
        Assert.Equal(handler.Bodies[0], handler.Bodies[1]);
    }

    [Theory]
    [InlineData("openai", HttpStatusCode.BadRequest)]
    [InlineData("openai", HttpStatusCode.Unauthorized)]
    [InlineData("openai", HttpStatusCode.TooManyRequests)]
    [InlineData("cartesia", HttpStatusCode.BadRequest)]
    [InlineData("cartesia", HttpStatusCode.Unauthorized)]
    [InlineData("cartesia", HttpStatusCode.NotFound)]
    public async Task ARefusalRaisesTheSameExceptionAsAWholeRequest(string id, HttpStatusCode status)
    {
        const string Said = """{"error":{"message":"Something was refused.","param":"voice"}}""";
        using var provider = Provider(id, new Answering(status, System.Text.Encoding.UTF8.GetBytes(Said)));
        var token = TestContext.Current.CancellationToken;

        var whole = await Assert.ThrowsAsync<TtsException>(
            () => provider.SynthesizeAsync("Docking granted.", new VoiceSelection("voice-1"), token));
        var streamed = await Assert.ThrowsAsync<TtsException>(
            () => provider.StreamAsync("Docking granted.", new VoiceSelection("voice-1"), token));

        Assert.Equal(whole.Message, streamed.Message);
        Assert.Equal(whole.Fault, streamed.Fault);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task ABodyInOddChunksUpsamplesToTheSameBytesAsTheWholeBody(string id)
    {
        using var provider = Provider(id, new Answering(HttpStatusCode.OK, Body, chunks: [1, 3, 7, 2, 513, 5, 1]));

        var clip = await provider.StreamAsync(
            "Docking granted.", new VoiceSelection("voice-1"), TestContext.Current.CancellationToken);

        Assert.Equal(PcmUpsample.Double(Body), (await clip.Whole).Pcm.ToArray());
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task ABodyThatFailsPartWayFailsTheClip(string id)
    {
        using var provider = Provider(id, new Answering(HttpStatusCode.OK, Body, chunks: [400], failAfter: 1));

        var clip = await provider.StreamAsync(
            "Docking granted.", new VoiceSelection("voice-1"), TestContext.Current.CancellationToken);

        var failed = await Assert.ThrowsAsync<TtsException>(() => clip.Whole);
        Assert.Equal(TtsFault.Unreachable, failed.Fault);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task AnEmptyBodyFailsTheClip(string id)
    {
        using var provider = Provider(id, new Answering(HttpStatusCode.OK, []));

        var clip = await provider.StreamAsync(
            "Docking granted.", new VoiceSelection("voice-1"), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<TtsException>(() => clip.Whole);
    }

    private static Owned Provider(string id, HttpMessageHandler handler) =>
        id == "openai"
            ? new Owned(new OpenAiTtsProvider(() => "sk_test", NullLogger<OpenAiTtsProvider>.Instance, handler))
            : new Owned(new CartesiaTtsProvider(() => "sk_test", NullLogger<CartesiaTtsProvider>.Instance, handler));

    private sealed class Owned(ITtsProvider provider) : IDisposable
    {
        public Task<AudioClip> SynthesizeAsync(string text, VoiceSelection voice, CancellationToken token) =>
            provider.SynthesizeAsync(text, voice, token);

        public Task<ArrivingClip> StreamAsync(string text, VoiceSelection voice, CancellationToken token) =>
            provider.StreamAsync(text, voice, token);

        public void Dispose() => (provider as IDisposable)?.Dispose();
    }

    /// <summary>Answers every request with one status and body, sent in the chunk sizes given.</summary>
    private sealed class Answering(HttpStatusCode status, byte[] body, int[]? chunks = null, int? failAfter = null)
        : HttpMessageHandler
    {
        public List<string> Uris { get; } = [];

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uris.Add(request.RequestUri!.ToString());
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));

            return new HttpResponseMessage(status)
            {
                Content = new StreamContent(new Chunked(body, chunks ?? [Math.Max(body.Length, 1)], failAfter)),
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

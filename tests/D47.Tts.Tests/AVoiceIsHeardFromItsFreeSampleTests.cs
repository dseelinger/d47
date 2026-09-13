using System.Net;
using System.Text;
using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>An ElevenLabs voice is auditioned from the sample its listing carries, fetched with no key (#106).</summary>
public class AVoiceIsHeardFromItsFreeSampleTests
{
    private const string Listing = """
        {"voices":[
          {"voice_id":"hosted","name":"Roger","preview_url":"https://storage.googleapis.com/eleven-public-prod/premade/voices/hosted/sample.mp3"},
          {"voice_id":"api","name":"Laura","preview_url":"https://api.us.elevenlabs.io/v1/voices/api/previews/audio?payload=abc"},
          {"voice_id":"elsewhere","name":"Mallory","preview_url":"https://example.com/sample.mp3"},
          {"voice_id":"plain","name":"Trent","preview_url":"http://storage.googleapis.com/sample.mp3"},
          {"voice_id":"none","name":"Nobody"}
        ]}
        """;

    private sealed class Recorder : HttpMessageHandler
    {
        public List<HttpRequestMessage> Seen { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen.Add(request);

            return Task.FromResult(
                request.RequestUri!.AbsolutePath.EndsWith("/v1/voices", StringComparison.Ordinal)
                    ? new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(Listing, Encoding.UTF8, "application/json"),
                    }
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });
        }
    }

    [Fact]
    public async Task TheSampleIsKeptOnlyFromTheHostsTheDisclosureNames()
    {
        using var recorder = new Recorder();
        using var http = new HttpClient(recorder);
        using var provider = new ElevenLabsTtsProvider(
            () => "sk_test", NullLogger<ElevenLabsTtsProvider>.Instance, http);

        var voices = (await provider.ListVoicesAsync(TestContext.Current.CancellationToken)).Voices
            .ToDictionary(voice => voice.Id, voice => voice.PreviewUrl);

        Assert.NotNull(voices["hosted"]);
        Assert.NotNull(voices["api"]);
        Assert.Null(voices["elsewhere"]);
        Assert.Null(voices["plain"]);
        Assert.Null(voices["none"]);
    }

    [Fact]
    public async Task TheSampleIsFetchedWithoutTheKey()
    {
        using var recorder = new Recorder();
        using var http = new HttpClient(recorder);
        using var provider = new ElevenLabsTtsProvider(
            () => "sk_test", NullLogger<ElevenLabsTtsProvider>.Instance, http);

        // Three bytes are not an MP3, so it fails to decode — after the request this test is about.
        await Assert.ThrowsAsync<TtsException>(
            () => provider.PreviewAsync("hosted", TestContext.Current.CancellationToken));

        var fetched = recorder.Seen[^1];

        Assert.Equal("storage.googleapis.com", fetched.RequestUri!.Host);
        Assert.False(fetched.Headers.Contains("xi-api-key"));
    }

    [Fact]
    public async Task AVoiceWithNoSampleHasNothingToPlay()
    {
        using var recorder = new Recorder();
        using var http = new HttpClient(recorder);
        using var provider = new ElevenLabsTtsProvider(
            () => "sk_test", NullLogger<ElevenLabsTtsProvider>.Instance, http);

        Assert.Null(await provider.PreviewAsync("none", TestContext.Current.CancellationToken));
        Assert.Null(await provider.PreviewAsync("elsewhere", TestContext.Current.CancellationToken));

        // The listing, and nothing fetched after it.
        Assert.Single(recorder.Seen);
    }

    /// <summary>Live: a real hosted sample decodes to the arbiter's format.</summary>
    [Fact]
    public async Task ARealSampleDecodesToTheArbitersFormat()
    {
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable("D47_TTS_LIVE") == "1",
            "set D47_TTS_LIVE=1 to run tests that contact ElevenLabs");

        using var http = new HttpClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var mp3 = await http.GetByteArrayAsync(
            "https://storage.googleapis.com/eleven-public-prod/premade/voices/CwhRBWXzGAHq8TQ4Fs17/58ee3ff5-f6f2-4628-93b8-e38eb31806b0.mp3",
            timeout.Token);

        var clip = new AudioClip("sample", ElevenLabsTtsProvider.DecodePreview(mp3), AudioFormat.Standard);

        Assert.True(clip.Duration > TimeSpan.FromSeconds(1), $"got {clip.Duration.TotalSeconds:0.00}s of audio");
        Assert.True(clip.Duration < TimeSpan.FromSeconds(60), "implausibly long for a sample");
    }
}

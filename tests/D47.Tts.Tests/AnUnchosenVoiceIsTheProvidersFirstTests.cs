using System.Net;
using System.Text.Json;
using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>A line with no voice chosen is spoken in the provider's first listed voice, fetching the list first where it must (#1050).</summary>
public class AnUnchosenVoiceIsTheProvidersFirstTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenAiSpeaksInAlloy(bool streamed)
    {
        var wire = new Wire(listing: null);

        using var provider = new OpenAiTtsProvider(() => "sk-test", NullLogger<OpenAiTtsProvider>.Instance, wire);

        await SpeakAsync(provider, streamed);

        Assert.Equal("alloy", JsonDocument.Parse(wire.SpeechBody!).RootElement.GetProperty("voice").GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CartesiaFetchesItsListAndSpeaksInTheFirst(bool streamed)
    {
        var wire = new Wire("""[{"id":"first-id","name":"First"},{"id":"second-id","name":"Second"}]""");

        using var provider = new CartesiaTtsProvider(() => "sk-test", NullLogger<CartesiaTtsProvider>.Instance, wire);

        await SpeakAsync(provider, streamed);

        Assert.Equal(
            "first-id",
            JsonDocument.Parse(wire.SpeechBody!).RootElement.GetProperty("voice").GetProperty("id").GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ElevenLabsFetchesItsListAndSpeaksInTheFirst(bool streamed)
    {
        var wire = new Wire("""{"voices":[{"voice_id":"first-id","name":"First","category":"premade"},{"voice_id":"second-id","name":"Second","category":"premade"}]}""");

        using var http = new HttpClient(wire);
        using var provider = new ElevenLabsTtsProvider(() => "sk-test", NullLogger<ElevenLabsTtsProvider>.Instance, http);

        await SpeakAsync(provider, streamed);

        Assert.Contains("/text-to-speech/first-id", wire.SpeechPath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AChosenVoiceIsSpokenWithoutAskingForTheList()
    {
        var wire = new Wire("""[{"id":"first-id","name":"First"}]""");

        using var provider = new CartesiaTtsProvider(() => "sk-test", NullLogger<CartesiaTtsProvider>.Instance, wire);

        await provider.SynthesizeAsync("Shields up.", new VoiceSelection("chosen-id"), TestContext.Current.CancellationToken);

        Assert.Equal(0, wire.Listings);
        Assert.Equal(
            "chosen-id",
            JsonDocument.Parse(wire.SpeechBody!).RootElement.GetProperty("voice").GetProperty("id").GetString());
    }

    private static async Task SpeakAsync(ITtsProvider provider, bool streamed)
    {
        if (streamed)
        {
            var arriving = await provider.StreamAsync("Shields up.", VoiceSelection.Default, TestContext.Current.CancellationToken);
            await arriving.Whole.WaitAsync(TestContext.Current.CancellationToken);
        }
        else
        {
            await provider.SynthesizeAsync("Shields up.", VoiceSelection.Default, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Answers a GET with the voice listing and anything else with a second of silence, recording the speech request.</summary>
    private sealed class Wire(string? listing) : HttpMessageHandler
    {
        public string? SpeechBody { get; private set; }

        public string SpeechPath { get; private set; } = "";

        public int Listings { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                Listings++;

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(listing ?? "[]", System.Text.Encoding.UTF8, "application/json"),
                };
            }

            SpeechPath = request.RequestUri!.AbsolutePath;
            SpeechBody = request.Content is { } content
                ? await content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)
                : null;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[24_000 * 2]),
            };
        }
    }
}

using System.Net;
using System.Text;
using D47.App.Voice;
using D47.Core.Listening;
using D47.Stt;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>One utterance through a hosted hearing provider: each failure's sentence, the key, the probe.</summary>
public class AHostedProviderSaysWhyItHeardNothingTests
{
    private static readonly Utterance Speech = new([0f, 0.5f, -0.5f, 0.25f], 16000);

    /// <summary>Stands in for a hosted transcriber, counting what it was asked.</summary>
    private sealed class FakeTranscriber(bool ready = true, Exception? fails = null, string text = "where am I")
        : ISpeechTranscriber
    {
        public int Asked { get; private set; }

        public string? Model => "fake";

        public bool IsReady => ready;

        public Task<Transcription> TranscribeAsync(
            Utterance utterance,
            IReadOnlyList<string> properNouns,
            CancellationToken cancellationToken = default)
        {
            Asked++;

            return fails is null
                ? Task.FromResult(new Transcription(text))
                : Task.FromException<Transcription>(fails);
        }

        public void Dispose()
        {
        }
    }

    private static Task<HearingOutcome> Hear(FakeTranscriber transcriber, double? probe = null) =>
        Hearing.TranscribeAsync(
            transcriber,
            SttProviderCatalog.Groq,
            () => Task.FromResult(probe),
            Speech,
            ["Shinrarta Dezhra"]);

    [Theory]
    [InlineData(TranscriptionFailure.Unreachable, "I couldn't reach Groq. Say it again, or type it.")]
    [InlineData(TranscriptionFailure.KeyRejected, "Groq refused the key. Check it in Settings.")]
    [InlineData(TranscriptionFailure.RateLimited, "Groq is limiting requests. Try again in a moment.")]
    [InlineData(TranscriptionFailure.Failed, "Groq couldn't transcribe that: audio file is too short.")]
    public async Task EachFailureIsSaidAndNothingIsHeard(TranscriptionFailure reason, string said)
    {
        var failure = new TranscriptionUnavailableException("Groq", reason, "Groq could not transcribe")
        {
            Detail = reason == TranscriptionFailure.Failed ? "audio file is too short" : null,
        };

        var outcome = await Hear(new FakeTranscriber(fails: failure));

        Assert.Equal(said, outcome.Problem);
        Assert.Null(outcome.Kept);
    }

    [Fact]
    public async Task WithNoKeyNothingIsSent()
    {
        var transcriber = new FakeTranscriber(ready: false);
        var probed = false;

        var outcome = await Hearing.TranscribeAsync(
            transcriber,
            SttProviderCatalog.Groq,
            () =>
            {
                probed = true;
                return Task.FromResult<double?>(null);
            },
            Speech,
            []);

        Assert.Equal("Groq needs an API key. Add it in Settings.", outcome.Problem);
        Assert.Equal(0, transcriber.Asked);
        Assert.False(probed);
    }

    [Fact]
    public async Task AProbeAtTheFloorRefusesAHostedTranscript()
    {
        var outcome = await Hear(new FakeTranscriber(), probe: Hearing.NoSpeechFloor);

        Assert.Equal("where am I", outcome.Raw?.Text);
        Assert.Equal(string.Empty, outcome.Kept?.Text);
        Assert.Equal(Hearing.NoSpeechFloor, outcome.RefusedAt);
    }

    [Fact]
    public async Task AProbeBelowTheFloorKeepsIt()
    {
        var outcome = await Hear(new FakeTranscriber(), probe: 0.2);

        Assert.Equal("where am I", outcome.Kept?.Text);
        Assert.Null(outcome.RefusedAt);
        Assert.Null(outcome.Problem);
    }

    [Fact]
    public async Task NoProbeKeepsIt()
    {
        var outcome = await Hear(new FakeTranscriber(), probe: null);

        Assert.Equal("where am I", outcome.Kept?.Text);
    }

    /// <summary>Records the address and the model field of the one request it answers.</summary>
    private sealed class Endpoint : HttpMessageHandler
    {
        public Uri? Address { get; private set; }

        public string? Model { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancel)
        {
            Address = request.RequestUri;

            if (request.Content is MultipartFormDataContent form)
            {
                foreach (var part in form)
                {
                    if (part.Headers.ContentDisposition?.Name?.Trim('"') == "model")
                    {
                        Model = await part.ReadAsStringAsync(cancel);
                    }
                }
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"text":"where am I"}""", Encoding.UTF8, "application/json"),
            };
        }
    }

    [Theory]
    [InlineData(SttProviderCatalog.GroqId, OpenAiCompatibleTranscriber.GroqEndpoint, OpenAiCompatibleTranscriber.GroqModel)]
    [InlineData(SttProviderCatalog.OpenAiId, OpenAiCompatibleTranscriber.OpenAiEndpoint, OpenAiCompatibleTranscriber.OpenAiModel)]
    public async Task EachHostedProviderIsSentToItsOwnEndpoint(string id, string endpoint, string model)
    {
        var provider = SttProviderCatalog.Selected(id);
        var handler = new Endpoint();

        using var transcriber = Hearing.Hosted(provider, () => "sk-test", NullLoggerFactory.Instance, handler);

        var outcome = await Hearing.TranscribeAsync(
            transcriber, provider, () => Task.FromResult<double?>(null), Speech, []);

        Assert.Equal("where am I", outcome.Kept?.Text);
        Assert.Equal(new Uri(endpoint), handler.Address);
        Assert.Equal(model, handler.Model);
        Assert.Equal(model, provider.Model);
    }

    [Fact]
    public async Task WithNoTinyModelInTheFolderTheProbeIsSkipped()
    {
        using var whisper = new WhisperTranscriber(NullLogger<WhisperTranscriber>.Instance);
        var folder = Directory.CreateTempSubdirectory("d47-no-probe").FullName;

        try
        {
            Assert.Null(await whisper.NoSpeechAsync(Speech, folder, TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}

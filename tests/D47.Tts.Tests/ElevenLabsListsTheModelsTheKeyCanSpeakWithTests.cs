using System.Net;
using System.Text;
using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Catalog;
using D47.Tts;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>Lists the shared catalog's speech models, so nothing may run beside it.</summary>
[CollectionDefinition(nameof(SharedSpeechListingCollection), DisableParallelization = true)]
public class SharedSpeechListingCollection;

/// <summary>ElevenLabs' model list, and how a model it lists that the catalog does not name speaks.</summary>
[Collection(nameof(SharedSpeechListingCollection))]
public sealed class ElevenLabsListsTheModelsTheKeyCanSpeakWithTests : IDisposable
{
    private const string Listing = """
        [
          { "model_id": "eleven_v4_turbo", "name": "Eleven v4 Turbo", "can_do_text_to_speech": true },
          { "model_id": "eleven_test", "name": "Eleven Test", "can_do_text_to_speech": true },
          { "model_id": "eleven_english_sts_v2", "name": "Eleven English v2", "can_do_text_to_speech": false }
        ]
        """;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => ModelCatalogSource.Shared.ListSpeech(TtsProviderCatalog.ElevenLabsId, []);

    private static ElevenLabsTtsProvider Provider(HttpMessageHandler handler, string? key = "sk_test", string? model = null) =>
        new(() => key, NullLogger<ElevenLabsTtsProvider>.Instance, new HttpClient(handler), model: () => model);

    [Fact]
    public async Task OnlyTheModelsThatSpeakAreListed()
    {
        var handler = new Answering(HttpStatusCode.OK, Listing);
        using var provider = Provider(handler);

        var listed = await provider.ListModelsAsync(Token);

        Assert.Equal(["eleven_v4_turbo", "eleven_test"], listed.Select(model => model.Id));
        Assert.Equal("Eleven Test", listed[1].Name);
        Assert.Equal("https://api.elevenlabs.io/v1/models", handler.Asked!.RequestUri!.ToString());
        Assert.Equal("sk_test", handler.Asked.Headers.GetValues("xi-api-key").Single());
    }

    [Fact]
    public async Task ARefusedOrKeylessListListsNothing()
    {
        using var refused = Provider(new Answering(HttpStatusCode.Unauthorized, """{ "detail": { "message": "Invalid API key" } }"""));
        Assert.Empty(await refused.ListModelsAsync(Token));

        var keyless = new Answering(HttpStatusCode.OK, Listing);
        using var withoutKey = Provider(keyless, key: null);
        Assert.Empty(await withoutKey.ListModelsAsync(Token));
        Assert.Null(keyless.Asked);
    }

    [Fact]
    public async Task ANewModelIsSentByNameWithNoRateAndNoDirection()
    {
        using (var lister = Provider(new Answering(HttpStatusCode.OK, Listing)))
        {
            ModelCatalogSource.Shared.ListSpeech(TtsProviderCatalog.ElevenLabsId, await lister.ListModelsAsync(Token));
        }

        var speech = new Answering(HttpStatusCode.OK, null);
        using var provider = Provider(speech, model: "eleven_test");

        await provider.SynthesizeAsync("Hull integrity is nominal.", new VoiceSelection("voice-1"), Token);

        var sent = JsonDocument.Parse(speech.Body!).RootElement;

        Assert.Equal("eleven_test", sent.GetProperty("model_id").GetString());
        Assert.False(sent.TryGetProperty("voice_settings", out _));
        Assert.False(provider.ReadsAudioTags);
        Assert.Equal(0, provider.GroupsSentencesUpTo);
    }

    /// <summary>Answers with a JSON body, or with two seconds of silence where there is none.</summary>
    private sealed class Answering(HttpStatusCode status, string? json) : HttpMessageHandler
    {
        public HttpRequestMessage? Asked { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Asked = request;

            if (request.Content is { } content)
            {
                Body = await content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }

            return new HttpResponseMessage(status)
            {
                Content = json is null
                    ? new ByteArrayContent(new byte[24_000 * 2 * 2])
                    : new StringContent(json, Encoding.UTF8, "application/json"),
            };
        }
    }
}

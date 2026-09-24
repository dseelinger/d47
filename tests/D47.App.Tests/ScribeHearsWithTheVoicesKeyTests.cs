using System.Net;
using System.Text;
using D47.Core.Audio;
using D47.Core.Listening;
using D47.Stt;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.App.Tests;

/// <summary>One utterance to ElevenLabs Scribe's <c>/v1/speech-to-text</c>, and each way that can fail.</summary>
public class ScribeHearsWithTheVoicesKeyTests
{
    private const string Key = "xi-test-key";

    private const string Answer = """
        {"language_code":"en","language_probability":0.98,"text":"  dock at Jameson Memorial  ","words":[]}
        """;

    private static readonly Utterance Speech = new([0f, 0.5f, -0.5f, 1f], 16000);

    /// <summary>Answers with a fixed status and body, and keeps the request and its form fields.</summary>
    private sealed class Endpoint(HttpStatusCode status = HttpStatusCode.OK, string body = Answer) : HttpMessageHandler
    {
        public HttpRequestMessage? Last { get; private set; }

        public List<(string Name, string? FileName, string? MediaType, byte[] Value)> Fields { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancel)
        {
            Last = request;

            if (request.Content is MultipartFormDataContent form)
            {
                foreach (var part in form)
                {
                    var disposition = part.Headers.ContentDisposition!;
                    Fields.Add((
                        disposition.Name!.Trim('"'),
                        disposition.FileName?.Trim('"'),
                        part.Headers.ContentType?.MediaType,
                        await part.ReadAsByteArrayAsync(cancel)));
                }
            }

            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }

        public string Value(string name) => Encoding.UTF8.GetString(Fields.Single(field => field.Name == name).Value);

        public IEnumerable<string> Values(string name) =>
            Fields.Where(field => field.Name == name).Select(field => Encoding.UTF8.GetString(field.Value));
    }

    private sealed class Throws(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancel) => throw exception;
    }

    private static ElevenLabsScribeTranscriber Transcriber(
        HttpMessageHandler handler, CapturingLogger? log = null, string? key = Key) =>
        new(() => key, log ?? new CapturingLogger(), handler);

    [Fact]
    public async Task TheFormCarriesTheWavTheModelAndTheOptions()
    {
        var endpoint = new Endpoint();
        using var transcriber = Transcriber(endpoint);

        await transcriber.TranscribeAsync(Speech, [], TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, endpoint.Last!.Method);
        Assert.Equal(ElevenLabsScribeTranscriber.Endpoint, endpoint.Last.RequestUri!.AbsoluteUri);
        Assert.Equal(Key, endpoint.Last.Headers.GetValues("xi-api-key").Single());
        Assert.Null(endpoint.Last.Headers.Authorization);

        var file = endpoint.Fields.Single(field => field.Name == "file");
        Assert.Equal("utterance.wav", file.FileName);
        Assert.Equal("audio/wav", file.MediaType);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(file.Value, 0, 4));
        Assert.Equal(44 + (Speech.Samples.Length * 2), file.Value.Length);

        Assert.Equal("scribe_v2", endpoint.Value("model_id"));
        Assert.Equal("en", endpoint.Value("language_code"));
        Assert.Equal("false", endpoint.Value("tag_audio_events"));
        Assert.Empty(endpoint.Values("keyterms"));
    }

    [Fact]
    public async Task EachNameIsItsOwnKeytermsField()
    {
        var endpoint = new Endpoint();
        using var transcriber = Transcriber(endpoint);

        await transcriber.TranscribeAsync(
            Speech,
            ["Shinrarta Dezhra", " ", "Col 285 Sector AB-C d14-5", "shinrarta dezhra"],
            TestContext.Current.CancellationToken);

        Assert.Equal(["Shinrarta Dezhra", "Col 285 Sector AB-C d14-5"], endpoint.Values("keyterms"));
    }

    [Fact]
    public void KeytermsScribeWouldNotAcceptAreSkipped()
    {
        var kept = ElevenLabsScribeTranscriber.Keyterms(
        [
            "Jameson Memorial",
            new string('x', ElevenLabsScribeTranscriber.KeytermCharacters + 1),
            "one two three four five six",
            "Station [Alpha]",
            "back\\slash",
            "Wregoe TC-X b29-0",
        ]);

        Assert.Equal(["Jameson Memorial", "Wregoe TC-X b29-0"], kept);
    }

    [Fact]
    public void KeytermsStopBeforeTheCountThatRaisesTheMinimumCharge()
    {
        var names = Enumerable.Range(0, 200).Select(i => $"Col 285 Sector AB-C d14-{i}").ToList();

        var kept = ElevenLabsScribeTranscriber.Keyterms(names);

        Assert.Equal(99, kept.Count);
        Assert.Equal(names.Take(99), kept);
    }

    [Fact]
    public async Task TheTextComesBackTrimmed()
    {
        using var transcriber = Transcriber(new Endpoint());

        var result = await transcriber.TranscribeAsync(Speech, [], TestContext.Current.CancellationToken);

        Assert.Equal("dock at Jameson Memorial", result.Text);
        Assert.Equal("scribe_v2", result.Model);
        Assert.Equal("scribe_v2", SttProviderCatalog.ElevenLabs.Model);
    }

    [Theory]
    [InlineData("""{"language_code":"en"}""")]
    [InlineData("not json")]
    public async Task AnAnswerWithNoTextIsAFailure(string body)
    {
        using var transcriber = Transcriber(new Endpoint(body: body));

        var thrown = await Assert.ThrowsAsync<TranscriptionUnavailableException>(
            () => transcriber.TranscribeAsync(Speech, [], TestContext.Current.CancellationToken));

        Assert.Equal(TranscriptionFailure.Failed, thrown.Reason);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, TranscriptionFailure.KeyRejected)]
    [InlineData(HttpStatusCode.Forbidden, TranscriptionFailure.KeyRejected)]
    [InlineData(HttpStatusCode.TooManyRequests, TranscriptionFailure.RateLimited)]
    [InlineData(HttpStatusCode.BadRequest, TranscriptionFailure.Failed)]
    [InlineData(HttpStatusCode.InternalServerError, TranscriptionFailure.Failed)]
    public async Task EachRefusalIsNamedFromDetailMessage(HttpStatusCode status, TranscriptionFailure reason)
    {
        using var transcriber = Transcriber(new Endpoint(
            status, """{"detail":{"status":"invalid_api_key","message":"Invalid API key"}}"""));

        var thrown = await Assert.ThrowsAsync<TranscriptionUnavailableException>(
            () => transcriber.TranscribeAsync(Speech, [], TestContext.Current.CancellationToken));

        Assert.Equal(reason, thrown.Reason);
        Assert.Equal("ElevenLabs", thrown.Provider);
        Assert.Equal("Invalid API key", thrown.Detail);
    }

    [Fact]
    public async Task AValidationErrorIsNamedFromItsFirstMsg()
    {
        using var transcriber = Transcriber(new Endpoint(
            HttpStatusCode.UnprocessableEntity,
            """{"detail":[{"loc":["body","keyterms"],"msg":"Keyterm too long","type":"value_error"}]}"""));

        var thrown = await Assert.ThrowsAsync<TranscriptionUnavailableException>(
            () => transcriber.TranscribeAsync(Speech, [], TestContext.Current.CancellationToken));

        Assert.Equal(TranscriptionFailure.Failed, thrown.Reason);
        Assert.Equal("Keyterm too long", thrown.Detail);
    }

    [Fact]
    public async Task ANetworkFailureIsUnreachable()
    {
        using var transcriber = Transcriber(new Throws(new HttpRequestException("No such host is known.")));

        var thrown = await Assert.ThrowsAsync<TranscriptionUnavailableException>(
            () => transcriber.TranscribeAsync(Speech, [], TestContext.Current.CancellationToken));

        Assert.Equal(TranscriptionFailure.Unreachable, thrown.Reason);
    }

    [Fact]
    public async Task ATimeoutIsUnreachable()
    {
        using var transcriber = Transcriber(new Throws(new TaskCanceledException("The request timed out.")));

        var thrown = await Assert.ThrowsAsync<TranscriptionUnavailableException>(
            () => transcriber.TranscribeAsync(Speech, [], TestContext.Current.CancellationToken));

        Assert.Equal(TranscriptionFailure.Unreachable, thrown.Reason);
    }

    [Fact]
    public async Task NoKeyIsAKeyRejectedWithoutARequest()
    {
        var endpoint = new Endpoint();
        using var transcriber = Transcriber(endpoint, key: null);

        Assert.False(transcriber.IsReady);

        var thrown = await Assert.ThrowsAsync<TranscriptionUnavailableException>(
            () => transcriber.TranscribeAsync(Speech, [], TestContext.Current.CancellationToken));

        Assert.Equal(TranscriptionFailure.KeyRejected, thrown.Reason);
        Assert.Null(endpoint.Last);
    }

    [Theory]
    [InlineData("""{"detail":{"message":"Invalid API key"},"echo":"SECRET-BODY"}""")]
    [InlineData("SECRET-BODY is not JSON")]
    public async Task ARefusedKeysBodyIsNotLogged(string body)
    {
        var log = new CapturingLogger();
        using var transcriber = Transcriber(new Endpoint(HttpStatusCode.Unauthorized, body), log);

        var thrown = await Assert.ThrowsAsync<TranscriptionUnavailableException>(
            () => transcriber.TranscribeAsync(Speech, [], TestContext.Current.CancellationToken));

        Assert.NotEmpty(log.Lines);
        Assert.DoesNotContain(log.Lines, line => line.Contains("SECRET-BODY", StringComparison.Ordinal));
        Assert.DoesNotContain(log.Lines, line => line.Contains(Key, StringComparison.Ordinal));
        Assert.DoesNotContain("SECRET-BODY", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScribeReadsTheKeyTheVoiceStores()
    {
        Assert.Equal(TtsProviderCatalog.ElevenLabs.KeySecretName, SttProviderCatalog.ElevenLabs.KeySecretName);
        Assert.Equal(D47.Tts.ElevenLabsTtsProvider.KeySecretName, SttProviderCatalog.ElevenLabs.KeySecretName);
    }

    private sealed class CapturingLogger : ILogger<ElevenLabsScribeTranscriber>
    {
        private readonly List<string> _lines = [];

        public IReadOnlyList<string> Lines
        {
            get
            {
                lock (_lines)
                {
                    return [.. _lines];
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (_lines)
            {
                _lines.Add(formatter(state, exception) + exception);
            }
        }
    }
}

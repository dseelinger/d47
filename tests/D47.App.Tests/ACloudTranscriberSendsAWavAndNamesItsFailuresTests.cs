using System.Buffers.Binary;
using System.Net;
using System.Text;
using D47.Core.Listening;
using D47.Stt;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.App.Tests;

/// <summary>One utterance to an OpenAI-compatible transcription endpoint, and each way that can fail.</summary>
public class ACloudTranscriberSendsAWavAndNamesItsFailuresTests
{
    private const string Key = "sk-test-key";

    private static readonly Utterance Speech = new([0f, 0.5f, -0.5f, 1f, -1f, 2f], 16000);

    /// <summary>Answers with a fixed status and body, and keeps each multipart field of the request.</summary>
    private sealed class Endpoint(HttpStatusCode status = HttpStatusCode.OK, string body = """{"text":"  Jameson Memorial  "}""")
        : HttpMessageHandler
    {
        public Dictionary<string, byte[]> Fields { get; } = [];

        public HttpRequestMessage? Last { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancel)
        {
            Last = request;

            if (request.Content is MultipartFormDataContent form)
            {
                foreach (var part in form)
                {
                    var name = part.Headers.ContentDisposition!.Name!.Trim('"');
                    Fields[name] = await part.ReadAsByteArrayAsync(cancel);
                }
            }

            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }

        public string Field(string name) => Encoding.UTF8.GetString(Fields[name]);
    }

    private sealed class Throws(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancel) => throw exception;
    }

    private sealed class NeverAnswers : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancel)
        {
            await Task.Delay(Timeout.Infinite, cancel);
            throw new InvalidOperationException("unreachable");
        }
    }

    private static OpenAiCompatibleTranscriber Transcriber(
        HttpMessageHandler handler, CapturingLogger? log = null, string? key = Key) =>
        new(
            "Groq",
            () => key,
            OpenAiCompatibleTranscriber.GroqEndpoint,
            OpenAiCompatibleTranscriber.GroqModel,
            log ?? new CapturingLogger(),
            handler);

    [Fact]
    public async Task TheRequestCarriesTheFormFieldsAndTheKey()
    {
        var endpoint = new Endpoint();
        using var transcriber = Transcriber(endpoint);

        var result = await transcriber.TranscribeAsync(Speech, ["Shinrarta Dezhra", " Jameson Memorial "], TestContext.Current.CancellationToken);

        Assert.Equal("Jameson Memorial", result.Text);
        Assert.Equal(OpenAiCompatibleTranscriber.GroqModel, result.Model);
        Assert.Equal(1, result.Confidence);
        Assert.True(result.Elapsed > TimeSpan.Zero);

        Assert.Equal(new Uri(OpenAiCompatibleTranscriber.GroqEndpoint), endpoint.Last!.RequestUri);
        Assert.Equal("Bearer", endpoint.Last.Headers.Authorization!.Scheme);
        Assert.Equal(Key, endpoint.Last.Headers.Authorization.Parameter);

        Assert.Equal(OpenAiCompatibleTranscriber.GroqModel, endpoint.Field("model"));
        Assert.Equal("en", endpoint.Field("language"));
        Assert.Equal("json", endpoint.Field("response_format"));
        Assert.Equal("Shinrarta Dezhra, Jameson Memorial", endpoint.Field("prompt"));
        Assert.True(endpoint.Fields.ContainsKey("file"));
    }

    [Fact]
    public async Task TheFileIsA16BitMonoWavAtTheUtterancesOwnRate()
    {
        var endpoint = new Endpoint();
        using var transcriber = Transcriber(endpoint);

        await transcriber.TranscribeAsync(new Utterance(Speech.Samples, 22050), [], TestContext.Current.CancellationToken);

        var wav = endpoint.Fields["file"];

        Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal(1, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(20)));
        Assert.Equal(1, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(22)));
        Assert.Equal(22050, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(24)));
        Assert.Equal(16, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(34)));
        Assert.Equal(Speech.Samples.Length * 2, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(40)));
        Assert.Equal(44 + (Speech.Samples.Length * 2), wav.Length);

        // Full scale, and an out-of-range sample clamped rather than wrapped.
        Assert.Equal(short.MaxValue, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(44 + (3 * 2))));
        Assert.Equal(short.MaxValue, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(44 + (5 * 2))));
    }

    [Fact]
    public async Task NoNamesSendsNoPrompt()
    {
        var endpoint = new Endpoint();
        using var transcriber = Transcriber(endpoint);

        await transcriber.TranscribeAsync(Speech, [" ", ""], TestContext.Current.CancellationToken);

        Assert.False(endpoint.Fields.ContainsKey("prompt"));
    }

    [Fact]
    public void ThePromptKeepsWholeNamesFromTheFrontWithinTheWindow()
    {
        var names = Enumerable.Range(0, 100).Select(i => $"Col 285 Sector AB-C d14-{i}").ToList();

        var prompt = OpenAiCompatibleTranscriber.Prompt(names)!;

        Assert.True(prompt.Length <= OpenAiCompatibleTranscriber.PromptCharacters);
        Assert.StartsWith("Col 285 Sector AB-C d14-0, ", prompt, StringComparison.Ordinal);

        var kept = prompt.Split(", ");
        Assert.Equal(names.Take(kept.Length), kept);
        Assert.True(prompt.Length + 2 + names[kept.Length].Length > OpenAiCompatibleTranscriber.PromptCharacters);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, TranscriptionFailure.KeyRejected)]
    [InlineData(HttpStatusCode.Forbidden, TranscriptionFailure.KeyRejected)]
    [InlineData(HttpStatusCode.TooManyRequests, TranscriptionFailure.RateLimited)]
    [InlineData(HttpStatusCode.BadRequest, TranscriptionFailure.Failed)]
    [InlineData(HttpStatusCode.InternalServerError, TranscriptionFailure.Failed)]
    public async Task EachRefusalIsNamed(HttpStatusCode status, TranscriptionFailure reason)
    {
        using var transcriber = Transcriber(
            new Endpoint(status, """{"error":{"message":"Nope.","type":"x"}}"""));

        var thrown = await Assert.ThrowsAsync<TranscriptionUnavailableException>(
            () => transcriber.TranscribeAsync(Speech, [], TestContext.Current.CancellationToken));

        Assert.Equal(reason, thrown.Reason);
        Assert.Equal("Groq", thrown.Provider);
        Assert.Contains("Nope.", thrown.Message, StringComparison.Ordinal);
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
        using var transcriber = Transcriber(endpoint, key: "");

        Assert.False(transcriber.IsReady);

        var thrown = await Assert.ThrowsAsync<TranscriptionUnavailableException>(
            () => transcriber.TranscribeAsync(Speech, [], TestContext.Current.CancellationToken));

        Assert.Equal(TranscriptionFailure.KeyRejected, thrown.Reason);
        Assert.Null(endpoint.Last);
    }

    [Fact]
    public void AStoredKeyIsReadyAndTheModelIsReported()
    {
        using var transcriber = Transcriber(new Endpoint());

        Assert.True(transcriber.IsReady);
        Assert.Equal(OpenAiCompatibleTranscriber.GroqModel, transcriber.Model);
    }

    [Fact]
    public async Task CancellingIsACancellationNotAFailure()
    {
        using var transcriber = Transcriber(new NeverAnswers());
        using var cancel = new CancellationTokenSource();

        var pending = transcriber.TranscribeAsync(Speech, [], cancel.Token);
        await cancel.CancelAsync();

        var thrown = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.IsNotType<TranscriptionUnavailableException>(thrown);
    }

    [Theory]
    [InlineData("""{"error":{"message":"Invalid API Key"},"echo":"SECRET-BODY"}""")]
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

    private sealed class CapturingLogger : ILogger<OpenAiCompatibleTranscriber>
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

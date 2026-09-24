using System.Net;
using System.Text;
using System.Web;
using D47.Core.Listening;
using D47.Stt;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.App.Tests;

/// <summary>One utterance to Deepgram's <c>/v1/listen</c>, and each way that can fail.</summary>
public class DeepgramHearsFromTheQueryStringAndSaysHowSureItIsTests
{
    private const string Key = "dg-test-key";

    private const string Answer = """
        {"metadata":{"request_id":"x"},"results":{"channels":[{"alternatives":[
          {"transcript":"  dock at Jameson Memorial  ","confidence":0.42,"words":[]},
          {"transcript":"dog at Jameson Memorial","confidence":0.2}]}]}}
        """;

    private static readonly Utterance Speech = new([0f, 0.5f, -0.5f, 1f], 16000);

    /// <summary>Answers with a fixed status and body, and keeps the request and its body.</summary>
    private sealed class Endpoint(HttpStatusCode status = HttpStatusCode.OK, string body = Answer) : HttpMessageHandler
    {
        public HttpRequestMessage? Last { get; private set; }

        public byte[] Body { get; private set; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancel)
        {
            Last = request;
            Body = await request.Content!.ReadAsByteArrayAsync(cancel);

            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }

    private sealed class Throws(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancel) => throw exception;
    }

    private static DeepgramTranscriber Transcriber(
        HttpMessageHandler handler, CapturingLogger? log = null, string? key = Key) =>
        new(() => key, log ?? new CapturingLogger(), handler);

    [Fact]
    public async Task TheOptionsAndOneKeytermPerNameAreInTheQuery()
    {
        var endpoint = new Endpoint();
        using var transcriber = Transcriber(endpoint);

        await transcriber.TranscribeAsync(
            Speech, ["Shinrarta Dezhra", " ", "Col 285 Sector AB-C d14-5"], TestContext.Current.CancellationToken);

        var uri = endpoint.Last!.RequestUri!;
        var query = HttpUtility.ParseQueryString(uri.Query);

        Assert.Equal(HttpMethod.Post, endpoint.Last.Method);
        Assert.Equal(DeepgramTranscriber.Endpoint, uri.GetLeftPart(UriPartial.Path));
        Assert.Equal("nova-3", query["model"]);
        Assert.Equal("en", query["language"]);
        Assert.Equal("true", query["smart_format"]);
        Assert.Equal(["Shinrarta Dezhra", "Col 285 Sector AB-C d14-5"], query.GetValues("keyterm")!);
        Assert.Contains("keyterm=Shinrarta%20Dezhra", uri.AbsoluteUri, StringComparison.Ordinal);

        Assert.Equal("Token", endpoint.Last.Headers.Authorization!.Scheme);
        Assert.Equal(Key, endpoint.Last.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task TheBodyIsTheWavItself()
    {
        var endpoint = new Endpoint();
        using var transcriber = Transcriber(endpoint);

        await transcriber.TranscribeAsync(Speech, [], TestContext.Current.CancellationToken);

        Assert.Equal("audio/wav", endpoint.Last!.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(endpoint.Body, 0, 4));
        Assert.Equal(44 + (Speech.Samples.Length * 2), endpoint.Body.Length);
        Assert.DoesNotContain("keyterm", endpoint.Last.RequestUri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public void KeytermsStopBeforeTheUrlReachesTwoThousandCharacters()
    {
        var names = Enumerable.Range(0, 200).Select(i => $"Col 285 Sector AB-C d14-{i}").ToList();

        var uri = DeepgramTranscriber.Address(names);
        var kept = HttpUtility.ParseQueryString(uri.Query).GetValues("keyterm")!;

        Assert.True(uri.AbsoluteUri.Length < 2000);
        Assert.Equal(names.Take(kept.Length), kept);
        Assert.InRange(kept.Length, 1, names.Count - 1);

        var next = "&keyterm=" + Uri.EscapeDataString(names[kept.Length]);
        Assert.True(uri.AbsoluteUri.Length + next.Length > DeepgramTranscriber.MostUrlCharacters);
    }

    [Fact]
    public async Task TheFirstAlternativesTranscriptAndConfidenceComeBack()
    {
        using var transcriber = Transcriber(new Endpoint());

        var result = await transcriber.TranscribeAsync(Speech, [], TestContext.Current.CancellationToken);

        Assert.Equal("dock at Jameson Memorial", result.Text);
        Assert.Equal(0.42, result.Confidence);
        Assert.Equal("nova-3", result.Model);
        Assert.Equal("nova-3", SttProviderCatalog.Deepgram.Model);
    }

    [Fact]
    public async Task SilenceIsAnEmptyTranscriptNotAFailure()
    {
        using var transcriber = Transcriber(new Endpoint(
            body: """{"results":{"channels":[{"alternatives":[{"transcript":"","confidence":0}]}]}}"""));

        var result = await transcriber.TranscribeAsync(Speech, [], TestContext.Current.CancellationToken);

        Assert.True(result.IsEmpty);
    }

    [Theory]
    [InlineData("""{"results":{"channels":[]}}""")]
    [InlineData("""{"results":{"channels":[{"alternatives":[{"confidence":0.9}]}]}}""")]
    [InlineData("not json")]
    public async Task AnAnswerWithNoTranscriptIsAFailure(string body)
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
    public async Task EachRefusalIsNamedFromErrMsg(HttpStatusCode status, TranscriptionFailure reason)
    {
        // The shape Deepgram returned for a real 401 on 2026-09-24.
        using var transcriber = Transcriber(new Endpoint(
            status, """{"err_code":"INVALID_AUTH","err_msg":"Invalid credentials.","request_id":"x"}"""));

        var thrown = await Assert.ThrowsAsync<TranscriptionUnavailableException>(
            () => transcriber.TranscribeAsync(Speech, [], TestContext.Current.CancellationToken));

        Assert.Equal(reason, thrown.Reason);
        Assert.Equal("Deepgram", thrown.Provider);
        Assert.Equal("Invalid credentials.", thrown.Detail);
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
    [InlineData("""{"err_msg":"Invalid credentials.","echo":"SECRET-BODY"}""")]
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

    private sealed class CapturingLogger : ILogger<DeepgramTranscriber>
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

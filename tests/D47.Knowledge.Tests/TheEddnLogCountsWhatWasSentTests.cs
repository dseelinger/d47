using System.Net;
using System.Text.Json.Nodes;
using D47.Core.Contribution;
using D47.Knowledge;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.Knowledge.Tests;

public class TheEddnLogCountsWhatWasSentTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private sealed class Lines : ILogger<EddnUploader>
    {
        public List<(LogLevel Level, string Text)> Entries { get; } = [];

        public IEnumerable<string> Information => Entries.Where(e => e.Level == LogLevel.Information).Select(e => e.Text);

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    private sealed class Handler(Func<HttpStatusCode> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(respond()) { Content = new StringContent("FAIL: no") });
    }

    private static EddnEnvelope Message(string schema) =>
        new($"https://eddn.edcd.io/schemas/{schema}", new JsonObject(), new JsonObject());

    private static (EddnUploader Uploader, Lines Log) Uploader(Func<HttpStatusCode> respond, Func<bool>? enabled = null)
    {
        var log = new Lines();

        return (new EddnUploader(enabled ?? (() => true), log, new HttpClient(new Handler(respond))), log);
    }

    [Fact]
    public async Task AcceptedMessagesAreCountedBySchemaAndTheFirstIsLogged()
    {
        var (uploader, log) = Uploader(() => HttpStatusCode.OK);
        uploader.Post(Message("journal/1/test"));
        uploader.Post(Message("journal/1/test"));
        uploader.Post(Message("commodity/3/test"));

        await uploader.ProcessAsync(T0, TestContext.Current.CancellationToken);
        uploader.Dispose();

        var lines = log.Information.ToList();
        Assert.Equal(
            [
                "EDDN accepted the first message of this session (journal/1/test)",
                "EDDN this session: 3 accepted (commodity/3/test 1, journal/1/test 2), 0 refused, 0 dropped",
            ],
            lines);
    }

    [Fact]
    public async Task ARefusalIsCountedAsRefused()
    {
        var (uploader, log) = Uploader(() => HttpStatusCode.BadRequest);
        uploader.Post(Message("journal/1"));

        await uploader.ProcessAsync(T0, TestContext.Current.CancellationToken);
        uploader.Dispose();

        Assert.Equal(["EDDN this session: 0 accepted (), 1 refused, 0 dropped"], log.Information);
        Assert.Single(log.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task AMessageThatFailsThreeTimesIsCountedAsDropped()
    {
        var (uploader, log) = Uploader(() => HttpStatusCode.ServiceUnavailable);
        uploader.Post(Message("journal/1"));

        await uploader.ProcessAsync(T0, TestContext.Current.CancellationToken);
        await uploader.ProcessAsync(T0.AddSeconds(60), TestContext.Current.CancellationToken);
        await uploader.ProcessAsync(T0.AddSeconds(120), TestContext.Current.CancellationToken);
        uploader.Dispose();

        Assert.Equal(["EDDN this session: 0 accepted (), 0 refused, 1 dropped"], log.Information);
    }

    [Fact]
    public async Task ASessionWithSharingOffSaysNothingWasSent()
    {
        var (uploader, log) = Uploader(() => HttpStatusCode.OK, () => false);
        uploader.Post(Message("journal/1"));

        await uploader.ProcessAsync(T0, TestContext.Current.CancellationToken);
        uploader.Dispose();

        Assert.Equal(["EDDN this session: nothing was sent"], log.Information);
    }
}

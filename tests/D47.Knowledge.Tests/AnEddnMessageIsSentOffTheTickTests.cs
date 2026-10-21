using System.Net;
using System.Text.Json.Nodes;
using D47.Core.Contribution;
using D47.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

public class AnEddnMessageIsSentOffTheTickTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private sealed class Handler(Func<string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        public Version? Version { get; private set; }

        public Uri? Uri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Bodies.Add(body);
            Version = request.Version;
            Uri = request.RequestUri;

            return respond(body);
        }
    }

    private static EddnEnvelope Message(string name) =>
        new("https://eddn.edcd.io/schemas/commodity/3", new JsonObject(), new JsonObject { ["stationName"] = name });

    private static (EddnUploader Uploader, Handler Handler) Uploader(Func<string, HttpResponseMessage> respond, Func<bool>? enabled = null)
    {
        var handler = new Handler(respond);

        return (new EddnUploader(enabled ?? (() => true), NullLogger<EddnUploader>.Instance, new HttpClient(handler)), handler);
    }

    private static HttpResponseMessage Status(HttpStatusCode code, string body = "") =>
        new(code) { Content = new StringContent(body) };

    [Fact]
    public async Task A200IsSentOnceToTheUploadAddress()
    {
        var (uploader, handler) = Uploader(_ => Status(HttpStatusCode.OK));
        uploader.Post(Message("A"));

        await uploader.ProcessAsync(T0, TestContext.Current.CancellationToken);
        await uploader.ProcessAsync(T0.AddMinutes(10), TestContext.Current.CancellationToken);

        Assert.Single(handler.Bodies);
        Assert.Equal("https://eddn.edcd.io:4430/upload/", handler.Uri!.ToString());
        Assert.Equal(new Version(1, 1), handler.Version);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.UpgradeRequired)]
    public async Task ARefusalIsNotRetried(HttpStatusCode code)
    {
        var (uploader, handler) = Uploader(_ => Status(code, "FAIL: bad schema\nmore"));
        uploader.Post(Message("A"));

        await uploader.ProcessAsync(T0, TestContext.Current.CancellationToken);
        await uploader.ProcessAsync(T0.AddHours(1), TestContext.Current.CancellationToken);

        Assert.Single(handler.Bodies);
    }

    [Fact]
    public async Task A503IsRetriedNoSoonerThanSixtySecondsAndAtMostThreeTimes()
    {
        var (uploader, handler) = Uploader(_ => Status(HttpStatusCode.ServiceUnavailable));
        uploader.Post(Message("A"));

        await uploader.ProcessAsync(T0, TestContext.Current.CancellationToken);
        await uploader.ProcessAsync(T0.AddSeconds(59), TestContext.Current.CancellationToken);
        Assert.Single(handler.Bodies);

        await uploader.ProcessAsync(T0.AddSeconds(60), TestContext.Current.CancellationToken);
        Assert.Equal(2, handler.Bodies.Count);

        await uploader.ProcessAsync(T0.AddSeconds(119), TestContext.Current.CancellationToken);
        Assert.Equal(2, handler.Bodies.Count);

        await uploader.ProcessAsync(T0.AddSeconds(120), TestContext.Current.CancellationToken);
        await uploader.ProcessAsync(T0.AddHours(1), TestContext.Current.CancellationToken);
        Assert.Equal(3, handler.Bodies.Count);
    }

    [Fact]
    public async Task AMessageBehindARetryIsSentMeanwhile()
    {
        var (uploader, handler) = Uploader(body => body.Contains("\"A\"")
            ? Status(HttpStatusCode.ServiceUnavailable)
            : Status(HttpStatusCode.OK));
        uploader.Post(Message("A"));
        await uploader.ProcessAsync(T0, TestContext.Current.CancellationToken);

        uploader.Post(Message("B"));
        await uploader.ProcessAsync(T0.AddSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(2, handler.Bodies.Count);
        Assert.Contains("\"B\"", handler.Bodies[1]);
    }

    [Fact]
    public async Task AFullQueueDropsTheOldest()
    {
        var (uploader, handler) = Uploader(_ => Status(HttpStatusCode.OK));

        for (var i = 0; i <= EddnUploader.Capacity; i++)
        {
            uploader.Post(Message($"M{i}"));
        }

        await uploader.ProcessAsync(T0, TestContext.Current.CancellationToken);

        Assert.Equal(EddnUploader.Capacity, handler.Bodies.Count);
        Assert.DoesNotContain(handler.Bodies, body => body.Contains("\"M0\""));
        Assert.Contains("\"M100\"", handler.Bodies[^1]);
    }

    [Fact]
    public async Task TurningTheSettingOffEmptiesTheQueueAndSendsNothing()
    {
        var on = true;
        var (uploader, handler) = Uploader(_ => Status(HttpStatusCode.ServiceUnavailable), () => on);
        uploader.Post(Message("A"));
        await uploader.ProcessAsync(T0, TestContext.Current.CancellationToken);
        uploader.Post(Message("B"));

        on = false;
        await uploader.ProcessAsync(T0.AddMinutes(5), TestContext.Current.CancellationToken);
        on = true;
        await uploader.ProcessAsync(T0.AddMinutes(10), TestContext.Current.CancellationToken);

        Assert.Single(handler.Bodies);
    }
}

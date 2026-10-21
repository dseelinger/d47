using System.Net;
using System.Text;
using D47.Core.Knowledge;
using D47.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>RavenColonialPlanService against synthesised answers; no network.</summary>
public class ARavenColonialPlanIsReadByNameAndCountedTests
{
    private const string Sites = """
        [
          {"id":"x1","name":"Alpha","bodyNum":"2","buildType":"coriolis","status":"build","buildId":"b-1","marketId":"3958082818"},
          {"id":"x2","name":"Bravo","bodyNum":3,"buildType":"outpost","status":"plan"},
          {"id":"x3","name":"Charlie","bodyNum":2,"status":"complete","marketId":42}
        ]
        """;

    private const string Bodies = """[{"name":"Sys A 1","num":"2"},{"name":"Sys A 2","num":3}]""";

    private const string Project = """
        {
          "buildId":"b-1","complete":false,"isPrimaryPort":true,
          "commodities":{"computercomponents":"120","steel":0,"water":15},
          "linkedFC":[{"marketId":"1","name":"AAA-111","displayName":"SECRET CARRIER","assign":[]},{"marketId":2,"name":"BBB-222","displayName":"OTHER","assign":[]}],
          "commanders":{"CMDR One":["steel"],"CMDR Two":[]},
          "notes":"meet in the bar","discordLink":"https://discord.example/secret",
          "architectName":"CMDR Architect","timeDue":"2026-12-01T10:00:00+00:00"
        }
        """;

    private const string Carriers = """{"1":{"computercomponents":"30","steel":5},"2":{"$computercomponents_name;":10,"water":0}}""";

    private sealed class Fake(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.OriginalString);

            return Task.FromResult(answer(request));
        }
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Answer(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath;

        return path switch
        {
            _ when path.EndsWith("/sites", StringComparison.Ordinal) => Json(Sites),
            _ when path.EndsWith("/bodies", StringComparison.Ordinal) => Json(Bodies),
            _ when path.EndsWith("/fc", StringComparison.Ordinal) => Json(Carriers),
            _ => Json(Project),
        };
    }

    private static RavenColonialPlanService Service(Fake handler) =>
        new(NullLogger<RavenColonialPlanService>.Instance, new HttpClient(handler), () => DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task NumbersAsIntegersOrStringsAreReadAlike()
    {
        var plan = await Service(new Fake(Answer)).SystemAsync("Sys A", CancellationToken.None);

        var alpha = Assert.IsType<SystemPlan>(plan).Sites[0];

        Assert.Equal(2, alpha.BodyNum);
        Assert.Equal("Sys A 1", alpha.BodyName);
        Assert.Equal(3958082818, alpha.MarketId);
        Assert.Equal(120, alpha.Needed["computercomponents"]);
        Assert.Equal(15, alpha.Needed["water"]);
        Assert.DoesNotContain("steel", alpha.Needed.Keys);
        Assert.Equal(42, plan.Sites[2].MarketId);
        Assert.Equal(DateTimeOffset.UnixEpoch, plan.FetchedAt);
    }

    [Fact]
    public async Task CarrierCargoFoldsToTheJournalsSymbol()
    {
        var plan = await Service(new Fake(Answer)).SystemAsync("Sys A", CancellationToken.None);

        var alpha = plan!.Sites[0];

        Assert.Equal(40, alpha.OnLinkedCarriers["computercomponents"]);
        Assert.Equal(5, alpha.OnLinkedCarriers["steel"]);
        Assert.Equal(
            D47.Core.Journal.JournalJson.Symbol("$computercomponents_name;"),
            D47.Core.Journal.JournalJson.Symbol("computercomponents"));
    }

    [Fact]
    public async Task ASiteStillInPlanHasNoBuildIdAndNoNeeds()
    {
        var handler = new Fake(Answer);
        var plan = await Service(handler).SystemAsync("Sys A", CancellationToken.None);

        var bravo = plan!.Sites[1];

        Assert.Equal(PlanStatus.Plan, bravo.Status);
        Assert.Null(bravo.BuildId);
        Assert.Empty(bravo.Needed);
        Assert.Single(handler.Requests, request => request.Contains("api/project/b-1", StringComparison.Ordinal) && request.EndsWith("b-1", StringComparison.Ordinal));
        Assert.Equal(2, handler.Requests.Count(request => request.Contains("api/project/", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CommandersAndCarriersAreCountedAndNoNameOrNoteIsKept()
    {
        var plan = await Service(new Fake(Answer)).SystemAsync("Sys A", CancellationToken.None);

        var alpha = plan!.Sites[0];

        Assert.Equal(2, alpha.Commanders);
        Assert.Equal(2, alpha.LinkedCarriers);
        Assert.True(alpha.IsPrimaryPort);
        Assert.Equal(DateTimeOffset.Parse("2026-12-01T10:00:00+00:00"), alpha.Due);

        var text = string.Join(' ', plan.Sites.Select(site => site.ToString()))
            + string.Join(' ', alpha.Needed.Keys.Concat(alpha.OnLinkedCarriers.Keys));

        foreach (var left in new[] { "CMDR One", "CMDR Two", "CMDR Architect", "meet in the bar", "discord", "SECRET CARRIER", "AAA-111" })
        {
            Assert.DoesNotContain(left, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("Sol A", "api/v2/system/Sol%20A/sites")]
    [InlineData("Barnard's Star", "api/v2/system/Barnard%27s%20Star/sites")]
    public async Task ASystemNameIsEscapedInThePath(string name, string path)
    {
        var handler = new Fake(Answer);

        await Service(handler).SystemAsync(name, CancellationToken.None);

        Assert.Contains(handler.Requests, request => request.EndsWith(path, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task ASystemThePlannerDoesNotKnowIsNull(HttpStatusCode status)
    {
        var handler = new Fake(_ => new HttpResponseMessage(status));

        Assert.Null(await Service(handler).SystemAsync("Nowhere", CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ANonSuccessAnswerIsUnavailable(HttpStatusCode status)
    {
        var handler = new Fake(_ => new HttpResponseMessage(status));

        await Assert.ThrowsAsync<ColonisationPlanUnavailableException>(
            () => Service(handler).SystemAsync("Sys A", CancellationToken.None));
    }

    [Fact]
    public async Task AnUnparseableAnswerIsUnavailable()
    {
        var handler = new Fake(_ => Json("not json"));

        await Assert.ThrowsAsync<ColonisationPlanUnavailableException>(
            () => Service(handler).SystemAsync("Sys A", CancellationToken.None));
    }

    [Fact]
    public async Task ANetworkFailureIsUnavailable()
    {
        var handler = new Fake(_ => throw new HttpRequestException("down"));

        await Assert.ThrowsAsync<ColonisationPlanUnavailableException>(
            () => Service(handler).SystemAsync("Sys A", CancellationToken.None));
    }

    [Fact]
    public async Task AFailureOnALaterCallIsUnavailable()
    {
        var handler = new Fake(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/fc", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : Answer(request));

        await Assert.ThrowsAsync<ColonisationPlanUnavailableException>(
            () => Service(handler).SystemAsync("Sys A", CancellationToken.None));
    }

    [Fact]
    public async Task ATimeoutIsUnavailableButACancelledCallIsNot()
    {
        using var timesOut = new RavenColonialPlanService(
            NullLogger<RavenColonialPlanService>.Instance,
            new HttpClient(new HangingHandler()),
            now: null,
            budget: TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<ColonisationPlanUnavailableException>(
            () => timesOut.SystemAsync("Sys A", CancellationToken.None));

        // The budget is far longer than the caller's 50 ms, so the caller's cancellation is the one that fires.
        using var outlasts = new RavenColonialPlanService(
            NullLogger<RavenColonialPlanService>.Instance,
            new HttpClient(new HangingHandler()),
            now: null,
            budget: TimeSpan.FromMinutes(5));

        using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => outlasts.SystemAsync("Sys A", cancelled.Token));
    }

    private sealed class HangingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}

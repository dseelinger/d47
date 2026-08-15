using System.Net;
using System.Text;
using System.Text.Json;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>
/// The Inara envelope, driven against recorded answers. Every shape here comes from
/// <c>https://inara.cz/elite/inara-api-docs/</c> as read on 2026-08-15, including the worked
/// example whose <c>tierMax</c> is zero.
/// <para>
/// The one that would fail silently in the game is the status code: this API answers HTTP 200
/// and puts the real outcome inside the body, so a rejected key looks exactly like an empty
/// board to anything reading the transport.
/// </para>
/// </summary>
public class InaraCommunityGoalServiceTests
{
    private sealed class Recorder(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        public List<Uri> Urls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri!);

            if (request.Content is not null)
            {
                Requests.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }

    private static InaraCommunityGoalService Service(Recorder recorder, string? key = "abc123") =>
        new(() => key, "1.2.3", NullLogger<InaraCommunityGoalService>.Instance, new HttpClient(recorder));

    private const string OneGoal =
        """
        { "header": { "eventStatus": 200 },
          "events": [ { "eventStatus": 200, "eventData": [ {
            "communitygoalName": "Operation Andronicus",
            "starsystemName": "Pleiades Sector IR-W d1-55",
            "stationName": "The Oracle",
            "goalExpiry": "2026-11-09T16:00:00Z",
            "tierReached": 4,
            "tierMax": 0,
            "contributorsNum": 408,
            "contributionsTotal": 2838230000,
            "isCompleted": false,
            "lastUpdate": "2026-11-08T14:10:05Z",
            "goalObjectiveText": "Hand in Pilots Federation Combat Bonds",
            "goalRewardText": "",
            "goalDescriptionText": "With Thargoid attacks becoming a regular occurrence in the Pleiades Nebula, Aegis has leveraged its considerable reserves to fund a military operation.",
            "inaraURL": "https://inara.cz/galaxy-communitygoals/" } ] } ] }
        """;

    [Fact]
    public async Task TheRequestNamesTheEventAndCarriesTheKeyAndNothingAboutTheCommander()
    {
        var recorder = new Recorder(HttpStatusCode.OK, OneGoal);

        await Service(recorder).RecentAsync(TestContext.Current.CancellationToken);

        var sent = Assert.Single(recorder.Requests);

        using var body = JsonDocument.Parse(sent);
        var header = body.RootElement.GetProperty("header");

        Assert.Equal("d47", header.GetProperty("appName").GetString());
        Assert.Equal("1.2.3", header.GetProperty("appVersion").GetString());
        Assert.Equal("abc123", header.GetProperty("APIkey").GetString());
        Assert.False(header.GetProperty("isBeingDeveloped").GetBoolean());

        // The disclosure says the request says nothing about the Commander, and this is what
        // makes that true rather than merely intended. Both fields are optional and the site
        // recommends sending them; this event is a read of a public board and needs neither.
        Assert.False(header.TryGetProperty("commanderName", out _));
        Assert.False(header.TryGetProperty("commanderFrontierID", out _));

        var sentEvent = Assert.Single(body.RootElement.GetProperty("events").EnumerateArray());

        Assert.Equal("getCommunityGoalsRecent", sentEvent.GetProperty("eventName").GetString());
        Assert.Equal(JsonValueKind.Array, sentEvent.GetProperty("eventData").ValueKind);
        Assert.Equal(InaraCommunityGoalService.Endpoint, Assert.Single(recorder.Urls).ToString());
    }

    [Fact]
    public async Task AGoalIsProjectedToTheFieldsWorthSayingAndNoOthers()
    {
        var report = await Service(new Recorder(HttpStatusCode.OK, OneGoal))
            .RecentAsync(TestContext.Current.CancellationToken);

        var goal = Assert.Single(report.Goals);

        Assert.Equal("Operation Andronicus", goal.Name);
        Assert.Equal("The Oracle", goal.StationName);
        Assert.Equal(4, goal.TierReached);
        Assert.Equal(408, goal.Contributors);
        Assert.Equal(2838230000, goal.ContributionsTotal);
        Assert.Equal("Hand in Pilots Federation Combat Bonds", goal.Objective);

        // Zero is the site saying it does not know — the journals carry no maximum tier, so an
        // entry built from a journal upload has zero there. Reading it literally would announce
        // a goal whose top tier is zero.
        Assert.Null(goal.TopTier);

        // An empty string is not a reward.
        Assert.Null(goal.Reward);
    }

    [Fact]
    public async Task ARejectedKeyIsAFailureRatherThanAnEmptyBoard()
    {
        // The load-bearing one. HTTP 200 with a 400 inside is how this API says "no": reading
        // the transport code would report a bad key as "no community goals are running", which
        // is the one wrong answer here that looks exactly like a right one.
        var recorder = new Recorder(
            HttpStatusCode.OK,
            """{ "header": { "eventStatus": 400, "eventStatusText": "Invalid API key." }, "events": [] }""");

        var failure = await Assert.ThrowsAsync<CommunityGoalsUnavailableException>(
            () => Service(recorder).RecentAsync(TestContext.Current.CancellationToken));

        Assert.Contains("Invalid API key", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnEventLevelFailureIsAlsoAFailure()
    {
        var recorder = new Recorder(
            HttpStatusCode.OK,
            """
            { "header": { "eventStatus": 200 },
              "events": [ { "eventStatus": 400, "eventStatusText": "Something was wrong." } ] }
            """);

        await Assert.ThrowsAsync<CommunityGoalsUnavailableException>(
            () => Service(recorder).RecentAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NoResultsIsAnAnswerRatherThanAFault()
    {
        // 204 is documented as a soft error: formally fine, nothing found. An empty board is
        // true, and it is a different thing from the site refusing.
        var recorder = new Recorder(
            HttpStatusCode.OK,
            """{ "header": { "eventStatus": 200 }, "events": [ { "eventStatus": 204 } ] }""");

        var report = await Service(recorder).RecentAsync(TestContext.Current.CancellationToken);

        Assert.Empty(report.Goals);
    }

    [Fact]
    public async Task WithNoKeyNothingIsSent()
    {
        var recorder = new Recorder(HttpStatusCode.OK, OneGoal);
        var service = Service(recorder, key: null);

        Assert.False(service.IsConfigured);

        await Assert.ThrowsAsync<CommunityGoalsUnavailableException>(
            () => service.RecentAsync(TestContext.Current.CancellationToken));

        Assert.Empty(recorder.Requests);
    }

    [Fact]
    public async Task AnHttpFailureBecomesASentenceRatherThanAnHttpException()
    {
        var recorder = new Recorder(HttpStatusCode.TooManyRequests, "");

        var failure = await Assert.ThrowsAsync<CommunityGoalsUnavailableException>(
            () => Service(recorder).RecentAsync(TestContext.Current.CancellationToken));

        Assert.Contains("rate limiting", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheGoalsGalnetCopyNeverLeavesTheImplementation()
    {
        // Several hundred words of third-party prose per goal, on its way into a model's
        // context. Dropped at the seam rather than trimmed, because nobody asked for it.
        var report = await Service(new Recorder(HttpStatusCode.OK, OneGoal))
            .RecentAsync(TestContext.Current.CancellationToken);

        var goal = Assert.Single(report.Goals);

        Assert.All(
            new[] { goal.Name, goal.Objective, goal.Reward, goal.SystemName, goal.StationName },
            field => Assert.DoesNotContain("Thargoid attacks becoming", field ?? "", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FreeTextIsCappedRatherThanTrusted()
    {
        var recorder = new Recorder(
            HttpStatusCode.OK,
            $$"""
              { "header": { "eventStatus": 200 },
                "events": [ { "eventStatus": 200, "eventData": [
                  { "communitygoalName": "Long one", "goalObjectiveText": "{{new string('x', 5000)}}" } ] } ] }
              """);

        var goal = Assert.Single(
            (await Service(recorder).RecentAsync(TestContext.Current.CancellationToken)).Goals);

        Assert.NotNull(goal.Objective);
        Assert.True(goal.Objective!.Length < 400, $"The objective came through at {goal.Objective.Length} characters.");
    }
}

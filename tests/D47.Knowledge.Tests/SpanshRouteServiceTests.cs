using System.Net;
using System.Text;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>
/// The job-and-poll protocol, driven against recorded answers. Everything asserted here was
/// measured against the live service on 2026-08-14 — the shapes, the parameter names the service
/// silently drops, and the fact that "no route exists" arrives as a completed job.
/// </summary>
public class SpanshRouteServiceTests
{
    /// <summary>Answers a scripted sequence and records what it was sent.</summary>
    private sealed class Recorder(params (HttpStatusCode Status, string Body)[] answers) : HttpMessageHandler
    {
        private int _next;

        public List<string> Requests { get; } = [];

        public List<string> Paths { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);

            if (request.Content is not null)
            {
                Requests.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            var (status, body) = answers[Math.Min(_next++, answers.Length - 1)];

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }

    private static SpanshRouteService Service(Recorder recorder, out List<TimeSpan> waits)
    {
        var recorded = new List<TimeSpan>();
        waits = recorded;

        return new SpanshRouteService(
            NullLogger<SpanshRouteService>.Instance,
            new HttpClient(recorder) { BaseAddress = new Uri("https://spansh.co.uk/") },

            // No real time passes. The poll loop is the thing under test, and a test that took
            // ninety seconds to prove the ninety-second budget would never be run.
            (wait, _) =>
            {
                recorded.Add(wait);
                return Task.CompletedTask;
            });
    }

    private const string Queued = """{"job":"JOB-1","status":"queued"}""";

    private static RouteQuery Route()
    {
        Assert.True(RouteQuery.TryParse("Sol", "Colonia", 50, 60, out var query, out _));
        return query;
    }

    [Fact]
    public async Task APlotIsSubmittedAndThenPolledUntilItStopsBeingQueued()
    {
        var recorder = new Recorder(
            (HttpStatusCode.Accepted, Queued),
            (HttpStatusCode.OK, Queued),
            (HttpStatusCode.OK,
                """
                {"status":"ok","result":{"source_system":"Sol","destination_system":"Colonia","distance":22000.47,
                 "system_jumps":[{"system":"Sol","jumps":0,"neutron_star":false},
                                 {"system":"PSR J1752-2806","jumps":10,"neutron_star":true,"distance_left":21629.39}]}}
                """));

        using var service = Service(recorder, out var waits);

        var route = await service.PlotAsync(Route(), TestContext.Current.CancellationToken);

        Assert.NotNull(route);
        Assert.Equal("Colonia", route.Destination);
        Assert.Equal(10, route.TotalJumps);

        // The origin waypoint carries zero jumps and is dropped: it is a line about where the
        // Commander already is.
        var waypoint = Assert.Single(route.Waypoints);
        Assert.Equal("PSR J1752-2806", waypoint.System);
        Assert.True(waypoint.IsNeutron);

        Assert.Equal(["/api/route", "/api/results/JOB-1", "/api/results/JOB-1"], recorder.Paths);
        Assert.Single(waits);
    }

    [Fact]
    public async Task ACompletedJobWithNoRouteIsNullRatherThanAnOutage()
    {
        // "Unable to find route" arrives as a finished job whose status is failed. Treating it as
        // an outage would tell the Commander to retry something that fails identically every time.
        var recorder = new Recorder(
            (HttpStatusCode.Accepted, Queued),
            (HttpStatusCode.OK, """{"status":"failed","error":"Unable to find route"}"""));

        using var service = Service(recorder, out _);

        Assert.Null(await service.PlotAsync(Route(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AJobThatNeverFinishesBecomesASentenceWithSomethingToDoAboutIt()
    {
        var recorder = new Recorder((HttpStatusCode.Accepted, Queued), (HttpStatusCode.OK, Queued));

        using var service = Service(recorder, out var waits);

        var failure = await Assert.ThrowsAsync<GalaxyUnavailableException>(
            () => service.PlotAsync(Route(), TestContext.Current.CancellationToken));

        Assert.Contains("still working", failure.Message, StringComparison.Ordinal);
        Assert.Contains("fewer hops", failure.Message, StringComparison.Ordinal);

        // Bounded: ninety seconds of budget at a second and a half a poll.
        Assert.Equal(60, waits.Count);
    }

    [Fact]
    public async Task ARefusalQuotesTheServicesOwnReasonBecauseItIsTheUsefulPart()
    {
        // "Could not find finishing system" is a better answer than "the plotter refused that",
        // and unlike the search endpoints the plotters actually say what was wrong.
        var recorder = new Recorder(
            (HttpStatusCode.BadRequest, """{"error":"Could not find finishing system"}"""));

        using var service = Service(recorder, out _);

        var failure = await Assert.ThrowsAsync<GalaxyUnavailableException>(
            () => service.PlotAsync(Route(), TestContext.Current.CancellationToken));

        Assert.Contains("Could not find finishing system", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnEndlessRefusalMessageIsCutDownBeforeItReachesTheModel()
    {
        // Third-party text on its way into a prompt. It is one short clause today and nothing
        // guarantees it stays that way.
        var recorder = new Recorder(
            (HttpStatusCode.BadRequest, $$"""{"error":"{{new string('x', 500)}}"}"""));

        using var service = Service(recorder, out _);

        var failure = await Assert.ThrowsAsync<GalaxyUnavailableException>(
            () => service.PlotAsync(Route(), TestContext.Current.CancellationToken));

        Assert.True(failure.Message.Length < 250, failure.Message.Length.ToString());
    }

    [Fact]
    public async Task ATradePlotSendsTheParameterNamesTheServiceActuallyKeeps()
    {
        // The route endpoints echo back only the keys they understood, which is how these names
        // were established. `capital` and `cargo_capacity` are both silently dropped, and a trade
        // plot with no capital finds nothing affordable — a wrong answer that looks like a right
        // one about a poor Commander.
        var recorder = new Recorder(
            (HttpStatusCode.Accepted, Queued),
            (HttpStatusCode.OK, """{"status":"ok","result":[]}"""));

        using var service = Service(recorder, out _);

        Assert.True(TradeQuery.TryParse(
            "Sol", "Abraham Lincoln", 50_000_000, 384, 4, 40, 1000, false, 720, out var query, out _));

        await service.PlotTradeAsync(query, TestContext.Current.CancellationToken);

        var sent = Assert.Single(recorder.Requests)
            .Split('&')
            .ToDictionary(pair => pair.Split('=')[0], pair => pair.Split('=')[1], StringComparer.Ordinal);

        Assert.Equal("50000000", sent["starting_capital"]);
        Assert.Equal("384", sent["max_cargo"]);
        Assert.DoesNotContain("capital", sent.Keys);
        Assert.DoesNotContain("cargo_capacity", sent.Keys);
    }

    [Fact]
    public async Task ARichesStopWithNothingToScanIsNotAStop()
    {
        // The plotter includes the origin, and a loop includes the return leg. Both come back
        // with an empty body list and would read as "stop here and scan nothing".
        var recorder = new Recorder(
            (HttpStatusCode.Accepted, Queued),
            (HttpStatusCode.OK,
                """
                {"status":"ok","result":[
                  {"name":"Sol","jumps":1,"bodies":[]},
                  {"name":"Aries Dark Region FW-W d1-59","jumps":3,"bodies":[
                    {"name":"A 4","subtype":"Water world","estimated_mapping_value":971799,
                     "distance_to_arrival":759.29,"is_terraformable":true}]},
                  {"name":"Sol","jumps":3,"bodies":[]}]}
                """));

        using var service = Service(recorder, out _);

        Assert.True(RichesQuery.TryParse("Sol", 50, 500, 10, 500_000, 10_000, true, out var query, out _));

        var riches = await service.PlotRichesAsync(query, TestContext.Current.CancellationToken);

        var stop = Assert.Single(riches!.Stops);
        Assert.Equal("Aries Dark Region FW-W d1-59", stop.System);
        Assert.Equal(971_799, riches.TotalValue);
    }
}

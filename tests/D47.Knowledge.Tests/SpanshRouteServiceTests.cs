using System.Net;
using System.Text;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

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

            // No real time passes.
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

        // The origin waypoint carries zero jumps and is dropped.
        var waypoint = Assert.Single(route.Waypoints);
        Assert.Equal("PSR J1752-2806", waypoint.System);
        Assert.True(waypoint.IsNeutron);

        Assert.Equal(["/api/route", "/api/results/JOB-1", "/api/results/JOB-1"], recorder.Paths);
        Assert.Single(waits);
    }

    [Fact]
    public async Task ACompletedJobWithNoRouteIsNullRatherThanAnOutage()
    {
        // "Unable to find route" arrives as a finished job whose status is failed.
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
        // Unlike the search endpoints, the plotters say what was wrong.
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
        // Third-party text on its way into a prompt.
        var recorder = new Recorder(
            (HttpStatusCode.BadRequest, $$"""{"error":"{{new string('x', 500)}}"}"""));

        using var service = Service(recorder, out _);

        var failure = await Assert.ThrowsAsync<GalaxyUnavailableException>(
            () => service.PlotAsync(Route(), TestContext.Current.CancellationToken));

        Assert.True(failure.Message.Length < 250, failure.Message.Length.ToString());
    }

    [Fact]
    public async Task ARichesStopWithNothingToScanIsNotAStop()
    {
        // The plotter includes the origin, and a loop includes the return leg.
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

    [Fact]
    public async Task AnExobiologyPlotCarriesSpeciesAndWhatEachPays()
    {
        var recorder = new Recorder(
            (HttpStatusCode.Accepted, Queued),
            (HttpStatusCode.OK,
                """
                {"status":"ok","result":[
                  {"name":"Sol","jumps":1,"bodies":[]},
                  {"name":"Opet","jumps":3,"bodies":[
                    {"name":"Opet 7 b","subtype":"Rocky body","type":"Planet",
                     "distance_to_arrival":2536.772878,"estimated_scan_value":500,
                     "estimated_mapping_value":2221,"landmark_value":6904100,
                     "landmarks":[
                       {"count":1,"subtype":"Frutexa Flabellum","type":"Frutexa","value":1808900},
                       {"count":1,"subtype":"Tussock Cultro","type":"Tussock","value":1766600},
                       {"count":1,"subtype":"Fungoida Setisis","type":"Fungoida","value":1670100},
                       {"count":1,"subtype":"Bacterium Alcyoneum","type":"Bacterium","value":1658500}]}]}]}
                """));

        using var service = Service(recorder, out _);

        Assert.True(ExobiologyQuery.TryParse(
            "Sol", 50, 200, 3, 1_000_000, 10_000, false, out var query, out _));

        var route = await service.PlotExobiologyAsync(query, TestContext.Current.CancellationToken);

        // The origin comes back as a stop with no bodies.
        var stop = Assert.Single(route!.Stops);
        Assert.Equal("Opet", stop.System);

        var body = Assert.Single(stop.Bodies);
        Assert.Equal("Opet 7 b", body.Name);
        Assert.Equal(6_904_100, body.LandmarkValue);

        // The species, which is the half SAASignalsFound cannot supply — the game names the genus and stops,
        // and the species is what carries the price.
        Assert.Equal(4, body.Species.Count);
        Assert.Equal("Frutexa Flabellum", body.Species[0].Name);
        Assert.Equal("Frutexa", body.Species[0].Genus);
        Assert.Equal(1_808_900, body.Species[0].Value);

        // Reported rather than summed, and the two agree.
        Assert.Equal(body.LandmarkValue, body.Species.Sum(species => species.Value));
    }

    /// <summary><c>from</c> is required, works, and is echoed back as <c>source</c>.</summary>
    [Fact]
    public async Task TheExobiologyPlotSendsFromRatherThanSource()
    {
        var recorder = new Recorder(
            (HttpStatusCode.Accepted, Queued),
            (HttpStatusCode.OK, """{"status":"ok","result":[]}"""));

        using var service = Service(recorder, out _);

        Assert.True(ExobiologyQuery.TryParse(
            "Sol", 50, 200, 10, 1_000_000, 10_000, true, out var query, out _));

        await service.PlotExobiologyAsync(query, TestContext.Current.CancellationToken);

        var sent = recorder.Requests[0]
            .Split('&')
            .ToDictionary(pair => pair.Split('=')[0], pair => pair.Split('=')[1], StringComparer.Ordinal);

        Assert.Equal("Sol", sent["from"]);
        Assert.DoesNotContain("source", sent.Keys);

        // Honoured by the Road to Riches plotter and silently dropped by this one, so it is not sent.
        Assert.DoesNotContain("use_mapping_value", sent.Keys);

        Assert.Equal("/api/exobiology/route", recorder.Paths[0]);
    }
}

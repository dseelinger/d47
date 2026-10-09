using System.Net;
using System.Text;
using System.Text.Json;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>
/// Against an <c>api/systems/search</c> response for 20 ly around LTT 7786 captured from the live service on
/// 2026-10-05, with each result's <c>bodies</c>, <c>stations</c>, <c>minor_faction_presences</c> and
/// <c>synthesis_recipes</c> removed.
/// </summary>
public class PowerplayNeighboursComeFromSpanshsSystemSearchTests
{
    private sealed class Answer(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }

    /// <summary>What <see cref="HttpClient"/> throws when its own timeout passes.</summary>
    private sealed class TimesOut : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new TaskCanceledException();
    }

    private static SpanshStarSystemService Service(HttpMessageHandler handler) =>
        new(NullLogger<SpanshStarSystemService>.Instance, new HttpClient(handler));

    private static string Captured() =>
        Fixture.Text("spansh-powerplay-near-ltt-7786.json");

    private static async Task<PowerplayNeighbourhood> Near()
    {
        using var service = Service(new Answer(HttpStatusCode.OK, Captured()));

        return await service.PowerplayNearAsync("LTT 7786", 20, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void TheRequestAsksForTheThreeHeldStatesNearestFirstWithinTheRange()
    {
        var body = JsonDocument.Parse(SpanshRequest.PowerplayNear("LTT 7786", 30)).RootElement;

        var filters = body.GetProperty("filters");
        Assert.Equal("0", filters.GetProperty("distance").GetProperty("min").GetString());
        Assert.Equal("30", filters.GetProperty("distance").GetProperty("max").GetString());
        Assert.Equal(
            ["Exploited", "Fortified", "Stronghold"],
            filters.GetProperty("power_state").GetProperty("value").EnumerateArray().Select(v => v.GetString()));

        var sort = Assert.Single(body.GetProperty("sort").EnumerateArray());
        Assert.Equal("asc", sort.GetProperty("distance").GetProperty("direction").GetString());

        Assert.Equal(100, body.GetProperty("size").GetInt32());
        Assert.Equal("LTT 7786", body.GetProperty("reference_system").GetString());
    }

    [Fact]
    public async Task TheSearchIsPostedToSpansh()
    {
        var answer = new Answer(HttpStatusCode.OK, Captured());
        using var service = Service(answer);

        await service.PowerplayNearAsync("LTT 7786", 20, TestContext.Current.CancellationToken);

        var request = Assert.Single(answer.Requests);
        Assert.Equal("spansh.co.uk", request.Host);
        Assert.Equal("/api/systems/search", request.AbsolutePath);
        Assert.Equal(SpanshRequest.PowerplayNear("LTT 7786", 20), Assert.Single(answer.Bodies));
    }

    [Fact]
    public async Task TheSystemsComeNearestFirstWithoutTheSystemItself()
    {
        var near = await Near();

        Assert.Equal(49, near.Total);
        Assert.Equal(49, near.Systems.Count);
        Assert.DoesNotContain(near.Systems, s => s.Name == "LTT 7786");
        Assert.Equal(near.Systems.OrderBy(s => s.Distance).Select(s => s.Name), near.Systems.Select(s => s.Name));
        Assert.Equal("LHS 480", near.Systems[0].Name);
        Assert.Equal("Aburu", near.Systems[^1].Name);
    }

    [Fact]
    public async Task EachSystemCarriesItsPowerStateAndProgress()
    {
        var lhs480 = (await Near()).Systems[0];

        Assert.Equal(6.11550603486743, lhs480.Distance, 9);
        Assert.Equal("Yuri Grom", lhs480.ControllingPower);
        Assert.Equal("Exploited", lhs480.State);
        Assert.Equal(0.106569, lhs480.ControlProgress);
        Assert.Equal(
            ["A. Lavigny-Duval", "Aisling Duval", "Denton Patreus", "Yuri Grom", "Zemina Torval", "Jerome Archer"],
            lhs480.Powers);
    }

    [Theory]
    [InlineData("Stronghold", 30)]
    [InlineData("Fortified", 20)]
    [InlineData("Exploited", 20)]
    [InlineData("Unoccupied", 20)]
    [InlineData(null, 20)]
    public void AStrongholdReachesThirtyLightYearsAndEveryOtherStateTwenty(string? state, double reach) =>
        Assert.Equal(reach, PowerplayNeighbourhood.PowerplayReach(state));

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ARefusalIsTheServiceBeingUnavailable(HttpStatusCode status)
    {
        using var service = Service(new Answer(status, "{}"));

        await Assert.ThrowsAsync<GalaxyUnavailableException>(
            () => service.PowerplayNearAsync("LTT 7786", 20, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ATimeoutIsTheServiceBeingUnavailable()
    {
        using var service = Service(new TimesOut());

        var thrown = await Assert.ThrowsAsync<GalaxyUnavailableException>(
            () => service.PowerplayNearAsync("LTT 7786", 20, TestContext.Current.CancellationToken));

        Assert.Equal("The Powerplay search took too long to answer.", thrown.Message);
    }
}

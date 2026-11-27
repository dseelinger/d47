using System.Net;
using System.Text;
using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>A systems search can be centred on a point, and a name Spansh rejects is reported as unknown (#2758).</summary>
public class ASystemSpanshLacksIsSearchedByPositionTests
{
    private static readonly StarPosition Point = new(1.5, -2, 398.25);

    private static GalaxyQuery Query(string? name = "Sol") =>
        GalaxyQuery.TryParse(name, new Dictionary<string, string>(), 5, out var query, out var failure)
            ? query
            : throw new InvalidOperationException(failure);

    [Fact]
    public void ASystemsSearchAroundAPointWritesReferenceCoordsAndNoSystem()
    {
        var body = JsonDocument.Parse(SpanshRequest.Search(Query() with { ReferenceSystem = null, ReferencePosition = Point })).RootElement;

        var coords = body.GetProperty("reference_coords");
        Assert.Equal(1.5, coords.GetProperty("x").GetDouble());
        Assert.Equal(-2, coords.GetProperty("y").GetDouble());
        Assert.Equal(398.25, coords.GetProperty("z").GetDouble());
        Assert.False(body.TryGetProperty("reference_system", out _));
    }

    [Fact]
    public void ASystemsSearchWithAPointAndASystemUsesThePoint()
    {
        var body = JsonDocument.Parse(SpanshRequest.Search(Query() with { ReferencePosition = Point })).RootElement;

        Assert.True(body.TryGetProperty("reference_coords", out _));
        Assert.False(body.TryGetProperty("reference_system", out _));
    }

    [Fact]
    public void ASystemsSearchAroundANameStillWritesReferenceSystem()
    {
        var body = JsonDocument.Parse(SpanshRequest.Search(Query())).RootElement;

        Assert.Equal("Sol", body.GetProperty("reference_system").GetString());
        Assert.False(body.TryGetProperty("reference_coords", out _));
    }

    private sealed class Answer(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("""{"error":"Invalid request"}""", Encoding.UTF8, "application/json"),
            });
    }

    private static SpanshGalaxyService Service(HttpStatusCode status) =>
        new(NullLogger<SpanshGalaxyService>.Instance, new HttpClient(new Answer(status)) { BaseAddress = new Uri("https://spansh.co.uk/") });

    [Fact]
    public async Task ARefusedNamedSystemSearchSaysSpanshHasNoRecordOfIt()
    {
        var ex = await Assert.ThrowsAsync<GalaxyUnavailableException>(
            () => Service(HttpStatusCode.BadRequest).SearchAsync(Query("Nowhere Sector XY-Z a1"), TestContext.Current.CancellationToken));

        Assert.Equal("The galaxy search has no record of Nowhere Sector XY-Z a1.", ex.Message);
    }

    [Fact]
    public async Task ARefusedStationSearchByNameSaysSpanshHasNoRecordOfIt()
    {
        var ex = await Assert.ThrowsAsync<GalaxyUnavailableException>(
            () => Service(HttpStatusCode.BadRequest).FindStationsAsync(StationQuery.Near("Nowhere", 100, 5), TestContext.Current.CancellationToken));

        Assert.Equal("The galaxy search has no record of Nowhere.", ex.Message);
    }

    [Fact]
    public async Task ARefusedBodySearchByNameSaysSpanshHasNoRecordOfIt()
    {
        var ex = await Assert.ThrowsAsync<GalaxyUnavailableException>(
            () => Service(HttpStatusCode.BadRequest).FindBodiesAsync(BodyQuery.LandableNear("Nowhere", 100, 5), TestContext.Current.CancellationToken));

        Assert.Equal("The galaxy search has no record of Nowhere.", ex.Message);
    }

    [Fact]
    public async Task ARefusedSearchByPositionStillSaysItWasRefused()
    {
        var ex = await Assert.ThrowsAsync<GalaxyUnavailableException>(
            () => Service(HttpStatusCode.BadRequest).SearchAsync(
                Query() with { ReferenceSystem = null, ReferencePosition = Point },
                TestContext.Current.CancellationToken));

        Assert.Equal("The galaxy search refused that request.", ex.Message);
    }
}

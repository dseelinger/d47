using System.Text.Json;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>A report age bound is sent as a date range on the field that matches the query (#1769).</summary>
public class AReportAgeBoundReachesTheSearchRequestTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 30, 15, TimeSpan.Zero);

    private static StationQuery Stations(string? module, string? ship, int? hours)
    {
        var parsed = module is null && ship is null
            ? StationQuery.TryParse(
                new StationSearch { ReferenceSystem = "Sol", StationTypes = "Starport", Size = 1 }, out var plain, out _)
            : StationQuery.TryParse("Sol", module, null, null, ship, false, null, 1, out plain, out _);

        Assert.True(parsed);
        return plain with { MaxReportAgeHours = hours };
    }

    private static JsonElement Filters(string body) =>
        JsonDocument.Parse(body).RootElement.GetProperty("filters");

    private static void AssertBound(JsonElement filters, string field, string cutoff)
    {
        var filter = filters.GetProperty(field);

        Assert.Equal("<=>", filter.GetProperty("comparison").GetString());
        Assert.Equal(cutoff, filter.GetProperty("value")[0].GetString());
        Assert.Equal("2999-12-31T00:00:00", filter.GetProperty("value")[1].GetString());
    }

    [Fact]
    public void ASystemSearchBoundsUpdatedAt()
    {
        Assert.True(GalaxyQuery.TryParse("Sol", new Dictionary<string, string>(), 5, out var query, out _));

        var body = SpanshRequest.Search(query with { MaxReportAgeHours = 48 }, Now);

        AssertBound(Filters(body), "updated_at", "2026-10-24T12:30:15");
    }

    [Fact]
    public void AModuleSearchBoundsOutfittingUpdatedAt()
    {
        var filters = Filters(SpanshRequest.Stations(Stations("Frame Shift Drive", null, 24), Now));

        AssertBound(filters, "outfitting_updated_at", "2026-10-25T12:30:15");
        Assert.False(filters.TryGetProperty("shipyard_updated_at", out _));
        Assert.False(filters.TryGetProperty("updated_at", out _));
    }

    [Fact]
    public void AShipSearchBoundsShipyardUpdatedAt()
    {
        var filters = Filters(SpanshRequest.Stations(Stations(null, "Python", 1), Now));

        AssertBound(filters, "shipyard_updated_at", "2026-10-26T11:30:15");
        Assert.False(filters.TryGetProperty("outfitting_updated_at", out _));
    }

    [Fact]
    public void AStationSearchWithNeitherBoundsUpdatedAt()
    {
        var filters = Filters(SpanshRequest.Stations(Stations(null, null, 24), Now));

        AssertBound(filters, "updated_at", "2026-10-25T12:30:15");
    }

    [Fact]
    public void WithNoAgeTheBodyIsUnchanged()
    {
        Assert.True(GalaxyQuery.TryParse("Sol", new Dictionary<string, string>(), 5, out var systems, out _));

        Assert.Equal(SpanshRequest.Search(systems), SpanshRequest.Search(systems with { MaxReportAgeHours = null }, Now));
        Assert.DoesNotContain("updated_at", SpanshRequest.Search(systems, Now), StringComparison.Ordinal);
        Assert.DoesNotContain("updated_at", SpanshRequest.Stations(Stations("Frame Shift Drive", "Python", null), Now), StringComparison.Ordinal);
    }
}

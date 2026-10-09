using System.Text.Encodings.Web;
using System.Text.Json;
using D47.Core.Knowledge;
using D47.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>The body filters INARA offers, in the shapes probed against Spansh on 2026-10-03 (#811).</summary>
public class ABodySearchReachesInarasFiltersTests
{
    private static BodyQuery Parse(BodyRequest request)
    {
        Assert.True(
            BodyQuery.TryParse(request with { ReferenceSystem = "Sol", MaxDistance = 20 }, out var query, out var failure),
            failure);

        return query;
    }

    private static JsonElement Filters(BodyRequest request) =>
        JsonDocument.Parse(SpanshRequest.Bodies(Parse(request))).RootElement.GetProperty("filters");

    private static string Raw(JsonElement filters, string key) =>
        JsonSerializer.Serialize(
            filters.GetProperty(key),
            new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    [Fact]
    public void GravityIsAComparisonBetweenItsBounds()
    {
        var filters = Filters(new() { Gravity = "0-0.1" });

        Assert.Equal("{\"value\":[\"0\",\"0.1\"],\"comparison\":\"<=>\"}", Raw(filters, "gravity"));
    }

    [Fact]
    public void ABareGravityIsAnUpperBound()
    {
        var filters = Filters(new() { Gravity = "0.1" });

        Assert.Equal("{\"value\":[\"0\",\"0.1\"],\"comparison\":\"<=>\"}", Raw(filters, "gravity"));
    }

    [Fact]
    public void TemperatureIsAComparisonInKelvin()
    {
        var filters = Filters(new() { Temperature = "0-100" });

        Assert.Equal("{\"value\":[\"0\",\"100\"],\"comparison\":\"<=>\"}", Raw(filters, "surface_temperature"));
    }

    [Fact]
    public void ArrivalDistanceIsAComparisonFromZero()
    {
        var filters = Filters(new() { MaxArrivalDistance = 10 });

        Assert.Equal("{\"value\":[\"0\",\"10\"],\"comparison\":\"<=>\"}", Raw(filters, "distance_to_arrival"));
    }

    [Fact]
    public void TidallyLockedTrueSendsTheStringTrue()
    {
        var filters = Filters(new() { TidallyLocked = true });

        Assert.Equal("{\"value\":[\"true\"]}", Raw(filters, "is_rotational_period_tidally_locked"));
    }

    [Fact]
    public void TidallyLockedFalseWritesNoKey()
    {
        var filters = Filters(new() { Subtype = "Icy body", TidallyLocked = false });

        Assert.False(filters.TryGetProperty("is_rotational_period_tidally_locked", out _));
    }

    [Fact]
    public void VolcanismIsSentInTheCataloguesSpelling()
    {
        var filters = Filters(new() { Volcanism = "water geysers" });

        Assert.Equal("{\"value\":[\"Water Geysers\"]}", Raw(filters, "volcanism_type"));
    }

    [Fact]
    public void AtmosphereIsSentInTheCataloguesSpelling()
    {
        var filters = Filters(new() { Atmosphere = "thin ammonia" });

        Assert.Equal("{\"value\":[\"Thin Ammonia\"]}", Raw(filters, "atmosphere"));
    }

    [Fact]
    public void AMaterialIsTheMaterialsGroup()
    {
        var filters = Filters(new() { Material = "polonium" });

        Assert.Equal("{\"name\":{\"value\":[\"Polonium\"]}}", Raw(filters, "materials"));
    }

    [Fact]
    public void APowerIsSentUnderTheBodyIndexsSystemKeys()
    {
        var filters = Filters(new()
        {
            Filters = new Dictionary<string, string> { ["power"] = "jerome archer", ["power_state"] = "Fortified" },
        });

        Assert.Equal("{\"value\":[\"Jerome Archer\"]}", Raw(filters, "system_controlling_power"));
        Assert.Equal("{\"value\":[\"Fortified\"]}", Raw(filters, "system_power_state"));
    }

    [Fact]
    public void OrderingByMaterialAsksForTheFiftyNearest()
    {
        var body = JsonDocument.Parse(SpanshRequest.Bodies(Parse(new()
        {
            Material = "Polonium",
            OrderBy = "material",
            Size = 3,
        }))).RootElement;

        Assert.Equal(50, body.GetProperty("size").GetInt32());
        Assert.Equal("asc", body.GetProperty("sort")[0].GetProperty("distance").GetProperty("direction").GetString());
    }

    [Fact]
    public void OrderingByMaterialKeepsTheRichestOfACapturedResponse()
    {
        // 50 bodies carrying Polonium within 50 ly of Sol, nearest first, captured 2026-10-06.
        using var document = Fixture.Json("spansh-bodies-polonium-near-sol.json");
        var captured = SpanshResponse.ReadBodies(document);

        var query = Parse(new() { Material = "Polonium", OrderBy = "material", Size = 3 });
        var kept = query.Keep(captured);

        Assert.Equal(50, captured.Bodies.Count);
        Assert.Equal(
            ["36 Ophiuchi B 1", "Alpha Centauri B 1", "Kokary 1"],
            kept.Bodies.Select(body => body.Name));
        Assert.Equal(1.018313, kept.Bodies[1].Share("Polonium"));
    }

    [Fact]
    public void TheNewFieldsAreReadFromAResult()
    {
        using var document = Fixture.Json("spansh-bodies-polonium-near-sol.json");

        var body = SpanshResponse.ReadBodies(document).Bodies[0];

        Assert.Equal("Rocky Magma", body.Volcanism);
        Assert.Equal("No atmosphere", body.Atmosphere);
        Assert.NotNull(body.Gravity);
        Assert.NotNull(body.SurfaceTemperature);
        Assert.True(body.IsTidallyLocked);
    }

    [Fact]
    public void OrderingByDistanceKeepsTheResponseAsItCame()
    {
        var query = Parse(new() { Material = "Polonium" });

        Assert.Equal(5, query.Size);
        Assert.False(query.OrderByMaterial);
    }
}

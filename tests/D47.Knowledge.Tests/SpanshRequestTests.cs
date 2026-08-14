using System.Text.Json;
using D47.Core.Knowledge;
using D47.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>
/// The request body, asserted against shapes measured from the live service on 2026-08-14. There
/// is no published API, so these are the only record of what it actually accepts — and both of
/// the facts below were established by getting them wrong first.
/// </summary>
public class SpanshRequestTests
{
    private static JsonElement Build(params (string Filter, string Value)[] filters)
    {
        Assert.True(GalaxyQuery.TryParse(
            "Sol",
            filters.ToDictionary(f => f.Filter, f => f.Value, StringComparer.Ordinal),
            size: 5,
            out var query,
            out var failure), failure);

        return JsonDocument.Parse(SpanshRequest.Search(query)).RootElement;
    }

    [Fact]
    public void AChoiceFilterIsAnObjectWithAValueArray()
    {
        // The bare string is a 400. This is the shape that works.
        var body = Build(("allegiance", "Federation"));

        var allegiance = body.GetProperty("filters").GetProperty("allegiance");

        Assert.Equal(JsonValueKind.Object, allegiance.ValueKind);
        Assert.Equal("Federation", allegiance.GetProperty("value")[0].GetString());
    }

    [Fact]
    public void ARangeFilterCarriesItsBoundsAsStrings()
    {
        var body = Build(("distance", "10-50"));

        var distance = body.GetProperty("filters").GetProperty("distance");

        Assert.Equal(JsonValueKind.String, distance.GetProperty("min").ValueKind);
        Assert.Equal("10", distance.GetProperty("min").GetString());
        Assert.Equal("50", distance.GetProperty("max").GetString());
    }

    [Fact]
    public void AnAbsentBoundIsWrittenRatherThanOmitted()
    {
        // A missing key is a key the service does not recognise, which is the silent-ignore case
        // all over again — so both ends are always present.
        var body = Build(("distance", "20"));

        var distance = body.GetProperty("filters").GetProperty("distance");

        Assert.Equal("0", distance.GetProperty("min").GetString());
        Assert.Equal("20", distance.GetProperty("max").GetString());
    }

    [Fact]
    public void TheReferenceSystemIsSentSoDistancesMeanSomething()
    {
        var body = Build(("distance", "20"));

        Assert.Equal("Sol", body.GetProperty("reference_system").GetString());
    }

    [Fact]
    public void ResultsAreSortedNearestFirst()
    {
        var body = Build(("distance", "20"));

        var sort = body.GetProperty("sort")[0].GetProperty("distance");

        Assert.Equal("asc", sort.GetProperty("direction").GetString());
    }

    [Fact]
    public void OnlyValidatedFiltersCanReachTheBody()
    {
        // Not a property of this file so much as of the type it takes: a GalaxyQuery cannot be
        // constructed with a filter the vocabulary does not have, so there is no path from a
        // hallucinated filter name to a request body.
        Assert.False(GalaxyQuery.TryParse(
            "Sol",
            new Dictionary<string, string> { ["not_a_real_filter"] = "x" },
            size: 5,
            out _,
            out _));
    }
}

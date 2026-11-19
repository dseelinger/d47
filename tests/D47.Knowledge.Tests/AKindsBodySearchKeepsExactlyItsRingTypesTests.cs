using System.Text.Json;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>A body search for a star or planet kind: its luminosity, terraforming and ring filters, and the cut to the exact ring set (#2587).</summary>
public sealed class AKindsBodySearchKeepsExactlyItsRingTypesTests
{
    private static BodyQuery Query(BodyRequest request)
    {
        Assert.True(
            BodyQuery.TryParse(request with { ReferenceSystem = "Sol", Subtype = "Class I gas giant" }, out var query, out var failure),
            failure);

        return query;
    }

    private static JsonElement Filters(BodyRequest request) =>
        JsonDocument.Parse(SpanshRequest.Bodies(Query(request))).RootElement.GetProperty("filters");

    private static string[] Values(JsonElement filters, string name) =>
        [.. filters.GetProperty(name).GetProperty("value").EnumerateArray().Select(value => value.GetString()!)];

    private static BodySearchResult Kept(BodyRequest request)
    {
        using var document = Fixture.Json("spansh-bodies-search-class-i-rings.json");

        return Query(request).Keep(SpanshResponse.ReadBodies(document));
    }

    [Fact]
    public void SeveralRingTypesAreSentAsAListOfTypeMembers()
    {
        var rings = Filters(new() { RingType = "Icy, Rocky" }).GetProperty("rings");

        Assert.Equal(JsonValueKind.Array, rings.ValueKind);
        Assert.Equal(["Icy", "Rocky"], rings.EnumerateArray().Select(ring => ring.GetProperty("type").GetString()));
    }

    [Fact]
    public void NoRingsSendsNoRingFilter()
    {
        Assert.False(Filters(new() { RingType = "None" }).TryGetProperty("rings", out _));
    }

    [Fact]
    public void ALuminosityBandIsSentAsItsClasses()
    {
        Assert.Equal(
            ["IV", "IVa", "IVab", "IVb", "V", "Va", "Vab", "Vb", "Vz", "VI"],
            Values(Filters(new() { Luminosity = "dwarf" }), "luminosity_class"));
        Assert.Equal(["O", "Ia0"], Values(Filters(new() { Luminosity = "Hypergiant" }), "luminosity_class"));
    }

    [Fact]
    public void TerraformableCoversTheBodiesBeingAndAlreadyTerraformed()
    {
        Assert.Equal(["Terraformable", "Terraforming", "Terraformed"], Values(Filters(new() { Terraformable = true }), "terraforming_state"));
        Assert.Equal(["Not terraformable"], Values(Filters(new() { Terraformable = false }), "terraforming_state"));
    }

    [Fact]
    public void ExactRingsKeepOnlyTheBodiesWithThatSetAndAskForMoreToChooseFrom()
    {
        var request = new BodyRequest { RingType = "Icy, Rocky", RingsExactly = true, Size = 5 };
        var kept = Kept(request);

        Assert.Equal(["Gas Giant Bravo 2", "Gas Giant Echo 5"], kept.Bodies.Select(body => body.Name));
        Assert.Equal(2, kept.Total);
        Assert.False(kept.More);
        Assert.Equal(BodyQuery.ExactRingsOf, Query(request).Size);
        Assert.Equal(5, Query(request).Limit);
    }

    [Fact]
    public void ExactIcyRingsLeaveOutABodyWithRockyRingsAsWell()
    {
        Assert.Equal(["Gas Giant Alpha 1"], Kept(new() { RingType = "Icy", RingsExactly = true }).Bodies.Select(body => body.Name));
    }

    [Fact]
    public void NoneKeepsOnlyTheUnringedBodies()
    {
        Assert.Equal(["Gas Giant Delta 4"], Kept(new() { RingType = "None" }).Bodies.Select(body => body.Name));
    }

    [Fact]
    public void ARingTypeWithoutExactKeepsEveryBodyTheServiceReturned()
    {
        Assert.Equal(5, Kept(new() { RingType = "Icy" }).Bodies.Count);
    }

    [Fact]
    public void AnExactRingSearchHasNoSecondPage()
    {
        Assert.False(BodyQuery.TryParse(
            new BodyRequest { ReferenceSystem = "Sol", Subtype = "Class I gas giant", RingType = "Icy", RingsExactly = true, Page = 2 },
            out _,
            out var failure));
        Assert.Contains("no page 2", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void ExactWithNoRingTypeIsRefused()
    {
        Assert.False(BodyQuery.TryParse(
            new BodyRequest { ReferenceSystem = "Sol", Subtype = "Class I gas giant", RingsExactly = true },
            out _,
            out _));
    }

    [Fact]
    public void NoneBesideARingTypeIsRefused()
    {
        Assert.False(BodyQuery.TryParse(
            new BodyRequest { ReferenceSystem = "Sol", Subtype = "Class I gas giant", RingType = "Icy, None" },
            out _,
            out _));
    }
}

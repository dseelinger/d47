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

    private static JsonElement Bodies(Action<BodyBuilder> configure)
    {
        var builder = new BodyBuilder();
        configure(builder);

        Assert.True(
            BodyQuery.TryParse(
                "Sol",
                builder.Subtype,
                builder.Signal,
                builder.SignalCount,
                builder.RingSignal,
                builder.RingSignalCount,
                builder.RingType,
                builder.ReserveLevel,
                builder.Landable,
                builder.Terraformable,
                maxDistance: 20,
                size: 5,
                out var query,
                out var failure),
            failure);

        return JsonDocument.Parse(SpanshRequest.Bodies(query)).RootElement;
    }

    private sealed class BodyBuilder
    {
        public string? Subtype { get; set; }

        public string? Signal { get; set; }

        public int? SignalCount { get; set; }

        public string? RingSignal { get; set; }

        public int? RingSignalCount { get; set; }

        public string? RingType { get; set; }

        public string? ReserveLevel { get; set; }

        public bool? Landable { get; set; }

        public bool? Terraformable { get; set; }
    }

    [Fact]
    public void ASignalFilterIsAGroupWithANameMember()
    {
        // The obvious spelling — {"signals":{"Biological":{"min":"1","max":"40"}}} — is accepted
        // and ignored, returning the unfiltered 1,315 bodies within 20 ly of Sol on 2026-08-14.
        // This is the shape that actually narrows.
        var body = Bodies(builder => builder.Signal = "Biological");

        var signals = body.GetProperty("filters").GetProperty("signals");

        Assert.Equal("Biological", signals.GetProperty("name").GetProperty("value")[0].GetString());
        Assert.False(signals.TryGetProperty("count", out _));
    }

    [Fact]
    public void ASignalCountIsABareNumberBecauseARangeAnswersNothing()
    {
        // Written as {"min":"1","max":"40"} the count member returned zero results every time.
        // Written as a number it matches exactly that many.
        var body = Bodies(builder =>
        {
            builder.RingSignal = "Painite";
            builder.RingSignalCount = 3;
        });

        var count = body.GetProperty("filters").GetProperty("ring_signals").GetProperty("count");

        Assert.Equal(JsonValueKind.Number, count.ValueKind);
        Assert.Equal(3, count.GetInt32());
    }

    [Fact]
    public void ARingTypeIsAPlainChoiceRatherThanAGroup()
    {
        // The group spelling that works for modules and signals is a 500 here.
        var body = Bodies(builder => builder.RingType = "Icy");

        Assert.Equal("Icy", body.GetProperty("filters").GetProperty("rings").GetProperty("value")[0].GetString());
    }

    [Fact]
    public void TerraformableBecomesAStateRatherThanABoolean()
    {
        // The service models this as one of four states, so "not terraformable" is a value rather
        // than the absence of the filter.
        var body = Bodies(builder => builder.Terraformable = true);

        Assert.Equal(
            "Terraformable",
            body.GetProperty("filters").GetProperty("terraforming_state").GetProperty("value")[0].GetString());
    }

    [Fact]
    public void ABodySearchLeavesOutTheFiltersNobodyAskedFor()
    {
        // A filter written with a default value is a filter, and the service has no way to tell
        // "the Commander did not say" from "the Commander said no".
        var filters = Bodies(builder => builder.Subtype = "Earth-like world").GetProperty("filters");

        Assert.False(filters.TryGetProperty("is_landable", out _));
        Assert.False(filters.TryGetProperty("terraforming_state", out _));
        Assert.False(filters.TryGetProperty("signals", out _));
        Assert.False(filters.TryGetProperty("rings", out _));
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

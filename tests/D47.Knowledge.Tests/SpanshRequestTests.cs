using System.Text.Json;
using D47.Core.Knowledge;
using D47.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>The request body, asserted against shapes measured from the live service on 2026-08-14.</summary>
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
        // The bare string is a 400.
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
        // A missing key is a key the service does not recognise, so both ends are always present.
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
        // The obvious spelling — {"signals":{"Biological":{"min":"1","max":"40"}}} — is accepted and ignored,
        // returning the unfiltered 1,315 bodies within 20 ly of Sol on 2026-08-14.
        var body = Bodies(builder => builder.Signal = "Biological");

        var signals = body.GetProperty("filters").GetProperty("signals");

        Assert.Equal("Biological", signals.GetProperty("name").GetProperty("value")[0].GetString());
        Assert.False(signals.TryGetProperty("count", out _));
    }

    [Fact]
    public void ASignalCountIsABareNumberBecauseARangeAnswersNothing()
    {
        // Written as {"min":"1","max":"40"} the count member returned zero results every time.
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
        // The service models this as one of four states, so "not terraformable" is a value rather than the
        // absence of the filter.
        var body = Bodies(builder => builder.Terraformable = true);

        Assert.Equal(
            "Terraformable",
            body.GetProperty("filters").GetProperty("terraforming_state").GetProperty("value")[0].GetString());
    }

    [Fact]
    public void ABodySearchLeavesOutTheFiltersNobodyAskedFor()
    {
        // A filter written with a default value is a filter, and the service has no way to tell "the
        // Commander did not say" from "the Commander said no".
        var filters = Bodies(builder => builder.Subtype = "Earth-like world").GetProperty("filters");

        Assert.False(filters.TryGetProperty("is_landable", out _));
        Assert.False(filters.TryGetProperty("terraforming_state", out _));
        Assert.False(filters.TryGetProperty("signals", out _));
        Assert.False(filters.TryGetProperty("rings", out _));
    }

    [Fact]
    public void OnlyValidatedFiltersCanReachTheBody()
    {
        Assert.False(GalaxyQuery.TryParse(
            "Sol",
            new Dictionary<string, string> { ["not_a_real_filter"] = "x" },
            size: 5,
            out _,
            out _));
    }

    [Fact]
    public void TheStateFilterIsSentUnderTheKeyThatActuallyMatchesSomething()
    {
        var body = Build(("state", "Boom"));

        var filters = body.GetProperty("filters");

        Assert.False(
            filters.TryGetProperty("state", out _),
            "'state' is a real key that matches nothing. Sending it returns an empty result, which "
            + "reads as 'there are none near you'.");

        var state = filters.GetProperty("controlling_minor_faction_state");

        Assert.Equal("Boom", state.GetProperty("value")[0].GetString());
    }

    [Fact]
    public void AFilterWhoseKeyNeedsNoTranslationStillSendsItsOwnName()
    {
        var body = Build(("security", "High"));

        Assert.True(body.GetProperty("filters").TryGetProperty("security", out _));
    }

    [Fact]
    public void TheStateVocabularyIsTheServicesOwnAndAWordOutsideItIsRefusedHere()
    {
        var filter = GalaxyFilters.Find("state");

        Assert.NotNull(filter);
        Assert.Equal(21, filter.Choices.Count);
        Assert.Contains("Outbreak", filter.Choices, StringComparer.Ordinal);

        Assert.False(GalaxyQuery.TryParse(
            "Sol",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["state"] = "Prosperity" },
            size: 5,
            out _,
            out var failure));

        Assert.Contains("Prosperity", failure, StringComparison.Ordinal);
    }


    private static JsonElement Colonisation(double maxDistance = 15)
    {
        Assert.True(
            ColonisationQuery.TryParse(
                "Sol",
                subtype: null,
                terraformable: false,
                rings: false,
                minimumLandable: null,
                maxDistance,
                size: 3,
                out var query,
                out var failure),
            failure);

        return JsonDocument.Parse(SpanshRequest.Colonisation(query)).RootElement;
    }

    [Fact]
    public void ThePopulationFilterIsNeverSentBecauseTheServiceDropsIt()
    {
        var filters = Colonisation().GetProperty("filters");

        Assert.False(filters.TryGetProperty("population", out _));

        Assert.Null(GalaxyFilters.Find("population"));
    }

    [Fact]
    public void PopulationIsSortedOnInsteadNearestFirstWithinTheTies()
    {
        var sort = Colonisation().GetProperty("sort");

        Assert.Equal(2, sort.GetArrayLength());
        Assert.Equal("asc", sort[0].GetProperty("population").GetProperty("direction").GetString());
        Assert.Equal("asc", sort[1].GetProperty("distance").GetProperty("direction").GetString());
    }

    // Both flags are presence-only: the value is discarded, so asking for "false" returns the true ones.
    [Fact]
    public void NeitherColonisationFlagIsSentBecauseAskingForFalseReturnsTheTrueOnes()
    {
        var filters = Colonisation().GetProperty("filters");

        Assert.False(filters.TryGetProperty("is_colonised", out _));
        Assert.False(filters.TryGetProperty("is_being_colonised", out _));
    }

    /// <summary>The page has to hold every unpopulated system in range, since the sort is the only thing putting them where they can be seen. 120 is above the densest 15 light years measured — 106 systems, around Colonia — and below the 500 the service caps a page at, past which it silently returns 25 rather than erroring.</summary>
    [Fact]
    public void TheScanAsksForMoreSystemsThanTheDensestClaimRangeHolds()
    {
        var body = Colonisation();

        Assert.Equal(ColonisationQuery.ScanSize, body.GetProperty("size").GetInt32());
        Assert.InRange(body.GetProperty("size").GetInt32(), 107, 500);
        Assert.Equal("Sol", body.GetProperty("reference_system").GetString());
        Assert.Equal("15", body.GetProperty("filters").GetProperty("distance").GetProperty("max").GetString());
    }

    [Fact]
    public void BodiesInNamedSystemsAreAskedForAsOneChoiceCarryingEveryName()
    {
        var query = BodyQuery.ForSystems("Sol", ["HIP 22711", "HIP 22460"], maxDistance: 15);
        var filters = JsonDocument.Parse(SpanshRequest.Bodies(query)).RootElement.GetProperty("filters");

        var names = filters.GetProperty("system_name").GetProperty("value");

        Assert.Equal(2, names.GetArrayLength());
        Assert.Equal("HIP 22711", names[0].GetString());
        Assert.Equal("HIP 22460", names[1].GetString());

        Assert.Equal("15", filters.GetProperty("distance").GetProperty("max").GetString());
    }

    [Fact]
    public void AnOrdinaryBodySearchSendsNoSystemNameFilter()
    {
        Assert.False(Bodies(builder => builder.Subtype = "Water world")
            .GetProperty("filters")
            .TryGetProperty("system_name", out _));
    }
}

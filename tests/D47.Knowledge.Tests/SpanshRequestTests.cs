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

    /// <summary>
    /// The one filter whose wire key is not the word d47 offers for it, and the reason it is not.
    /// <para>
    /// <b>There is a real field called <c>state</c> and it is a trap.</b> Measured against the
    /// live service on 2026-08-15: it has its own <c>field_values</c> list carrying exactly the 21
    /// words this filter accepts, and it is honoured rather than dropped — but it matches nothing.
    /// Zero systems for every value including <c>None</c>, where a bogus key returns the
    /// unfiltered count and no result row carries a <c>state</c> field at all.
    /// </para>
    /// <para>
    /// That fails as an <em>empty</em> answer rather than as a wrong one, which is worse than the
    /// silent-ignore case this class was built around: "no systems in Boom near you" reads as a
    /// fact about the galaxy. <c>controlling_minor_faction_state</c> returns 1,286 systems in Boom
    /// within 200 light years of Sol.
    /// </para>
    /// </summary>
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
        // The translation is the exception, not the rule. Two names for one filter is a thing to
        // keep in step, so everything else sends the word it is offered as.
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

        // Refused locally rather than sent and answered wrong, which is what the closed
        // vocabulary is for.
        Assert.False(GalaxyQuery.TryParse(
            "Sol",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["state"] = "Prosperity" },
            size: 5,
            out _,
            out var failure));

        Assert.Contains("Prosperity", failure, StringComparison.Ordinal);
    }

    // ------------------------------------------------- the colonisation scan

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

    /// <summary>
    /// The one criterion that decides a colonisation candidate is the one filter this index drops
    /// without saying so. Measured 2026-08-16 within 15 light years of Sol, where 48 of the 51
    /// systems are populated: every range spelling of <c>population</c> returned 51, identical to
    /// a key the service has never heard of. So it must not be sent, and something else has to do
    /// its job.
    /// </summary>
    [Fact]
    public void ThePopulationFilterIsNeverSentBecauseTheServiceDropsIt()
    {
        var filters = Colonisation().GetProperty("filters");

        Assert.False(filters.TryGetProperty("population", out _));

        // And the vocabulary no longer offers it either, so no other request can send it.
        Assert.Null(GalaxyFilters.Find("population"));
    }

    /// <summary>
    /// What cannot be filtered can be sorted, and the second key is what keeps the page in
    /// distance order inside the ties.
    /// </summary>
    [Fact]
    public void PopulationIsSortedOnInsteadNearestFirstWithinTheTies()
    {
        var sort = Colonisation().GetProperty("sort");

        Assert.Equal(2, sort.GetArrayLength());
        Assert.Equal("asc", sort[0].GetProperty("population").GetProperty("direction").GetString());
        Assert.Equal("asc", sort[1].GetProperty("distance").GetProperty("direction").GetString());
    }

    /// <summary>
    /// Both flags are presence-only: the value is discarded, so asking for <c>"false"</c> returns
    /// precisely the systems that are true. Neither can be sent at all.
    /// </summary>
    [Fact]
    public void NeitherColonisationFlagIsSentBecauseAskingForFalseReturnsTheTrueOnes()
    {
        var filters = Colonisation().GetProperty("filters");

        Assert.False(filters.TryGetProperty("is_colonised", out _));
        Assert.False(filters.TryGetProperty("is_being_colonised", out _));
    }

    /// <summary>
    /// The page has to hold every unpopulated system in range, since the sort is the only thing
    /// putting them where they can be seen. 120 is above the densest 15 light years measured — 106
    /// systems, around Colonia — and below the 500 the service caps a page at, past which it
    /// silently returns 25 rather than erroring.
    /// </summary>
    [Fact]
    public void TheScanAsksForMoreSystemsThanTheDensestClaimRangeHolds()
    {
        var body = Colonisation();

        Assert.Equal(ColonisationQuery.ScanSize, body.GetProperty("size").GetInt32());
        Assert.InRange(body.GetProperty("size").GetInt32(), 107, 500);
        Assert.Equal("Sol", body.GetProperty("reference_system").GetString());
        Assert.Equal("15", body.GetProperty("filters").GetProperty("distance").GetProperty("max").GetString());
    }

    /// <summary>
    /// One candidate's bodies, reached by name. The obvious alternative — a zero-width distance
    /// range around the system — is not "this system only": it matched every body in the galaxy
    /// and took 120 seconds before it was given up on.
    /// </summary>
    [Fact]
    public void BodiesInNamedSystemsAreAskedForAsOneChoiceCarryingEveryName()
    {
        var query = BodyQuery.ForSystems("Sol", ["HIP 22711", "HIP 22460"], maxDistance: 15);
        var filters = JsonDocument.Parse(SpanshRequest.Bodies(query)).RootElement.GetProperty("filters");

        var names = filters.GetProperty("system_name").GetProperty("value");

        Assert.Equal(2, names.GetArrayLength());
        Assert.Equal("HIP 22711", names[0].GetString());
        Assert.Equal("HIP 22460", names[1].GetString());

        // Still bounded, and never a zero-width one.
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

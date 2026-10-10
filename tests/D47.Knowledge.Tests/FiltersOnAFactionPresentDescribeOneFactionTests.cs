using System.Text.Json;
using D47.Core.Knowledge;
using D47.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>The filters on a faction present, against shapes probed on 2026-10-09 (#1036).</summary>
public class FiltersOnAFactionPresentDescribeOneFactionTests
{
    private static JsonElement Systems(params (string Filter, string Value)[] filters)
    {
        Assert.True(GalaxyQuery.TryParse(
            "Sol",
            filters.ToDictionary(f => f.Filter, f => f.Value, StringComparer.Ordinal),
            size: 5,
            out var query,
            out var failure), failure);

        return JsonDocument.Parse(SpanshRequest.Search(query)).RootElement.GetProperty("filters");
    }

    private static string[] Values(JsonElement filter) =>
        [.. filter.GetProperty("value").EnumerateArray().Select(value => value.GetString()!)];

    private static int Count(JsonElement filters, string key) =>
        filters.EnumerateObject().Count(property => property.Name == key);

    [Fact]
    public void SeveralStatesAreOneElementMatchingAnyOfThem()
    {
        var filters = Systems(("faction_state", "Civil Unrest, expansion"));

        var element = Assert.Single(filters.GetProperty("minor_faction_presences").EnumerateArray());

        Assert.Equal(["state"], element.EnumerateObject().Select(property => property.Name));
        Assert.Equal(["Civil Unrest", "Expansion"], Values(element.GetProperty("state")));
    }

    [Fact]
    public void NameStateAndGovernmentAreOneFaction()
    {
        var filters = Systems(
            ("faction", "Sol Nationalists"),
            ("faction_state", "Drought"),
            ("faction_government", "Corporate"),
            ("allegiance", "Federation"));

        Assert.Equal(1, Count(filters, "minor_faction_presences"));

        var element = Assert.Single(filters.GetProperty("minor_faction_presences").EnumerateArray());

        Assert.Equal(["Sol Nationalists"], Values(element.GetProperty("name")));
        Assert.Equal(["Drought"], Values(element.GetProperty("state")));
        Assert.Equal(["Corporate"], Values(element.GetProperty("government")));
        Assert.Equal(3, element.EnumerateObject().Count());
        Assert.Equal(["Federation"], Values(filters.GetProperty("allegiance")));
    }

    [Fact]
    public void AFactionAloneIsANameInTheArrayShape()
    {
        var presences = Systems(("faction", "Eurybia Blue Mafia")).GetProperty("minor_faction_presences");

        Assert.Equal(JsonValueKind.Array, presences.ValueKind);
        Assert.Equal(["Eurybia Blue Mafia"], Values(Assert.Single(presences.EnumerateArray()).GetProperty("name")));
    }
}

using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Campaigns;
using Xunit;

namespace D47.App.Tests;

/// <summary>Commander › Campaign: the Distance column of the Fronts and Systems tables (#2431).</summary>
public class TheCampaignTablesShowEachSystemsDistanceTests
{
    private static Front FrontIn(string system) =>
        new(FrontKind.ThinMargin, system, "Thin margin", SystemIntent.Hold, true, null, string.Empty, 0.05, null, false);

    private static CampaignSystemRow RowIn(string system, double? distance) =>
        new(system, true, 0.5, 0.1, false, CampaignSystemRows.NoState, SystemIntent.Hold, true, null, false, false) { Distance = distance };

    private static int Column<TRow>(IReadOnlyList<SortColumn<TRow>> heads, string label) =>
        heads.Select((head, index) => (head, index)).Single(pair => pair.head.Label == label).index;

    [Theory]
    [InlineData(6.1234, "6.12 ly")]
    [InlineData(0.0, "0.00 ly")]
    [InlineData(22013.456, "22013.46 ly")]
    [InlineData(null, "—")]
    public void TheDistanceIsInLightYearsToTwoDecimalsOrADash(double? lightYears, string text) =>
        Assert.Equal(text, CampaignModel.DistanceText(lightYears));

    [Fact]
    public void EachFrontTakesItsSystemsDistanceByName()
    {
        var distances = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["Lave"] = 12.5 };

        var rows = CampaignModel.FrontRows([FrontIn("lave"), FrontIn("Diso")], null, distances);

        Assert.Equal(12.5, rows[0].Distance);
        Assert.Null(rows[1].Distance);
    }

    [Fact]
    public void DistanceSitsBetweenSystemAndReasonInTheFrontsAndBetweenSystemAndInfluenceInTheSystems()
    {
        Assert.Equal(["Rank", "Kind", "System", "Distance", "Reason", "Data age", ""], CampaignModel.FrontHeads().Select(head => head.Label));
        Assert.Equal(["System", "Distance", "Influence", "Lead", "State", "Intent"], CampaignModel.Heads(headset: false).Select(head => head.Label));
        Assert.Equal(["System", "Distance", "Infl.", "Lead", "State", "Intent"], CampaignModel.Heads(headset: true).Select(head => head.Label));
    }

    [Fact]
    public void TheDefaultOrdersStayRankAndInfluence()
    {
        Assert.Equal("Rank", CampaignModel.FrontHeads()[CampaignModel.FrontOrder.Column].Label);
        Assert.Equal(SortDirection.Ascending, CampaignModel.FrontOrder.Direction);
        Assert.Equal("Influence", CampaignModel.Heads(headset: false)[CampaignModel.DefaultOrder.Column].Label);
        Assert.Equal(SortDirection.Descending, CampaignModel.DefaultOrder.Direction);
    }

    [Fact]
    public void TheFrontsSortNearestFirstThenFarthestFirstWithNoDistanceLast()
    {
        var distances = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["Lave"] = 20, ["Diso"] = 5, ["Leesti"] = 40 };
        var rows = CampaignModel.FrontRows([FrontIn("Lave"), FrontIn("Unknown"), FrontIn("Diso"), FrontIn("Leesti")], null, distances);
        var heads = CampaignModel.FrontHeads();
        var distance = Column(heads, "Distance");

        var first = ColumnSort.Next(heads, null, distance);
        var second = ColumnSort.Next(heads, first, distance);

        Assert.Equal(["Diso", "Lave", "Leesti", "Unknown"], ColumnSort.Sorted(rows, heads, first).Select(row => row.Front.System));
        Assert.Equal(["Leesti", "Lave", "Diso", "Unknown"], ColumnSort.Sorted(rows, heads, second).Select(row => row.Front.System));
    }

    [Fact]
    public void TheSystemsSortNearestFirstThenFarthestFirstWithNoDistanceLast()
    {
        IReadOnlyList<CampaignSystemRow> rows = [RowIn("Lave", 20), RowIn("Unknown", null), RowIn("Diso", 5), RowIn("Leesti", 40)];
        var heads = CampaignModel.Heads(headset: false);
        var distance = Column(heads, "Distance");

        var first = ColumnSort.Next(heads, null, distance);
        var second = ColumnSort.Next(heads, first, distance);

        Assert.Equal(["Diso", "Lave", "Leesti", "Unknown"], ColumnSort.Sorted(rows, heads, first).Select(row => row.System));
        Assert.Equal(["Leesti", "Lave", "Diso", "Unknown"], ColumnSort.Sorted(rows, heads, second).Select(row => row.System));
    }
}

using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Campaigns;
using D47.Core.Journal;
using Xunit;

namespace D47.App.Tests;

/// <summary>Commander › Campaign: the Systems section's hub block line (#2448).</summary>
public class TheCampaignHubIsNamedOnTheSystemsPageTests
{
    private static CampaignSystemRow RowAt(string system, double? x) =>
        new(system, true, 0.5, 0.1, false, CampaignSystemRows.NoState, SystemIntent.Hold, true, null, false, false)
        {
            Position = x is { } at ? new StarPosition(at, 0, 0) : null,
        };

    private static IReadOnlyList<CampaignSystemRow> Rows(int count) =>
        [.. Enumerable.Range(0, count).Select(i => RowAt($"S{i}", i * (i % 2 == 0 ? 1 : 3)))];

    [Fact]
    public void TenPositionedRowsNameTheNineOthersAndTheSortWithItsArrow()
    {
        var hub = CampaignModel.Hub(Rows(12), "Influence", SortDirection.Descending, headset: false);

        Assert.NotNull(hub.System);
        Assert.Equal("The system nearest on average to the other nine of the first ten rows below, by Influence ▼.", hub.Line);
    }

    [Fact]
    public void TheHubIsTakenOverTheFirstTenRowsOnly()
    {
        IReadOnlyList<CampaignSystemRow> rows = [.. Enumerable.Range(0, 10).Select(i => RowAt($"S{i}", i)), RowAt("Middle", 4.5), RowAt("Far", 1_000)];

        Assert.Equal("S4", CampaignModel.Hub(rows, "Distance", SortDirection.Ascending, headset: false).System);
    }

    [Fact]
    public void FewerThanTenRowsCountThemselves()
    {
        var hub = CampaignModel.Hub(Rows(8), "Distance", SortDirection.Ascending, headset: false);

        Assert.Equal("The system nearest on average to the other seven of the 8 rows below, by Distance ▲.", hub.Line);
    }

    [Fact]
    public void TheHeadsetLineIsShorter()
    {
        var hub = CampaignModel.Hub(Rows(12), "Influence", SortDirection.Descending, headset: true);

        Assert.Equal("of the first ten rows, by Influence ▼", hub.Line);
    }

    [Fact]
    public void ARowWithNoPositionIsNamedAndNotReplacedByTheEleventh()
    {
        IReadOnlyList<CampaignSystemRow> rows = [.. Enumerable.Range(0, 10).Select(i => RowAt($"S{i}", i == 3 ? null : i)), RowAt("Eleventh", 5)];

        var hub = CampaignModel.Hub(rows, "Influence", SortDirection.Descending, headset: false);

        Assert.Equal("The system nearest on average to the other eight of the first ten rows below, by Influence ▼. S3 has no position, so it is left out.", hub.Line);
        Assert.NotEqual("Eleventh", hub.System);
    }

    [Fact]
    public void TheHeadsetLineEndsItsClauseBeforeNamingALeftOutRow()
    {
        IReadOnlyList<CampaignSystemRow> rows = [RowAt("A", 0), RowAt("B", null), RowAt("C", 4)];

        var hub = CampaignModel.Hub(rows, "Influence", SortDirection.Descending, headset: true);

        Assert.Equal("of the 3 rows, by Influence ▼. B has no position, so it is left out.", hub.Line);
    }

    [Fact]
    public void SeveralRowsWithNoPositionAreNamedTogether()
    {
        IReadOnlyList<CampaignSystemRow> rows = [RowAt("A", 0), RowAt("B", null), RowAt("C", 4), RowAt("D", null), RowAt("E", null)];

        var hub = CampaignModel.Hub(rows, "Lead", SortDirection.Descending, headset: false);

        Assert.EndsWith("B, D and E have no position, so they are left out.", hub.Line, StringComparison.Ordinal);
    }

    [Fact]
    public void FewerThanTwoPositionedRowsShowADashAndNoHub()
    {
        var hub = CampaignModel.Hub([RowAt("A", 0), RowAt("B", null)], "Influence", SortDirection.Descending, headset: false);

        Assert.Null(hub.System);
        Assert.Equal("Fewer than two of the first ten rows below have a position.", hub.Line);
    }
}

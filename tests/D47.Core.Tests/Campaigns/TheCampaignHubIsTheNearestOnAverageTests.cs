using D47.Core.Campaigns;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;
using static D47.Core.Tests.Campaigns.CampaignReports;

namespace D47.Core.Tests.Campaigns;

/// <summary>The hub of the first ten rows of the Campaign page's Systems table (#2448).</summary>
public sealed class TheCampaignHubIsTheNearestOnAverageTests
{
    private static StarPosition At(double x) => new(x, 0, 0);

    [Fact]
    public void TheHubIsTheOneWithTheSmallestTotalDistanceToTheOthers()
    {
        StarPosition?[] positions = [At(0), At(10), At(11), At(30)];

        Assert.Equal(1, CampaignHub.IndexOf(positions));
    }

    [Fact]
    public void ATieGoesToTheEarlierRow()
    {
        Assert.Equal(0, CampaignHub.IndexOf([At(0), At(10)]));
        Assert.Equal(0, CampaignHub.IndexOf([At(5), At(-5), At(5)]));
    }

    [Fact]
    public void ARowWithNoPositionIsLeftOutAndItsIndexIsNotTheHub()
    {
        StarPosition?[] positions = [null, At(0), At(2), At(3)];

        Assert.Equal(2, CampaignHub.IndexOf(positions));
    }

    [Fact]
    public void FewerThanTwoPositionsHaveNoHub()
    {
        Assert.Null(CampaignHub.IndexOf([]));
        Assert.Null(CampaignHub.IndexOf([At(1)]));
        Assert.Null(CampaignHub.IndexOf([null, At(1), null]));
    }

    [Fact]
    public void EverySystemRowCarriesThePositionTheReportHasForIt()
    {
        var placed = Place("Lave", 0.5) with { Position = new StarPosition(1, 2, 3) };

        var rows = CampaignSystemRows.Of(Report(placed, Place("Diso", 0.2)), SystemStandings.Empty, Campaign.None.Intents, Now, null);

        Assert.Equal(new StarPosition(1, 2, 3), rows.Single(row => row.System == "Lave").Position);
        Assert.Null(rows.Single(row => row.System == "Diso").Position);
    }
}

using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>A page of a page-holding root answers for the root's old trail: links, switches, drills and saved tabs reach it (#2686).</summary>
public class APageUnderAPageHoldingRootIsWhereTheSurfaceIsTests
{
    private bool _carrierOwned = true;

    private PanelNavigator Assets()
    {
        var nav = new PanelNavigator();
        nav.Register(PanelTab.Commander, new NavCrumb("checklist", "Checklist"));
        nav.Register(PanelTab.Commander, new NavCrumb(PanelTabNames.AssetsRoot, "Assets"));
        nav.Register(PanelTab.Transcript, new NavCrumb("conversation", "Conversation"));

        Assert.True(nav.Declare(PanelTabNames.AssetsRoot, new NavCrumb("ships", "Ships") { Starts = true }));
        Assert.True(nav.Declare(PanelTabNames.AssetsRoot, new NavCrumb("carrier", "Carrier") { Starts = true }, () => _carrierOwned));
        Assert.True(nav.Declare(PanelTabNames.AssetsRoot, new NavCrumb("engineers", "Engineers") { Starts = true }));
        Assert.True(nav.Declare("ships", new NavCrumb("compare", "Compare")));
        Assert.True(nav.HoldPages(PanelTabNames.AssetsRoot, ["ships", "carrier", "engineers"]));

        return nav;
    }

    [Fact]
    public void APageOfAPageIsADestinationThatOpensUnderItsPage()
    {
        var nav = Assets();

        Assert.True(nav.Show("compare"));

        Assert.Equal([PanelTabNames.AssetsRoot, "ships", "compare"], nav.Trail.Select(crumb => crumb.Key));
        Assert.Equal("compare", nav.DestinationKey);
        Assert.Contains(nav.Destinations, destination => destination.Key == "compare" && destination.Parent?.Key == "ships");
        Assert.Equal("Compare (Ships)", nav.Destinations.Single(destination => destination.Key == "compare").Describe());
    }

    [Fact]
    public void APageOfAPageIsNotInThePageSidebar()
    {
        var nav = Assets();

        Assert.Equal(["ships", "carrier", "engineers"], nav.PagesOf(PanelTabNames.AssetsRoot).Select(page => page.Key));
    }

    [Fact]
    public void DrilledBelowAPageTheSurfaceIsStillOnThatPage()
    {
        var nav = Assets();

        nav.Show("ships");
        nav.Drill(new NavCrumb("ship:1", "Cold Iron"));

        Assert.Equal("ships", nav.DestinationKey);
        Assert.Equal("ships", nav.PageKeyOf(PanelTab.Commander));
    }

    [Fact]
    public void GoingToCrumbsKeepsThePageOpen()
    {
        var nav = Assets();

        nav.Show("engineers");
        nav.GoTo(new NavCrumb("engineer:3", "Felicity Farseer"));

        Assert.Equal([PanelTabNames.AssetsRoot, "engineers", "engineer:3"], nav.Trail.Select(crumb => crumb.Key));
    }

    [Fact]
    public void GoingToCrumbsThatStartAtAPageOpensThatPage()
    {
        var nav = Assets();

        nav.Show("engineers");
        nav.GoTo(new NavCrumb("ships", "Ships"), new NavCrumb("ship:1", "Cold Iron"));

        Assert.Equal([PanelTabNames.AssetsRoot, "ships", "ship:1"], nav.Trail.Select(crumb => crumb.Key));
    }

    [Fact]
    public void APageThatCanNoLongerOpenGivesWayToTheFirstPage()
    {
        var nav = Assets();

        nav.Show("carrier");
        _carrierOwned = false;
        nav.Refresh();

        Assert.Equal([PanelTabNames.AssetsRoot, "ships"], nav.Trail.Select(crumb => crumb.Key));
        Assert.Equal(["ships", "engineers"], nav.PagesOf(PanelTabNames.AssetsRoot).Select(page => page.Key));
        Assert.False(nav.Show("carrier"));
    }

    [Fact]
    public void ASetOrderListsTheRootsWhateverOrderTheyAreRegisteredIn()
    {
        var nav = new PanelNavigator();
        nav.Order(PanelTab.Commander, ["checklist", PanelTabNames.AssetsRoot, "missions", "watches"]);
        nav.Register(PanelTab.Commander, new NavCrumb("checklist", "Checklist"));
        nav.Register(PanelTab.Commander, new NavCrumb("watches", "Watches"));
        nav.Register(PanelTab.Commander, new NavCrumb("missions", "Missions"));
        nav.Register(PanelTab.Commander, new NavCrumb("ledgers", "Ledgers"));
        nav.Register(PanelTab.Commander, new NavCrumb(PanelTabNames.AssetsRoot, "Assets"));

        Assert.Equal(["checklist", PanelTabNames.AssetsRoot, "missions", "watches", "ledgers"], nav.Roots(PanelTab.Commander).Select(root => root.Key));
        Assert.Equal("checklist", nav.RootKeyOf(PanelTab.Commander));
    }

    [Theory]
    [InlineData("Assets")]
    [InlineData("Loadout")]
    public void ASavedAssetsTabMeansCommanderAndItsAssetsRoot(string saved)
    {
        Assert.Equal(PanelTab.Commander, PanelTabNames.Parse(saved));
        Assert.Equal(PanelTabNames.AssetsRoot, PanelTabNames.RootOf(saved));
    }

    [Fact]
    public void SayingAssetsOpensCommanderAssets()
    {
        var nav = Assets();

        Assert.Equal("Assets.", PanelPhrases.Apply("assets", nav));
        Assert.Equal(PanelTab.Commander, nav.Tab);
        Assert.Equal([PanelTabNames.AssetsRoot, "ships"], nav.Trail.Select(crumb => crumb.Key));
    }
}

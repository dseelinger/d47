using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

public class AHiddenRootLeavesTheTabsRootsTests
{
    private static PanelNavigator Assets()
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Media, new NavCrumb("ships", "Ships"));
        nav.Register(PanelTab.Media, new NavCrumb("carrier", "Carrier"));
        nav.Register(PanelTab.Media, new NavCrumb("engineers", "Engineers"));

        return nav;
    }

    [Fact]
    public void AHiddenRootIsNotARootOrADestination()
    {
        var nav = Assets();

        nav.SetVisible(PanelTab.Media, "carrier", false);

        Assert.Equal(["ships", "engineers"], nav.Roots(PanelTab.Media).Select(r => r.Key));
        Assert.DoesNotContain(nav.Destinations, d => d.Root.Key == "carrier");
        Assert.False(nav.Show("carrier"));
    }

    [Fact]
    public void AShownRootReturnsToItsPlaceInTheOrder()
    {
        var nav = Assets();

        nav.SetVisible(PanelTab.Media, "carrier", false);
        nav.SetVisible(PanelTab.Media, "carrier", true);

        Assert.Equal(["ships", "carrier", "engineers"], nav.Roots(PanelTab.Media).Select(r => r.Key));
    }

    [Fact]
    public void HidingTheRootInViewMovesTheTabToItsFirstRoot()
    {
        var nav = Assets();

        nav.Select(PanelTab.Media);
        nav.SelectRoot("carrier");
        nav.SetVisible(PanelTab.Media, "carrier", false);

        Assert.Equal("ships", nav.RootKeyOf(PanelTab.Media));
    }
}

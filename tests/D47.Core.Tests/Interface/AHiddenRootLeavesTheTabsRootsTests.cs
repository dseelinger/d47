using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

public class AHiddenRootLeavesTheTabsRootsTests
{
    private static PanelNavigator Assets()
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Assets, new NavCrumb("ships", "Ships"));
        nav.Register(PanelTab.Assets, new NavCrumb("carrier", "Carrier"));
        nav.Register(PanelTab.Assets, new NavCrumb("engineers", "Engineers"));

        return nav;
    }

    [Fact]
    public void AHiddenRootIsNotARootOrADestination()
    {
        var nav = Assets();

        nav.SetVisible(PanelTab.Assets, "carrier", false);

        Assert.Equal(["ships", "engineers"], nav.Roots(PanelTab.Assets).Select(r => r.Key));
        Assert.DoesNotContain(nav.Destinations, d => d.Root.Key == "carrier");
        Assert.False(nav.Show("carrier"));
    }

    [Fact]
    public void AShownRootReturnsToItsPlaceInTheOrder()
    {
        var nav = Assets();

        nav.SetVisible(PanelTab.Assets, "carrier", false);
        nav.SetVisible(PanelTab.Assets, "carrier", true);

        Assert.Equal(["ships", "carrier", "engineers"], nav.Roots(PanelTab.Assets).Select(r => r.Key));
    }

    [Fact]
    public void HidingTheRootInViewMovesTheTabToItsFirstRoot()
    {
        var nav = Assets();

        nav.Select(PanelTab.Assets);
        nav.SelectRoot("carrier");
        nav.SetVisible(PanelTab.Assets, "carrier", false);

        Assert.Equal("ships", nav.RootKeyOf(PanelTab.Assets));
    }
}

using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary><see cref="PanelNavigator.Next"/> returns through the places <see cref="PanelNavigator.Previous"/> left, until a move is made.</summary>
public class NextRetracesWhatPreviousLeftTests
{
    private static PanelNavigator Furnished()
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Transcript, new NavCrumb("conversation", "Conversation"));
        nav.Register(PanelTab.Commander, new NavCrumb("checklist", "Checklist"));
        nav.Register(PanelTab.Assets, new NavCrumb("fleet", "Ships"));
        nav.Register(PanelTab.Assets, new NavCrumb("locker", "Suits and weapons"));

        return nav;
    }

    private static string Place(PanelNavigator nav) => $"{nav.Tab}:{string.Join('>', nav.Trail.Select(crumb => crumb.Key))}";

    [Fact]
    public void BackTwiceThenForwardTwiceReturnsToTheStart()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Assets);
        nav.Drill(new NavCrumb("ship:1", "Corsair"));
        var start = Place(nav);

        Assert.True(nav.Previous());
        Assert.True(nav.Previous());
        Assert.Equal("Transcript:conversation", Place(nav));
        Assert.True(nav.CanGoNext);

        Assert.True(nav.Next());
        Assert.Equal("Assets:fleet", Place(nav));
        Assert.True(nav.Next());
        Assert.Equal(start, Place(nav));
        Assert.False(nav.Next());
        Assert.False(nav.CanGoNext);
    }

    [Fact]
    public void AMoveAfterBackClearsForward()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Assets);
        nav.Drill(new NavCrumb("ship:1", "Corsair"));
        Assert.True(nav.Previous());
        Assert.True(nav.CanGoNext);

        nav.Select(PanelTab.Commander);

        Assert.False(nav.CanGoNext);
        Assert.False(nav.Next());
    }

    [Fact]
    public void ForwardThenBackReturnsToWhereBackStarted()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Assets);
        nav.Select(PanelTab.Commander);
        Assert.True(nav.Previous());
        Assert.True(nav.Next());
        Assert.Equal("Commander:checklist", Place(nav));

        Assert.True(nav.Previous());
        Assert.Equal("Assets:fleet", Place(nav));
    }

    [Fact]
    public void ForwardRefusesWhileAChooserIsOpen()
    {
        var nav = Furnished();

        nav.Select(PanelTab.Assets);
        Assert.True(nav.Previous());
        Assert.True(nav.Take(new NavCrumb("choose", "Choose")));

        Assert.False(nav.Next());
        Assert.False(nav.CanGoNext);
    }
}

using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Opening the audio player puts every surface on Media › Music; leaving it puts every surface back on the address they
/// shared before (#2683, #2739).
/// </summary>
public sealed class TheAudioPlayerReturnsEverySurfaceWhereItWasTests
{
    private static readonly NavCrumb Music = new(MediaFurnishing.MusicRoot, "Music");

    private static readonly NavCrumb Podcasts = new(MediaFurnishing.PodcastsRoot, "Podcasts");

    private static readonly NavCrumb Ships = new("loadout.ships", "Ships");

    private static readonly NavCrumb Ship = new("ship.one", "Python");

    private static PanelNavigator Surface()
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Transcript, new NavCrumb("transcript.conversation", "In Ship"));
        nav.Register(PanelTab.Commander, Ships);
        nav.Register(PanelTab.Media, Music);
        nav.Register(PanelTab.Media, Podcasts);

        return nav;
    }

    private static PanelRouting Routed(params PanelNavigator[] surfaces)
    {
        var routing = new PanelRouting(() => false, () => []);

        foreach (var nav in surfaces)
        {
            routing.RouteNavigation(nav, move => move());
        }

        return routing;
    }

    [Fact]
    public void EverySurfaceGoesToMusicAndBackToWhereTheyWere()
    {
        var window = Surface();
        var headset = Surface();
        var mini = Surface();
        var routing = Routed(window, headset, mini);

        Assert.True(headset.Show(Ships.Key));
        Assert.True(headset.Drill(Ship));

        routing.Show(MediaFurnishing.MusicRoot);

        Assert.All([window, headset, mini], nav => Assert.Equal(MediaFurnishing.MusicRoot, nav.DestinationKey));
        Assert.All(routing.Snapshot.Shown, key => Assert.Equal(MediaFurnishing.MusicRoot, key));

        routing.Leave(MediaFurnishing.MusicRoot);

        Assert.All([window, headset, mini], nav =>
        {
            Assert.Equal(PanelTab.Commander, nav.Tab);
            Assert.Equal([Ships.Key, Ship.Key], nav.Trail.Select(crumb => crumb.Key));
        });
        Assert.DoesNotContain(MediaFurnishing.MusicRoot, routing.Snapshot.Shown);
    }

    /// <summary>A surface moved off Music takes every surface with it, so leaving Music afterwards moves nothing.</summary>
    [Fact]
    public void LeavingMusicAfterASurfaceLeftItMovesNothing()
    {
        var window = Surface();
        var headset = Surface();
        var routing = Routed(window, headset);

        routing.Show(MediaFurnishing.MusicRoot);
        Assert.True(headset.Show(Ships.Key));

        Assert.DoesNotContain(MediaFurnishing.MusicRoot, routing.Snapshot.Shown);

        routing.Leave(MediaFurnishing.MusicRoot);

        Assert.Equal(Ships.Key, window.DestinationKey);
        Assert.Equal(Ships.Key, headset.DestinationKey);
    }

    [Fact]
    public void ToggleOpensThenClosesEverySurface()
    {
        var window = Surface();
        var mini = Surface();
        var routing = Routed(window, mini);

        Assert.Equal(PageToggle.Opened, routing.Toggle(MediaFurnishing.MusicRoot, null));
        Assert.Equal(PageToggle.AlreadyOpen, routing.Toggle(MediaFurnishing.MusicRoot, true));
        Assert.Equal(PageToggle.Closed, routing.Toggle(MediaFurnishing.MusicRoot, null));
        Assert.Equal(PanelTab.Transcript, window.Tab);
        Assert.Equal(PanelTab.Transcript, mini.Tab);
        Assert.Equal(PageToggle.AlreadyClosed, routing.Toggle(MediaFurnishing.MusicRoot, false));
    }

    /// <summary>The window is clicked away from Music and the mini panel goes with it: the next press opens it on both again.</summary>
    [Fact]
    public void ToggleAfterASurfaceLeftMusicOpensItEverywhereAgain()
    {
        var window = Surface();
        var mini = Surface();
        var routing = Routed(window, mini);

        routing.Toggle(MediaFurnishing.MusicRoot, null);
        Assert.True(window.Show(Ships.Key));

        Assert.Equal(Ships.Key, mini.DestinationKey);

        Assert.Equal(PageToggle.Opened, routing.Toggle(MediaFurnishing.MusicRoot, null));
        Assert.Equal(MediaFurnishing.MusicRoot, window.DestinationKey);
        Assert.Equal(MediaFurnishing.MusicRoot, mini.DestinationKey);

        Assert.Equal(PageToggle.Closed, routing.Toggle(MediaFurnishing.MusicRoot, null));
        Assert.Equal(Ships.Key, window.DestinationKey);
        Assert.Equal(Ships.Key, mini.DestinationKey);
    }
}

using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>Opening the audio player puts every surface on Media › Music; leaving it puts each surface on it back where it was (#2683).</summary>
public sealed class TheAudioPlayerReturnsEachSurfaceWhereItWasTests
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
    public void EverySurfaceGoesToMusicAndBackToItsOwnPage()
    {
        var window = Surface();
        var headset = Surface();
        var mini = Surface();

        Assert.True(headset.Show(Ships.Key));
        Assert.True(headset.Drill(Ship));
        Assert.True(mini.Show(Podcasts.Key));

        var routing = Routed(window, headset, mini);

        routing.Show(MediaFurnishing.MusicRoot);

        Assert.All([window, headset, mini], nav => Assert.Equal(MediaFurnishing.MusicRoot, nav.DestinationKey));
        Assert.All(routing.Snapshot.Shown, key => Assert.Equal(MediaFurnishing.MusicRoot, key));

        routing.Leave(MediaFurnishing.MusicRoot);

        Assert.Equal(PanelTab.Transcript, window.Tab);
        Assert.Equal(PanelTab.Commander, headset.Tab);
        Assert.Equal([Ships.Key, Ship.Key], headset.Trail.Select(crumb => crumb.Key));
        Assert.Equal(PanelTab.Media, mini.Tab);
        Assert.Equal(Podcasts.Key, mini.DestinationKey);
        Assert.DoesNotContain(MediaFurnishing.MusicRoot, routing.Snapshot.Shown);
    }

    [Fact]
    public void ASurfaceThatLeftMusicItselfStaysWhereItWent()
    {
        var window = Surface();
        var headset = Surface();
        var routing = Routed(window, headset);

        routing.Show(MediaFurnishing.MusicRoot);
        Assert.True(headset.Show(Ships.Key));

        Assert.Contains(MediaFurnishing.MusicRoot, routing.Snapshot.Shown);

        routing.Leave(MediaFurnishing.MusicRoot);

        Assert.Equal(PanelTab.Transcript, window.Tab);
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

    /// <summary>The window is clicked away from Music while the mini panel stays on it: the next press opens it on the window again.</summary>
    [Fact]
    public void ToggleAfterASurfaceLeftMusicOpensItThereAgain()
    {
        var window = Surface();
        var mini = Surface();
        var routing = Routed(window, mini);

        routing.Toggle(MediaFurnishing.MusicRoot, null);
        Assert.True(window.Show(Ships.Key));

        Assert.Equal(PageToggle.Opened, routing.Toggle(MediaFurnishing.MusicRoot, null));
        Assert.Equal(MediaFurnishing.MusicRoot, window.DestinationKey);
        Assert.Equal(MediaFurnishing.MusicRoot, mini.DestinationKey);

        Assert.Equal(PageToggle.Closed, routing.Toggle(MediaFurnishing.MusicRoot, null));
        Assert.Equal(Ships.Key, window.DestinationKey);
        Assert.Equal(PanelTab.Transcript, mini.Tab);
    }
}

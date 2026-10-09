using Avalonia.Headless.XUnit;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>A spoken or switched request reaches every routed panel surface through that surface's own thread.</summary>
public class EverySurfaceMovesWithTheOthersTests
{
    private sealed class Surface
    {
        public PanelNavigator Nav { get; } = Furnished();

        public List<Action> Posted { get; } = [];

        public void Post(Action action) => Posted.Add(action);

        public void RunPosted()
        {
            foreach (var action in Posted.ToList())
            {
                action();
            }

            Posted.Clear();
        }
    }

    private static PanelNavigator Furnished()
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Transcript, new NavCrumb("conversation", "Conversation"));
        nav.Register(PanelTab.Search, new NavCrumb("lookup", "Lookup"));
        nav.Register(PanelTab.Search, new NavCrumb("compare", "Compare"));

        return nav;
    }

    private static (PanelRouting Routing, Surface Window, Surface Headset) TwoSurfaces(Func<bool>? headsetShowing = null)
    {
        var routing = new PanelRouting(headsetShowing ?? (() => false), () => []);
        var window = new Surface();
        var headset = new Surface();

        routing.RouteNavigation(window.Nav, window.Post);
        routing.RouteNavigation(headset.Nav, headset.Post);

        return (routing, window, headset);
    }

    [Fact]
    public void AShowReachesEachSurfaceOnlyThroughItsOwnPost()
    {
        var (routing, window, headset) = TwoSurfaces();

        routing.Show("lookup");

        Assert.Equal(PanelTab.Transcript, window.Nav.Tab);
        Assert.Equal(PanelTab.Transcript, headset.Nav.Tab);

        window.RunPosted();

        Assert.Equal("lookup", window.Nav.Root.Key);
        Assert.Equal(PanelTab.Transcript, headset.Nav.Tab);

        headset.RunPosted();

        Assert.Equal("lookup", headset.Nav.Root.Key);
    }

    [Fact]
    public void ThePanelIsShowingARootOnlyWhileEverySurfaceAgrees()
    {
        var (routing, window, headset) = TwoSurfaces();

        routing.Show("lookup");
        window.RunPosted();
        headset.RunPosted();

        Assert.Equal("lookup", routing.Snapshot.Showing);

        window.Nav.Show("compare");

        Assert.Null(routing.Snapshot.Showing);
    }

    [Fact]
    public void EachRootIsOfferedOnceHoweverManySurfacesFurnishIt()
    {
        var (routing, _, _) = TwoSurfaces();

        var keys = routing.Snapshot.Destinations.Select(destination => destination.Root.Key).ToList();

        Assert.Equal(["conversation", "lookup", "compare"], keys);
    }

    private static (PanelRouting Routing, PanelPrompts Window, PanelPrompts Headset, Surface WindowSurface, Surface HeadsetSurface, Box<bool> Showing)
        Prompting()
    {
        var showing = new Box<bool>();
        var routing = new PanelRouting(() => showing.Value, () => []);
        var windowSurface = new Surface();
        var headsetSurface = new Surface();
        var window = new PanelPrompts(windowSurface.Nav, new Avalonia.Controls.Panel());
        var headset = new PanelPrompts(headsetSurface.Nav, new Avalonia.Controls.Panel());

        routing.RoutePromptSurface(window, windowSurface.Post);
        routing.RoutePromptSurface(headset, headsetSurface.Post, headset: true);

        return (routing, window, headset, windowSurface, headsetSurface, showing);
    }

    private sealed class Box<T>
    {
        public T Value { get; set; } = default!;
    }

    private static readonly EntryRequest Request =
        new("entry", "Minutes", "How long?", "Minutes, from now.", string.Empty, EntrySurface.Voice);

    [AvaloniaFact]
    public void AnEntryOpensOnTheWindowWhileTheHeadsetIsNotShowing()
    {
        var (routing, window, headset, windowSurface, headsetSurface, _) = Prompting();

        routing.OnPromptSurface(prompts => prompts.Enter(Request, _ => { }));
        windowSurface.RunPosted();
        headsetSurface.RunPosted();

        Assert.True(window.IsOpen);
        Assert.False(headset.IsOpen);
    }

    [AvaloniaFact]
    public void AnEntryOpensOnTheHeadsetWhileItIsShowing()
    {
        var (routing, window, headset, windowSurface, headsetSurface, showing) = Prompting();

        showing.Value = true;
        routing.OnPromptSurface(prompts => prompts.Enter(Request, _ => { }));
        windowSurface.RunPosted();
        headsetSurface.RunPosted();

        Assert.False(window.IsOpen);
        Assert.True(headset.IsOpen);
    }

    [AvaloniaFact]
    public void AnEntryOpensNothingWhileEitherSurfaceHoldsOne()
    {
        var (routing, window, headset, windowSurface, headsetSurface, showing) = Prompting();

        window.Enter(Request, _ => { });
        showing.Value = true;

        var opened = false;

        routing.OnPromptSurface(_ => opened = true);
        windowSurface.RunPosted();
        headsetSurface.RunPosted();

        Assert.False(opened);
        Assert.False(headset.IsOpen);
    }

    [Fact]
    public void SpeechIsTakenWhenARoutedSurfaceTakesIt()
    {
        var routing = new PanelRouting(() => false, () => []);
        var heard = new Heard("five", 1, true);

        Assert.False(routing.Prompted(heard));

        routing.RoutePrompts(_ => false);

        Assert.False(routing.Prompted(heard));

        routing.RoutePrompts(_ => true);

        Assert.True(routing.Prompted(heard));
    }
}

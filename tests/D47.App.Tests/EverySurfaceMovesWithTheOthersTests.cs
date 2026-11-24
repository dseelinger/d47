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

    /// <summary>Nothing moves until a surface's post runs; the first to run moves its surface and the mirror carries it to the others (#2739).</summary>
    [Fact]
    public void AShowMovesNothingUntilAPostRunsAndTheFirstIsCarriedToTheOthers()
    {
        var (routing, window, headset) = TwoSurfaces();

        routing.Show("lookup");

        Assert.Equal(PanelTab.Transcript, window.Nav.Tab);
        Assert.Equal(PanelTab.Transcript, headset.Nav.Tab);

        window.RunPosted();

        Assert.Equal("lookup", window.Nav.Root.Key);
        Assert.Equal("lookup", headset.Nav.Root.Key);

        var moves = headset.Nav.Moves;

        headset.RunPosted();

        Assert.Equal("lookup", headset.Nav.Root.Key);
        Assert.Equal(moves, headset.Nav.Moves);
    }

    [Fact]
    public void ThePanelIsShowingARootOnlyWhileEverySurfaceAgrees()
    {
        var (routing, window, headset) = TwoSurfaces();

        routing.Show("lookup");
        window.RunPosted();
        headset.RunPosted();

        Assert.Equal("lookup", routing.Snapshot.Showing);

        // A chooser holds the headset where it is while the window moves.
        Assert.True(headset.Nav.Take(new NavCrumb("pick", "Pick one")));

        window.Nav.Show("compare");

        Assert.Null(routing.Snapshot.Showing);

        Assert.True(headset.Nav.Back());

        Assert.Equal("compare", routing.Snapshot.Showing);
    }

    /// <summary>A chooser drawn over the headset's page holds it like one on the trail, and closing it takes the headset to the window (#2739).</summary>
    [AvaloniaFact]
    public void AChooserOnTheLayerHoldsItsSurfaceUntilItCloses()
    {
        var (_, window, headset) = TwoSurfaces();
        var prompts = new PanelPrompts(headset.Nav, new Avalonia.Controls.Panel());

        prompts.Choose(
            new ChoiceRequest("pick", "Pick", "Pick one", null, [new ChoiceOption("a", "A")], null, ChoiceSurface.Layer),
            _ => { });

        Assert.True(prompts.IsOpen);
        Assert.False(headset.Nav.Modal);

        Assert.True(window.Nav.Show("compare"));

        Assert.Equal(PanelTab.Transcript, headset.Nav.Tab);

        prompts.Abandon();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal("compare", headset.Nav.Root.Key);
    }

    [Fact]
    public void EachRootIsOfferedOnceHoweverManySurfacesFurnishIt()
    {
        var (routing, _, _) = TwoSurfaces();

        var keys = routing.Snapshot.Destinations.Select(destination => destination.Root.Key).ToList();

        Assert.Equal(["conversation", "lookup", "compare"], keys);
    }

    private static (PanelRouting Routing, Surface[] Surfaces) ThreeSurfaces()
    {
        var routing = new PanelRouting(() => false, () => []);
        Surface[] surfaces = [new(), new(), new()];

        foreach (var surface in surfaces)
        {
            routing.RouteNavigation(surface.Nav, surface.Post);
        }

        return (routing, surfaces);
    }

    /// <summary>A spoken "back" moves one surface one place, and the mirror takes the others with it: one place, not one per surface (#2739).</summary>
    [Fact]
    public void ASpokenBackGoesOnePlaceOnEverySurface()
    {
        var (routing, surfaces) = ThreeSurfaces();
        var first = surfaces[0].Nav;

        Assert.True(first.Show("lookup"));
        Assert.True(first.Drill(new NavCrumb("item", "An item")));
        Assert.True(first.Drill(new NavCrumb("step", "A step")));

        Assert.NotNull(routing.Navigate("back"));

        Assert.All(surfaces, surface => Assert.Equal(["lookup", "item"], surface.Nav.Trail.Select(crumb => crumb.Key)));

        Assert.NotNull(routing.Navigate("back"));

        Assert.All(surfaces, surface => Assert.Equal(["lookup"], surface.Nav.Trail.Select(crumb => crumb.Key)));
    }

    /// <summary>A switch's next-tab step, posted to every surface, moves every surface one tab (#2739).</summary>
    [Fact]
    public void ANextTabSwitchStepsEverySurfaceOneTab()
    {
        var (routing, surfaces) = ThreeSurfaces();

        routing.Show(D47.Core.Hotas.RelativeTab.Next);

        foreach (var surface in surfaces)
        {
            surface.RunPosted();
        }

        Assert.All(surfaces, surface => Assert.Equal(PanelTab.Search, surface.Nav.Tab));

        routing.Show(D47.Core.Hotas.RelativeTab.Next);

        foreach (var surface in surfaces)
        {
            surface.RunPosted();
        }

        Assert.All(surfaces, surface => Assert.Equal(PanelTab.Transcript, surface.Nav.Tab));
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

    [Fact]
    public async Task RoutingWhileTheTickShowsAndSpeechIsHeardThrowsNothing()
    {
        var routing = new PanelRouting(() => false, () => []);
        var routed = false;
        var failures = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        var heard = new Heard("five", 1, true);

        var reader = Task.Run(() =>
        {
            while (!Volatile.Read(ref routed))
            {
                try
                {
                    routing.Show("lookup");
                    routing.Prompted(heard);
                }
                catch (Exception ex)
                {
                    failures.Add(ex);
                }
            }
        }, TestContext.Current.CancellationToken);

        for (var i = 0; i < 200; i++)
        {
            routing.RouteNavigation(new PanelNavigator(), _ => { });
            routing.RoutePrompts(_ => false);
        }

        Volatile.Write(ref routed, true);
        await reader;

        Assert.Empty(failures);
    }
}

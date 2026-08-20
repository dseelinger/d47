using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Journal;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Routing tab (list.md Phase 37): where the Commander is going, in three readings of one
/// journey.
/// <para>
/// The tab is furnished by the desktop window and by nothing else, so most of what is asserted
/// here is that it behaves like every other furnished tab — and that Progress draws the whole
/// route, which is the thing the spoken form structurally cannot do.
/// </para>
/// </summary>
public class TheRoutingTabTests
{
    private static RouteHop Hop(string system, double x = 0, string? starClass = "G") =>
        new(system, starClass) { Position = (x, 0, 0) };

    private static NavRoute Route(params RouteHop[] hops) => new() { Hops = hops };

    private static PanelView Laid(PanelView panel, double width = 900)
    {
        var window = new Window { Content = panel, Width = width, Height = 700 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return panel;
    }

    private static PanelView Furnished(NavRoute route, string? here = null)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableRouting(() => route, () => here);

        return Laid(panel);
    }

    private static IEnumerable<string> TextOf(PanelView panel) =>
        panel.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .Where(text => text.Length > 0);

    /// <summary>
    /// A tab nobody furnished is not drawn, and the headset furnishes nothing — which is the
    /// whole of "desktop only" (CLAUDE.md: a tab is withdrawn from a surface by not making the
    /// call).
    /// </summary>
    [AvaloniaFact]
    public void TheTabIsAbsentUntilAHostFurnishesIt()
    {
        var panel = Laid(new PanelView { DataContext = new PanelViewModel() });

        Assert.False(panel.GetControl<RadioButton>("RoutingTab").IsVisible);

        panel.Tab = PanelTab.Routing;

        // Not merely undrawn — unreachable. Selecting a tab nobody built has to fall back rather
        // than leave the panel on an empty page.
        Assert.Equal(PanelTab.Transcript, panel.Tab);
    }

    [AvaloniaFact]
    public void FurnishingItShowsTheTabAndSelectingItSticks()
    {
        var panel = Furnished(Route(Hop("Sol"), Hop("Alpha Centauri", 4)));

        Assert.True(panel.GetControl<RadioButton>("RoutingTab").IsVisible);

        panel.Tab = PanelTab.Routing;

        Assert.Equal(PanelTab.Routing, panel.Tab);

        // The root is the first crumb, and it is a word the Commander can say as well as press.
        Assert.Equal("Progress", panel.Nav.Root.Word);
    }

    /// <summary>
    /// The item that justifies the tab: every hop, not the next handful. A spoken answer is
    /// capped because reading 131 waypoints aloud is not an answer; a screen has no such cap.
    /// </summary>
    [AvaloniaFact]
    public void ProgressDrawsEveryHopRatherThanTheNextFew()
    {
        var hops = Enumerable.Range(0, 40).Select(index => Hop($"Waypoint {index}", index)).ToArray();
        var panel = Furnished(Route(hops), "Waypoint 0");

        panel.Tab = PanelTab.Routing;
        Dispatcher.UIThread.RunJobs();

        var drawn = TextOf(panel).ToArray();

        Assert.Contains("Waypoint 0", drawn);
        Assert.Contains("Waypoint 39", drawn);
        Assert.Contains(drawn, text => text.Contains("39 jumps left", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void NothingPlottedSaysSoRatherThanDrawingAnEmptyList()
    {
        var panel = Furnished(NavRoute.None);

        panel.Tab = PanelTab.Routing;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(TextOf(panel), text => text.Contains("No route plotted", StringComparison.Ordinal));
    }

    /// <summary>
    /// A hazard rides on the hop rather than in a preamble, because it is the only line that
    /// changes what the Commander does on arrival — the same call routes.md records for the
    /// spoken form.
    /// </summary>
    [AvaloniaFact]
    public void AHazardousHopCarriesItsWarningOnItsOwnRow()
    {
        var panel = Furnished(
            Route(Hop("Sol"), Hop("Jackson's Lighthouse", 4, "N"), Hop("Colonia", 9)),
            "Sol");

        panel.Tab = PanelTab.Routing;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(TextOf(panel), text => text.Contains("neutron", StringComparison.Ordinal));
    }

    /// <summary>
    /// A class d47 does not recognise is drawn as not knowing, never as "no". A Commander told a
    /// star is unscoopable routes around one that would have refuelled them.
    /// </summary>
    [AvaloniaFact]
    public void AnUnknownStarClassIsDrawnAsUnknownRatherThanAsUnscoopable()
    {
        var panel = Furnished(Route(Hop("Sol"), Hop("Somewhere", 4, "ZZ")), "Sol");

        panel.Tab = PanelTab.Routing;
        Dispatcher.UIThread.RunJobs();

        var drawn = TextOf(panel).ToArray();

        Assert.Contains(drawn, text => text.Contains("scoop unknown", StringComparison.Ordinal));
        Assert.DoesNotContain(drawn, text => text.Contains("no scoop", StringComparison.Ordinal));
    }

    /// <summary>
    /// Being somewhere the route does not mention is said out loud, because the alternative is a
    /// jumps-remaining figure that quietly means something else.
    /// </summary>
    [AvaloniaFact]
    public void BeingOffTheRouteIsStatedRatherThanImplied()
    {
        var panel = Furnished(Route(Hop("Sol"), Hop("Alpha Centauri", 4)), "Shinrarta Dezhra");

        panel.Tab = PanelTab.Routing;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(
            TextOf(panel),
            text => text.Contains("not on this route", StringComparison.Ordinal));
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>The last neutron plot, on the mini panel (#197).</summary>
public sealed class RouteMini : UserControl
{
    private readonly RoutePlanBook _plans;
    private readonly Func<NavRoute> _route;
    private readonly Func<string?> _here;
    private readonly StackPanel _body = new() { Spacing = 2 };

    public RouteMini(RoutePlanBook plans, Func<NavRoute> route, Func<string?> here)
    {
        _plans = plans;
        _route = route;
        _here = here;

        Content = new ScrollViewer
        {
            Content = _body,
            Margin = new Thickness(10, 8),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        Fill();
    }

    /// <summary>Redrawn when the plan, the route file or the current system moves. Dispatched to the
    /// UI thread, since <see cref="RoutePlanBook.Apply"/> can call it from the tick thread.</summary>
    public void Refresh() => Dispatcher.UIThread.Post(Fill);

    /// <summary>No waypoint marked as next.</summary>
    private const int None = -1;

    /// <summary>The final waypoint is behind the Commander.</summary>
    private const int Arrived = -2;

    private void Fill()
    {
        _body.Children.Clear();

        if (_plans.Last(RoutePlanKind.Jump) is not { Jump: { } jump })
        {
            _body.Children.Add(Muted("No neutron route is plotted."));
            return;
        }

        var title = TitleText.Build(jump.Origin, TypeScale.Heading, TitleRank.Screen, sentence: true);
        title.Margin = new Thickness(0, 0, 0, 4);
        _body.Children.Add(title);

        var here = _here();
        var next = NextWaypoint(jump, _route(), here);

        if (next == Arrived)
        {
            _body.Children.Add(Muted("The destination is reached."));
        }

        for (var index = 0; index < jump.Waypoints.Count; index++)
        {
            _body.Children.Add(Row(jump.Waypoints[index], index == next, here));
        }
    }

    /// <summary>Which waypoint the Commander is headed to next (#197).</summary>
    private static int NextWaypoint(PlottedRoute jump, NavRoute route, string? here)
    {
        if (route.IsPlotted && IndexOf(jump, route.Hops[^1].StarSystem) is var byRoute and >= 0)
        {
            return byRoute;
        }

        var byHere = IndexOf(jump, here);

        if (byHere == None)
        {
            return None;
        }

        return byHere + 1 == jump.Waypoints.Count ? Arrived : byHere + 1;
    }

    private static int IndexOf(PlottedRoute jump, string? system)
    {
        if (system is null)
        {
            return None;
        }

        for (var index = 0; index < jump.Waypoints.Count; index++)
        {
            if (string.Equals(jump.Waypoints[index].System, system, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return None;
    }

    private static Control Row(RouteWaypoint waypoint, bool isNext, string? here)
    {
        var values = new List<string> { waypoint.Jumps == 1 ? "1 jump" : $"{waypoint.Jumps} jumps" };

        if (waypoint.DistanceJumped is { } jumped)
        {
            values.Add($"{jumped:N0} ly");
        }

        if (waypoint.DistanceLeftToReport is { } left)
        {
            values.Add($"{left:N0} ly left");
        }

        var tags = new List<Control>();

        if (waypoint.IsNeutron)
        {
            tags.Add(RoutingKit.Tag("neutron", ThemeManager.AKey));
        }

        if (isNext)
        {
            tags.Add(RoutingKit.Tag("next", ThemeManager.AKey));
        }

        var row = RoutingKit.Row(
            [
                RoutingKit.Named(waypoint.System, RoutingKit.SystemKey(waypoint.System, here), [.. tags]),
                RoutingKit.Values(string.Join(" · ", values)),
            ],
            waypoint.System,
            copy: null);

        row.Padding = new Thickness(10, 4);
        row.MinHeight = 0;

        return row;
    }

    private static TextBlock Muted(string text) => RoutingKit.Ink(text, TypeScale.Secondary, ThemeManager.GreyKey);
}

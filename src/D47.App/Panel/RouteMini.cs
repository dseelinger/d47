using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>The Plan root's plan on the mini panel (#197, #1926).</summary>
public sealed class RouteMini : UserControl
{
    private readonly Func<StoredRoutePlan?> _plan;
    private readonly Func<NavRoute> _route;
    private readonly Func<string?> _here;
    private readonly Func<string, string?>? _offDuty;
    private readonly Func<string, Task<bool>>? _copy;
    private readonly StackPanel _body = new() { Spacing = 2 };
    private readonly ScrollViewer _scroll;

    /// <summary>The plan and stop the list was last scrolled to; null while no plan is drawn.</summary>
    private (RoutePlanKind Kind, DateTimeOffset PlottedAt, int Stop)? _anchored;

    /// <summary>The row to bring to the top once it has been laid out; null when the list is where it belongs.</summary>
    private Control? _anchorRow;

    /// <param name="plan">The plan to draw; the last Jump plan when null.</param>
    public RouteMini(
        RoutePlanBook plans,
        Func<NavRoute> route,
        Func<string?> here,
        Func<string, string?>? offDuty = null,
        Func<StoredRoutePlan?>? plan = null,
        Func<string, Task<bool>>? copy = null)
    {
        _copy = copy;
        _offDuty = offDuty;
        _plan = plan ?? (() => plans.Last(RoutePlanKind.Jump));
        _route = route;
        _here = here;

        Content = _scroll = new ScrollViewer
        {
            Content = _body,
            Margin = new Thickness(10, 8),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        LayoutUpdated += (_, _) => KeepAnchor();

        Fill();
    }

    /// <summary>
    /// Scrolls the Commander's stop to the top once its row has been laid out. Called after each layout
    /// pass, and by <see cref="PanelView.KeepUp"/> between an offscreen surface's two passes.
    /// </summary>
    public void KeepAnchor()
    {
        if (_anchorRow is not { IsArrangeValid: true } row
            || row.Bounds.Height <= 0
            || _scroll.Viewport.Height <= 0
            || !_body.Children.Contains(row))
        {
            return;
        }

        _anchorRow = null;
        _scroll.Offset = new Vector(_scroll.Offset.X, row.Bounds.Y);
    }

    /// <summary>Redrawn when the plan, the route file or the current system moves. Dispatched to the
    /// UI thread, since <see cref="RoutePlanBook.Apply"/> can call it from the tick thread.</summary>
    public void Refresh() => Dispatcher.UIThread.Post(Fill);

    /// <summary>No waypoint marked as next.</summary>
    private const int None = -1;

    /// <summary>The final waypoint is behind the Commander.</summary>
    private const int Arrived = -2;

    private readonly record struct Stop(string System, string Values, bool Neutron = false, string? Restock = null);

    private void Fill()
    {
        _body.Children.Clear();

        if (_offDuty?.Invoke(OffDutyNote.Route) is { } offDuty)
        {
            Unanchor();
            _body.Children.Add(Muted(offDuty));
            return;
        }

        if (_plan() is not { } plan || Stops(plan) is not { Count: > 0 } stops)
        {
            Unanchor();
            _body.Children.Add(Muted(_route().LiveTarget is { } ahead ? ahead.NextJump : "No plan is plotted."));
            return;
        }

        var title = Title(plan);
        _body.Children.Add(title);

        var here = _here();
        var anchor = Anchor(stops, plan.Reached, here);
        var moved = _anchored != (plan.Kind, plan.PlottedAt, anchor);

        if (moved)
        {
            _anchored = (plan.Kind, plan.PlottedAt, anchor);
        }

        if (moved || _anchorRow is not null)
        {
            _anchorRow = title;
        }

        var next = plan.Jump is { } jump
            ? NextWaypoint(jump, _route(), here)
            : plan.Reached is { } reached ? (reached + 1 >= stops.Count ? Arrived : reached + 1) : 0;

        if (next == Arrived)
        {
            _body.Children.Add(Muted("The destination is reached."));
        }

        for (var index = 0; index < stops.Count; index++)
        {
            var row = Row(stops[index], index == next, here);
            _body.Children.Add(row);

            if (index == anchor && _anchorRow is not null)
            {
                _anchorRow = row;
            }
        }
    }

    private void Unanchor()
    {
        _anchored = null;
        _anchorRow = null;
    }

    /// <summary>
    /// The stop the list scrolls to: the current system, else the last stop reached, else none, which
    /// is the top of the list.
    /// </summary>
    private static int Anchor(List<Stop> stops, int? reached, string? here)
    {
        var last = reached is { } at && at >= 0 && at < stops.Count ? at : None;
        var found = IndexOf(stops, here, Math.Max(last, 0));

        if (found == None && last > 0)
        {
            found = IndexOf(stops, here, 0);
        }

        return found == None ? last : found;
    }

    private static int IndexOf(List<Stop> stops, string? here, int from)
    {
        for (var index = from; index < stops.Count; index++)
        {
            if (RoutingKit.IsHere(stops[index].System, here))
            {
                return index;
            }
        }

        return None;
    }

    private static Control Title(StoredRoutePlan plan)
    {
        var word = RoutingKit.Ink($"{GalaxyMapPage.PlanKindWord(plan.Kind)} plan:".ToUpperInvariant(), TypeScale.MetaSmall, ThemeManager.GreyKey);
        var headline = RoutingKit.Ink(plan.Headline, 14, ThemeManager.WhiteKey);
        headline.TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;

        return new StackPanel { Margin = new Thickness(0, 0, 0, 4), Children = { word, headline } };
    }

    private static string Jumps(int count) => count == 1 ? "1 jump" : $"{count} jumps";

    private static List<Stop>? Stops(StoredRoutePlan plan) => plan switch
    {
        { Jump: { } jump } => [.. jump.Waypoints.Select(waypoint => new Stop(waypoint.System, JumpValues(waypoint), waypoint.IsNeutron))],
        { Riches: { } riches } => [.. riches.Stops.Select(stop => new Stop(stop.System, Jumps(stop.Jumps)))],
        { Exobiology: { } exobiology } => [.. exobiology.Stops.Select(stop => new Stop(stop.System, Jumps(stop.Jumps)))],
        { Carrier: { } carrier } => [.. carrier.Waypoints.Select(waypoint => new Stop(
            waypoint.Name,
            waypoint.Distance > 0 ? $"{waypoint.Distance:N1} ly" : "start",
            Restock: waypoint.MustRestock ? $"restock {waypoint.RestockAmount:N0} t" : null))],
        _ => null,
    };

    private static string JumpValues(RouteWaypoint waypoint)
    {
        var values = new List<string> { Jumps(waypoint.Jumps) };

        if (waypoint.DistanceJumped is { } jumped)
        {
            values.Add($"{jumped:N0} ly");
        }

        if (waypoint.DistanceLeftToReport is { } left)
        {
            values.Add($"{left:N0} ly left");
        }

        return string.Join(" · ", values);
    }

    /// <summary>Which waypoint the Commander is headed to next (#197).</summary>
    private static int NextWaypoint(PlottedRoute jump, NavRoute route, string? here)
    {
        if (route.LiveTarget is { } target && IndexOf(jump, target.StarSystem) is var byTarget and >= 0)
        {
            return byTarget;
        }

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

    private Control Row(Stop stop, bool isNext, string? here)
    {
        var tags = new List<Control>();

        if (stop.Neutron)
        {
            tags.Add(RoutingKit.Tag("neutron", ThemeManager.AKey));
        }

        if (stop.Restock is { } restock)
        {
            tags.Add(RoutingKit.Tag(restock, ThemeManager.AKey));
        }

        if (isNext)
        {
            tags.Add(RoutingKit.Tag("next", ThemeManager.AKey));
        }

        var row = RoutingKit.Row(
            [
                RoutingKit.Named(stop.System, RoutingKit.SystemKey(stop.System, here), [.. tags]),
                RoutingKit.Values(stop.Values),
            ],
            stop.System,
            _copy);

        row.Padding = new Thickness(10, 4);
        row.MinHeight = 0;

        return row;
    }

    private static TextBlock Muted(string text) => RoutingKit.Ink(text, TypeScale.Secondary, ThemeManager.GreyKey);
}

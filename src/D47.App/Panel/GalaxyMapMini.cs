using Avalonia;
using Avalonia.Controls;
using D47.App.Theming;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>The Galaxy map root on the mini panel: the route still ahead, framed to hold the Commander and its stops (#1916).</summary>
public sealed class GalaxyMapMini : UserControl
{
    private const double Inset = 20;

    private readonly Func<CommanderGameState?> _commander;
    private readonly Func<NavRoute> _route;
    private readonly Func<string?> _here;
    private readonly Func<string, string?>? _offDuty;
    private readonly Func<StoredRoutePlan?> _plan;
    private readonly GalaxyMapView _map = new() { IsHitTestVisible = false };
    private object? _seen;

    public GalaxyMapMini(
        Func<CommanderGameState?> commander,
        Func<NavRoute> route,
        Func<string?> here,
        Func<string, string?>? offDuty,
        Func<StoredRoutePlan?> plan)
    {
        _commander = commander;
        _route = route;
        _here = here;
        _offDuty = offDuty;
        _plan = plan;

        _map.Fit(Inset, eachAxis: true);
        Refresh();
    }

    internal GalaxyMapView Map => _map;

    /// <summary>The words shown in place of the map; empty while the map is drawn.</summary>
    internal string Note => Content is TextBlock note ? note.Text ?? string.Empty : string.Empty;

    /// <summary>Redraws when the route, the position or the plan moved; true when it did.</summary>
    public bool Tick()
    {
        var plan = _plan();
        var key = (_route(), _here(), _commander()?.Location.StarPos, _offDuty?.Invoke(OffDutyNote.Route), plan, plan is null ? -1 : RouteMapStop.ReachedIndex(plan));

        if (Equals(key, _seen))
        {
            return false;
        }

        Refresh();

        return true;
    }

    public void Refresh()
    {
        var plan = _plan();
        _seen = (_route(), _here(), _commander()?.Location.StarPos, _offDuty?.Invoke(OffDutyNote.Route), plan, plan is null ? -1 : RouteMapStop.ReachedIndex(plan));

        if (_offDuty?.Invoke(OffDutyNote.Route) is { } offDuty)
        {
            Content = Muted(offDuty);
            return;
        }

        var here = _here();
        var route = _route();
        var planStops = plan is null ? null : RouteMapStop.From(plan);
        var planned = planStops is not null && planStops.Count(stop => stop.Position is not null) >= 2;
        var stops = planned ? planStops! : route.IsPlotted ? RouteMapStop.From(route) : [];
        var reached = planned ? RouteMapStop.ReachedIndex(plan!) : RouteProgress.For(route, here).Index;

        if (stops.Count == 0)
        {
            Content = Muted("No route is plotted.");
            return;
        }

        if (reached >= stops.Count - 1)
        {
            Content = Muted("The destination is reached.");
            return;
        }

        var ahead = stops.Skip(reached + 1).ToList();
        var origin = reached >= 0 && stops[reached].Position is { } onRoute ? onRoute : _commander()?.Location.StarPos;

        IReadOnlyList<RouteMapStop> drawn = origin is { } from
            ? [new RouteMapStop(here ?? string.Empty, from, RouteStopMarks.None), .. ahead]
            : ahead;

        _map.Show(new GalaxyMapScene(
            Regions: false,
            Travels: null,
            Route: drawn,
            Reached: origin is null ? -1 : 0,
            Commander: origin,
            Here: here));

        Content = _map;
    }

    private static TextBlock Muted(string text)
    {
        var note = RoutingKit.Ink(text, TypeScale.Secondary, ThemeManager.GreyKey);
        note.Margin = new Thickness(10, 8);

        return note;
    }
}

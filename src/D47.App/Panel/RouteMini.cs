using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
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
    private readonly StackPanel _body = new() { Spacing = 3 };

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

    /// <summary>Redrawn when the plan, the route file or the current system moves.</summary>
    public void Refresh() => Fill();

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

        _body.Children.Add(Title(jump.Origin));

        var next = NextWaypoint(jump, _route(), _here());

        if (next == Arrived)
        {
            _body.Children.Add(Muted("The destination is reached."));
        }

        for (var index = 0; index < jump.Waypoints.Count; index++)
        {
            _body.Children.Add(Row(jump.Waypoints[index], index == next));
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

    private static Control Row(RouteWaypoint waypoint, bool isNext)
    {
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        line.Children.Add(Text(waypoint.System, TypeScale.Body, isNext ? ThemeManager.AccentKey : ThemeManager.TextKey));
        line.Children.Add(Muted(waypoint.Jumps == 1 ? "1 jump" : $"{waypoint.Jumps} jumps"));

        if (waypoint.DistanceLeftToReport is { } left)
        {
            line.Children.Add(Muted($"{left:N0} ly left"));
        }

        if (waypoint.IsNeutron)
        {
            line.Children.Add(Muted("neutron"));
        }

        if (isNext)
        {
            line.Children.Add(Muted("next"));
        }

        return line;
    }

    private static TextBlock Title(string text)
    {
        var block = Text(text, TypeScale.Subheading, ThemeManager.TextKey);
        block.FontWeight = FontWeight.SemiBold;

        return block;
    }

    private static TextBlock Muted(string text) => Text(text, TypeScale.Secondary, ThemeManager.TextMutedKey);

    private static TextBlock Text(string text, double size, string colourKey)
    {
        var block = new TextBlock { Text = text, FontSize = size };

        block.Bind(
            TextBlock.ForegroundProperty,
            Application.Current!.Resources.GetResourceObservable(colourKey));

        return block;
    }
}

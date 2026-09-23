using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>A plan that was made, drawn whole (Phase 37, "Plan").</summary>
public sealed class RoutePlanResultPage : UserControl
{
    private readonly Func<string, Task<bool>>? _copy;
    private readonly RoutePlanBook? _plans;
    private readonly Func<string?>? _here;
    private readonly RoutePlanKind _kind;
    private readonly StackPanel _stack = new() { Spacing = 2 };

    private StoredRoutePlan? _planSeen;

    public RoutePlanResultPage(
        StoredRoutePlan plan,
        Func<string, Task<bool>>? copy = null,
        RoutePlanBook? plans = null,
        Func<string?>? here = null)
    {
        _copy = copy;
        _plans = plans;
        _here = here;
        _kind = plan.Kind;

        Content = new ScrollViewer
        {
            Padding = new Thickness(14),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _stack,
        };

        Draw(plan);
    }

    /// <summary>
    /// Redraws from the book rather than the plan passed at construction, because the stored record is
    /// replaced on arrival, not mutated (#200), and replaced wholesale by a new plot (#212). Compared by
    /// reference rather than by <see cref="StoredRoutePlan.Reached"/> alone, so a second plot with nothing
    /// reached still counts as a change. Returns whether anything actually moved, so a caller can tell
    /// whether the redraw is worth serving to the headset.
    /// </summary>
    public bool Refresh()
    {
        if (_plans?.Last(_kind) is not { } plan || ReferenceEquals(plan, _planSeen))
        {
            return false;
        }

        Dispatcher.UIThread.Post(() => Draw(plan));

        return true;
    }

    private void Draw(StoredRoutePlan plan)
    {
        _planSeen = plan;

        var here = _here?.Invoke();

        _stack.Children.Clear();
        _stack.Children.Add(RoutingKit.Title(Heading(plan)).Row);

        var provenance = RoutingKit.Prose(Provenance(plan));
        provenance.Margin = new Thickness(0, 0, 0, 10);
        _stack.Children.Add(provenance);

        foreach (var row in Rows(plan, here))
        {
            _stack.Children.Add(row);
        }
    }

    private static string Heading(StoredRoutePlan plan) => plan switch
    {
        { Jump: { } jump } =>
            $"{jump.Origin} → {jump.Destination}: {jump.TotalDistance:N0} ly, "
            + $"{Count(jump.TotalJumps, "jump")} across {Count(jump.Waypoints.Count, "waypoint")}.",
        { Riches: { } riches } =>
            $"{Count(riches.Stops.Count, "stop")}, {Count(riches.TotalJumps, "jump")}, "
            + $"{riches.TotalValue:N0} credits of mapping.",
        { Trade: { } trade } =>
            $"{Count(trade.Stops.Count, "stop")}, {trade.TotalProfit:N0} credits on {trade.Capital:N0} "
            + $"over {trade.TotalDistance:N0} ly.",
        _ => plan.Headline,
    };

    private static string Count(int n, string noun) => n == 1 ? $"1 {noun}" : $"{n} {noun}s";

    /// <summary>When it was worked out, and what the plan itself does not claim.</summary>
    private static string Provenance(StoredRoutePlan plan)
    {
        var when = plan.PlottedAt == default
            ? "Plotted earlier."
            : $"Plotted {plan.PlottedAt.ToLocalTime():d MMM, HH:mm}.";

        if (plan.Trade is not { } trade)
        {
            return when;
        }

        var parts = new List<string> { when };

        if (trade.CarriersIgnored > 0)
        {
            parts.Add(
                $"{trade.CarriersIgnored} fleet carrier{(trade.CarriersIgnored == 1 ? "" : "s")} left out — "
                + "they set their own prices and then move.");
        }

        // Said every time rather than only where it bit, because the figure is not known and a plan that
        // silently assumed one would be wrong in a way that reads like it working.
        parts.Add("No leg sells past a station's demand, and what dumping past it does to a price is not modelled.");

        return string.Join(" ", parts);
    }

    private IEnumerable<Control> Rows(StoredRoutePlan plan, string? here) => plan switch
    {
        { Jump: { } jump } => jump.Waypoints.Select((waypoint, index) => Waypoint(waypoint, State(index, plan.Reached), here)),
        { Riches: { } riches } => riches.Stops.Select((stop, index) => Stop(stop, State(index, plan.Reached), here)),
        { Trade: { } trade } => trade.Stops.Select((stop, index) => Stop(stop, State(index, plan.Reached), here)),
        _ => [],
    };

    /// <summary>
    /// Whether a row is behind the Commander or is the next one to copy. With nothing reached, the first
    /// row is next (#200).
    /// </summary>
    private readonly record struct RowState(bool Reached, bool Next);

    private static RowState State(int index, int? reached) => new(
        Reached: reached is { } r && index <= r,
        Next: index == (reached is { } r2 ? r2 + 1 : 0));

    private Control Waypoint(RouteWaypoint waypoint, RowState state, string? here)
    {
        var values = new List<string> { waypoint.Jumps == 1 ? "1 jump" : $"{waypoint.Jumps} jumps" };

        if (waypoint.DistanceJumped is { } jumped)
        {
            values.Add($"{jumped:N0} ly");
        }

        // The destination's own row carried "0 ly left" here for the same reason the spoken line did (#405).
        if (waypoint.DistanceLeftToReport is { } left)
        {
            values.Add($"{left:N0} ly left");
        }

        // The only line that changes what you do on arrival, so it rides on the row.
        Control[] tags = waypoint.IsNeutron ? [RoutingKit.Tag("neutron — supercharge here", ThemeManager.AKey)] : [];

        return RoutingKit.Row(
            [Line(waypoint.System, waypoint.System, state, here, tags), RoutingKit.Values(Joined(values), state.Reached)],
            waypoint.System,
            _copy);
    }

    private Control Stop(RichesStop stop, RowState state, string? here)
    {
        var lines = new List<Control>
        {
            Line(stop.System, stop.System, state, here),
            RoutingKit.Values(
                Joined(
                [
                    stop.Jumps == 1 ? "1 jump" : $"{stop.Jumps} jumps",
                    stop.Bodies.Count == 1 ? "1 body" : $"{stop.Bodies.Count} bodies",
                ]),
                state.Reached),
        };

        // Worth-most first, because that is the number deciding whether the stop is worth making.
        foreach (var body in stop.Bodies.OrderByDescending(body => body.MappingValue ?? 0))
        {
            lines.Add(RoutingKit.Values(
                body.MappingValue is { } value
                    ? $"{body.Name} — {body.Subtype ?? "unknown"}, {value:N0} cr"
                    : $"{body.Name} — {body.Subtype ?? "unknown"}",
                state.Reached));
        }

        return RoutingKit.Row(lines, stop.System, _copy);
    }

    private Control Stop(TradeStop stop, RowState state, string? here)
    {
        var values = new List<string>();

        if (stop.Distance is { } distance)
        {
            values.Add($"{distance:N1} ly");
            values.Add(stop.Jumps == 1 ? "1 jump" : $"{stop.Jumps} jumps");
        }

        if (stop.DistanceToArrival is { } arrival)
        {
            values.Add($"{arrival:N0} ls in");
        }

        var lines = new List<Control> { Line($"{stop.Station} in {stop.System}", stop.System, state, here) };

        if (values.Count > 0)
        {
            lines.Add(RoutingKit.Values(Joined(values), state.Reached));
        }

        foreach (var lot in stop.Sell)
        {
            lines.Add(RoutingKit.Values(
                $"sell {lot.Amount} × {lot.Commodity} at {lot.UnitPrice:N0} — {lot.Value:N0} cr", state.Reached));
        }

        // A keep line always says what declining to sell here is worth.
        foreach (var lot in stop.Hold)
        {
            lines.Add(RoutingKit.Values(
                $"keep {lot.Amount} × {lot.Commodity} — this station only pays {lot.UnitPrice:N0}", state.Reached));
        }

        foreach (var lot in stop.Buy)
        {
            lines.Add(RoutingKit.Values(
                $"buy {lot.Amount} × {lot.Commodity} at {lot.UnitPrice:N0} — {lot.Value:N0} cr", state.Reached));
        }

        if (stop.CappedByDemand)
        {
            lines.Add(Caveat("a lot here was cut short by what the station will take"));
        }

        lines.Add(Caveat(
            stop.PricesSeen is { } seen
                ? stop.PricesAreYours
                    ? $"your own prices, read {seen.ToLocalTime():d MMM yyyy}"
                    : $"prices reported {seen.ToLocalTime():d MMM yyyy}"
                : "prices of unknown age"));

        return RoutingKit.Row(lines, stop.System, _copy);
    }

    private static string Joined(IEnumerable<string> values) => string.Join(" · ", values);

    /// <summary>
    /// A row's first line: a Blue tick where it is reached, the name in its system's ink, and NEXT on the
    /// next one to copy.
    /// </summary>
    private static Control Line(string title, string system, RowState state, string? here, params Control[] tags)
    {
        var line = RoutingKit.Named(title, RoutingKit.SystemKey(system, here, state.Reached), tags);

        if (state.Reached)
        {
            var tick = RoutingKit.Ink("✓", TypeScale.Body, ThemeManager.BlueKey);
            tick.VerticalAlignment = VerticalAlignment.Center;
            line.Children.Insert(0, tick);
        }

        if (state.Next)
        {
            line.Children.Add(RoutingKit.Tag("next", ThemeManager.AKey));
        }

        return line;
    }

    private static TextBlock Caveat(string text)
    {
        var line = RoutingKit.Ink(text, TypeScale.Small, ThemeManager.Grey2Key);
        line.TextWrapping = TextWrapping.Wrap;

        return line;
    }
}

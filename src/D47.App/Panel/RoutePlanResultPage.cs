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
    private readonly RoutePlanKind _kind;
    private readonly StackPanel _stack = new() { Spacing = 4 };

    private StoredRoutePlan? _planSeen;

    public RoutePlanResultPage(StoredRoutePlan plan, Func<string, Task<bool>>? copy = null, RoutePlanBook? plans = null)
    {
        _copy = copy;
        _plans = plans;
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

        _stack.Children.Clear();
        _stack.Children.Add(Heading(plan));
        _stack.Children.Add(Aside(Provenance(plan)));

        foreach (var row in Rows(plan))
        {
            _stack.Children.Add(row);
        }
    }

    private Control Heading(StoredRoutePlan plan)
    {
        var text = plan switch
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

        var block = new TextBlock
        {
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 2),
        };

        TitleText.Style(block, TypeScale.Caption, TitleRank.Subgroup, sentence: true);
        TitleText.Show(block, text, sentence: true);

        return block;
    }

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

    private IEnumerable<Control> Rows(StoredRoutePlan plan) => plan switch
    {
        { Jump: { } jump } => jump.Waypoints.Select((waypoint, index) => Waypoint(waypoint, State(index, plan.Reached))),
        { Riches: { } riches } => riches.Stops.Select((stop, index) => Stop(stop, State(index, plan.Reached))),
        { Trade: { } trade } => trade.Stops.Select((stop, index) => Stop(stop, State(index, plan.Reached))),
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

    private Control Waypoint(RouteWaypoint waypoint, RowState state)
    {
        var line = Line(waypoint.System, state);

        line.Children.Add(Muted(
            waypoint.Jumps == 1 ? "1 jump" : $"{waypoint.Jumps} jumps"));

        if (waypoint.DistanceJumped is { } jumped)
        {
            line.Children.Add(Muted($"{jumped:N0} ly"));
        }

        // The destination's own row carried "0 ly left" here for the same reason the spoken line did (#405).
        if (waypoint.DistanceLeftToReport is { } left)
        {
            line.Children.Add(Muted($"{left:N0} ly left"));
        }

        if (waypoint.IsNeutron)
        {
            // The only line that changes what you do on arrival, so it rides on the row.
            line.Children.Add(Badge("neutron — supercharge here", ThemeManager.AccentKey));
        }

        return Wrap(line, waypoint.System, state);
    }

    private Control Stop(RichesStop stop, RowState state)
    {
        var line = Line(stop.System, state);

        line.Children.Add(Muted(stop.Jumps == 1 ? "1 jump" : $"{stop.Jumps} jumps"));
        line.Children.Add(Muted(
            stop.Bodies.Count == 1 ? "1 body" : $"{stop.Bodies.Count} bodies"));

        var stack = new StackPanel { Spacing = 2, Children = { line } };

        // Worth-most first, because that is the number deciding whether the stop is worth making.
        foreach (var body in stop.Bodies.OrderByDescending(body => body.MappingValue ?? 0))
        {
            var detail = body.MappingValue is { } value
                ? $"    {body.Name} — {body.Subtype ?? "unknown"}, {value:N0} cr"
                : $"    {body.Name} — {body.Subtype ?? "unknown"}";

            stack.Children.Add(Muted(detail));
        }

        return Wrap(stack, stop.System, state);
    }

    private Control Stop(TradeStop stop, RowState state)
    {
        var line = Line($"{stop.Station} in {stop.System}", state);

        if (stop.Distance is { } distance)
        {
            line.Children.Add(Muted($"{distance:N1} ly"));
            line.Children.Add(Muted(stop.Jumps == 1 ? "1 jump" : $"{stop.Jumps} jumps"));
        }

        if (stop.DistanceToArrival is { } arrival)
        {
            line.Children.Add(Muted($"{arrival:N0} ls in"));
        }

        var stack = new StackPanel { Spacing = 2, Children = { line } };

        foreach (var lot in stop.Sell)
        {
            stack.Children.Add(Muted($"    sell {lot.Amount} × {lot.Commodity} at {lot.UnitPrice:N0} — {lot.Value:N0} cr"));
        }

        // A keep line always says what declining to sell here is worth.
        foreach (var lot in stop.Hold)
        {
            stack.Children.Add(Muted($"    keep {lot.Amount} × {lot.Commodity} — this station only pays {lot.UnitPrice:N0}"));
        }

        foreach (var lot in stop.Buy)
        {
            stack.Children.Add(Muted($"    buy {lot.Amount} × {lot.Commodity} at {lot.UnitPrice:N0} — {lot.Value:N0} cr"));
        }

        if (stop.CappedByDemand)
        {
            stack.Children.Add(Muted("    a lot here was cut short by what the station will take"));
        }

        stack.Children.Add(Muted(
            stop.PricesSeen is { } seen
                ? stop.PricesAreYours
                    ? $"    your own prices, read {seen.ToLocalTime():d MMM yyyy}"
                    : $"    prices reported {seen.ToLocalTime():d MMM yyyy}"
                : "    prices of unknown age"));

        return Wrap(stack, stop.System, state);
    }

    private static StackPanel Line(string title, RowState state)
    {
        var text = state.Reached ? $"✓ {title}" : title;
        var name = Text(text, TypeScale.Body, state.Reached ? ThemeManager.TextMutedKey : ThemeManager.TextKey);
        name.VerticalAlignment = VerticalAlignment.Center;

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { name },
        };
    }

    /// <summary>
    /// Every system name here is a copy target, for the reason the Course page gives: the clipboard is
    /// the part of plotting that always works.
    /// </summary>
    private Control Wrap(Control content, string system, RowState state)
    {
        Control body = content;

        if (_copy is { } copy)
        {
            body = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children = { content, D47.App.Controls.CopyGlyph.For(system, copy) },
            };
        }

        var row = new Border
        {
            Padding = new Thickness(8, 5),
            Child = body,
        };

        if (state.Next)
        {
            CardChrome.CurrentRow(row);
        }

        if (_copy is { } tap)
        {
            row.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
            row.Tapped += (_, _) => _ = tap(system);
        }

        return row;
    }

    private static TextBlock Muted(string text)
    {
        var block = Text(text, TypeScale.Secondary, ThemeManager.TextMutedKey);
        block.VerticalAlignment = VerticalAlignment.Center;

        return block;
    }

    private static TextBlock Aside(string text)
    {
        var block = Text(text, TypeScale.Small, ThemeManager.TextMutedKey, wrap: true);
        block.Margin = new Thickness(0, 0, 0, 10);

        return block;
    }

    private static Control Badge(string word, string colourKey)
    {
        var text = Text(word, TypeScale.Small, colourKey);
        text.VerticalAlignment = VerticalAlignment.Center;

        var badge = new Border
        {
            Padding = new Thickness(6, 1),
            CornerRadius = new CornerRadius(0),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = text,
        };

        badge.Bind(
            Border.BorderBrushProperty,
            Application.Current!.Resources.GetResourceObservable(colourKey));

        return badge;
    }

    private static TextBlock Text(string text, double size, string colourKey, bool wrap = false)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
        };

        block.Bind(
            TextBlock.ForegroundProperty,
            Application.Current!.Resources.GetResourceObservable(colourKey));

        return block;
    }
}

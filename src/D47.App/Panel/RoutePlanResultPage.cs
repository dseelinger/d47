using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>
/// A plan that was made, drawn whole (list.md Phase 37, "Plan").
/// <para>
/// <b>The whole thing, not the five waypoints the sentence carries.</b> The service returns every
/// waypoint of a Sol-to-Colonia plot and <c>RouteCapability</c> reads out the first few, because
/// 131 of them spoken aloud is a spreadsheet rather than an answer. That cap belongs to speech.
/// This is the surface it does not apply to.
/// </para>
/// </summary>
public sealed class RoutePlanResultPage : UserControl
{
    private readonly Action<string>? _copy;

    public RoutePlanResultPage(StoredRoutePlan plan, Action<string>? copy = null)
    {
        _copy = copy;

        var stack = new StackPanel { Spacing = 4 };

        stack.Children.Add(Heading(plan));
        stack.Children.Add(Aside(Provenance(plan)));

        foreach (var row in Rows(plan))
        {
            stack.Children.Add(row);
        }

        Content = new ScrollViewer
        {
            Padding = new Thickness(14),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = stack,
        };
    }

    private Control Heading(StoredRoutePlan plan)
    {
        var text = plan switch
        {
            { Jump: { } jump } =>
                $"{jump.Origin} → {jump.Destination}: {jump.TotalDistance:N0} ly, "
                + $"{jump.TotalJumps} jumps across {jump.Waypoints.Count} waypoints.",
            { Riches: { } riches } =>
                $"{riches.Stops.Count} stops, {riches.TotalJumps} jumps, "
                + $"{riches.TotalValue:N0} credits of mapping.",
            { Trade: { } trade } =>
                $"{trade.Stops.Count} stops, {trade.TotalProfit:N0} credits on {trade.Capital:N0} "
                + $"over {trade.TotalDistance:N0} ly.",
            _ => plan.Headline,
        };

        var block = Text(text, TypeScale.Subheading, ThemeManager.TextKey, wrap: true);
        block.FontWeight = FontWeight.SemiBold;
        block.Margin = new Thickness(0, 0, 0, 2);

        return block;
    }

    /// <summary>
    /// When it was worked out, and what the plan itself refuses to promise. Both belong at the
    /// top: a route can be arithmetically perfect against a four-year-old price.
    /// </summary>
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

        // Said every time rather than only where it bit, because the figure is not known and a
        // plan that quietly assumed one would be wrong in a way that reads like it working.
        parts.Add("No leg sells past a station's demand, and what dumping past it does to a price is not modelled.");

        return string.Join(" ", parts);
    }

    private IEnumerable<Control> Rows(StoredRoutePlan plan) => plan switch
    {
        { Jump: { } jump } => jump.Waypoints.Select(Waypoint),
        { Riches: { } riches } => riches.Stops.Select(Stop),
        { Trade: { } trade } => trade.Stops.Select(Stop),
        _ => [],
    };

    private Control Waypoint(RouteWaypoint waypoint)
    {
        var line = Line(waypoint.System);

        line.Children.Add(Muted(
            waypoint.Jumps == 1 ? "1 jump" : $"{waypoint.Jumps} jumps"));

        if (waypoint.DistanceLeft is { } left)
        {
            line.Children.Add(Muted($"{left:N0} ly left"));
        }

        if (waypoint.IsNeutron)
        {
            // The only line that changes what you do on arrival, so it rides on the row.
            line.Children.Add(Badge("neutron — supercharge here", ThemeManager.AccentKey));
        }

        return Wrap(line, waypoint.System);
    }

    private Control Stop(RichesStop stop)
    {
        var line = Line(stop.System);

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

        return Wrap(stack, stop.System);
    }

    private Control Stop(TradeStop stop)
    {
        var line = Line($"{stop.Station} in {stop.System}");

        if (stop.Distance is { } distance)
        {
            line.Children.Add(Muted($"{distance:N1} ly"));
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

        // A keep line always says what declining to sell here is worth. A Commander not told why
        // they are flying past a buyer will sell there, and the plan stops being the plan.
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

        return Wrap(stack, stop.System);
    }

    private static StackPanel Line(string title)
    {
        var name = Text(title, TypeScale.Body, ThemeManager.TextKey);
        name.VerticalAlignment = VerticalAlignment.Center;

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { name },
        };
    }

    /// <summary>
    /// Every system name here is a copy target, for the reason the Course page gives: the
    /// clipboard is the part of plotting that always works.
    /// </summary>
    private Control Wrap(Control content, string system)
    {
        var row = new Border
        {
            Padding = new Thickness(8, 5),
            CornerRadius = new CornerRadius(3),
            Child = content,
        };

        if (_copy is { } copy)
        {
            row.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
            row.Tapped += (_, _) => copy(system);
            ToolTip.SetTip(row, $"Copy {system}");
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
            CornerRadius = new CornerRadius(2),
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

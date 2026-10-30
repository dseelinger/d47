using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>A plan that was made, drawn whole (Phase 37, "Plan").</summary>
public sealed class RoutePlanResultPage : UserControl
{
    private readonly Func<string, Task<bool>>? _copy;
    private readonly RoutePlanBook? _plans;
    private readonly Func<string?>? _here;
    private readonly Func<string, string?>? _offDuty;
    private readonly RoutePlanKind _kind;
    private readonly OutfittingPlotter? _plotter;
    private readonly MaxReportAge _age = new();
    private readonly StackPanel _searchBar;
    private readonly Button _search = new() { Content = "Search" };
    private readonly Button _stop = new() { Content = "Cancel", IsVisible = false };
    private readonly StatusLine _searchStatus = RoutingKit.Status();
    private readonly StackPanel _stack = new() { Spacing = 2 };

    public const string ShowOnGalaxyMapWords = "Show on galaxy map";

    private static readonly Thickness MapMargin = new(14, 14, 0, 0);

    private readonly Action<RoutePlanKind>? _showOnGalaxyMap;

    private readonly RouteMapView _map = new()
    {
        IsVisible = false,
        VerticalAlignment = VerticalAlignment.Top,
        HorizontalAlignment = HorizontalAlignment.Left,
        Margin = MapMargin,
    };

    private readonly Button _mapPress = new()
    {
        IsVisible = false,
        Padding = default,
        Margin = MapMargin,
        Template = new FuncControlTemplate<Button>((_, _) => new Border { Background = Brushes.Transparent }),
    };

    private readonly Button _showTile = new()
    {
        Content = ShowOnGalaxyMapWords,
        IsVisible = false,
        Height = TypeScale.MinimumTarget,
        Padding = default,
        Margin = new Thickness(14, 6, 0, 0),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    private StoredRoutePlan? _planSeen;
    private DateTimeOffset? _ageShownFor;
    private string? _offDutySeen;

    public RoutePlanResultPage(
        StoredRoutePlan plan,
        Func<string, Task<bool>>? copy = null,
        RoutePlanBook? plans = null,
        Func<string?>? here = null,
        Func<string, string?>? offDuty = null,
        Action<RoutePlanKind>? showOnGalaxyMap = null,
        OutfittingPlotter? outfittingPlotter = null)
    {
        _plotter = outfittingPlotter;
        _searchBar = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 10),
            Children = { _age.Field("Max report age"), RoutingKit.Actions(_search, _stop), _searchStatus },
        };
        _offDuty = offDuty;
        _copy = copy;
        _plans = plans;
        _here = here;
        _kind = plan.Kind;
        _showOnGalaxyMap = showOnGalaxyMap;

        _mapPress.Cursor = new Cursor(StandardCursorType.Hand);
        _mapPress.Click += (_, _) => _showOnGalaxyMap?.Invoke(_kind);
        _showTile.Click += (_, _) => _showOnGalaxyMap?.Invoke(_kind);
        AutomationProperties.SetName(_mapPress, ShowOnGalaxyMapWords);
        AutomationProperties.SetName(_showTile, ShowOnGalaxyMapWords);

        var list = new ScrollViewer
        {
            Padding = new Thickness(14),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _stack,
        };

        // The press covers the map in its own cell, so the map stays a static child of the page.
        var picture = new Grid { Children = { _map, _mapPress } };
        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Top, Children = { picture, _showTile } };
        var body = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)] };
        Grid.SetColumn(list, 1);
        body.Children.Add(left);
        body.Children.Add(list);
        Content = body;

        RoutingKit.RunPlot(
            _search,
            _stop,
            _searchStatus,
            TimeSpan.FromSeconds(70),
            "Searching…",
            () => _planSeen?.Outfitting is not null && _plotter?.LookupsEnabled() == true,
            token => _plotter!.Replot(_planSeen!.Outfitting!, _age.Value, token),
            _ => { });

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
        if (_plans?.Last(_kind) is not { } plan
            || (ReferenceEquals(plan, _planSeen) && _offDuty?.Invoke(OffDutyNote.Route) == _offDutySeen))
        {
            return false;
        }

        Dispatcher.UIThread.Post(() => Draw(plan));

        return true;
    }

    private void Draw(StoredRoutePlan plan)
    {
        _planSeen = plan;

        var offDuty = _offDuty?.Invoke(OffDutyNote.Route);
        _offDutySeen = offDuty;
        var here = offDuty is null ? _here?.Invoke() : null;

        _stack.Children.Clear();
        _map.IsVisible = false;
        _mapPress.IsVisible = false;
        _showTile.IsVisible = false;

        if (offDuty is null
            && RouteMapStop.From(plan) is { } stops
            && stops.Count(stop => stop.Position is not null) >= 2)
        {
            _map.Show(stops, RouteMapStop.ReachedIndex(plan), null, null);
            _map.IsVisible = true;
            _mapPress.IsVisible = true;
            _mapPress.IsHitTestVisible = _showOnGalaxyMap is not null;
            _mapPress.Focusable = _showOnGalaxyMap is not null;
            _showTile.IsVisible = _showOnGalaxyMap is not null;
        }

        _stack.Children.Add(RoutingKit.Title(Heading(plan), sentence: true).Row);

        var provenance = RoutingKit.Prose(Provenance(plan));
        provenance.Margin = new Thickness(0, 0, 0, 10);
        _stack.Children.Add(provenance);

        if (offDuty is not null)
        {
            var note = RoutingKit.Prose(offDuty);
            note.Margin = new Thickness(0, 0, 0, 10);
            _stack.Children.Add(note);
        }

        if (plan.Outfitting is { } searched && _plotter is not null)
        {
            if (_ageShownFor != plan.PlottedAt)
            {
                _age.Value = searched.MaxReportAgeHours ?? MaxReportAge.Any;
                _ageShownFor = plan.PlottedAt;
                _searchStatus.Clear();
            }

            var enabled = _plotter.LookupsEnabled();

            _search.IsEnabled = enabled && !_stop.IsVisible;
            ToolTip.SetShowOnDisabled(_search, true);
            ToolTip.SetTip(_search, enabled ? null : OutfittingPlotter.SwitchedOff);

            _stack.Children.Add(_searchBar);
        }

        if (plan.Outfitting is { StorageChecked: false })
        {
            _stack.Children.Add(new Notice(NoticeLevel.Warning, inline: true)
            {
                Label = "STORAGE NOT CHECKED",
                Text = "Stored modules were not counted, because D47 has not seen your module storage yet. "
                       + "Open storage at any station, then plot again.",
                Margin = new Thickness(0, 0, 0, 10),
            });
        }

        foreach (var row in Rows(plan, here))
        {
            _stack.Children.Add(row);
        }

        if (plan.Outfitting is { NotFound.Count: > 0 } outfitting)
        {
            _stack.Children.Add(NotFound(outfitting));
        }
    }

    private static Control NotFound(OutfittingRoute route)
    {
        var group = new StackPanel { Spacing = 2, Margin = new Thickness(0, 18, 0, 0) };

        group.Children.Add(ListRow.Head("Not found"));

        var reach = route.MaxDistance is { } distance ? $"within {distance:N0} ly of " : "near ";
        var recent = route.MaxReportAgeHours is { } hours ? $" reported in the last {MaxReportAge.Label(hours)}" : string.Empty;
        var prose = RoutingKit.Prose($"No station {reach}{route.Reference ?? "the start"}{recent} sells these.");
        prose.Margin = new Thickness(0, 6, 0, 8);
        group.Children.Add(prose);

        foreach (var item in route.NotFound)
        {
            var slab = new Border
            {
                MinHeight = TypeScale.MinimumTarget,
                Padding = new Thickness(12, 0),
                Child = RoutingKit.Ink(item, TypeScale.Body, ThemeManager.WhiteKey),
            };

            ((TextBlock)slab.Child).VerticalAlignment = VerticalAlignment.Center;
            ThemeInk.Bind(slab, Border.BackgroundProperty, ThemeManager.SlabKey);
            group.Children.Add(slab);
        }

        return group;
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
        { Outfitting: { } outfitting } => $"{plan.Headline}, {outfitting.TotalDistance:N0} ly.",
        { Exobiology: { } exobiology } =>
            $"{Count(exobiology.Stops.Count, "stop")}, {Count(exobiology.TotalJumps, "jump")}, "
            + $"{exobiology.TotalValue:N0} credits of biology.",
        { Carrier: { } carrier } =>
            $"Carrier route, {plan.Headline}: {Count(carrier.Waypoints.Count(waypoint => waypoint.Distance > 0), "jump")}, "
            + $"{carrier.TotalTritium:N0} t of tritium.",
        _ => plan.Headline,
    };

    private static string Count(int n, string noun) => n == 1 ? $"1 {noun}" : $"{n} {noun}s";

    /// <summary>When it was worked out, and what the plan itself does not claim.</summary>
    private static string Provenance(StoredRoutePlan plan)
    {
        var when = plan.PlottedAt == default
            ? "Plotted earlier."
            : $"Plotted {plan.PlottedAt.ToLocalTime():d MMM, HH:mm}.";

        if (plan.Exobiology is not null)
        {
            return $"{when} Every stop has already been surveyed by somebody, so none of it is a first footfall.";
        }

        if (plan.Carrier is not null)
        {
            return plan.CarrierStatsSeenAt is { } read
                ? $"{when} Tank and hold read from carrier management {read.ToLocalTime():d MMM, HH:mm}."
                : when;
        }

        if (plan.Outfitting is not null)
        {
            return $"{when} Fewest stations first, nearest stop next. What a station sells is as it was last reported.";
        }

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
        { Riches: { } riches } => riches.Stops.Select((stop, index) => Stop(
            stop,
            State(index, plan.Reached),
            here,
            LegLength(index == 0 ? riches.Start?.Position : riches.Stops[index - 1].Position, stop.Position))),
        { Trade: { } trade } => trade.Stops.Select((stop, index) => Stop(stop, State(index, plan.Reached), here)),
        { Outfitting: { } outfitting } => outfitting.Stops.Select((stop, index) => Stop(stop, State(index, plan.Reached), here)),
        { Exobiology: { } exobiology } =>
            exobiology.Stops.Select((stop, index) => Stop(
                stop,
                State(index, plan.Reached),
                here,
                LegLength(index == 0 ? exobiology.Start?.Position : exobiology.Stops[index - 1].Position, stop.Position))),
        { Carrier: { } carrier } =>
            carrier.Waypoints.Select((waypoint, index) => Waypoint(waypoint, State(index, plan.Reached), here)),
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

    private Control Waypoint(CarrierWaypoint waypoint, RowState state, string? here)
    {
        var values = waypoint.Distance > 0
            ? new List<string> { $"{waypoint.Distance:N1} ly", $"{waypoint.FuelUsed:N0} t tritium" }
            : ["start"];

        values.Add($"{waypoint.FuelInTank:N0} t in the tank");

        var tags = new List<Control>();

        if (waypoint.MustRestock)
        {
            tags.Add(RoutingKit.Tag($"restock {waypoint.RestockAmount:N0} t", ThemeManager.AKey));
        }

        if (waypoint is { HasIcyRing: true, IsSystemPristine: true })
        {
            tags.Add(RoutingKit.Tag("pristine icy ring", ThemeManager.BlueKey));
        }

        return RoutingKit.Row(
            [Line(waypoint.Name, waypoint.Name, state, here, [.. tags]), RoutingKit.Values(Joined(values), state.Reached)],
            waypoint.Name,
            _copy);
    }

    private static string Away(double? lightSeconds) =>
        lightSeconds is { } ls ? $", {ls:N0} ls" : string.Empty;

    private static string? LegLength(StarPosition? from, StarPosition? to) =>
        from is { } a && to is { } b ? $"{a.DistanceTo(b):N0} ly" : null;

    private static string StopValues(int jumps, string? leg, int bodies)
    {
        var values = new List<string> { jumps == 1 ? "1 jump" : $"{jumps} jumps" };

        if (leg is not null)
        {
            values.Add(leg);
        }

        values.Add(bodies == 1 ? "1 body" : $"{bodies} bodies");

        return Joined(values);
    }

    /// <summary>"Body " and the designation when the name is the system name plus a designation; otherwise the full name.</summary>
    private static string BodyLabel(string system, string name) =>
        name.Length > system.Length + 1
        && name.StartsWith(system, StringComparison.Ordinal)
        && name[system.Length] == ' '
            ? $"Body {name[(system.Length + 1)..]}"
            : name;

    private Control Stop(RichesStop stop, RowState state, string? here, string? leg)
    {
        var lines = new List<Control>
        {
            Line(stop.System, stop.System, state, here),
            RoutingKit.Values(StopValues(stop.Jumps, leg, stop.Bodies.Count), state.Reached),
        };

        // Worth-most first, because that is the number deciding whether the stop is worth making.
        foreach (var body in stop.Bodies.OrderByDescending(body => body.MappingValue ?? 0))
        {
            lines.Add(RoutingKit.Values(
                $"{BodyLabel(stop.System, body.Name)} — {body.Subtype ?? "unknown"}{Away(body.DistanceToArrival)}"
                + (body.MappingValue is { } value ? $", {value:N0} cr" : string.Empty),
                state.Reached));
        }

        return RoutingKit.Row(lines, stop.System, _copy);
    }

    private Control Stop(ExobiologyStop stop, RowState state, string? here, string? leg)
    {
        var lines = new List<Control>
        {
            Line(stop.System, stop.System, state, here),
            RoutingKit.Values(StopValues(stop.Jumps, leg, stop.Bodies.Count), state.Reached),
        };

        foreach (var body in stop.Bodies.OrderByDescending(body => body.LandmarkValue))
        {
            lines.Add(RoutingKit.Values(
                $"{BodyLabel(stop.System, body.Name)} — {body.Subtype ?? "unknown"}{Away(body.DistanceToArrival)}, {body.LandmarkValue:N0} cr",
                state.Reached));

            foreach (var species in body.Species)
            {
                var count = species.Count > 1 ? $" ×{species.Count}" : string.Empty;
                var line = RoutingKit.Values($"{species.Name}{count} — {species.Value:N0} cr", state.Reached);
                line.Margin = new Thickness(16, 0, 0, 0);

                lines.Add(line);
            }

            if (body.LandmarkValue - body.Species.Sum(species => species.Value) is > 0 and var under)
            {
                var line = RoutingKit.Values($"species under the minimum — {under:N0} cr", state.Reached);
                line.Margin = new Thickness(16, 0, 0, 0);

                lines.Add(line);
            }
        }

        return RoutingKit.Row(lines, stop.System, _copy);
    }

    private Control Stop(OutfittingStop stop, RowState state, string? here)
    {
        var lines = new List<Control> { Line($"{stop.Station} in {stop.System}", stop.System, state, here) };

        if (stop.Distance is { } distance)
        {
            lines.Add(RoutingKit.Values($"{distance:N1} ly", state.Reached));
        }

        foreach (var item in stop.Items)
        {
            lines.Add(RoutingKit.Values($"buy {item}", state.Reached));
        }

        lines.Add(Caveat(
            stop.StockLastSeen is { } seen
                ? $"outfitting reported {seen.ToLocalTime():d MMM yyyy}"
                : "report date unknown"));

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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>
/// Where to buy it, and what it costs there (Phase 49, "Asked by voice, and drawn on the Navigation tab").
/// </summary>
public sealed class RouteMarketPage : UserControl
{
    public const string HeadName = "MarketHead";

    /// <summary>How long a search may run.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(70);

    public const string TableId = "Route.Market";

    private const double CopyGap = 6;

    private readonly CapabilityRegistry _registry;
    private readonly CommodityBoard _board;
    private readonly Func<bool> _lookupsEnabled;
    private readonly Action? _openSettings;
    private readonly Func<string, Task<bool>>? _copy;

    private readonly StackPanel _body = new() { Spacing = 12 };

    private readonly TextBox _commodity = new()
    {
        // An example where a default goes, on a required field (#253) — see the survey in the issue: Tritium
        // sat in the same grey and the same slot as the numbers that genuinely are what happens if you type
        // nothing.
        PlaceholderText = "which one",
        Width = 190,
        HorizontalAlignment = HorizontalAlignment.Left,
    };

    private readonly TextBox _tonnes = new()
    {
        PlaceholderText = "how many",
        Width = 120,
        HorizontalAlignment = HorizontalAlignment.Left,
    };

    private readonly CheckBox _selling;
    private readonly CheckBox _largePad;

    /// <summary>
    /// Opts back into surface stations (#309): #296 defaulted <c>surface_stations</c> to false for
    /// every search, so a commodity only available at a Planetary Port was otherwise unreachable from
    /// this page.
    /// </summary>
    private readonly CheckBox _surfaceStations;
    private readonly MaxReportAge _age = new(72);

    private readonly StatusLine _status;

    private bool _sorting;

    private readonly StackPanel _results = new() { Spacing = 6 };
    private readonly ColumnWidthMemory? _columnWidths;

    public RouteMarketPage(
        CapabilityRegistry registry,
        CommodityBoard board,
        Func<bool> lookupsEnabled,
        Action? openSettings = null,
        Func<string, Task<bool>>? copy = null,
        ColumnWidthMemory? columnWidths = null)
    {
        _columnWidths = columnWidths;
        _registry = registry;
        _board = board;
        _lookupsEnabled = lookupsEnabled;
        _openSettings = openSettings;
        _copy = copy;

        _selling = RoutingKit.Switch("Selling it, not buying");
        _largePad = RoutingKit.Switch("Large pad only");
        _surfaceStations = RoutingKit.Switch("Include surface stations");

        _status = RoutingKit.Status();

        Content = new ScrollViewer
        {
            Padding = new Thickness(14),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _body,
        };

        Build();
    }

    /// <summary>Redraws, after a search or after the setting behind the page moved.</summary>
    public void Refresh() => Dispatcher.UIThread.Post(Build);

    private void Build()
    {
        _body.Children.Clear();

        if (!_lookupsEnabled())
        {
            _body.Children.Add(SwitchedOff());
            return;
        }

        _body.Children.Add(SearchCard());
        DrawResults();
        _body.Children.Add(_results);
    }

    private Control SwitchedOff() =>
        RoutingKit.SwitchedOff(
            "Market lookups are off",
            "Looking up markets is switched off. It shares the galaxy search setting, so turning on "
            + "“Look things up in the galaxy” switches both on.",
            _openSettings);

    private Control SearchCard()
    {
        // _commodity, _tonnes, _selling and _largePad are readonly fields, built once and reused across every
        // Build() — Labelled() wraps the two boxes in a fresh StackPanel each call, and the switches join a
        // fresh form StackPanel directly, so a second call finds each one still parented to the wrapper
        // Build() just discarded.
        Detach(_commodity);
        Detach(_tonnes);
        Detach(_selling);
        Detach(_largePad);
        Detach(_surfaceStations);
        Detach(_age.Picker);
        Detach(_status);

        var find = new Button { Content = "Find it" };
        var cancel = new Button { Content = "Cancel", IsVisible = false };

        RoutingKit.RunPlot(
            find,
            cancel,
            _status,
            Budget,
            "Reading the markets nearby…",
            () => RoutingKit.Filled(_commodity, "Name a commodity first."),
            token => _registry.InvokeAsync("find_nearest_station", Arguments(), token),
            _ => Refresh());

        var form = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                RoutingKit.Section("Where to buy it"),
                RoutingKit.Fields(
                    Labelled("Commodity", _commodity, D47.App.Controls.FieldNeed.Required),
                    Labelled("Tonnes", _tonnes)),
                D47.App.Controls.FormField.Legend(required: true),
                RoutingKit.Fields(Labelled("Max price age", _age.Picker)),
                RoutingKit.Fields(_selling, _largePad, _surfaceStations),
                RoutingKit.Prose(
                    "Say the tonnage and stations that cannot fill the whole load drop out, and a "
                    + "bigger load is worth a longer trip. Leave it blank to rank on price alone."),
                RoutingKit.Actions(find, cancel),
                _status,
            },
        };

        return form;
    }

    private ToolArguments Arguments()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["commodity"] = _commodity.Text!.Trim(),
        };

        if (!string.IsNullOrWhiteSpace(_tonnes.Text))
        {
            values["tonnes"] = _tonnes.Text!.Replace(",", string.Empty).Trim();
        }

        if (_selling.IsChecked == true)
        {
            values["selling"] = "true";
        }

        if (_largePad.IsChecked == true)
        {
            values["large_pad"] = "true";
        }

        if (_surfaceStations.IsChecked == true)
        {
            values["surface_stations"] = "true";
        }

        values["max_price_age_hours"] = _age.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return new ToolArguments(values);
    }

    private void DrawResults()
    {
        _results.Children.Clear();

        if (_board.Last is not { } posting || posting.Answer.Offers.Count == 0)
        {
            return;
        }

        var buying = posting.Query.Side == TradeSide.Buying;

        var named = posting.Query.Wants.Select(want => want.Commodity).ToList();
        var what = named.Count == 1 ? named[0] : $"{string.Join(", ", named.Take(named.Count - 1))} and {named[^1]}";

        var heading = RoutingKit.Ink(
            $"{(buying ? "Buying" : "Selling")} {what} near {posting.Near}",
            TypeScale.Body,
            ThemeManager.AKey,
            wrap: true);

        var rows = new StackPanel { Spacing = 2 };
        var layout = new TableLayout(TableId, ColumnsOf(posting), _columnWidths, 10);

        rows.Children.Add(HeaderRow(posting, layout));

        foreach (var offer in posting.Answer.Offers)
        {
            rows.Children.Add(OfferRow(offer, posting, layout));
        }

        var table = rows;

        var stack = new StackPanel { Spacing = 10, Children = { RoutingKit.Section("What came back"), heading, table } };

        // The date on the answer itself, which is a different caveat from the date on each price and a
        // Commander needs both: a twenty-minute-old answer quoting six-hour-old prices is two kinds of stale
        // at once.
        stack.Children.Add(RoutingKit.Prose(
            $"Searched {Ago(DateTimeOffset.UtcNow - posting.AskedAt)}. Prices are reported by other "
            + "Commanders; supply moves fastest of all."));

        _results.Children.Add(stack);
    }

    private static List<TableColumn> ColumnsOf(CommodityPosting posting)
    {
        List<TableColumn> columns = [new("Station", ColumnFit.Fill) { Min = 160 }];

        if (posting.Answer.OriginKnown)
        {
            columns.Add(new("Distance"));
        }

        if (Several(posting))
        {
            columns.Add(new("Commodity"));
        }

        columns.Add(new("Price"));
        columns.Add(new("Stock"));

        if (ByTheTonne(posting))
        {
            columns.Add(new("Load"));
        }

        columns.Add(new("Priced"));

        return columns;
    }

    /// <summary>The distance and price columns sort the whole answer, so a press asks again; the rest are plain labels.</summary>
    private Control HeaderRow(CommodityPosting posting, TableLayout layout)
    {
        var buying = posting.Query.Side == TradeSide.Buying;
        var best = buying ? SortDirection.Ascending : SortDirection.Descending;

        List<(string Label, SortColumn<object> Column)> heads =
        [
            ("Station", SortColumn<object>.Plain("Station")),
        ];

        if (posting.Answer.OriginKnown)
        {
            heads.Add(("Distance", SortColumn<object>.Remote("Distance", SortDirection.Ascending)));
        }

        if (Several(posting))
        {
            heads.Add(("Commodity", SortColumn<object>.Plain("Commodity")));
        }

        heads.Add((buying ? "Price" : "Pays", SortColumn<object>.Remote(buying ? "Price" : "Pays", best)));
        heads.Add((buying ? "Stock" : "Demand", SortColumn<object>.Plain(buying ? "Stock" : "Demand")));

        if (ByTheTonne(posting))
        {
            heads.Add(("The load", SortColumn<object>.Plain("The load")));
        }

        heads.Add(("Priced", SortColumn<object>.Plain("Priced")));

        var columns = heads.Select(head => head.Column).ToList();
        var header = new SortHeader<object>(HeadName, columns);
        var distance = heads.FindIndex(head => head.Label == "Distance");
        var price = heads.FindIndex(head => head.Label is "Price" or "Pays");

        // Ranked by the usual order until a column is pressed, so a first press reverses it.
        var current = posting.Query.OrderBy == CommodityOrder.Distance && distance >= 0
            ? new SortOrder(distance, posting.Query.Descending == true ? SortDirection.Descending : SortDirection.Ascending)
            : new SortOrder(price, posting.Query.Descending is { } descending
                ? descending ? SortDirection.Descending : SortDirection.Ascending
                : best);

        header.Show(current);
        header.Changed += () => SortBy(posting, header.Order, distance);

        // Inset by a list row's padding, so each header sits over its column.
        var row = layout.Header(header);
        row.Margin = new Thickness(12, 0);

        return row;
    }

    /// <summary>Asks the same question again, ranked by the pressed column; null is the usual order.</summary>
    private void SortBy(CommodityPosting posting, SortOrder? order, int distance)
    {
        var query = posting.Query;

        // Several commodities go back as the list they came from, each with its own tonnage.
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["commodity"] = Several(posting)
                ? string.Join(", ", query.Wants.Select(want => want.Tonnes is { } load
                    ? $"{want.Commodity}: {load.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                    : want.Commodity))
                : query.Commodity,
            ["near"] = posting.Near,
            ["max_distance"] = query.MaxDistance.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            ["limit"] = query.Limit.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        foreach (var (key, on) in new (string, bool)[]
                 {
                     ("selling", query.Side == TradeSide.Selling),
                     ("large_pad", query.LargePadOnly),
                     ("include_carriers", query.IncludeCarriers),
                     ("surface_stations", query.SurfaceStations),
                 })
        {
            if (on)
            {
                values[key] = "true";
            }
        }

        if (!Several(posting) && query.Tonnes is { } tonnes)
        {
            values["tonnes"] = tonnes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (query.MaxStationDistance is { } lightSeconds)
        {
            values["max_station_distance"] = lightSeconds.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        if (query.MinAvailable is { } least)
        {
            values["min_supply"] = least.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (query.Tag is { } tag)
        {
            values["tag"] = tag;
        }

        if (order is { } sort)
        {
            values["order_by"] = sort.Column == distance ? "distance" : "price";
            values["order"] = sort.Direction == SortDirection.Ascending ? "asc" : "desc";
        }

        _ = AskAsync(new ToolArguments(values));
    }

    private async Task AskAsync(ToolArguments arguments)
    {
        if (!_lookupsEnabled() || _sorting)
        {
            return;
        }

        _sorting = true;
        RoutingKit.Say(_status, "Reading the markets nearby…");

        try
        {
            using var inFlight = new CancellationTokenSource(Budget);
            var result = await _registry.InvokeAsync("find_nearest_station", arguments, inFlight.Token).ConfigureAwait(true);

            RoutingKit.Say(_status, result.Content, result.IsError);
            Refresh();
        }
        catch (OperationCanceledException)
        {
            RoutingKit.Say(_status, "Stopped.");
        }
        finally
        {
            _sorting = false;
        }
    }

    private Control OfferRow(CommodityOffer offer, CommodityPosting posting, TableLayout layout)
    {
        var buying = posting.Query.Side == TradeSide.Buying;
        var quote = offer.Market.Quote(posting.Query.Commodity);
        List<Control> cells = [StationCell(offer.Market.Station, offer.Market.System)];

        if (posting.Answer.OriginKnown)
        {
            cells.Add(Cell($"{offer.Distance:0.#} ly"));
        }

        if (Several(posting))
        {
            // One line per commodity, read across the three columns.
            cells.Add(Lines(offer.Lots.Select(lot => lot.Commodity)));
            cells.Add(Lines(offer.Lots.Select(lot => $"{lot.UnitPrice:N0}")));
            cells.Add(Lines(offer.Lots.Select(lot => offer.Market.Quote(lot.Commodity) is { } held
                ? $"{(buying ? held.Supply : held.Demand):N0}"
                : "?")));
        }
        else
        {
            cells.Add(Cell($"{offer.UnitPrice:N0}"));
            cells.Add(Cell($"{(buying ? quote?.Supply ?? 0 : quote?.Demand ?? 0):N0}"));
        }

        if (ByTheTonne(posting))
        {
            cells.Add(Cell($"{offer.Total:N0}"));
        }

        // The Commander's own reading is named as theirs, because it is the one figure here with no caveat on
        // it.
        cells.Add(Cell(
            offer.Market.UpdatedAt is { } when
                ? $"{(offer.IsTheirs ? "you saw it " : string.Empty)}{Ago(DateTimeOffset.UtcNow - when)}"
                : "undated",
            offer.IsTheirs ? ThemeManager.CyanKey : ThemeManager.GreyKey));

        return ListRow.Dress(new Border { Child = layout.Row(cells) });
    }

    private static string Ago(TimeSpan old) => old switch
    {
        { TotalHours: < 1 } => "within the hour",
        { TotalHours: < 24 } => $"{old.TotalHours:0} hours ago",
        { TotalDays: < 14 } => $"{old.TotalDays:0} days ago",
        _ => $"{old.TotalDays / 7:0} weeks ago",
    };

    /// <summary>The Station column: the name, and a copy glyph for the system it names (#157).</summary>
    private Control StationCell(string station, string system)
    {
        // The name takes what the copy glyph leaves, so a long one trims rather than running under it.
        var cells = new DockPanel();

        if (_copy is { } copy)
        {
            var glyph = D47.App.Controls.CopyGlyph.For(system, copy);
            glyph.VerticalAlignment = VerticalAlignment.Center;
            glyph.Margin = new Thickness(CopyGap, 0, 0, 0);

            DockPanel.SetDock(glyph, Dock.Right);
            cells.Children.Add(glyph);
        }

        var name = Cell($"{station} ({system})", ThemeManager.WhiteKey);
        cells.Children.Add(name);

        return cells;
    }

    /// <summary>Whether the answer is for several commodities at one station.</summary>
    private static bool Several(CommodityPosting posting) => posting.Query.More is { Count: > 0 };

    /// <summary>Whether any commodity was asked for by the tonne, so the answer has a load to price.</summary>
    private static bool ByTheTonne(CommodityPosting posting) => posting.Query.Wants.Any(want => want.Tonnes is not null);

    private static Control Lines(IEnumerable<string> lines)
    {
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        foreach (var line in lines)
        {
            stack.Children.Add(Cell(line));
        }

        return stack;
    }

    private static Control Cell(string text, bool muted = false) =>
        Cell(text, muted ? ThemeManager.GreyKey : ThemeManager.AKey);

    private static Control Cell(string text, string key)
    {
        var block = RoutingKit.Ink(text, TypeScale.Secondary, key);

        block.TextTrimming = TextTrimming.CharacterEllipsis;
        block.VerticalAlignment = VerticalAlignment.Center;

        return block;
    }

    /// <summary>Removes a control from whatever panel currently holds it, if any.</summary>
    private static void Detach(Control control)
    {
        if (control.Parent is Avalonia.Controls.Panel parent)
        {
            parent.Children.Remove(control);
        }
    }

    private static Control Labelled(
        string label,
        Control box,
        D47.App.Controls.FieldNeed need = D47.App.Controls.FieldNeed.Optional)
    {
        var stack = new StackPanel { Spacing = 3 };

        stack.Children.Add(D47.App.Controls.FormField.Label(label, need));
        stack.Children.Add(box);

        if (box is TextBox typed)
        {
            D47.App.Controls.FormField.Announce(typed, label, need);
        }

        return stack;
    }
}

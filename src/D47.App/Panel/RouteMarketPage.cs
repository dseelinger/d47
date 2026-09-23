using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>
/// Where to buy it, and what it costs there (Phase 49, "Asked by voice, and drawn on the Routing tab").
/// </summary>
public sealed class RouteMarketPage : UserControl
{
    /// <summary>How long a search may run.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(70);

    // The Station column is a name cell, a gap and a copy button, and the header above it has to line up
    // with every row (#157).
    private const double StationColumnWidth = 260;
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
        MinHeight = 30,
        HorizontalAlignment = HorizontalAlignment.Left,
    };

    private readonly TextBox _tonnes = new()
    {
        PlaceholderText = "how many",
        Width = 120,
        MinHeight = 30,
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

    private readonly TextBlock _status;

    private readonly StackPanel _results = new() { Spacing = 6 };

    public RouteMarketPage(
        CapabilityRegistry registry,
        CommodityBoard board,
        Func<bool> lookupsEnabled,
        Action? openSettings = null,
        Func<string, Task<bool>>? copy = null)
    {
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
        _body.Children.Add(RoutingKit.Title("Market").Row);

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
        Detach(_status);

        var find = new Button { Content = "Find it" };
        var cancel = new Button { Content = "Cancel", IsVisible = false };

        CancellationTokenSource? inFlight = null;

        find.Click += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_commodity.Text))
            {
                RoutingKit.Say(_status, "Name a commodity first.", error: true);
                return;
            }

            inFlight?.Cancel();
            inFlight = new CancellationTokenSource(Budget);

            find.IsEnabled = false;
            cancel.IsVisible = true;
            RoutingKit.Say(_status, "Reading the markets nearby…");

            try
            {
                var result = await _registry
                    .InvokeAsync("find_nearest_station", Arguments(), inFlight.Token)
                    .ConfigureAwait(true);

                // The sentence stays: it is what the Commander would have been told, and it carries the
                // caveats the table cannot.
                RoutingKit.Say(_status, result.Content, result.IsError);
                Refresh();
            }
            catch (OperationCanceledException)
            {
                RoutingKit.Say(_status, "Stopped.");
            }
            finally
            {
                find.IsEnabled = true;
                cancel.IsVisible = false;
                inFlight?.Dispose();
                inFlight = null;
            }
        };

        cancel.Click += (_, _) => inFlight?.Cancel();

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

        var heading = RoutingKit.Ink(
            $"{(buying ? "Buying" : "Selling")} {posting.Query.Commodity} near {posting.Near}",
            TypeScale.Body,
            ThemeManager.AKey,
            wrap: true);

        var rows = new StackPanel { Spacing = 2 };

        rows.Children.Add(HeaderRow(buying, posting.Query.Tonnes is not null, posting.Answer.OriginKnown));

        foreach (var offer in posting.Answer.Offers)
        {
            rows.Children.Add(OfferRow(offer, posting));
        }

        // Fixed pixel columns again, and the same overflow the Community Goal table had (#333): one scroll
        // viewport around the header and the offer rows together, so the block clips at the card's inner edge
        // and every column stays under its own header as it scrolls.
        var table = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = rows,
        };

        var stack = new StackPanel { Spacing = 10, Children = { RoutingKit.Section("What came back"), heading, table } };

        // The date on the answer itself, which is a different caveat from the date on each price and a
        // Commander needs both: a twenty-minute-old answer quoting six-hour-old prices is two kinds of stale
        // at once.
        stack.Children.Add(RoutingKit.Prose(
            $"Searched {Ago(DateTimeOffset.UtcNow - posting.AskedAt)}. Prices are reported by other "
            + "Commanders; supply moves fastest of all."));

        _results.Children.Add(stack);
    }

    private static Control HeaderRow(bool buying, bool hasLoad, bool distances)
    {
        // Inset by a list row's padding, so each header sits over its column.
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(12, 0) };

        row.Children.Add(Cell("Station", StationColumnWidth, muted: true));

        if (distances)
        {
            row.Children.Add(Cell("Distance", 80, muted: true));
        }

        row.Children.Add(Cell(buying ? "Price" : "Pays", 90, muted: true));
        row.Children.Add(Cell(buying ? "Stock" : "Demand", 90, muted: true));

        if (hasLoad)
        {
            row.Children.Add(Cell("The load", 110, muted: true));
        }

        row.Children.Add(Cell("Priced", 130, muted: true));

        return row;
    }

    private Control OfferRow(CommodityOffer offer, CommodityPosting posting)
    {
        var buying = posting.Query.Side == TradeSide.Buying;
        var quote = offer.Market.Quote(posting.Query.Commodity);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        row.Children.Add(StationCell(offer.Market.Station, offer.Market.System));

        if (posting.Answer.OriginKnown)
        {
            row.Children.Add(Cell($"{offer.Distance:0.#} ly", 80));
        }

        row.Children.Add(Cell($"{offer.UnitPrice:N0}", 90));
        row.Children.Add(Cell($"{(buying ? quote?.Supply ?? 0 : quote?.Demand ?? 0):N0}", 90));

        if (posting.Query.Tonnes is not null)
        {
            row.Children.Add(Cell($"{offer.Total:N0}", 110));
        }

        // The Commander's own reading is named as theirs, because it is the one figure here with no caveat on
        // it.
        row.Children.Add(Cell(
            offer.Market.UpdatedAt is { } when
                ? $"{(offer.IsTheirs ? "you saw it " : string.Empty)}{Ago(DateTimeOffset.UtcNow - when)}"
                : "undated",
            130,
            offer.IsTheirs ? ThemeManager.CyanKey : ThemeManager.GreyKey));

        return ListRow.Dress(new Border { Padding = new Thickness(12, 6), Child = row });
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
        // The name takes what the copy word leaves, so a long one trims rather than running under it.
        var cells = new DockPanel { Width = StationColumnWidth };

        if (_copy is { } copy)
        {
            var glyph = D47.App.Controls.CopyWord.For(system, copy);
            glyph.VerticalAlignment = VerticalAlignment.Center;
            glyph.Margin = new Thickness(CopyGap, 0, 0, 0);

            DockPanel.SetDock(glyph, Dock.Right);
            cells.Children.Add(glyph);
        }

        var name = Cell($"{station} ({system})", double.NaN, ThemeManager.WhiteKey);
        cells.Children.Add(name);

        return cells;
    }

    private static Control Cell(string text, double width, bool muted = false) =>
        Cell(text, width, muted ? ThemeManager.GreyKey : ThemeManager.AKey);

    private static Control Cell(string text, double width, string key)
    {
        var block = RoutingKit.Ink(text, TypeScale.Secondary, key);

        block.Width = width;
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

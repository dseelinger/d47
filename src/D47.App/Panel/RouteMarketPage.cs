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
/// Where to buy it, and what it costs there (Phase 49, "Asked by voice, and drawn on the
/// Routing tab").
/// <para>
/// <b>The panel is where this answer belongs.</b> Six stations with a price, a stock figure, a
/// distance and a date each is thirty numbers, which is a table to look at and a paragraph to
/// listen to. The spoken answer is still most of the value — this is a question asked with hands
/// on the stick — so both exist and neither is a second implementation.
/// </para>
/// <para>
/// <b>Nothing here searches anything itself.</b> The button invokes <c>find_nearest_station</c>
/// through the registry, exactly as the planners do and exactly as the model does; the rows are
/// read back from <see cref="CommodityBoard"/>, which the capability writes on its way out. So a
/// Commander who asks by voice and then looks at the tab sees the answer they were just given
/// rather than a second search that might disagree with it.
/// </para>
/// </summary>
public sealed class RouteMarketPage : UserControl
{
    /// <summary>
    /// How long a search may run. The sweep's own page budget is twenty seconds each across three
    /// pages, and this is the surface declining to sit on a spinner longer than that can take.
    /// </summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(70);

    private readonly CapabilityRegistry _registry;
    private readonly CommodityBoard _board;
    private readonly Func<bool> _lookupsEnabled;
    private readonly Action? _openSettings;

    private readonly StackPanel _body = new() { Spacing = 12 };

    private readonly TextBox _commodity = new()
    {
        // An example where a default goes, on a required field (#253) — see the survey in the
        // issue: Tritium sat in the same grey and the same slot as the numbers that genuinely are
        // what happens if you type nothing.
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

    private readonly CheckBox _selling = new() { Content = "Selling it, not buying" };

    private readonly CheckBox _largePad = new() { Content = "Large pad only" };

    private readonly TextBlock _status;

    private readonly StackPanel _results = new() { Spacing = 6 };

    public RouteMarketPage(
        CapabilityRegistry registry,
        CommodityBoard board,
        Func<bool> lookupsEnabled,
        Action? openSettings = null)
    {
        _registry = registry;
        _board = board;
        _lookupsEnabled = lookupsEnabled;
        _openSettings = openSettings;

        _status = Text(string.Empty, TypeScale.Secondary, ThemeManager.TextMutedKey, wrap: true);
        _status.IsVisible = false;

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

    private Control SwitchedOff()
    {
        var body = new StackPanel { Spacing = 8 };

        body.Children.Add(Text(
            "Looking up markets is switched off. It shares the galaxy search setting, so turning on "
            + "“Look things up in the galaxy” switches both on.",
            TypeScale.Body,
            ThemeManager.TextKey,
            wrap: true));

        if (_openSettings is { } open)
        {
            var button = new Button { Content = "Open settings", Padding = new Thickness(12, 4) };
            button.Click += (_, _) => open();
            body.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { button } });
        }

        return Card("Market lookups are off", body);
    }

    private Control SearchCard()
    {
        // _commodity, _tonnes, _selling and _largePad are readonly fields, built once and reused
        // across every Build() — Labelled() wraps the two boxes in a fresh StackPanel each call,
        // and the checkboxes join a fresh form StackPanel directly, so a second call finds each
        // one still parented to the wrapper Build() just discarded. Detach before rewrapping.
        Detach(_commodity);
        Detach(_tonnes);
        Detach(_selling);
        Detach(_largePad);
        Detach(_status);

        var find = new Button { Content = "Find it", Padding = new Thickness(12, 4), MinHeight = 30 };
        var cancel = new Button
        {
            Content = "Cancel",
            Padding = new Thickness(12, 4),
            MinHeight = 30,
            IsVisible = false,
        };

        CancellationTokenSource? inFlight = null;

        find.Click += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_commodity.Text))
            {
                _status.IsVisible = true;
                _status.Text = "Name a commodity first.";
                return;
            }

            inFlight?.Cancel();
            inFlight = new CancellationTokenSource(Budget);

            find.IsEnabled = false;
            cancel.IsVisible = true;
            _status.IsVisible = true;
            _status.Text = "Reading the markets nearby…";

            try
            {
                var result = await _registry
                    .InvokeAsync("find_nearest_station", Arguments(), inFlight.Token)
                    .ConfigureAwait(true);

                // The sentence stays: it is what the Commander would have been told, and it
                // carries the caveats the table cannot.
                _status.Text = result.Content;
                Refresh();
            }
            catch (OperationCanceledException)
            {
                _status.Text = "Stopped.";
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
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10,
                    Children =
                    {
                        Labelled("Commodity", _commodity, D47.App.Controls.FieldNeed.Required),
                        Labelled("Tonnes", _tonnes),
                    },
                },
                D47.App.Controls.FormField.Legend(required: true),
                _selling,
                _largePad,
                Text(
                    "Say the tonnage and stations that cannot fill the whole load drop out, and a "
                    + "bigger load is worth a longer trip. Leave it blank to rank on price alone.",
                    TypeScale.Small,
                    ThemeManager.TextMutedKey,
                    wrap: true),
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Margin = new Thickness(0, 4, 0, 0),
                    Children = { find, cancel },
                },
                _status,
            },
        };

        return Card("Where to buy it", form);
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

        var heading = Text(
            $"{(buying ? "Buying" : "Selling")} {posting.Query.Commodity} near {posting.Near}",
            TypeScale.Subheading,
            ThemeManager.TextKey);

        heading.FontWeight = FontWeight.SemiBold;

        var rows = new StackPanel { Spacing = 4 };

        rows.Children.Add(HeaderRow(buying, posting.Query.Tonnes is not null, posting.Answer.OriginKnown));

        foreach (var offer in posting.Answer.Offers)
        {
            rows.Children.Add(OfferRow(offer, posting));
        }

        var stack = new StackPanel { Spacing = 10, Children = { heading, rows } };

        // The date on the answer itself, which is a different caveat from the date on each price
        // and a Commander needs both: a twenty-minute-old answer quoting six-hour-old prices is
        // two kinds of stale at once.
        stack.Children.Add(Text(
            $"Searched {Ago(DateTimeOffset.UtcNow - posting.AskedAt)}. Prices are reported by other "
            + "Commanders; supply moves fastest of all.",
            TypeScale.Small,
            ThemeManager.TextMutedKey,
            wrap: true));

        _results.Children.Add(Card("What came back", stack));
    }

    private static Control HeaderRow(bool buying, bool hasLoad, bool distances)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        row.Children.Add(Cell("Station", 200, muted: true));

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

    private static Control OfferRow(CommodityOffer offer, CommodityPosting posting)
    {
        var buying = posting.Query.Side == TradeSide.Buying;
        var quote = offer.Market.Quote(posting.Query.Commodity);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        row.Children.Add(Cell($"{offer.Market.Station} ({offer.Market.System})", 200));

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

        // The Commander's own reading is named as theirs, because it is the one figure here with
        // no caveat on it.
        row.Children.Add(Cell(
            offer.Market.UpdatedAt is { } when
                ? $"{(offer.IsTheirs ? "you saw it " : string.Empty)}{Ago(DateTimeOffset.UtcNow - when)}"
                : "undated",
            130,
            muted: !offer.IsTheirs));

        return row;
    }

    private static string Ago(TimeSpan old) => old switch
    {
        { TotalHours: < 1 } => "within the hour",
        { TotalHours: < 24 } => $"{old.TotalHours:0} hours ago",
        { TotalDays: < 14 } => $"{old.TotalDays:0} days ago",
        _ => $"{old.TotalDays / 7:0} weeks ago",
    };

    private static Control Cell(string text, double width, bool muted = false)
    {
        var block = Text(text, TypeScale.Secondary, muted ? ThemeManager.TextMutedKey : ThemeManager.TextKey);

        block.Width = width;
        block.TextTrimming = TextTrimming.CharacterEllipsis;

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

    private static TextBlock Text(string text, double size, string colourKey, bool wrap = false)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MaxWidth = wrap ? 520 : double.PositiveInfinity,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        block.Bind(
            TextBlock.ForegroundProperty,
            Application.Current!.Resources.GetResourceObservable(colourKey));

        return block;
    }

    private static Control Card(string title, Control body)
    {
        var heading = Text(title, TypeScale.Subheading, ThemeManager.TextKey);
        heading.FontWeight = FontWeight.SemiBold;

        var card = new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            Child = new StackPanel { Spacing = 10, Children = { heading, body } },
        };

        card.Bind(
            Border.BackgroundProperty,
            Application.Current!.Resources.GetResourceObservable(ThemeManager.SurfaceAltKey));

        card.Bind(
            Border.BorderBrushProperty,
            Application.Current!.Resources.GetResourceObservable(ThemeManager.BorderKey));

        return card;
    }
}

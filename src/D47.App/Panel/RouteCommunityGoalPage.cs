using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>
/// The Community Goal supply search and what it has earned
/// (<a href="https://github.com/dseelinger/d47/issues/296">#296</a>).
/// <para>
/// <b>One saved question, run again and again.</b> The form is the INARA query the Commander was
/// typing by hand: the commodity is the one field that moves, and the rest is written on the page
/// as the fixed shape of the search rather than offered as knobs. Run invokes
/// <c>find_nearest_station</c> through the registry with exactly the arguments the spoken
/// "community goal search" bakes, so the two roads cannot disagree; the answer is read back from
/// <see cref="CommodityBoard"/>, which the capability writes on its way out.
/// </para>
/// <para>
/// <b>Built once, redrawn in place.</b> The Market page crashed d47 on every successful Find
/// because it cleared its body and wrapped its <c>readonly</c> controls in fresh containers
/// (#284). This page never clears the body: the form card, the results panel and the ledger panel
/// are three fixed children, and <see cref="Refresh"/> redraws the inside of the two that change.
/// Nothing that holds a field is ever re-parented.
/// </para>
/// </summary>
public sealed class RouteCommunityGoalPage : UserControl
{
    /// <summary>The Market page's budget, for the same sweep.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(70);

    private readonly CapabilityRegistry _registry;
    private readonly CommodityBoard _board;
    private readonly CommunityGoalSurface _goal;
    private readonly Func<bool> _lookupsEnabled;
    private readonly Action? _openSettings;

    private readonly Border _off;
    private readonly Border _form;
    private readonly StackPanel _results = new() { Spacing = 6 };
    private readonly StackPanel _ledger = new() { Spacing = 6 };

    private readonly TextBox _commodity = new()
    {
        PlaceholderText = CommunityGoalSearch.DefaultCommodity,
        Width = 190,
        MinHeight = 30,
        HorizontalAlignment = HorizontalAlignment.Left,
    };

    private readonly Button _run = new() { Content = "Run", Padding = new Thickness(12, 4), MinHeight = 30 };

    private readonly Button _cancel = new()
    {
        Content = "Cancel",
        Padding = new Thickness(12, 4),
        MinHeight = 30,
        IsVisible = false,
    };

    private readonly TextBlock _status;

    private CancellationTokenSource? _inFlight;

    public RouteCommunityGoalPage(
        CapabilityRegistry registry,
        CommodityBoard board,
        CommunityGoalSurface goal,
        Func<bool> lookupsEnabled,
        Action? openSettings = null)
    {
        _registry = registry;
        _board = board;
        _goal = goal;
        _lookupsEnabled = lookupsEnabled;
        _openSettings = openSettings;

        _status = Text(string.Empty, TypeScale.Secondary, ThemeManager.TextMutedKey, wrap: true);
        _status.IsVisible = false;

        _commodity.Text = goal.Search.Commodity;
        _commodity.LostFocus += (_, _) => Save();

        _run.Click += async (_, _) => await RunAsync();
        _cancel.Click += (_, _) => _inFlight?.Cancel();

        _off = SwitchedOff();
        _form = SearchCard();

        var body = new StackPanel { Spacing = 12, Children = { _off, _form, _results, _ledger } };

        Content = new ScrollViewer
        {
            Padding = new Thickness(14),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = body,
        };

        Build();
    }

    /// <summary>Redraws the answer and the ledger. Never the form, which holds what was typed.</summary>
    public void Refresh() => Dispatcher.UIThread.Post(Build);

    private void Build()
    {
        var on = _lookupsEnabled();

        _off.IsVisible = !on;
        _form.IsVisible = on;
        _results.IsVisible = on;

        DrawResults();
        DrawLedger();
    }

    private void Save() => _goal.Search.Commodity = _commodity.Text ?? string.Empty;

    private async Task RunAsync()
    {
        Save();

        // The box shows what was saved, so a blank falls back to the default in view.
        _commodity.Text = _goal.Search.Commodity;

        _inFlight?.Cancel();
        _inFlight = new CancellationTokenSource(Budget);

        _run.IsEnabled = false;
        _cancel.IsVisible = true;
        _status.IsVisible = true;
        _status.Text = "Reading the markets nearby…";

        try
        {
            var result = await _registry
                .InvokeAsync(MaterialSeam.MarketTool, _goal.Search.Arguments(), _inFlight.Token)
                .ConfigureAwait(true);

            // The sentence stays: it is what the Commander would have heard, and it carries the
            // caveats the table cannot.
            _status.Text = result.Content;
            Refresh();
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Stopped.";
        }
        finally
        {
            _run.IsEnabled = true;
            _cancel.IsVisible = false;
            _inFlight?.Dispose();
            _inFlight = null;
        }
    }

    private Border SwitchedOff()
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

    private Border SearchCard()
    {
        var form = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Labelled("Commodity", _commodity),
                Text(
                    $"Buying, from where you are, nearest first. Within {CommunityGoalSearch.MaxDistance:0} ly, "
                    + $"prices under {CommunityGoalSearch.MaxPriceAgeHours} hours old, a large pad, a station within "
                    + $"{CommunityGoalSearch.MaxStationDistance:N0} Ls of the star, at least "
                    + $"{CommunityGoalSearch.MinSupply:N0} in stock, no surface stations, no carriers. "
                    + "Say “community goal search” to run the same thing by voice, and “refresh” while this "
                    + "page is up to run it again.",
                    TypeScale.Small,
                    ThemeManager.TextMutedKey,
                    wrap: true),
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Margin = new Thickness(0, 4, 0, 0),
                    Children = { _run, _cancel },
                },
                _status,
            },
        };

        return Card("Community Goal search", form);
    }

    private void DrawResults()
    {
        _results.Children.Clear();

        if (_board.Last is not { } posting || posting.Answer.Offers.Count == 0)
        {
            return;
        }

        var heading = Text(
            $"{posting.Query.Commodity} near {posting.Near}, nearest first",
            TypeScale.Subheading,
            ThemeManager.TextKey);

        heading.FontWeight = FontWeight.SemiBold;

        var rows = new StackPanel { Spacing = 4 };

        rows.Children.Add(HeaderRow(posting.Answer.OriginKnown));

        foreach (var offer in posting.Answer.Offers)
        {
            rows.Children.Add(OfferRow(offer, posting));
        }

        var stack = new StackPanel { Spacing = 10, Children = { heading, rows } };

        stack.Children.Add(Text(
            $"Searched {Ago(DateTimeOffset.UtcNow - posting.AskedAt)}. Prices are reported by other "
            + "Commanders; supply moves fastest of all.",
            TypeScale.Small,
            ThemeManager.TextMutedKey,
            wrap: true));

        _results.Children.Add(Card("What came back", stack));
    }

    private void DrawLedger()
    {
        _ledger.Children.Clear();

        var commodity = _goal.Search.Commodity;
        var who = _goal.Commander();
        var now = _goal.Now();
        var week = _goal.Ledger.Week(now);

        var lines = new StackPanel { Spacing = 4 };

        lines.Children.Add(LedgerRow("This session", _goal.Ledger.Session(who, commodity)));
        lines.Children.Add(LedgerRow("Today", _goal.Ledger.Between(who, commodity, CommodityLedger.Today(now))));
        lines.Children.Add(LedgerRow(
            week.Label == "this week" ? "This week" : $"This goal — {week.Label}",
            _goal.Ledger.Between(who, commodity, week)));

        var stack = new StackPanel { Spacing = 10, Children = { lines } };

        stack.Children.Add(Text(
            _goal.Ledger.LastSale(who, commodity) is { } sale
                ? $"Last sale: {sale.Count:N0} tonnes at {sale.UnitPrice:N0} cr, "
                  + (sale.CostBasis > 0 ? $"paid {sale.UnitPaid:N0} cr each, " : "cost unknown, ")
                  + $"{Signed(sale.Net)} — {Ago(now - sale.At)}."
                : $"No {commodity} sold yet.",
            TypeScale.Secondary,
            ThemeManager.TextKey,
            wrap: true));

        stack.Children.Add(Text(
            "Net of what the cargo cost, from your journal. The week is the running goal's own window "
            + "when one is live, and the calendar week when none is.",
            TypeScale.Small,
            ThemeManager.TextMutedKey,
            wrap: true));

        _ledger.Children.Add(Card($"{commodity} ledger", stack));
    }

    private static Control LedgerRow(string label, LedgerTotal total)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        row.Children.Add(Cell(label, 200, muted: true));
        row.Children.Add(Cell(total.Sales == 0 ? "—" : Signed(total.Net), 130));
        row.Children.Add(Cell(
            total.Sales == 0 ? "no sales" : $"{total.Sales} sales, {total.Tonnes:N0} t",
            180,
            muted: true));

        return row;
    }

    private static string Signed(long net) => net switch
    {
        > 0 => $"+{net:N0} cr",
        < 0 => $"−{Math.Abs(net):N0} cr",
        _ => "level",
    };

    private static Control HeaderRow(bool distances)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        row.Children.Add(Cell("Station", 170, muted: true));
        row.Children.Add(Cell("System", 130, muted: true));
        row.Children.Add(Cell("Pad", 50, muted: true));
        row.Children.Add(Cell("From star", 90, muted: true));

        if (distances)
        {
            row.Children.Add(Cell("Distance", 80, muted: true));
        }

        row.Children.Add(Cell("Supply", 80, muted: true));
        row.Children.Add(Cell("Price", 80, muted: true));
        row.Children.Add(Cell("Updated", 130, muted: true));

        return row;
    }

    private static Control OfferRow(CommodityOffer offer, CommodityPosting posting)
    {
        var quote = offer.Market.Quote(posting.Query.Commodity);
        var buying = posting.Query.Side == TradeSide.Buying;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        row.Children.Add(Cell(offer.Market.Station, 170));
        row.Children.Add(Cell(offer.Market.System, 130));
        row.Children.Add(Cell(offer.Market.HasLargePad ? "L" : "M", 50));
        row.Children.Add(Cell(
            offer.Market.DistanceToArrival is { } arrival ? $"{arrival:N0} Ls" : "?",
            90));

        if (posting.Answer.OriginKnown)
        {
            row.Children.Add(Cell($"{offer.Distance:0.#} ly", 80));
        }

        row.Children.Add(Cell($"{(buying ? quote?.Supply ?? 0 : quote?.Demand ?? 0):N0}", 80));
        row.Children.Add(Cell($"{offer.UnitPrice:N0}", 80));
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
        { TotalMinutes: < 1 } => "just now",
        { TotalHours: < 1 } => $"{old.TotalMinutes:0} minutes ago",
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

    private static Control Labelled(string label, TextBox box)
    {
        var stack = new StackPanel { Spacing = 3 };

        stack.Children.Add(D47.App.Controls.FormField.Label(label, D47.App.Controls.FieldNeed.Optional));
        stack.Children.Add(box);

        D47.App.Controls.FormField.Announce(box, label, D47.App.Controls.FieldNeed.Optional);

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

    private static Border Card(string title, Control body)
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

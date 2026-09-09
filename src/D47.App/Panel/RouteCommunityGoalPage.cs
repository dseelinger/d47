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

/// <summary>The Community Goal supply search and what it has earned (#296).</summary>
public sealed class RouteCommunityGoalPage : UserControl
{
    /// <summary>The Market page's budget, for the same sweep.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(70);

    // The System column is a name cell, a gap and a copy button, and the header above it has to line up with
    // every row.
    private const double SystemNameWidth = 200;
    private const double CopyGap = 6;
    private const double CopyButtonSize = 24;
    private const double SystemColumnWidth = SystemNameWidth + CopyGap + CopyButtonSize;

    // The rest of the results table, narrowed against the card's inner width (#333).
    private const double StationWidth = 150;
    private const double FromStarWidth = 80;
    private const double UpdatedWidth = 110;
    private const double RowSpacing = 8;

    private readonly CapabilityRegistry _registry;
    private readonly CommodityBoard _board;
    private readonly CommunityGoalSurface _goal;
    private readonly Func<bool> _lookupsEnabled;
    private readonly Action? _openSettings;
    private readonly Action<string>? _copy;

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

    /// <summary>A posting on <see cref="_board"/> to treat as if it were not there.</summary>
    private CommodityPosting? _hidden;

    /// <summary>
    /// The colour bindings <see cref="DrawResults"/> and <see cref="DrawLedger"/> hand out to controls
    /// that get discarded on the next rebuild.
    /// </summary>
    private readonly List<IDisposable> _transientBindings = [];

    private CancellationTokenSource? _inFlight;

    public RouteCommunityGoalPage(
        CapabilityRegistry registry,
        CommodityBoard board,
        CommunityGoalSurface goal,
        Func<bool> lookupsEnabled,
        Action? openSettings = null,
        Action<string>? copy = null)
    {
        _registry = registry;
        _board = board;
        _goal = goal;
        _lookupsEnabled = lookupsEnabled;
        _openSettings = openSettings;
        _copy = copy;

        _status = Text(string.Empty, TypeScale.Secondary, ThemeManager.TextMutedKey, wrap: true);
        _status.IsVisible = false;

        _commodity.Text = goal.Search.Commodity;

        // Every keystroke, not just LostFocus (#315): a voice-triggered "refresh" never moves UI focus, so a
        // saved value that only updated on LostFocus left a spoken refresh running against whatever was typed
        // last time the box lost focus.
        _commodity.TextChanged += (_, _) => Save();
        _commodity.LostFocus += (_, _) => Refresh();

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

    /// <summary>Redraws the answer and the ledger.</summary>
    public void Refresh()
    {
        if (!_goal.Search.Showing())
        {
            return;
        }

        Dispatcher.UIThread.Post(Build);
    }

    private void Build()
    {
        foreach (var binding in _transientBindings)
        {
            binding.Dispose();
        }

        _transientBindings.Clear();

        var on = _lookupsEnabled();

        _off.IsVisible = !on;
        _form.IsVisible = on;
        _results.IsVisible = on;

        DrawResults();
        DrawLedger();
    }

    /// <summary>
    /// Writes what is typed straight through to the saved search, so a spoken "refresh" runs against it
    /// (#315).
    /// </summary>
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

        var before = _board.Last;

        try
        {
            var result = await _registry
                .InvokeAsync(MaterialSeam.MarketTool, _goal.Search.Arguments(), _inFlight.Token)
                .ConfigureAwait(true);

            // The sentence stays: it is what the Commander would have heard, and it carries the caveats the
            // table cannot.
            _status.Text = result.Content;

            // An error, and a non-cargo commodity answered before the search even runs, both leave the board
            // exactly as it was: neither counts as a fresh answer, so the table is cleared rather than left
            // showing whatever the board held before (#318).
            _hidden = result.IsError || ReferenceEquals(_board.Last, before) ? _board.Last : null;

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
                    $"Buying, from the goal's own system, nearest first. Within {CommunityGoalSearch.MaxDistance:0} ly, "
                    + $"prices under {CommunityGoalSearch.MaxPriceAgeHours} hours old, a large pad, a station within "
                    + $"{CommunityGoalSearch.MaxStationDistance:N0} Ls of the star, at least "
                    + $"{CommunityGoalSearch.MinSupply:N0} in stock, no surface stations, no carriers. "
                    + "With no goal running it searches from wherever the ship is, and says so. "
                    + "Say “community goal search” to run the same thing by voice, “cg search from here” to "
                    + "measure from the ship instead, and “refresh” while this page is up to run it again.",
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

        if (_board.Last is not { } posting
            || !CommunityGoalSearch.Owns(posting.Query.Tag)
            || ReferenceEquals(posting, _hidden)
            || posting.Answer.Offers.Count == 0)
        {
            return;
        }

        var order = posting.Query.OrderBy == CommodityOrder.Distance ? "nearest first" : "best price first";

        // Whose system this was measured from, in words (#331). "near Scorpii Sector ND-S b4-0" was already
        // true and still went unread: a system name alone does not say whether it is the goal's or the
        // ship's, and the whole point of the search is that those differ.
        var whose = CommunityGoalSearch.Whose(posting.Query.Tag) is { } named ? $", {named}" : string.Empty;

        var heading = Text(
            $"{posting.Query.Commodity} near {posting.Near}{whose}, {order}",
            TypeScale.Subheading,
            ThemeManager.TextKey,
            track: _transientBindings);

        heading.FontWeight = FontWeight.SemiBold;

        var rows = new StackPanel { Spacing = 4 };

        rows.Children.Add(HeaderRow(posting.Answer.OriginKnown));

        foreach (var offer in posting.Answer.Offers)
        {
            rows.Children.Add(OfferRow(offer, posting));
        }

        // Scrolls horizontally: the columns are fixed pixel widths and a narrow panel cannot hold them (#333).
        var table = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = rows,
        };

        var stack = new StackPanel { Spacing = 10, Children = { heading, table } };

        stack.Children.Add(Text(
            $"Searched {Ago(DateTimeOffset.UtcNow - posting.AskedAt)}. Prices are reported by other "
            + "Commanders; supply moves fastest of all.",
            TypeScale.Small,
            ThemeManager.TextMutedKey,
            wrap: true,
            track: _transientBindings));

        _results.Children.Add(Card("What came back", stack, _transientBindings));
    }

    private void DrawLedger()
    {
        _ledger.Children.Clear();

        var commodity = _goal.Search.Commodity;
        var who = _goal.Commander();

        if (who is null)
        {
            // Falling back to whoever wrote the newest journal file would silently answer as the wrong
            // Commander with Elite not running (#313); a page can say so plainly instead.
            _ledger.Children.Add(Card(
                $"{commodity} ledger",
                Text(
                    "No active Commander — start Elite Dangerous to see what this has made or lost.",
                    TypeScale.Secondary,
                    ThemeManager.TextMutedKey,
                    wrap: true,
                    track: _transientBindings),
                _transientBindings));

            return;
        }

        var now = _goal.Now();
        var week = _goal.Week(now);

        var lines = new StackPanel { Spacing = 4 };

        lines.Children.Add(LedgerRow("This session", _goal.Ledger.Session(who, commodity)));
        lines.Children.Add(LedgerRow(
            "Today", _goal.Ledger.Between(who, commodity, CommodityLedger.Today(now))));
        lines.Children.Add(LedgerRow("This week", _goal.Ledger.Between(who, commodity, week)));

        var stack = new StackPanel { Spacing = 10, Children = { lines } };

        stack.Children.Add(Text(
            _goal.Ledger.LastSale(who, commodity) is { } sale
                ? $"Last sale: {sale.Count:N0} tonnes at {sale.UnitPrice:N0} cr, "
                  + (sale.CostBasis > 0 ? $"paid {sale.UnitPaid:N0} cr each, " : "cost unknown, ")
                  + $"{Signed(sale.Net)} — {Ago(now - sale.At)}."
                : $"No {commodity} sold yet.",
            TypeScale.Secondary,
            ThemeManager.TextKey,
            wrap: true,
            track: _transientBindings));

        stack.Children.Add(Text(
            "Net of what the cargo cost, from your journal. The week turns where the advanced "
            + "setting says, Thursday 07:00 UTC by default.",
            TypeScale.Small,
            ThemeManager.TextMutedKey,
            wrap: true,
            track: _transientBindings));

        _ledger.Children.Add(Card($"{commodity} ledger", stack, _transientBindings));
    }

    private Control LedgerRow(string label, LedgerTotal total)
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

    private Control HeaderRow(bool distances)
    {
        // Trimmed against the card's inner width (#333) so the common case needs no scrolling at all.
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = RowSpacing };

        row.Children.Add(Cell("Station", StationWidth, muted: true));
        row.Children.Add(Cell("System", SystemColumnWidth, muted: true));
        row.Children.Add(Cell("Pad", 50, muted: true));
        row.Children.Add(Cell("From star", FromStarWidth, muted: true));

        if (distances)
        {
            row.Children.Add(Cell("Distance", 80, muted: true));
        }

        row.Children.Add(Cell("Supply", 80, muted: true));
        row.Children.Add(Cell("Price", 80, muted: true));
        row.Children.Add(Cell("Updated", UpdatedWidth, muted: true));

        return row;
    }

    private Control OfferRow(CommodityOffer offer, CommodityPosting posting)
    {
        var quote = offer.Market.Quote(posting.Query.Commodity);
        var buying = posting.Query.Side == TradeSide.Buying;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = RowSpacing };

        row.Children.Add(Cell(offer.Market.Station, StationWidth));
        row.Children.Add(SystemCell(offer.Market.System));
        row.Children.Add(Cell(offer.Market.HasLargePad ? "L" : "M", 50));
        row.Children.Add(Cell(
            offer.Market.DistanceToArrival is { } arrival ? $"{arrival:N0} Ls" : "?",
            FromStarWidth));

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
            UpdatedWidth,
            muted: !offer.IsTheirs));

        return row;
    }

    /// <summary>
    /// The System cell plus a small copy-to-clipboard button beside it, so a Commander can grab a
    /// system name in full without it ever needing to fit uncut in the column (#328).
    /// </summary>
    private Control SystemCell(string system)
    {
        var cells = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = CopyGap,
            Width = SystemColumnWidth,
            Children = { Cell(system, SystemNameWidth) },
        };

        if (_copy is { } copy)
        {
            var button = new Button
            {
                Width = CopyButtonSize,
                Height = CopyButtonSize,
                Padding = new Thickness(4),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
            };

            button.Click += (_, _) => copy(system);

            D47.App.Controls.Glyphs.Mark(
                button, D47.App.Controls.Glyphs.Copy, ThemeManager.AccentKey, $"Copy {system}", size: 12);

            cells.Children.Add(button);
        }

        return cells;
    }

    private static string Ago(TimeSpan old) => old switch
    {
        { TotalMinutes: < 1 } => "just now",
        { TotalHours: < 1 } => $"{old.TotalMinutes:0} minutes ago",
        { TotalHours: < 24 } => $"{old.TotalHours:0} hours ago",
        { TotalDays: < 14 } => $"{old.TotalDays:0} days ago",
        _ => $"{old.TotalDays / 7:0} weeks ago",
    };

    private Control Cell(string text, double width, bool muted = false)
    {
        var block = Text(
            text,
            TypeScale.Secondary,
            muted ? ThemeManager.TextMutedKey : ThemeManager.TextKey,
            track: _transientBindings);

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

    /// <summary>
    /// <param name="track"> Where the returned <c>Bind</c> disposable is kept, for a control that will
    /// later be discarded and rebuilt — <see cref="_transientBindings"/> from <see cref="DrawResults"/>
    /// and <see cref="DrawLedger"/> (#320).
    /// </summary>
    /// <param name="track">
    /// Where the returned <c>Bind</c> disposable is kept, for a control that will later be discarded
    /// and rebuilt — <see cref="_transientBindings"/> from <see cref="DrawResults"/> and <see
    /// cref="DrawLedger"/> (#320).
    /// </param>
    private static TextBlock Text(string text, double size, string colourKey, bool wrap = false, List<IDisposable>? track = null)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MaxWidth = wrap ? 520 : double.PositiveInfinity,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var binding = block.Bind(
            TextBlock.ForegroundProperty,
            Application.Current!.Resources.GetResourceObservable(colourKey));

        track?.Add(binding);

        return block;
    }

    /// <summary>
    /// <param name="track">See <see cref="Text"/>; threaded through to the heading it draws
    /// too.</param>
    /// </summary>
    /// <param name="track">
    /// See <see cref="Text"/>; threaded through to the heading it draws too.
    /// </param>
    private static Border Card(string title, Control body, List<IDisposable>? track = null)
    {
        var heading = Text(title, TypeScale.Subheading, ThemeManager.TextKey, track: track);
        heading.FontWeight = FontWeight.SemiBold;

        var card = new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            Child = new StackPanel { Spacing = 10, Children = { heading, body } },
        };

        var background = card.Bind(
            Border.BackgroundProperty,
            Application.Current!.Resources.GetResourceObservable(ThemeManager.SurfaceAltKey));

        var borderBrush = card.Bind(
            Border.BorderBrushProperty,
            Application.Current!.Resources.GetResourceObservable(ThemeManager.BorderKey));

        track?.Add(background);
        track?.Add(borderBrush);

        return card;
    }
}

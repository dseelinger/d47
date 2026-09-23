using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
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
    private const double SystemColumnWidth = 260;
    private const double CopyGap = 6;

    // The rest of the results table, narrowed against the page's inner width (#333).
    private const double StationWidth = 150;
    private const double FromStarWidth = 80;
    private const double UpdatedWidth = 110;
    private const double RowSpacing = 8;

    /// <summary>A list row's horizontal padding, which the header row is inset by.</summary>
    private const double RowInset = 12;

    private readonly CapabilityRegistry _registry;
    private readonly CommodityBoard _board;
    private readonly CommunityGoalSurface _goal;
    private readonly Func<bool> _lookupsEnabled;
    private readonly Action? _openSettings;
    private readonly Func<string, Task<bool>>? _copy;
    private readonly Func<string?>? _here;

    private readonly Control _off;
    private readonly Control _form;
    private readonly StackPanel _results = new() { Spacing = 6 };
    private readonly StackPanel _ledger = new() { Spacing = 6 };

    private readonly TextBox _commodity = new()
    {
        PlaceholderText = CommunityGoalSearch.DefaultCommodity,
        Width = 190,
        MinHeight = 30,
        HorizontalAlignment = HorizontalAlignment.Left,
    };

    private readonly Button _run = new() { Content = "Run" };

    private readonly Button _cancel = new() { Content = "Cancel", IsVisible = false };

    private readonly TextBlock _status = RoutingKit.Status();

    /// <summary>A posting on <see cref="_board"/> to treat as if it were not there.</summary>
    private CommodityPosting? _hidden;

    private CancellationTokenSource? _inFlight;

    public RouteCommunityGoalPage(
        CapabilityRegistry registry,
        CommodityBoard board,
        CommunityGoalSurface goal,
        Func<bool> lookupsEnabled,
        Action? openSettings = null,
        Func<string, Task<bool>>? copy = null,

        // Community Goal's own settings, on the tab they only affect (#218). Docked outside the
        // scroller and never touched by Build(), so it survives every Refresh() (#340).
        Control? settingsStrip = null,
        Func<string?>? here = null)
    {
        _registry = registry;
        _board = board;
        _goal = goal;
        _lookupsEnabled = lookupsEnabled;
        _openSettings = openSettings;
        _copy = copy;
        _here = here;

        _commodity.Text = goal.Search.Commodity;

        // Every keystroke, not just LostFocus (#315): a voice-triggered "refresh" never moves UI focus, so a
        // saved value that only updated on LostFocus left a spoken refresh running against whatever was typed
        // last time the box lost focus.
        _commodity.TextChanged += (_, _) => Save();
        _commodity.LostFocus += (_, _) => Refresh();

        _run.Click += async (_, _) => await RunAsync();
        _cancel.Click += (_, _) => _inFlight?.Cancel();

        _off = RoutingKit.SwitchedOff(
            "Market lookups are off",
            "Looking up markets is switched off. It shares the galaxy search setting, so turning on "
            + "“Look things up in the galaxy” switches both on.",
            _openSettings);

        _form = SearchCard();

        var body = new StackPanel { Spacing = 12 };

        body.Children.Add(RoutingKit.Title("Community Goal").Row);
        body.Children.Add(_off);
        body.Children.Add(_form);
        body.Children.Add(_results);
        body.Children.Add(_ledger);

        var scroller = new ScrollViewer
        {
            Padding = new Thickness(14),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = body,
        };

        if (settingsStrip is not null)
        {
            var root = new DockPanel();

            DockPanel.SetDock(settingsStrip, Dock.Bottom);
            root.Children.Add(settingsStrip);
            root.CapStripHeight(settingsStrip);
            root.Children.Add(scroller);

            Content = root;
        }
        else
        {
            Content = scroller;
        }

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
        RoutingKit.Say(_status, "Reading the markets nearby…");

        var before = _board.Last;

        try
        {
            var result = await _registry
                .InvokeAsync(MaterialSeam.MarketTool, _goal.Search.Arguments(), _inFlight.Token)
                .ConfigureAwait(true);

            // The sentence stays: it is what the Commander would have heard, and it carries the caveats the
            // table cannot.
            RoutingKit.Say(_status, result.Content, result.IsError);

            // An error, and a non-cargo commodity answered before the search even runs, both leave the board
            // exactly as it was: neither counts as a fresh answer, so the table is cleared rather than left
            // showing whatever the board held before (#318).
            _hidden = result.IsError || ReferenceEquals(_board.Last, before) ? _board.Last : null;

            Refresh();
        }
        catch (OperationCanceledException)
        {
            RoutingKit.Say(_status, "Stopped.");
        }
        finally
        {
            _run.IsEnabled = true;
            _cancel.IsVisible = false;
            _inFlight?.Dispose();
            _inFlight = null;
        }
    }

    private StackPanel SearchCard() => new()
    {
        Spacing = 8,
        Children =
        {
            RoutingKit.Section("Community Goal search"),
            Labelled("Commodity", _commodity),
            RoutingKit.Prose(
                $"Buying, from the goal's own system, nearest first. Within {CommunityGoalSearch.MaxDistance:0} ly, "
                + $"prices under {CommunityGoalSearch.MaxPriceAgeHours} hours old, a large pad, a station within "
                + $"{CommunityGoalSearch.MaxStationDistance:N0} Ls of the star, at least "
                + $"{CommunityGoalSearch.MinSupply:N0} in stock, no surface stations, no carriers. "
                + "With no goal running it searches from wherever the ship is, and says so. "
                + "Say “community goal search” to run the same thing by voice, “cg search from here” to "
                + "measure from the ship instead, and “refresh” while this page is up to run it again."),
            RoutingKit.Actions(_run, _cancel),
            _status,
        },
    };

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

        // Whose system this was measured from, in words (#331): a system name alone does not say whether it
        // is the goal's or the ship's, and the reason for the search is that those differ.
        var whose = CommunityGoalSearch.Whose(posting.Query.Tag) is { } named ? $", {named}" : string.Empty;

        var heading = RoutingKit.Ink(
            $"{posting.Query.Commodity} near {posting.Near}{whose}, {order}",
            TypeScale.Body,
            ThemeManager.AKey,
            wrap: true);

        var rows = new StackPanel { Spacing = 2 };

        rows.Children.Add(HeaderRow(posting.Answer.OriginKnown));

        var here = _here?.Invoke();

        foreach (var offer in posting.Answer.Offers)
        {
            rows.Children.Add(OfferRow(offer, posting, here));
        }

        // Scrolls horizontally: the columns are fixed pixel widths and a narrow panel cannot hold them (#333).
        var table = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = rows,
        };

        _results.Children.Add(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                RoutingKit.Section("What came back"),
                heading,
                table,
                RoutingKit.Prose(
                    $"Searched {Ago(DateTimeOffset.UtcNow - posting.AskedAt)}. Prices are reported by other "
                    + "Commanders; supply moves fastest of all."),
            },
        });
    }

    private void DrawLedger()
    {
        _ledger.Children.Clear();

        var commodity = _goal.Search.Commodity;
        var who = _goal.Commander();

        _ledger.Children.Add(RoutingKit.Section($"{commodity} ledger"));

        if (who is null)
        {
            // Falling back to whoever wrote the newest journal file would silently answer as the wrong
            // Commander with Elite not running (#313); a page can say so plainly instead.
            _ledger.Children.Add(RoutingKit.Prose(
                "No active Commander — start Elite Dangerous to see what this has made or lost.",
                TypeScale.Secondary));

            return;
        }

        var now = _goal.Now();
        var week = _goal.Week(now);

        _ledger.Children.Add(StatTile.Grid(
            [
                LedgerTile("This session", _goal.Ledger.Session(who, commodity)),
                LedgerTile("Today", _goal.Ledger.Between(who, commodity, CommodityLedger.Today(now))),
                LedgerTile("This week", _goal.Ledger.Between(who, commodity, week)),
            ],
            maxColumns: 3));

        var last = RoutingKit.Ink(
            _goal.Ledger.LastSale(who, commodity) is { } sale
                ? $"Last sale: {sale.Count:N0} tonnes at {sale.UnitPrice:N0} cr, "
                  + (sale.CostBasis > 0 ? $"paid {sale.UnitPaid:N0} cr each, " : "cost unknown, ")
                  + $"{Signed(sale.Net)} — {Ago(now - sale.At)}."
                : $"No {commodity} sold yet.",
            TypeScale.Secondary,
            ThemeManager.AKey,
            wrap: true);

        last.Margin = new Thickness(0, 4, 0, 0);

        _ledger.Children.Add(last);
        _ledger.Children.Add(RoutingKit.Prose(
            "Net of what the cargo cost, from your journal. The week turns where the advanced "
            + "setting says, Thursday 07:00 UTC by default."));
    }

    /// <summary>One window's net as a stat tile, with its sales under the figure.</summary>
    private static Border LedgerTile(string label, LedgerTotal total)
    {
        var tile = StatTile.Build(label, total.Sales == 0 ? "—" : Signed(total.Net));

        ((StackPanel)tile.Child!).Children.Add(RoutingKit.Ink(
            total.Sales == 0 ? "no sales" : $"{total.Sales} sales, {total.Tonnes:N0} t",
            TypeScale.Secondary,
            ThemeManager.GreyKey));

        return tile;
    }

    private static string Signed(long net) => net switch
    {
        > 0 => $"+{net:N0} cr",
        < 0 => $"−{Math.Abs(net):N0} cr",
        _ => "level",
    };

    private static Control HeaderRow(bool distances)
    {
        // Trimmed against the page's inner width (#333) so the common case needs no scrolling at all.
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = RowSpacing,
            Margin = new Thickness(RowInset, 0),
        };

        row.Children.Add(Cell("Station", StationWidth, ThemeManager.GreyKey));
        row.Children.Add(Cell("System", SystemColumnWidth, ThemeManager.GreyKey));
        row.Children.Add(Cell("Pad", 50, ThemeManager.GreyKey));
        row.Children.Add(Cell("From star", FromStarWidth, ThemeManager.GreyKey));

        if (distances)
        {
            row.Children.Add(Cell("Distance", 80, ThemeManager.GreyKey));
        }

        row.Children.Add(Cell("Supply", 80, ThemeManager.GreyKey));
        row.Children.Add(Cell("Price", 80, ThemeManager.GreyKey));
        row.Children.Add(Cell("Updated", UpdatedWidth, ThemeManager.GreyKey));

        return row;
    }

    private Control OfferRow(CommodityOffer offer, CommodityPosting posting, string? here)
    {
        var quote = offer.Market.Quote(posting.Query.Commodity);
        var buying = posting.Query.Side == TradeSide.Buying;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = RowSpacing };

        row.Children.Add(Cell(offer.Market.Station, StationWidth, ThemeManager.WhiteKey));
        row.Children.Add(SystemCell(offer.Market.System, here));
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
            offer.IsTheirs ? ThemeManager.CyanKey : ThemeManager.GreyKey));

        return ListRow.Dress(new Border { Padding = new Thickness(RowInset, 6), Child = row });
    }

    /// <summary>
    /// The System cell plus a small copy-to-clipboard button beside it, so a Commander can grab a
    /// system name in full without it ever needing to fit uncut in the column (#328).
    /// </summary>
    private Control SystemCell(string system, string? here)
    {
        // The name takes what the copy word leaves, so a long one trims rather than running under it.
        var cells = new DockPanel { Width = SystemColumnWidth };

        if (_copy is { } copy)
        {
            var glyph = D47.App.Controls.CopyWord.For(system, copy);
            glyph.VerticalAlignment = VerticalAlignment.Center;
            glyph.Margin = new Thickness(CopyGap, 0, 0, 0);

            DockPanel.SetDock(glyph, Dock.Right);
            cells.Children.Add(glyph);
        }

        var name = Cell(system, double.NaN, RoutingKit.SystemKey(system, here));
        cells.Children.Add(name);

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

    private static Control Cell(string text, double width, string key = ThemeManager.AKey)
    {
        var block = RoutingKit.Ink(text, TypeScale.Secondary, key);

        block.Width = width;
        block.TextTrimming = TextTrimming.CharacterEllipsis;
        block.VerticalAlignment = VerticalAlignment.Center;

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
}

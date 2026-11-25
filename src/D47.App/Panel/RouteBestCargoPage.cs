using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>What the Best cargo page draws below the Sell in row.</summary>
public enum BestCargoView
{
    /// <summary>The Commander is not docked.</summary>
    NotDocked,

    /// <summary>Docked, with no search for this station yet.</summary>
    Unasked,

    /// <summary>The last search came back without the docked market's prices.</summary>
    MarketUnseen,

    /// <summary>The last search found nothing that sells at a profit.</summary>
    NoProfit,

    /// <summary>The last search found commodities to carry.</summary>
    Picks,
}

/// <summary>Astrometrics › Best cargo: what to buy at the docked station to sell in another system (#849).</summary>
public sealed class RouteBestCargoPage : UserControl
{
    public const string Tool = "best_commodities_for";

    public const string PlotTool = "plot_course";

    public const string NotDockedText =
        "Best cargo reads the market at the station you are docked at. Dock somewhere with a commodity market and this page fills in.";

    public const string MarketUnseenText =
        "Nobody has reported this market's prices, and you have not opened it while D47 was watching. Open the commodity market once and D47 will have them.";

    public const string TableId = "Route.BestCargo";

    private const double BarColumn = 150;

    /// <summary>The table's columns; the picks arrive ordered by total.</summary>
    public static readonly IReadOnlyList<SortColumn<CargoPick>> Headings =
    [
        SortColumn<CargoPick>.Figure<long>("#", pick => pick.Total, SortDirection.Descending),
        SortColumn<CargoPick>.Text("Commodity", pick => pick.Commodity),
        SortColumn<CargoPick>.Text("Sell at", pick => pick.Station),
        SortColumn<CargoPick>.Figure("Profit / t", pick => (long?)pick.ProfitPerTonne, SortDirection.Descending),
        SortColumn<CargoPick>.Figure("Tonnes", pick => (long?)pick.Tonnes, SortDirection.Descending),
        SortColumn<CargoPick>.Figure("Total", pick => (long?)pick.Total, SortDirection.Descending),
    ];

    private readonly ArrangedTable<CargoPick> _table;
    private IReadOnlyList<CargoPick> _picks = [];
    private Dictionary<CargoPick, int> _ranks = new(ReferenceEqualityComparer.Instance);
    private long _best = 1;

    private const double ColumnGap = 16;

    /// <summary>How long a search may run.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(70);

    private readonly CapabilityRegistry _registry;
    private readonly BestCargoBoard _board;
    private readonly Func<CommanderGameState?> _commander;
    private readonly Func<NavRoute> _route;
    private readonly Func<string, string?>? _offDuty;
    private readonly SettingsService? _settings;
    private readonly Action? _changeFilters;

    private readonly TextBox _sellIn;
    private readonly SystemNameSuggestions<SystemNameMatch>? _suggestions;
    private readonly Button _find = new() { Content = "Find cargo", Height = TypeScale.MinimumTarget };
    private readonly StatusLine _status = RoutingKit.Status();

    private readonly Func<string, Task<bool>>? _copy;
    private readonly D47.App.Voice.WaitSound? _waitSound;

    private CancellationTokenSource? _inFlight;
    private object? _seen;

    public RouteBestCargoPage(
        CapabilityRegistry registry,
        BestCargoBoard board,
        Func<CommanderGameState?> commander,
        Func<NavRoute> route,
        SettingsService? settings = null,
        Action? changeFilters = null,
        Func<string, CancellationToken, Task<IReadOnlyList<SystemNameMatch>>>? prefixNames = null,
        TimeSpan? suggestionPause = null,
        Func<string, string?>? offDuty = null,
        ColumnWidthMemory? columnWidths = null,
        Func<string, Task<bool>>? copy = null,
        D47.App.Voice.WaitSound? waitSound = null,
        ColumnArrangementMemory? columnArrangements = null)
    {
        _waitSound = waitSound;
        _copy = copy;
        _offDuty = offDuty;
        _table = new ArrangedTable<CargoPick>(
            TableId,
            Fields(),
            columnArrangements,
            columnArrangements?.Headset == true ? null : columnWidths,
            ColumnGap,
            sortedByDefault: "Total",
            headName: "BestCargoHead");
        _table.Changed += Draw;
        _registry = registry;
        _board = board;
        _commander = commander;
        _route = route;
        _settings = settings;
        _changeFilters = changeFilters;

        _sellIn = new TextBox
        {
            PlaceholderText = "a system",
            Height = TypeScale.MinimumTarget,
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Secondary,
            FontWeight = FontWeight.Medium,
            VerticalContentAlignment = VerticalAlignment.Center,
            InnerLeftContent = Prefix("Sell in"),
        };
        ThemeInk.Bind(_sellIn, TextBox.ForegroundProperty, ThemeManager.AKey);
        Avalonia.Automation.AutomationProperties.SetName(_sellIn, "Sell in");

        if (prefixNames is not null)
        {
            _suggestions = new SystemNameSuggestions<SystemNameMatch>(
                _sellIn,
                prefixNames,
                match => match.Name,
                () => true,
                match => _suggestions!.SetText(match.Name),
                suggestionPause);
        }

        _sellIn.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                _ = SearchAsync();
            }
        };

        _find.Click += (_, _) => _ = SearchAsync();

        if (_board.Last is { } last)
        {
            SetSellIn(last.Search.Destination);
        }

        Draw();
    }

    /// <summary>What is drawn below the Sell in row.</summary>
    public BestCargoView View { get; private set; }

    /// <summary>The system ROUTE END fills in, or null when no route is plotted and the tile is not drawn.</summary>
    public string? RouteEnd { get; private set; }

    /// <summary>The ranked rows drawn.</summary>
    public IReadOnlyList<CargoPick> Rows { get; private set; } = [];

    /// <summary>The last system of a plotted route, or null when nothing is plotted.</summary>
    private string? RouteEndNow() => _offDuty?.Invoke(OffDutyNote.Route) is null ? EndOf(_route()) : null;

    public static string? EndOf(NavRoute route) => route.IsPlotted ? route.Hops[^1].StarSystem : null;

    /// <summary>The saved trade filters the search uses, as the filters line names them.</summary>
    public static IReadOnlyList<string> Filters(TradeSettings trade) =>
    [
        trade.LargePadOnly ? "large pad only" : "any pad",
        trade.Planetary ? "surface ports included" : "no surface ports",
        trade.MaxPriceAgeHours == 0 ? "prices of any age" : $"prices under {Age(trade.MaxPriceAgeHours)}",
        $"stations within {Number((long)trade.MaxStationDistance)} ls",
    ];

    /// <summary>Redraws when the dock or the route has moved; true when it did.</summary>
    public bool Tick()
    {
        if (Equals(Stamp(), _seen))
        {
            return false;
        }

        Draw();
        return true;
    }

    /// <summary>Fills Sell in with the route's last system.</summary>
    public void UseRouteEnd()
    {
        if (RouteEnd is { } end)
        {
            SetSellIn(end);
        }
    }

    /// <summary>Asks <see cref="Tool"/> for the system in Sell in; the answer arrives through the board.</summary>
    public async Task SearchAsync()
    {
        var system = _sellIn.Text?.Trim();

        if (string.IsNullOrEmpty(system) || !_find.IsEnabled)
        {
            return;
        }

        _inFlight?.Cancel();
        var inFlight = _inFlight = new CancellationTokenSource(Budget);

        _find.IsEnabled = false;

        try
        {
            ToolResult? result = null;

            await Waiting.While(
                _status,
                "Looking up prices…",
                _waitSound,
                async () => result = await _registry
                    .InvokeAsync(
                        Tool,
                        new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { ["system"] = system }),
                        inFlight.Token,
                        ToolCaller.Commander)
                    .ConfigureAwait(true))
                .ConfigureAwait(true);

            if (result!.IsError)
            {
                RoutingKit.Say(_status, result.Content, error: true);
            }
            else
            {
                _status.Clear();
            }
        }
        catch (OperationCanceledException)
        {
            RoutingKit.Say(_status, "Stopped.");
        }
        finally
        {
            _inFlight = null;
            inFlight.Dispose();
            Draw();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _table.Reopen();
        _board.Posted += OnPosted;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _board.Posted -= OnPosted;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnPosted() => Dispatcher.UIThread.Post(() =>
    {
        if (_board.Last is { } last)
        {
            SetSellIn(last.Search.Destination);
        }

        Draw();
    });

    private object Stamp()
    {
        var location = _commander()?.Location;

        return (location?.Docked, location?.StationName, location?.StarSystem, RouteEndNow(), _board.Last);
    }

    private void Draw()
    {
        _seen = Stamp();

        var state = _commander();
        var location = state?.Location;
        var docked = location is { Docked: true, StationName: { Length: > 0 }, StarSystem: { Length: > 0 } };

        RouteEnd = RouteEndNow();

        var posting = docked && _board.Last is { } last && Same(last.Search, location!) ? last : null;

        View = !docked ? BestCargoView.NotDocked
            : posting is null ? BestCargoView.Unasked
            : posting.Answer is null ? BestCargoView.MarketUnseen
            : posting.Answer.Picks.Count == 0 ? BestCargoView.NoProfit
            : BestCargoView.Picks;

        _picks = View == BestCargoView.Picks ? posting!.Answer!.Picks : [];
        Rows = View == BestCargoView.Picks ? _table.Shown(_picks) : [];
        _ranks = new Dictionary<CargoPick, int>(ReferenceEqualityComparer.Instance);

        for (var index = 0; index < Rows.Count; index++)
        {
            _ranks[Rows[index]] = index + 1;
        }

        _best = Rows.Count > 0 ? Math.Max(1, Rows.Max(pick => pick.Total)) : 1;

        _find.IsEnabled = docked && _inFlight is null;

        Detach(_sellIn);

        if (_suggestions is not null)
        {
            Detach(_suggestions.List);
        }

        Detach(_find);
        Detach(_status);

        var body = new StackPanel();

        body.Children.Add(TitleBlock(state, docked));

        var sellRow = SellRow();
        sellRow.Margin = new Thickness(0, 24, 0, 0);
        body.Children.Add(sellRow);

        if (_suggestions is not null)
        {
            body.Children.Add(_suggestions.List);
        }

        if (docked && _settings is { } settings)
        {
            var filters = FiltersLine(settings.Current.Trade);
            filters.Margin = new Thickness(0, 10, 0, 0);
            body.Children.Add(filters);
        }

        _status.Margin = new Thickness(0, 8, 0, 0);
        body.Children.Add(_status);

        switch (View)
        {
            case BestCargoView.NotDocked:
                body.Children.Add(Notice("Not docked", NotDockedText));
                break;

            case BestCargoView.MarketUnseen:
                body.Children.Add(Notice($"No prices for {posting!.Search.Station}", MarketUnseenText));
                break;

            case BestCargoView.NoProfit:
                body.Children.Add(GroupHead(posting!.Search.Destination, plot: false));
                body.Children.Add(Slab(Prose(
                    BestCargo.Describe(posting.Search.Destination, posting.Answer!),
                    TypeScale.Secondary,
                    ThemeManager.WhiteKey)));
                break;

            case BestCargoView.Picks:
                body.Children.Add(GroupHead(posting!.Search.Destination, plot: true));
                body.Children.Add(Table(Rows, _picks));
                break;
        }

        var footer = Footer(posting);
        footer.Margin = new Thickness(0, 28, 0, 0);

        var page = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(footer, Dock.Bottom);
        page.Children.Add(footer);
        page.Children.Add(LoadoutPages.Scrolling(body));

        Content = page;
    }

    private static bool Same(BestCargoSearch search, JournalLocation location) =>
        string.Equals(search.Station, location.StationName, StringComparison.OrdinalIgnoreCase)
        && string.Equals(search.System, location.StarSystem, StringComparison.OrdinalIgnoreCase);

    private Control TitleBlock(CommanderGameState? state, bool docked)
    {
        var location = state?.Location;

        var title = TitleText.Build(docked ? location!.StationName! : "Not docked", TypeScale.Title, TitleRank.Screen);
        ThemeInk.Bind(title, TextBlock.ForegroundProperty, docked ? ThemeManager.CyanKey : ThemeManager.GreyKey);

        var heading = new StackPanel { Spacing = 2, Children = { TitleText.Context("Buying at"), title } };

        if (location?.StarSystem is { Length: > 0 } system)
        {
            heading.Children.Add(CopyGlyph.LinkBeside(Chrome(docked ? $"{system} · Docked" : system, TypeScale.Small, ThemeManager.CyanKey), system, _copy));
        }

        var hold = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Bottom,
            Children =
            {
                Right(Chrome("Free hold", TypeScale.Meta, ThemeManager.GreyKey)),
                Right(Mono(state is null ? "—" : $"{Number(BestCargo.FreeHold(state))} T", TypeScale.Figure, ThemeManager.YellowKey)),
            },
        };
        Grid.SetColumn(hold, 1);

        var row = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            ColumnSpacing = 16,
            Children = { heading, hold },
        };

        var rule = new Border { Height = 1, Margin = new Thickness(0, 10, 0, 0) };
        ThemeInk.Bind(rule, Border.BackgroundProperty, ThemeManager.AKey);

        return new StackPanel { Margin = new Thickness(0, 16, 0, 0), Children = { row, rule } };
    }

    private void SetSellIn(string? text)
    {
        if (_suggestions is not null)
        {
            _suggestions.SetText(text ?? string.Empty);
        }
        else
        {
            _sellIn.Text = text;
        }
    }

    private Grid SellRow()
    {
        var row = new Grid { ColumnSpacing = Gaps.Tile, Height = TypeScale.MinimumTarget };
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        row.Children.Add(_sellIn);

        if (RouteEnd is { } end)
        {
            var tile = new Button { Content = $"Route end · {end}", Height = TypeScale.MinimumTarget };
            tile.Click += (_, _) => UseRouteEnd();
            Grid.SetColumn(tile, row.ColumnDefinitions.Count);
            row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            row.Children.Add(tile);
        }

        Grid.SetColumn(_find, row.ColumnDefinitions.Count);
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        row.Children.Add(_find);

        return row;
    }

    private Grid FiltersLine(TradeSettings trade)
    {
        var line = new TextBlock
        {
            FontFamily = Fonts.ProseFamily,
            FontSize = TypeScale.Tip,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ThemeInk.Bind(line, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        line.Inlines!.Add(new Run("Uses your trade route filters: "));

        var filters = Filters(trade);

        for (var index = 0; index < filters.Count; index++)
        {
            var named = new Run(filters[index]);
            named.Bind(TextElement.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(ThemeManager.WhiteKey));
            line.Inlines.Add(named);

            if (index < filters.Count - 1)
            {
                var gap = new Run(" · ");
                gap.Bind(TextElement.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(ThemeManager.WhiteKey));
                line.Inlines.Add(gap);
            }
        }

        line.Inlines.Add(new Run(". Limpets are left out of the hold."));

        var grid = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            ColumnSpacing = 24,
            Children = { line },
        };

        if (_changeFilters is { } change)
        {
            var tile = new Button { Content = "Change filters", MinHeight = TypeScale.MinimumTarget, Height = TypeScale.MinimumTarget, VerticalAlignment = VerticalAlignment.Center };
            tile.Click += (_, _) => change();
            Grid.SetColumn(tile, 1);
            grid.Children.Add(tile);
        }

        return grid;
    }

    private Control GroupHead(string destination, bool plot)
    {
        var head = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        TitleText.Style(head, TypeScale.Secondary, TitleRank.Group);
        head.Inlines!.Add(new Run("WHAT TO CARRY TO "));

        if (destination.Length > 0 && _copy is not null)
        {
            var name = new TextBlock { Text = destination.ToUpperInvariant(), FontSize = TypeScale.Secondary };
            ThemeInk.Bind(name, TextBlock.ForegroundProperty, ThemeManager.AKey);

            head.Inlines.Add(SystemLink.Inline(name, destination));
        }
        else
        {
            var system = new Run(destination.ToUpperInvariant());
            system.Bind(TextElement.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(ThemeManager.AKey));
            head.Inlines.Add(system);
        }

        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, VerticalAlignment = VerticalAlignment.Center, Children = { head } };

        if (destination.Length > 0 && _copy is not null)
        {
            left.Children.Add(CopyGlyph.For(destination, _copy));
        }

        if (plot)
        {
            var description = Prose("Most credits for the whole load first.", TypeScale.Tip, ThemeManager.GreyKey);
            description.VerticalAlignment = VerticalAlignment.Center;
            left.Children.Add(description);
        }

        var row = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            ColumnSpacing = 16,
            Children = { left },
        };

        if (plot)
        {
            var tile = new Button { Content = $"Plot {destination}", MinHeight = TypeScale.MinimumTarget, Height = TypeScale.MinimumTarget };
            tile.Click += async (_, _) => await _registry
                .InvokeAsync(
                    PlotTool,
                    new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { ["system"] = destination }),
                    CancellationToken.None,
                    ToolCaller.Commander)
                .ConfigureAwait(true);
            Grid.SetColumn(tile, 1);
            row.Children.Add(tile);
        }

        var rule = new Border { Height = 1, Margin = new Thickness(0, 6, 0, 0) };
        ThemeInk.Bind(rule, Border.BackgroundProperty, ThemeManager.AKey);

        return new StackPanel { Margin = new Thickness(0, 24, 0, 6), Children = { row, rule } };
    }

    private Control Table(IReadOnlyList<CargoPick> picks, IReadOnlyList<CargoPick> all)
    {
        _table.Refresh();

        var heads = LedgerTables.Head(_table, new Thickness(14, 8, 14, 8));
        var rule = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Child = heads, Margin = new Thickness(0, 0, 0, 6) };
        ThemeInk.Bind(rule, Border.BorderBrushProperty, ThemeManager.LineKey);

        var rows = new StackPanel { Spacing = Gaps.Tile, IsVisible = !ColumnFilter.HidesRows(_table) };

        if (ColumnFilter.Empty(_table, all) is { } empty)
        {
            rows.Children.Add(empty);
        }

        foreach (var pick in picks)
        {
            rows.Children.Add(Row(pick));
        }

        var arranger = LedgerTables.Arranger(_table, all);
        arranger.Margin = new Thickness(0, 0, 0, 8);

        return new StackPanel { Children = { arranger, LedgerTables.Sideways(_table, new StackPanel { Children = { rule, rows } }) } };
    }

    private IReadOnlyList<TableField<CargoPick>> Fields() =>
    [
        StarSystemPage.Headed(
            Headings[0],
            new TableColumn("Rank", ColumnFit.Fixed) { Fixed = 22 },
            pick => Mono(_ranks.GetValueOrDefault(pick).ToString(CultureInfo.InvariantCulture), TypeScale.Meta, ThemeManager.GreyKey)),
        StarSystemPage.Headed(Headings[1], new TableColumn("Commodity", ColumnFit.Fill) { Min = 100 }, CommodityCell),
        StarSystemPage.Headed(Headings[2], new TableColumn("SellAt", ColumnFit.Fill) { Min = 100 }, StationCell)
            with { Filters = [ValueLists.One<CargoPick>("Station", pick => pick.Station, "No station")] },
        StarSystemPage.Headed(
            Headings[3], new TableColumn("Profit"), pick => Right(Mono(Number(pick.ProfitPerTonne), TypeScale.Small, ThemeManager.AKey)), right: true),
        StarSystemPage.Headed(Headings[4], new TableColumn("Tonnes"), TonnesCell, right: true)
            with { Filters = [ValueLists.One<CargoPick>("Limit", pick => Limit(pick.Limit), "No limit")] },
        StarSystemPage.Headed(
            Headings[5], new TableColumn("Total"), pick => Right(Mono(Number(pick.Total), TypeScale.Tip, ThemeManager.AKey)), right: true),
        LedgerTables.Control<CargoPick>(
            new TableColumn("Bar", ColumnFit.Fixed) { Fixed = BarColumn },
            pick => Gauge.Track((double)pick.Total / _best, ThemeManager.AKey)),
    ];

    private static Control CommodityCell(CargoPick pick)
    {
        var commodity = new TextBlock
        {
            Text = pick.Commodity,
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Secondary,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };
        ThemeInk.Bind(commodity, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        return commodity;
    }

    private static Control StationCell(CargoPick pick)
    {
        var station = new TextBlock
        {
            Text = pick.Station,
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Tip,
            FontWeight = FontWeight.Medium,
            TextWrapping = TextWrapping.Wrap,
        };
        ThemeInk.Bind(station, TextBlock.ForegroundProperty, ThemeManager.AKey);

        return station;
    }

    private static Control TonnesCell(CargoPick pick) => new StackPanel
    {
        HorizontalAlignment = HorizontalAlignment.Right,
        Children =
        {
            Right(Mono(Number(pick.Tonnes), TypeScale.Small, ThemeManager.WhiteKey)),
            Right(Chrome(Limit(pick.Limit), 10, ThemeManager.GreyKey)),
        },
    };

    private Border Row(CargoPick pick)
    {
        var row = new Border { MinHeight = TypeScale.MinimumTarget, Padding = new Thickness(14, 6), Child = _table.Row(pick) };
        ThemeInk.Bind(row, Border.BackgroundProperty, ThemeManager.SlabKey);

        return row;
    }

    /// <summary>The word under a row's tonnes.</summary>
    public static string Limit(CargoLimit limit) => limit switch
    {
        CargoLimit.Supply => "SUPPLY",
        CargoLimit.Demand => "DEMAND",
        _ => "HOLD",
    };

    private Border Footer(BestCargoPosting? posting)
    {
        var destination = posting?.Search.Destination
            ?? (string.IsNullOrWhiteSpace(_sellIn.Text) ? RouteEnd : _sellIn.Text.Trim());

        var phrase = destination is null
            ? "Say: “what should I buy here to sell in” and name the system"
            : $"Say: “what should I buy here to sell in {destination}”";

        var said = Prose(phrase, TypeScale.Tip, ThemeManager.GreyKey);
        said.TextTrimming = TextTrimming.CharacterEllipsis;
        said.TextWrapping = TextWrapping.NoWrap;
        said.VerticalAlignment = VerticalAlignment.Center;

        Control sentence = said;

        if (destination is not null && _copy is not null)
        {
            var glyph = CopyGlyph.For(destination, _copy);
            glyph.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(glyph, Dock.Right);
            sentence = new DockPanel { Children = { glyph, said } };
        }

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { sentence } };

        if (posting is not null)
        {
            var stamp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(16, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    Chrome("Prices looked up · ", TypeScale.Meta, ThemeManager.GreyKey),
                    Mono(posting.AskedAt.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture), TypeScale.Meta, ThemeManager.CyanKey),
                },
            };
            Grid.SetColumn(stamp, 1);
            grid.Children.Add(stamp);
        }

        var border = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 12, 0, 0), Child = grid };
        ThemeInk.Bind(border, Border.BorderBrushProperty, ThemeManager.Line2Key);

        return border;
    }

    private static Notice Notice(string label, string text) => new(NoticeLevel.Warning)
    {
        Label = label.ToUpperInvariant(),
        Text = text,
        Margin = new Thickness(0, 24, 0, 0),
    };

    private static Border Slab(Control child)
    {
        var slab = new Border { Padding = new Thickness(14, 14), Child = child };
        ThemeInk.Bind(slab, Border.BackgroundProperty, ThemeManager.SlabKey);
        return slab;
    }

    private static TextBlock Prefix(string text)
    {
        var block = Chrome(text, TypeScale.Meta, ThemeManager.GreyKey);
        block.VerticalAlignment = VerticalAlignment.Center;
        block.Margin = new Thickness(12, 0, 0, 0);
        return block;
    }

    private static TextBlock Right(TextBlock block)
    {
        block.HorizontalAlignment = HorizontalAlignment.Right;
        block.TextAlignment = TextAlignment.Right;
        return block;
    }

    private static string Age(int hours) =>
        hours >= 48 && hours % 24 == 0 ? $"{Number(hours / 24)} days" : $"{Number(hours)} h";

    private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static void Detach(Control control)
    {
        if (control.Parent is Avalonia.Controls.Panel parent)
        {
            parent.Children.Remove(control);
        }
    }

    private static TextBlock Chrome(string text, double size, string key) =>
        Typeset.Chrome(
            text.ToUpperInvariant(), size, key,
            weight: FontWeight.SemiBold, tracking: size * Fonts.ChromeTracking, wrap: true);

    private static TextBlock Mono(string text, double size, string key) =>
        Typeset.Mono(text, size, key);

    private static TextBlock Prose(string text, double size, string key) =>
        Typeset.Prose(text, size, key, wrap: true);

}

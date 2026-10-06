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

    /// <summary>“Look things up in the galaxy” is off.</summary>
    LookupsOff,

    /// <summary>Docked, with no search for this station yet.</summary>
    Unasked,

    /// <summary>The last search came back without the docked market's prices.</summary>
    MarketUnseen,

    /// <summary>The last search found nothing that sells at a profit.</summary>
    NoProfit,

    /// <summary>The last search found commodities to carry.</summary>
    Picks,
}

/// <summary>Navigation › Best cargo: what to buy at the docked station to sell in another system (#849).</summary>
public sealed class RouteBestCargoPage : UserControl
{
    public const string Tool = "best_commodities_for";

    public const string PlotTool = "plot_course";

    public const string NotDockedText =
        "Best cargo reads the market at the station you are docked at. Dock somewhere with a commodity market and this page fills in.";

    public const string LookupsOffText =
        "Best cargo looks up prices in other systems. Turn on “Look things up in the galaxy” in Settings and D47 will search.";

    public const string MarketUnseenText =
        "Nobody has reported this market's prices, and you have not opened it while D47 was watching. Open the commodity market once and D47 will have them.";

    /// <summary>The results table's columns: rank, commodity, sell at, profit, tonnes, total, bar.</summary>
    private const string Columns = "22,1.3*,1.1*,100,96,130,150";

    private const double ColumnGap = 16;

    /// <summary>How long a search may run.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(70);

    private readonly CapabilityRegistry _registry;
    private readonly BestCargoBoard _board;
    private readonly Func<CommanderGameState?> _commander;
    private readonly Func<NavRoute> _route;
    private readonly Func<bool> _lookupsEnabled;
    private readonly SettingsService? _settings;
    private readonly Action? _openSettings;
    private readonly Action? _changeFilters;

    private readonly TextBox _sellIn;
    private readonly Button _find = new() { Content = "Find cargo", Height = TypeScale.MinimumTarget };
    private readonly StatusLine _status = RoutingKit.Status();

    private CancellationTokenSource? _inFlight;
    private object? _seen;

    public RouteBestCargoPage(
        CapabilityRegistry registry,
        BestCargoBoard board,
        Func<CommanderGameState?> commander,
        Func<NavRoute> route,
        Func<bool> lookupsEnabled,
        SettingsService? settings = null,
        Action? openSettings = null,
        Action? changeFilters = null)
    {
        _registry = registry;
        _board = board;
        _commander = commander;
        _route = route;
        _lookupsEnabled = lookupsEnabled;
        _settings = settings;
        _openSettings = openSettings;
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
        Themed(_sellIn, TextBox.ForegroundProperty, ThemeManager.AKey);
        Avalonia.Automation.AutomationProperties.SetName(_sellIn, "Sell in");

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
            _sellIn.Text = last.Search.Destination;
        }

        Draw();
    }

    /// <summary>What is drawn below the Sell in row.</summary>
    public BestCargoView View { get; private set; }

    /// <summary>The system ROUTE END fills in, or null when no route is plotted and the tile is not drawn.</summary>
    public string? RouteEnd { get; private set; }

    /// <summary>The ranked rows drawn.</summary>
    public IReadOnlyList<CargoPick> Rows { get; private set; } = [];

    /// <summary>Whether FIND CARGO can be pressed.</summary>
    public bool CanFind => _find.IsEnabled;

    /// <summary>The Sell in field's text.</summary>
    public string SellIn
    {
        get => _sellIn.Text ?? string.Empty;
        set => _sellIn.Text = value;
    }

    /// <summary>The last system of a plotted route, or null when nothing is plotted.</summary>
    public static string? EndOf(NavRoute route) => route.IsPlotted ? route.Hops[^1].StarSystem : null;

    /// <summary>The saved trade filters the search uses, as the filters line names them.</summary>
    public static IReadOnlyList<string> Filters(TradeSettings trade) =>
    [
        trade.LargePadOnly ? "large pad only" : "any pad",
        trade.Planetary ? "surface ports included" : "no surface ports",
        $"prices under {Age(trade.MaxPriceAgeHours)}",
        $"stations within {Number((long)trade.MaxStationDistance)} ls",
    ];

    /// <summary>Redraws when the dock, the route or the setting has moved; true when it did.</summary>
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
            _sellIn.Text = end;
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
        RoutingKit.Say(_status, "Looking up prices…");

        try
        {
            var result = await _registry
                .InvokeAsync(
                    Tool,
                    new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { ["system"] = system }),
                    inFlight.Token,
                    ToolCaller.Commander)
                .ConfigureAwait(true);

            if (result.IsError)
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
            _sellIn.Text = last.Search.Destination;
        }

        Draw();
    });

    private object Stamp()
    {
        var location = _commander()?.Location;

        return (location?.Docked, location?.StationName, location?.StarSystem, EndOf(_route()), _lookupsEnabled(), _board.Last);
    }

    private void Draw()
    {
        _seen = Stamp();

        var state = _commander();
        var location = state?.Location;
        var docked = location is { Docked: true, StationName: { Length: > 0 }, StarSystem: { Length: > 0 } };
        var lookups = _lookupsEnabled();

        RouteEnd = EndOf(_route());

        var posting = docked && _board.Last is { } last && Same(last.Search, location!) ? last : null;

        View = !docked ? BestCargoView.NotDocked
            : !lookups ? BestCargoView.LookupsOff
            : posting is null ? BestCargoView.Unasked
            : posting.Answer is null ? BestCargoView.MarketUnseen
            : posting.Answer.Picks.Count == 0 ? BestCargoView.NoProfit
            : BestCargoView.Picks;

        Rows = View == BestCargoView.Picks ? posting!.Answer!.Picks : [];

        _find.IsEnabled = docked && lookups && _inFlight is null;

        Detach(_sellIn);
        Detach(_find);
        Detach(_status);

        var body = new StackPanel();

        body.Children.Add(TitleBlock(state, docked));

        var sellRow = SellRow();
        sellRow.Margin = new Thickness(0, 24, 0, 0);
        body.Children.Add(sellRow);

        if (docked && lookups && _settings is { } settings)
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

            case BestCargoView.LookupsOff:
                var off = Notice("Galaxy lookups are off", LookupsOffText);

                if (_openSettings is { } open)
                {
                    var settingsTile = new Button { Content = "Open settings" };
                    settingsTile.Click += (_, _) => open();
                    off.Actions.Add(settingsTile);
                }

                body.Children.Add(off);
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
                body.Children.Add(Table(posting.Answer!.Picks));
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

    private static Control TitleBlock(CommanderGameState? state, bool docked)
    {
        var location = state?.Location;

        var title = TitleText.Build(docked ? location!.StationName! : "Not docked", TypeScale.Title, TitleRank.Screen);
        Themed(title, TextBlock.ForegroundProperty, docked ? ThemeManager.CyanKey : ThemeManager.GreyKey);

        var heading = new StackPanel { Spacing = 2, Children = { TitleText.Context("Buying at"), title } };

        if (location?.StarSystem is { Length: > 0 } system)
        {
            heading.Children.Add(Chrome(docked ? $"{system} · Docked" : system, TypeScale.Small, ThemeManager.CyanKey));
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
        Themed(rule, Border.BackgroundProperty, ThemeManager.AKey);

        return new StackPanel { Margin = new Thickness(0, 16, 0, 0), Children = { row, rule } };
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
        Themed(line, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

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
            var tile = new Button { Content = "Change filters", MinHeight = 36, Height = 36, VerticalAlignment = VerticalAlignment.Center };
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

        var system = new Run(destination.ToUpperInvariant());
        system.Bind(TextElement.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(ThemeManager.AKey));
        head.Inlines.Add(system);

        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, VerticalAlignment = VerticalAlignment.Center, Children = { head } };

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
            var tile = new Button { Content = $"Plot {destination}", MinHeight = 32, Height = 32 };
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
        Themed(rule, Border.BackgroundProperty, ThemeManager.AKey);

        return new StackPanel { Margin = new Thickness(0, 24, 0, 6), Children = { row, rule } };
    }

    private static StackPanel Table(IReadOnlyList<CargoPick> picks)
    {
        var heads = new Grid { ColumnDefinitions = new ColumnDefinitions(Columns), ColumnSpacing = ColumnGap, Margin = new Thickness(14, 8, 14, 8) };

        foreach (var (text, column, right, key) in new[]
                 {
                     ("#", 0, false, ThemeManager.GreyKey),
                     ("Commodity", 1, false, ThemeManager.GreyKey),
                     ("Sell at", 2, false, ThemeManager.GreyKey),
                     ("Profit / t", 3, true, ThemeManager.GreyKey),
                     ("Tonnes", 4, true, ThemeManager.GreyKey),
                     ("Total ▼", 5, true, ThemeManager.AKey),
                 })
        {
            var head = Chrome(text, TypeScale.Meta, key);
            head.HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            Grid.SetColumn(head, column);
            heads.Children.Add(head);
        }

        var rule = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Child = heads, Margin = new Thickness(0, 0, 0, 6) };
        Themed(rule, Border.BorderBrushProperty, ThemeManager.LineKey);

        var rows = new StackPanel { Spacing = Gaps.Tile };
        var best = picks.Count > 0 ? Math.Max(1, picks[0].Total) : 1;

        for (var index = 0; index < picks.Count; index++)
        {
            rows.Children.Add(Row(index + 1, picks[index], best));
        }

        return new StackPanel { Children = { rule, rows } };
    }

    private static Border Row(int rank, CargoPick pick, long best)
    {
        var number = Cell(Mono(rank.ToString(CultureInfo.InvariantCulture), TypeScale.Meta, ThemeManager.GreyKey), 0);

        var commodity = new TextBlock
        {
            Text = pick.Commodity,
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Secondary,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Themed(commodity, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var station = new TextBlock
        {
            Text = pick.Station,
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Tip,
            FontWeight = FontWeight.Medium,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Themed(station, TextBlock.ForegroundProperty, ThemeManager.AKey);

        var profit = Right(Mono(Number(pick.ProfitPerTonne), TypeScale.Small, ThemeManager.AKey));

        var tonnes = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            Children =
            {
                Right(Mono(Number(pick.Tonnes), TypeScale.Small, ThemeManager.WhiteKey)),
                Right(Chrome(Limit(pick.Limit), 10, ThemeManager.GreyKey)),
            },
        };

        var total = Right(Mono(Number(pick.Total), TypeScale.Tip, ThemeManager.AKey));

        var bar = Gauge.Track((double)pick.Total / best, ThemeManager.AKey);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(Columns),
            ColumnSpacing = ColumnGap,
            Children =
            {
                number,
                Cell(commodity, 1),
                Cell(station, 2),
                Cell(profit, 3),
                Cell(tonnes, 4),
                Cell(total, 5),
                Cell(bar, 6),
            },
        };

        var row = new Border { MinHeight = TypeScale.MinimumTarget, Padding = new Thickness(14, 6), Child = grid };
        Themed(row, Border.BackgroundProperty, ThemeManager.SlabKey);

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

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { said } };

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
        Themed(border, Border.BorderBrushProperty, ThemeManager.Line2Key);

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
        Themed(slab, Border.BackgroundProperty, ThemeManager.SlabKey);
        return slab;
    }

    private static TextBlock Prefix(string text)
    {
        var block = Chrome(text, TypeScale.Meta, ThemeManager.GreyKey);
        block.VerticalAlignment = VerticalAlignment.Center;
        block.Margin = new Thickness(12, 0, 0, 0);
        return block;
    }

    private static T Cell<T>(T control, int column)
        where T : Control
    {
        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, column);
        return control;
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

    private static TextBlock Chrome(string text, double size, string key)
    {
        var block = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontFamily = Fonts.ChromeFamily,
            FontSize = size,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = size * Fonts.ChromeTracking,
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    private static TextBlock Mono(string text, double size, string key)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = size,
        };
        Themed(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    private static TextBlock Prose(string text, double size, string key)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = Fonts.ProseFamily,
            FontSize = size,
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    private static void Themed(StyledElement target, AvaloniaProperty property, string key) =>
        target.Bind(property, Application.Current!.Resources.GetResourceObservable(key));
}

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using D47.Core.Interface;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>
/// One station from a system's galaxy record, drilled from Search › System › Stations, from a body's page or from a
/// Stations result, and rebuilt from its crumb key alone.
/// </summary>
public sealed class SystemStationPage : UserControl, IPageSummary, IPageSection
{
    public const string KeyPrefix = "search.system.station:";

    public const string SummaryText = "One station in a system, as last reported.";

    public const string ReadingText = "Reading the galaxy record.";

    public const string UnavailableLabel = "Galaxy record unavailable";

    public const string UnavailableText = "The galaxy record could not be read.";

    public const string MissingLabel = "Station not in the record";

    public const string MissingText = "The galaxy record no longer lists this station.";

    public const string NoneReported = "None reported";

    public const string PricesText = "Prices are reported by other Commanders; supply moves fastest of all.";

    public const string ModulesText = "Modules are reported by other Commanders and change between visits.";

    public const string MarketTable = "Station.Market";

    /// <summary>Marks one of the three REPORTED TRADE blocks on the overview.</summary>
    public const string TradeClass = "station-trade";

    private const string Dash = "—";

    private static readonly TableColumn[] MarketColumns =
    [
        new("Commodity", ColumnFit.Fill) { Min = 140 },
        new("Category", ColumnFit.Fill) { Min = 100 },
        new("Buy"),
        new("Sell"),
        new("Supply"),
        new("Demand"),
    ];

    /// <summary>The market table's columns; every one but Category sorts.</summary>
    public static readonly IReadOnlyList<SortColumn<MarketCommodity>> MarketHeads =
    [
        SortColumn<MarketCommodity>.Text("Commodity", commodity => commodity.Name),
        SortColumn<MarketCommodity>.Plain("Category"),
        SortColumn<MarketCommodity>.Figure("Buy", commodity => (long?)commodity.BuyPrice, SortDirection.Ascending),
        SortColumn<MarketCommodity>.Figure("Sell", commodity => (long?)commodity.SellPrice, SortDirection.Descending),
        SortColumn<MarketCommodity>.Figure("Supply", commodity => (long?)commodity.Supply, SortDirection.Descending),
        SortColumn<MarketCommodity>.Figure("Demand", commodity => (long?)commodity.Demand, SortDirection.Descending),
    ];

    private readonly StarSystemSurface _surface;
    private readonly long _address;
    private readonly long _marketId;
    private readonly string _word;
    private readonly Action? _back;
    private readonly SortHeader<MarketCommodity> _marketHead = new("MarketHead", MarketHeads, new SortOrder(0, SortDirection.Ascending));
    private readonly StackPanel _page = new() { Spacing = 18 };

    private StarSystemProfile? _profile;
    private string? _failure;
    private bool _loading;
    private bool _redrawn;
    private CancellationTokenSource? _inFlight;
    private Section _section;

    /// <param name="profile">The system's record when the page it was pressed on had read it; null fetches it.</param>
    /// <param name="back">Goes back to the crumb above.</param>
    public SystemStationPage(
        StarSystemSurface surface,
        long address,
        long marketId,
        string word,
        StarSystemProfile? profile = null,
        Action? back = null)
    {
        _surface = surface;
        _address = address;
        _marketId = marketId;
        _word = word;
        _back = back;

        _marketHead.Changed += () =>
        {
            if (_section == Section.Market)
            {
                Draw();
            }
        };

        Content = new ScrollViewer
        {
            Padding = new Thickness(14, 18, 14, 24),
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = _page,
        };

        if (profile is not null && profile.SystemAddress == address)
        {
            _profile = profile;
            Draw();
        }
        else
        {
            Fetch();
        }
    }

    /// <summary>The sections a station page shows, in the order the switch lists them.</summary>
    public enum Section
    {
        Overview,
        Market,
        Outfitting,
        Shipyard,
    }

    public string Summary => SummaryText;

    private readonly PageSectionWord _sectionWord = new();

    public string OpenSection => _sectionWord.Word;

    event EventHandler? IPageSection.OpenSectionChanged
    {
        add => _sectionWord.Changed += value;
        remove => _sectionWord.Changed -= value;
    }

    event EventHandler? IPageSummary.SummaryChanged
    {
        add { }
        remove { }
    }

    /// <summary>The station drawn, or null while the record is read or does not list it.</summary>
    public StationProfile? Station => _profile?.Stations.FirstOrDefault(station => station.MarketId == _marketId);

    public static string Key(long address, long marketId) =>
        KeyPrefix + address.ToString(CultureInfo.InvariantCulture) + ":" + marketId.ToString(CultureInfo.InvariantCulture);

    /// <summary>The crumb for a station; null where the station has no market id to key it by.</summary>
    public static NavCrumb? Crumb(long address, StationProfile station) =>
        station.MarketId is { } marketId ? Crumb(address, marketId, station.Name) : null;

    public static NavCrumb Crumb(long address, long marketId, string name) =>
        new(Key(address, marketId), name) { Help = GalaxyCapability.Id };

    public static bool IsKey(string key) => key.StartsWith(KeyPrefix, StringComparison.Ordinal);

    /// <summary>The system address and market id in a station key; null for any other key.</summary>
    public static (long Address, long MarketId)? Parse(string key)
    {
        if (!IsKey(key))
        {
            return null;
        }

        var parts = key[KeyPrefix.Length..].Split(':');

        return parts.Length == 2
            && long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var address)
            && long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var marketId)
                ? (address, marketId)
                : null;
    }

    /// <summary>True when the page redrew since the last call, an answer arriving included. Call on the UI thread.</summary>
    public bool Tick()
    {
        var redrawn = _redrawn;
        _redrawn = false;

        return redrawn;
    }

    /// <summary>Shows one section.</summary>
    public void Show(Section section)
    {
        _section = section;
        Draw();
    }

    /// <summary>How many entries the section holds; 0 for the overview and for a list the record lacks.</summary>
    public static int CountOf(StationProfile station, Section section) => section switch
    {
        Section.Market => station.Market?.Commodities.Count ?? 0,
        Section.Outfitting => station.Outfitting?.Modules.Count ?? 0,
        Section.Shipyard => station.Shipyard?.Ships.Count ?? 0,
        _ => 0,
    };

    private void Fetch()
    {
        _inFlight?.Cancel();
        _inFlight?.Dispose();

        var request = _inFlight = new CancellationTokenSource();

        _profile = null;
        _failure = null;
        _loading = true;
        Draw();

        _ = Read(request);
    }

    private async Task Read(CancellationTokenSource request)
    {
        var token = request.Token;
        StarSystemProfile? profile = null;
        string? failure = null;

        try
        {
            profile = await Task.Run(() => _surface.Systems.ProfileAsync(_address, token), token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (GalaxyUnavailableException ex)
        {
            failure = ex.Message;
        }
        catch (Exception)
        {
            failure = StarSystemPage.Unreadable;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(request, _inFlight))
            {
                return;
            }

            _inFlight = null;
            request.Dispose();

            _loading = false;
            _profile = profile;
            _failure = failure;
            Draw();
        });
    }

    private void Draw()
    {
        DrawPage();
        _sectionWord.Set(this, !_loading && _failure is null && Station is not null ? _section.ToString() : string.Empty);
    }

    private void DrawPage()
    {
        _redrawn = true;
        _page.Children.Clear();

        if (_loading)
        {
            _page.Children.Add(Heading(null));
            _page.Children.Add(Sentence(ReadingText));
            return;
        }

        if (_failure is { } failure)
        {
            _page.Children.Add(Heading(null));
            _page.Children.Add(Unavailable(failure));
            return;
        }

        if (Station is not { } station)
        {
            _page.Children.Add(Heading(null));
            _page.Children.Add(Missing());
            return;
        }

        _page.Children.Add(Heading(station));
        _page.Children.Add(Switch(station));
        _page.Children.Add(_section switch
        {
            Section.Market => MarketSection(station),
            Section.Outfitting => OutfittingSection(station),
            Section.Shipyard => ShipyardSection(station),
            _ => Overview(station),
        });
    }

    private Control Heading(StationProfile? station)
    {
        var context = TitleText.Context(
            station is null ? "Station" : $"{StarSystemPage.StationType(station)} · {_profile?.Name}");

        var title = TitleText.Style(
            new SelectableTextBlock { TextWrapping = TextWrapping.Wrap },
            TypeScale.Heading,
            TitleRank.Screen);
        TitleText.Show(title, station?.Name ?? _word);
        ThemeInk.Bind(title, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        return TitleText.Block(title, context, station?.UpdatedAt is { } at ? StarSystemPage.Reported(at) : null);
    }

    private Grid Switch(StationProfile station)
    {
        var segment = new Segment
        {
            ItemsSource = new[]
            {
                "Overview",
                $"Market {Count(station, Section.Market)}",
                $"Outfitting {Count(station, Section.Outfitting)}",
                $"Shipyard {Count(station, Section.Shipyard)}",
            },
            SelectedIndex = (int)_section,
        };
        Avalonia.Automation.AutomationProperties.SetName(segment, "Sections");
        segment.SelectionChanged += (_, _) =>
        {
            if (segment.SelectedIndex >= 0 && (Section)segment.SelectedIndex != _section)
            {
                _section = (Section)segment.SelectedIndex;
                Draw();
            }
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*") };
        Grid.SetColumnSpan(segment, 4);
        grid.Children.Add(segment);

        return grid;
    }

    private static string Count(StationProfile station, Section section) =>
        CountOf(station, section).ToString(CultureInfo.InvariantCulture);

    private StackPanel Overview(StationProfile station)
    {
        var column = new StackPanel { Spacing = 24 };

        List<Control> tiles =
        [
            StatTile.Build("Orbits", station.Body ?? Dash, StatInk.Name),
            StatTile.Build("Distance", StarSystemPage.ArrivalText(station.DistanceToArrival), StatInk.Number),
        ];

        if (station.CarrierDockingAccess is { } access)
        {
            tiles.Add(StatTile.Build("Carrier docking", access));
        }

        tiles.Add(FactionTile(station));
        tiles.Add(StatTile.Build("Allegiance", station.Allegiance ?? Dash));
        tiles.Add(StatTile.Build("Government", station.Government ?? Dash));
        tiles.Add(StatTile.Tile("Economies", Figures(EconomyFigures(station))));
        tiles.Add(StatTile.Tile("Landing pads", Figures(PadFigures(station))));
        tiles.Add(StatTile.Build("Largest pad", StarSystemPage.PadText(station.LargestPad), StatInk.Figure));

        column.Children.Add(StatTile.Grid(tiles));

        if (station.Services.Count > 0)
        {
            var services = Typeset.Prose(
                string.Join(" · ", station.Services.Select(ServiceLabel)), TypeScale.ControlLarge, ThemeManager.WhiteKey, wrap: true);
            services.Name = "Services";

            column.Children.Add(Group("Services", Count(station.Services.Count), services));
        }

        column.Children.Add(Group("Reported trade", null, StatTile.Grid(
        [
            Trade(station, Section.Market, "Market", ("commodity", "commodities"), station.Market?.ReportedAt),
            Trade(station, Section.Outfitting, "Outfitting", ("module", "modules"), station.Outfitting?.ReportedAt),
            Trade(station, Section.Shipyard, "Shipyard", ("ship", "ships"), station.Shipyard?.ReportedAt),
        ], maxColumns: 3)));

        return column;
    }

    /// <summary>The controlling faction in White with its state at the right.</summary>
    private static Border FactionTile(StationProfile station)
    {
        var row = new DockPanel();

        if (station.ControllingFactionState is { } state)
        {
            var shown = Typeset.Chrome(state, TypeScale.Control, ThemeManager.AKey, vertical: VerticalAlignment.Center);
            shown.Margin = new Thickness(12, 0, 0, 0);
            DockPanel.SetDock(shown, Dock.Right);
            row.Children.Add(shown);
        }

        row.Children.Add(Typeset.Chrome(
            StarSystemPage.Faction(station), StatTile.ValueSize, ThemeManager.WhiteKey, weight: FontWeight.Medium, trimming: TextTrimming.CharacterEllipsis));

        return StatTile.Tile("Controlling faction", row);
    }

    /// <summary>The economies with their shares; the primary economy alone where the record gives no shares.</summary>
    public static IReadOnlyList<(string Name, string? Figure)> EconomyFigures(StationProfile station) =>
        station.Economies.Count == 0
            ? [(station.PrimaryEconomy ?? Dash, null)]
            : [.. station.Economies.Select(economy => (economy.Name, (string?)(economy.Share.ToString("0.#", CultureInfo.InvariantCulture) + "%")))];

    /// <summary>Large, Medium and Small with their counts, or a dash.</summary>
    public static IReadOnlyList<(string Name, string? Figure)> PadFigures(StationProfile station) =>
        station.Pads is { } pads
            ? [("Large", pads.Large.ToString(CultureInfo.InvariantCulture)), ("Medium", pads.Medium.ToString(CultureInfo.InvariantCulture)), ("Small", pads.Small.ToString(CultureInfo.InvariantCulture))]
            : [(Dash, null)];

    /// <summary>Each name in A with its figure in mono White beside it.</summary>
    private static WrapPanel Figures(IReadOnlyList<(string Name, string? Figure)> parts)
    {
        var line = new WrapPanel { ItemSpacing = 16, LineSpacing = 2 };

        foreach (var (name, figure) in parts)
        {
            var pair = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            pair.Children.Add(Typeset.Chrome(name, StatTile.ValueSize, ThemeManager.AKey, weight: FontWeight.Medium));

            if (figure is not null)
            {
                pair.Children.Add(Typeset.Mono(figure, TypeScale.Control, ThemeManager.WhiteKey, vertical: VerticalAlignment.Center));
            }

            line.Children.Add(pair);
        }

        return line;
    }

    private static string ServiceLabel(string spansh) =>
        StationService.All.FirstOrDefault(service => service.Spansh == spansh)?.Label ?? spansh;

    /// <summary>A pressable tile that selects the section, or a read-only block where the record holds nothing.</summary>
    private Border Trade(StationProfile station, Section section, string label, (string One, string Many) nouns, DateTimeOffset? reportedAt)
    {
        var count = CountOf(station, section);
        var noun = count == 1 ? nouns.One : nouns.Many;

        var text = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(StatTile.Caption(label, ThemeManager.GreyKey));

        if (count == 0)
        {
            text.Children.Add(Typeset.Chrome(NoneReported, TypeScale.Control, ThemeManager.GreyKey));
        }
        else
        {
            text.Children.Add(Typeset.Chrome(
                $"{count.ToString(CultureInfo.InvariantCulture)} {noun}", StatTile.ValueSize, ThemeManager.WhiteKey, weight: FontWeight.Medium));
            text.Children.Add(Typeset.Mono(TimeText(reportedAt), TypeScale.Meta, ThemeManager.GreyKey));
        }

        var line = new DockPanel();

        if (count > 0)
        {
            var more = Typeset.Chrome("›", TypeScale.ControlLarge, ThemeManager.AKey, vertical: VerticalAlignment.Center);
            more.Margin = new Thickness(12, 0, 0, 0);
            DockPanel.SetDock(more, Dock.Right);
            line.Children.Add(more);
        }

        line.Children.Add(text);

        var block = new Border
        {
            Name = "Trade" + section,
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 10),
            Child = line,
        };
        block.Classes.Add(TradeClass);
        Avalonia.Automation.AutomationProperties.SetName(
            block, count == 0 ? $"{label}, {NoneReported}" : $"{label}, {count.ToString(CultureInfo.InvariantCulture)} {noun}");

        if (count > 0)
        {
            ListRow.Dress(block);
            ListRow.Opens(block, () => Show(section));
        }
        else
        {
            ThemeInk.Bind(block, Border.BackgroundProperty, ThemeManager.SlabKey);
        }

        return block;
    }

    /// <summary>"2026-10-10 19:42 UTC", or "unknown time".</summary>
    public static string TimeText(DateTimeOffset? at) =>
        at is { } when ? when.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture) : "unknown time";

    /// <summary>"The galaxy record has no shipyard for Brin Hub. Either the station has none, or no Commander has reported one."</summary>
    public static string NoneText(string what, string station) =>
        $"The galaxy record has no {what} for {station}. Either the station has none, or no Commander has reported one.";

    private StackPanel MarketSection(StationProfile station)
    {
        var column = new StackPanel { Spacing = 16 };
        var market = station.Market;

        if (market is null || market.Commodities.Count == 0)
        {
            column.Children.Add(Sentence(NoneText("market", station.Name)));
            return column;
        }

        column.Children.Add(Intro($"{station.Name}'s market", market.ReportedAt, PricesText, "MarketReported"));

        var layout = new TableLayout(MarketTable, MarketColumns, _surface.ColumnWidths, 16);
        var header = layout.Header(
            _marketHead,
            [HorizontalAlignment.Left, HorizontalAlignment.Left, HorizontalAlignment.Right, HorizontalAlignment.Right, HorizontalAlignment.Right, HorizontalAlignment.Right]);
        header.MinHeight = TypeScale.MinimumTarget;
        header.Margin = new Thickness(12, 0);

        var rule = new Border { Height = 1 };
        ThemeInk.Bind(rule, Border.BackgroundProperty, ThemeManager.LineKey);

        var rows = new StackPanel { Spacing = Gaps.Tile };
        rows.Children.Add(header);
        rows.Children.Add(rule);

        foreach (var commodity in _marketHead.Sort(market.Commodities))
        {
            rows.Children.Add(CommodityRow(commodity, layout));
        }

        column.Children.Add(rows);

        return column;
    }

    private static Border CommodityRow(MarketCommodity commodity, TableLayout layout)
    {
        List<Control> cells =
        [
            Cell(Typeset.Chrome(commodity.Name, TypeScale.Control, ThemeManager.WhiteKey, weight: FontWeight.SemiBold, trimming: TextTrimming.CharacterEllipsis)),
            Cell(Typeset.Chrome(commodity.Category ?? Dash, TypeScale.Secondary, ThemeManager.AKey, weight: FontWeight.Medium, trimming: TextTrimming.CharacterEllipsis)),
            Figure(Amount(commodity.BuyPrice), ThemeManager.AKey),
            Figure(Amount(commodity.SellPrice), ThemeManager.AKey),
            Figure(Amount(commodity.Supply), ThemeManager.WhiteKey),
            Figure(Amount(commodity.Demand), ThemeManager.WhiteKey),
        ];

        var row = new Border
        {
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(12, 0),
            Child = layout.Row(cells),
        };
        ListRow.Dress(row);
        Avalonia.Automation.AutomationProperties.SetName(row, commodity.Name);

        return row;
    }

    /// <summary>The figure with thousands separators, or a dash for none.</summary>
    private static string Amount(long figure) => figure > 0 ? figure.ToString("N0", CultureInfo.InvariantCulture) : Dash;

    private static Control Cell(Control content)
    {
        content.VerticalAlignment = VerticalAlignment.Center;

        return content;
    }

    private static TextBlock Figure(string text, string key)
    {
        var block = Typeset.Mono(text, TypeScale.Secondary, key, vertical: VerticalAlignment.Center, align: TextAlignment.Right);
        block.HorizontalAlignment = HorizontalAlignment.Right;

        return block;
    }

    private StackPanel OutfittingSection(StationProfile station)
    {
        var column = new StackPanel { Spacing = 16 };
        var outfitting = station.Outfitting;

        if (outfitting is null || outfitting.Modules.Count == 0)
        {
            column.Children.Add(Sentence(NoneText("outfitting", station.Name)));
            return column;
        }

        column.Children.Add(Intro($"{station.Name}'s outfitting", outfitting.ReportedAt, ModulesText, "OutfittingReported"));

        foreach (var category in outfitting.Modules
            .GroupBy(module => module.Category ?? "Other")
            .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            var group = new StackPanel { Spacing = Gaps.Tile };
            group.Children.Add(StarSystemPage.GroupHead(category.Key, string.Empty, Count(category.Count())));

            foreach (var module in category.OrderBy(module => module.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                group.Children.Add(ModuleRow(module));
            }

            column.Children.Add(group);
        }

        return column;
    }

    /// <summary>"5A": the class and rating the record gives; a dash for neither.</summary>
    public static string ClassText(StationModule module) =>
        module.Class is null && string.IsNullOrEmpty(module.Rating)
            ? Dash
            : (module.Class?.ToString(CultureInfo.InvariantCulture) ?? string.Empty) + module.Rating;

    private static Border ModuleRow(StationModule module)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,64,0.8*"), ColumnSpacing = 16 };

        var name = Cell(Typeset.Chrome(module.Name, TypeScale.Control, ThemeManager.WhiteKey, weight: FontWeight.SemiBold, trimming: TextTrimming.CharacterEllipsis));
        var size = Figure(ClassText(module), ThemeManager.AKey);
        var ship = Cell(Typeset.Chrome(module.Ship ?? string.Empty, TypeScale.Secondary, ThemeManager.AKey, weight: FontWeight.Medium, trimming: TextTrimming.CharacterEllipsis));
        ship.HorizontalAlignment = HorizontalAlignment.Right;

        Grid.SetColumn(size, 1);
        Grid.SetColumn(ship, 2);
        grid.Children.Add(name);
        grid.Children.Add(size);
        grid.Children.Add(ship);

        var row = new Border { MinHeight = TypeScale.MinimumTarget, Padding = new Thickness(12, 0), Child = grid };
        ListRow.Dress(row);
        Avalonia.Automation.AutomationProperties.SetName(row, module.Name);

        return row;
    }

    private StackPanel ShipyardSection(StationProfile station)
    {
        var column = new StackPanel { Spacing = 16 };
        var shipyard = station.Shipyard;

        if (shipyard is null || shipyard.Ships.Count == 0)
        {
            column.Children.Add(Sentence(NoneText("shipyard", station.Name)));
            return column;
        }

        column.Children.Add(Intro($"{station.Name}'s shipyard", shipyard.ReportedAt, null, "ShipyardReported"));

        var list = new StackPanel { Spacing = Gaps.Tile };

        foreach (var ship in shipyard.Ships.OrderBy(ship => ship.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var row = new Border
            {
                MinHeight = TypeScale.MinimumTarget,
                Padding = new Thickness(12, 0),
                Child = Cell(Typeset.Chrome(ship.Name, TypeScale.Control, ThemeManager.WhiteKey, weight: FontWeight.SemiBold, trimming: TextTrimming.CharacterEllipsis)),
            };
            ListRow.Dress(row);
            Avalonia.Automation.AutomationProperties.SetName(row, ship.Name);
            list.Children.Add(row);
        }

        column.Children.Add(list);

        return column;
    }

    /// <summary>A list's heading in A, then its reported time and the caveat in grey.</summary>
    private static StackPanel Intro(string heading, DateTimeOffset? at, string? caveat, string name)
    {
        var title = Typeset.Prose(heading, TypeScale.ControlLarge, ThemeManager.AKey, wrap: true);
        title.Name = name.Replace("Reported", "Heading", StringComparison.Ordinal);

        var when = Typeset.Prose(
            $"Reported {TimeText(at)}." + (caveat is null ? string.Empty : " " + caveat), TypeScale.Secondary, ThemeManager.GreyKey, wrap: true);
        when.Name = name;

        return new StackPanel { Spacing = 2, Children = { title, when } };
    }

    private Control Unavailable(string failure)
    {
        var notice = new Notice(NoticeLevel.Error)
        {
            Label = UnavailableLabel,
            Text = UnavailableText,
            Detail = failure,
            MaxWidth = 760,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var retry = new Button { Name = "Retry", Content = "Retry" };
        retry.Click += (_, _) => Fetch();
        notice.Actions.Add(retry);

        return notice;
    }

    private Control Missing()
    {
        var notice = new Notice(NoticeLevel.Warning)
        {
            Label = MissingLabel,
            Text = MissingText,
            MaxWidth = 760,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        if (_back is { } back)
        {
            var tile = new Button { Name = "Back", Content = "Back" };
            tile.Click += (_, _) => back();
            notice.Actions.Add(tile);
        }

        return notice;
    }

    private static StackPanel Group(string name, string? count, Control content) =>
        new() { Spacing = 10, Children = { StarSystemPage.GroupHead(name, string.Empty, count), content } };

    private static string Count(int count) => count.ToString(CultureInfo.InvariantCulture);

    private static TextBlock Sentence(string text)
    {
        var block = RoutingKit.Ink(text, TypeScale.ControlLarge, ThemeManager.WhiteKey, wrap: true);
        block.MaxWidth = 640;

        return block;
    }
}

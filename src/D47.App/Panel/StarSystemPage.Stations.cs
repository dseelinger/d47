using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>A kind the STATIONS sidebar offers, and the stations it takes.</summary>
public sealed record StationGroup(string Key, string Label, Func<StationProfile, bool> Holds)
{
    public const string All = "all";

    /// <summary>All, then each kind in the order the sidebar lists them. A megaship is counted as Other.</summary>
    public static readonly IReadOnlyList<StationGroup> Kinds =
    [
        new(All, "All", _ => true),
        new("starport", "Starport", station => station.Kind == StationKind.Starport),
        new("outpost", "Outpost", station => station.Kind == StationKind.Outpost),
        new("surface", "Surface port", station => station.Kind == StationKind.SurfacePort),
        new("settlement", "Settlement", station => station.Kind == StationKind.Settlement),
        new("carrier", "Fleet carrier", station => station.Kind == StationKind.FleetCarrier),
        new("other", "Other", station => station.Kind is StationKind.Other or StationKind.Megaship),
    ];

    public static StationGroup Of(string key) => Kinds.FirstOrDefault(group => group.Key == key) ?? Kinds[0];
}

/// <summary>A service the STATIONS sidebar offers, by its label and Spansh's spelling in <c>services[]</c>.</summary>
public sealed record StationService(string Label, string Spansh)
{
    public static readonly IReadOnlyList<StationService> All =
    [
        new("Commodity market", "Market"),
        new("Black market", "Black Market"),
        new("Shipyard", "Shipyard"),
        new("Outfitting", "Outfitting"),
        new("Interstellar factors", "Interstellar Factors Contact"),
        new("Material trader", "Material Trader"),
        new("Technology broker", "Technology Broker"),
        new("Universal Cartographics", "Universal Cartographics"),
        new("Vista Genomics", "Vista Genomics"),
        new("Refuel", "Refuel"),
        new("Repair", "Repair"),
        new("Rearm", "Restock"),
    ];

    public bool OfferedBy(StationProfile station) => station.Services.Contains(Spansh, StringComparer.Ordinal);
}

/// <summary>Which of a system's stations the STATIONS table lists: one kind, every ticked service, a name.</summary>
public sealed class StationFilter
{
    public string Kind { get; set; } = StationGroup.All;

    /// <summary>Spansh's spellings of the ticked services.</summary>
    public HashSet<string> Services { get; } = new(StringComparer.Ordinal);

    public string Query { get; set; } = string.Empty;

    /// <summary>The stations that pass, nearest first, those with no distance last.</summary>
    public IReadOnlyList<StationProfile> Apply(IEnumerable<StationProfile> stations)
    {
        var group = StationGroup.Of(Kind);
        var query = Query.Trim();

        return
        [
            .. Nearest(stations.Where(station =>
                group.Holds(station)
                && Services.All(service => station.Services.Contains(service, StringComparer.Ordinal))
                && (query.Length == 0 || station.Name.Contains(query, StringComparison.OrdinalIgnoreCase)))),
        ];
    }

    public static IEnumerable<StationProfile> Nearest(IEnumerable<StationProfile> stations) =>
        stations
            .OrderBy(station => Arrival(station) is null)
            .ThenBy(Arrival)
            .ThenBy(station => station.Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>Light seconds from arrival, or null where Spansh gives none; it gives 0 for a carrier it has not placed.</summary>
    public static double? Arrival(StationProfile station) =>
        station.DistanceToArrival is > 0 and var ls ? ls : null;
}

public sealed partial class StarSystemPage
{
    public const string NoStationMatches = "No station here matches those filters.";

    public const string StationsTable = "StarSystem.Stations";

    /// <summary>The stations table's columns, each sortable from its header.</summary>
    public static readonly IReadOnlyList<SortColumn<StationProfile>> StationHeads =
    [
        SortColumn<StationProfile>.Text("Station", station => station.Name),
        SortColumn<StationProfile>.Text("Type", StationType),
        SortColumn<StationProfile>.Text("Controlling faction", station => station.ControllingFaction is null ? null : Faction(station)),
        SortColumn<StationProfile>.Text("Government", station => station.Government),
        SortColumn<StationProfile>.Text("Economy", station => station.PrimaryEconomy),
        SortColumn<StationProfile>.Figure("Pad", station => (int?)station.LargestPad, SortDirection.Descending),
        SortColumn<StationProfile>.Figure("Arrival", StationFilter.Arrival, SortDirection.Ascending),
    ];

    /// <summary>The default order: nearest first.</summary>
    public static readonly SortOrder ByArrival = new(6, SortDirection.Ascending);

    private const double StationSpacing = 12;

    /// <summary>Marks a station's row on the Stations section.</summary>
    public const string StationRowClass = "station-row";

    private const double MoreWidth = 28;

    /// <summary>Drills a station's page; null leaves the rows unpressable.</summary>
    public Action<StarSystemProfile, StationProfile>? OpenStation { get; init; }

    private ArrangedTable<StationProfile>? _stationColumns;
    private StationFilter _stationFilter = new();
    private Border? _stationTable;

    /// <summary>True while STATIONS is the open section of a system that has been read.</summary>
    public bool Filters => _view == View.Ready && _profile is not null && _section == Section.Stations;

    public string FilterPlaceholder => "Find a station by name";

    public double? FilterWidth => 340;

    public void Filter(string? query)
    {
        _stationFilter.Query = query ?? string.Empty;
        ShowStations();
    }

    /// <summary>"TYPE" as the table shows it: Spansh's type, or the kind where Spansh gives none.</summary>
    public static string StationType(StationProfile station) => station.Kind switch
    {
        StationKind.FleetCarrier => "Fleet carrier",
        StationKind.Megaship when station.Type is null => "Megaship",
        _ => station.Type ?? "Other",
    };

    /// <summary>"L", "M", "S", or a dash.</summary>
    public static string PadText(PadSize? pad) => pad switch
    {
        PadSize.Large => "L",
        PadSize.Medium => "M",
        PadSize.Small => "S",
        _ => Dash,
    };

    /// <summary>"1,788 Ls", or a dash for none or 0.</summary>
    public static string ArrivalText(double? lightSeconds) =>
        lightSeconds is > 0 and { } ls ? ls.ToString("N0", CultureInfo.InvariantCulture) + " Ls" : Dash;

    private void ShowSection(Section section, string? kind = null)
    {
        if (section == Section.Orbits && _section != Section.Orbits)
        {
            ResetOrbits();
        }

        _section = section;

        if (section == Section.Powerplay && _profile is { } profile)
        {
            AskNearby(profile);
        }

        if (kind is not null)
        {
            _stationFilter.Kind = kind;
        }

        Draw();
    }

    /// <summary>The section switch, laid on six equal columns.</summary>
    private Grid Sections(StarSystemProfile profile)
    {
        var sections = new Segment
        {
            ItemsSource =
            [
                "Overview",
                $"Stations {profile.Stations.Count.ToString(CultureInfo.InvariantCulture)}",
                $"Bodies {profile.Bodies.Count.ToString(CultureInfo.InvariantCulture)}",
                "Powerplay",
                BiologySegment(profile),
                "Orbits",
            ],
            SelectedIndex = (int)_section,
        };
        Avalonia.Automation.AutomationProperties.SetName(sections, "Sections");
        sections.SelectionChanged += (_, _) => ShowSection((Section)sections.SelectedIndex);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*,*") };
        Grid.SetColumnSpan(sections, 6);
        grid.Children.Add(sections);

        return grid;
    }

    /// <summary>STATIONS BY KIND on the Overview: one tile per kind, each opening STATIONS on it.</summary>
    private StackPanel KindTiles(StarSystemProfile profile)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*,*"),
            ColumnSpacing = Gaps.Tile,
        };

        var kinds = StationGroup.Kinds.Skip(1).ToList();

        for (var i = 0; i < kinds.Count; i++)
        {
            var group = kinds[i];
            var count = profile.Stations.Count(group.Holds);

            var label = new TextBlock
            {
                Text = group.Label.ToUpperInvariant(),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var figure = new TextBlock
            {
                Text = count.ToString(CultureInfo.InvariantCulture),
                FontFamily = new FontFamily(Fonts.MonoFamily),
                LetterSpacing = 0,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            ThemeInk.Bind(figure, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

            var line = new DockPanel();
            DockPanel.SetDock(figure, Dock.Right);
            line.Children.Add(figure);
            line.Children.Add(label);

            var tile = new Button
            {
                Content = line,
                Height = TypeScale.MinimumTarget,
                Padding = new Thickness(14, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            Avalonia.Automation.AutomationProperties.SetName(tile, $"{group.Label}, {count}");
            tile.Click += (_, _) => ShowSection(Section.Stations, group.Key);

            Grid.SetColumn(tile, i);
            grid.Children.Add(tile);
        }

        return new StackPanel
        {
            Spacing = 10,
            Children = { GroupHead("Stations by kind", "Choose one to list them."), grid },
        };
    }

    private DockPanel Stations(StarSystemProfile profile)
    {
        var sidebar = new Sidebar(
            [
                new SidebarGroup(
                    "Kind",
                    [
                        .. StationGroup.Kinds.Select(group => new SidebarItem(
                            group.Key,
                            group.Label,
                            profile.Stations.Count(group.Holds).ToString(CultureInfo.InvariantCulture))),
                    ]),
            ],
            _stationFilter.Kind,
            key =>
            {
                _stationFilter.Kind = key;
                ShowStations();
            })
        {
            VerticalAlignment = VerticalAlignment.Top,
        };

        sidebar.Add("Services", StationService.All.Select(service => ServiceBox(profile, service)));

        _stationTable = new Border { Child = StationTable(profile) };

        var section = new DockPanel();
        DockPanel.SetDock(sidebar, Dock.Left);
        section.Children.Add(sidebar);
        section.Children.Add(_stationTable);

        return section;
    }

    private CheckBox ServiceBox(StarSystemProfile profile, StationService service)
    {
        var count = profile.Stations.Count(service.OfferedBy);

        var label = new TextBlock
        {
            Text = service.Label.ToUpperInvariant(),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var figure = new TextBlock
        {
            Text = count.ToString(CultureInfo.InvariantCulture),
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.MetaSmall,
            LetterSpacing = 0,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var words = new DockPanel();
        DockPanel.SetDock(figure, Dock.Right);
        words.Children.Add(figure);
        words.Children.Add(label);

        var box = LabeledCheckBox.Caps(service.Label);
        box.Content = words;
        box.Classes.Add("composed");
        box.HorizontalAlignment = HorizontalAlignment.Stretch;
        box.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        box.IsEnabled = count > 0;
        box.IsChecked = count > 0 && _stationFilter.Services.Contains(service.Spansh);
        Avalonia.Automation.AutomationProperties.SetName(box, service.Label);

        box.IsCheckedChanged += (_, _) =>
        {
            if (box.IsChecked == true)
            {
                _stationFilter.Services.Add(service.Spansh);
            }
            else
            {
                _stationFilter.Services.Remove(service.Spansh);
            }

            ShowStations();
        };

        return box;
    }

    /// <summary>Redraws the table alone, when the filter changes while STATIONS shows.</summary>
    private void ShowStations()
    {
        if (_stationTable is not null && _profile is { } profile)
        {
            _stationTable.Child = StationTable(profile);
            _redrawn = true;
        }
    }

    private ArrangedTable<StationProfile> StationColumns
    {
        get
        {
            if (_stationColumns is null)
            {
                _stationColumns = new ArrangedTable<StationProfile>(
                    StationsTable, StationFields(), ColumnArrangements, Widths, StationSpacing, sortedByDefault: "Arrival", headName: "StationHead");
                _stationColumns.Changed += ShowStations;
            }

            return _stationColumns;
        }
    }

    /// <summary>Every field the stations table can show; the › that opens a station is a control column at the right.</summary>
    private IReadOnlyList<TableField<StationProfile>> StationFields() =>
    [
        StationHead(0, new TableColumn("Station", ColumnFit.Fill) { Min = 120 }, station => Ellipsed(station.Name, TypeScale.ControlLarge, FontWeight.SemiBold, ThemeManager.WhiteKey)),
        StationHead(1, new TableColumn("Type"), station => Ellipsed(StationType(station), TypeScale.Control, FontWeight.Medium, station.Kind == StationKind.Other && station.Type is null ? ThemeManager.GreyKey : ThemeManager.AKey)),
        StationHead(2, new TableColumn("Faction", ColumnFit.Fill) { Min = 100 }, station => Ellipsed(Faction(station), TypeScale.Control, FontWeight.Medium, ThemeManager.WhiteKey)),
        StationHead(3, new TableColumn("Government"), station => Ellipsed(station.Government ?? Dash, TypeScale.Control, FontWeight.Medium, ThemeManager.AKey)),
        StationHead(4, new TableColumn("Economy"), station => Ellipsed(station.PrimaryEconomy ?? Dash, TypeScale.Control, FontWeight.Medium, ThemeManager.AKey)),
        StationHead(5, new TableColumn("Pad"), station => Mono(PadText(station.LargestPad), ThemeManager.WhiteKey)),
        StationHead(6, new TableColumn("Arrival"), station => RightAligned(Mono(ArrivalText(station.DistanceToArrival), ThemeManager.AKey)), right: true),
        Added<StationProfile>("Body", new TableColumn("Body", ColumnFit.Fill) { Min = 100 }, station => TableCells.Text(station.Body)),
        Added<StationProfile>("Allegiance", new TableColumn("Allegiance"), station => TableCells.Text(station.Allegiance)),
        Added<StationProfile>("FactionState", new TableColumn("FactionState"), station => TableCells.Text(station.ControllingFactionState), "Faction state"),
        Added<StationProfile>("Updated", new TableColumn("Updated"), station => station.UpdatedAt is { } at ? Mono(at.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ThemeManager.WhiteKey) : TableCells.Missing()),
        Added<StationProfile>("Pads", new TableColumn("Pads"), station => TableCells.Text(PadCountsText(station.Pads))),
        Added<StationProfile>("Economies", new TableColumn("Economies", ColumnFit.Fill) { Min = 120 }, station => TableCells.List([.. station.Economies.Select(economy => economy.Name)])),
        Added<StationProfile>("Services", new TableColumn("Services", ColumnFit.Fill) { Min = 140 }, station => TableCells.List([.. station.Services.Select(SystemStationPage.ServiceLabel)])),
        Added<StationProfile>("DockingAccess", new TableColumn("DockingAccess"), station => TableCells.Text(station.CarrierDockingAccess), "Docking access"),
        Added<StationProfile>("Market", new TableColumn("Market"), station => TableCells.Flag(station.Market is { Commodities.Count: > 0 })),
        Added<StationProfile>("Outfitting", new TableColumn("Outfitting"), station => TableCells.Flag(station.Outfitting is { Modules.Count: > 0 })),
        Added<StationProfile>("Shipyard", new TableColumn("Shipyard"), station => TableCells.Flag(station.Shipyard is { Ships.Count: > 0 })),
        new TableField<StationProfile>("More", new TableColumn("More", ColumnFit.Fixed) { Fixed = MoreWidth - StationSpacing }, MoreCell),
    ];

    private static TableField<StationProfile> StationHead(int head, TableColumn column, Func<StationProfile, Control> cell, bool right = false) =>
        Headed(StationHeads[head], column, cell, right);

    /// <summary>A field that keeps a column the table always had, with that column's header and sort.</summary>
    internal static TableField<TRow> Headed<TRow>(SortColumn<TRow> head, TableColumn column, Func<TRow, Control> cell, bool right = false) =>
        new(column.Key, column, row => Centred(cell(row)))
        {
            Label = head.Label,
            Sort = head,
            Alignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left,
        };

    /// <summary>A field the Commander can add; it is not shown until they do.</summary>
    private static TableField<TRow> Added<TRow>(string key, TableColumn column, Func<TRow, Control> cell, string? label = null) =>
        new(key, column, row => Centred(cell(row))) { Label = label ?? key, ByDefault = false };

    private Control MoreCell(StationProfile station) =>
        OpenStation is not null && station.MarketId is not null
            ? Typeset.Chrome("›", TypeScale.ControlLarge, ThemeManager.AKey, vertical: VerticalAlignment.Center, horizontal: HorizontalAlignment.Right)
            : new Border();

    /// <summary>"S 2 · M 3 · L 1", or null where the station reports no pad counts.</summary>
    private static string? PadCountsText(PadCounts? pads) =>
        pads is null
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"S {pads.Small} · M {pads.Medium} · L {pads.Large}");

    private static TextBlock RightAligned(TextBlock block)
    {
        block.HorizontalAlignment = HorizontalAlignment.Right;

        return block;
    }

    private StackPanel StationTable(StarSystemProfile profile)
    {
        var columns = StationColumns;
        columns.Refresh();

        var shown = columns.Sort(_stationFilter.Apply(profile.Stations));

        var table = new StackPanel { Spacing = Gaps.Tile };

        var showing = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                Caption("Showing", ThemeManager.GreyKey),
                Figure(shown.Count, ThemeManager.WhiteKey),
                Caption("of", ThemeManager.GreyKey),
                Figure(profile.Stations.Count, ThemeManager.GreyKey),
            },
        };
        Avalonia.Automation.AutomationProperties.SetName(showing, $"Showing {shown.Count} of {profile.Stations.Count}");

        var line = new DockPanel { MinHeight = TypeScale.MinimumTarget, Margin = new Thickness(0, 0, 0, 8) };

        if (ColumnArranger.Tile(columns) is { } tile)
        {
            DockPanel.SetDock(tile, Dock.Right);
            line.Children.Add(tile);
        }

        line.Children.Add(showing);
        table.Children.Add(line);

        if (ColumnArranger.Bar(columns) is { } bar)
        {
            table.Children.Add(bar);
        }

        var header = columns.Header();
        header.MinHeight = TypeScale.MinimumTarget;
        header.Margin = new Thickness(12, 0);

        var rows = new StackPanel { Spacing = Gaps.Tile, Children = { header } };

        foreach (var station in shown)
        {
            rows.Children.Add(StationRow(profile, station));
        }

        table.Children.Add(columns.Headset ? rows : new TableScroll(columns.Layout, rows));

        if (shown.Count == 0)
        {
            var none = Sentence(NoStationMatches, ThemeManager.WhiteKey);
            none.Margin = new Thickness(0, 10, 0, 0);
            table.Children.Add(none);
        }

        return table;
    }

    private Border StationRow(StarSystemProfile profile, StationProfile station)
    {
        var row = new Border
        {
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(12, 0),
            Child = StationColumns.Row(station),
        };
        row.Classes.Add(StationRowClass);
        Avalonia.Automation.AutomationProperties.SetName(row, station.Name);

        if (OpenStation is { } open && station.MarketId is not null)
        {
            ListRow.Dress(row);
            ListRow.Opens(row, () => open(profile, station));
        }
        else
        {
            ThemeInk.Bind(row, Border.BackgroundProperty, ThemeManager.SlabKey);
        }

        return row;
    }

    /// <summary>The controlling faction, with "Fleet carrier" for Spansh's FleetCarrier.</summary>
    public static string Faction(StationProfile station) => station.ControllingFaction switch
    {
        null => Dash,
        "FleetCarrier" => "Fleet carrier",
        { } faction => faction,
    };

    private static TextBlock Ellipsed(string text, double size, FontWeight weight, string key) =>
        Typeset.Chrome(text, size, key, weight: weight, trimming: TextTrimming.CharacterEllipsis);

    private static TextBlock Mono(string text, string key) =>
        Typeset.Mono(text, TypeScale.Control, key);

    private static TextBlock Figure(int count, string key)
    {
        var block = Mono(count.ToString(CultureInfo.InvariantCulture), key);
        block.FontSize = TypeScale.Meta;

        return block;
    }
}

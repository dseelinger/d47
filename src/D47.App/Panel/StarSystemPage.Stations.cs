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

    private const string StationColumns = "1.5*,132,1.2*,96,92,34,72";

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
        _section = section;

        if (kind is not null)
        {
            _stationFilter.Kind = kind;
        }

        Draw();
    }

    /// <summary>The section switch, laid on four equal columns so it keeps its widths as sections are added.</summary>
    private Grid Sections(StarSystemProfile profile)
    {
        var sections = new Segment
        {
            ItemsSource =
            [
                "Overview",
                $"Stations {profile.Stations.Count.ToString(CultureInfo.InvariantCulture)}",
                $"Bodies {profile.Bodies.Count.ToString(CultureInfo.InvariantCulture)}",
            ],
            SelectedIndex = (int)_section,
        };
        Avalonia.Automation.AutomationProperties.SetName(sections, "Sections");
        sections.SelectionChanged += (_, _) => ShowSection((Section)sections.SelectedIndex);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*") };
        Grid.SetColumnSpan(sections, 3);
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
            RoutingKit.Themed(figure, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

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

    private StackPanel StationTable(StarSystemProfile profile)
    {
        var shown = _stationFilter.Apply(profile.Stations);

        var table = new StackPanel { Spacing = Gaps.Tile };

        var showing = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 0, 0, 8),
            Children =
            {
                Caption("Showing", ThemeManager.GreyKey),
                Figure(shown.Count, ThemeManager.WhiteKey),
                Caption("of", ThemeManager.GreyKey),
                Figure(profile.Stations.Count, ThemeManager.GreyKey),
                Caption("· nearest first", ThemeManager.GreyKey),
            },
        };
        Avalonia.Automation.AutomationProperties.SetName(showing, $"Showing {shown.Count} of {profile.Stations.Count}");
        table.Children.Add(showing);

        var header = StationGrid();
        header.Height = 28;
        header.Margin = new Thickness(12, 0);
        string[] titles = ["Station", "Type", "Controlling faction", "Government", "Economy", "Pad", "Arrival"];

        for (var i = 0; i < titles.Length; i++)
        {
            var title = Caption(titles[i], ThemeManager.GreyKey);
            title.VerticalAlignment = VerticalAlignment.Center;
            title.TextTrimming = TextTrimming.CharacterEllipsis;

            if (i == titles.Length - 1)
            {
                title.HorizontalAlignment = HorizontalAlignment.Right;
            }

            Grid.SetColumn(title, i);
            header.Children.Add(title);
        }

        table.Children.Add(header);

        foreach (var station in shown)
        {
            table.Children.Add(StationRow(station));
        }

        if (shown.Count == 0)
        {
            var none = Sentence(NoStationMatches, ThemeManager.WhiteKey);
            none.Margin = new Thickness(0, 10, 0, 0);
            table.Children.Add(none);
        }

        return table;
    }

    private static Grid StationGrid() => new()
    {
        ColumnDefinitions = new ColumnDefinitions(StationColumns),
        ColumnSpacing = 12,
    };

    private static Border StationRow(StationProfile station)
    {
        var grid = StationGrid();
        grid.VerticalAlignment = VerticalAlignment.Center;

        var name = Ellipsed(station.Name, TypeScale.ControlLarge, FontWeight.SemiBold, ThemeManager.WhiteKey);
        var untyped = station.Kind == StationKind.Other && station.Type is null;

        Cell(grid, 0, name);
        Cell(grid, 1, Ellipsed(StationType(station), TypeScale.Control, FontWeight.Medium, untyped ? ThemeManager.GreyKey : ThemeManager.AKey));
        Cell(grid, 2, Ellipsed(Faction(station), TypeScale.Control, FontWeight.Medium, ThemeManager.WhiteKey));
        Cell(grid, 3, Ellipsed(station.Government ?? Dash, TypeScale.Control, FontWeight.Medium, ThemeManager.AKey));
        Cell(grid, 4, Ellipsed(station.PrimaryEconomy ?? Dash, TypeScale.Control, FontWeight.Medium, ThemeManager.AKey));
        Cell(grid, 5, Mono(PadText(station.LargestPad), ThemeManager.WhiteKey));

        var arrival = Mono(ArrivalText(station.DistanceToArrival), ThemeManager.AKey);
        arrival.HorizontalAlignment = HorizontalAlignment.Right;
        Cell(grid, 6, arrival);

        var row = new Border
        {
            MinHeight = 40,
            Padding = new Thickness(12, 0),
            Child = grid,
        };
        RoutingKit.Themed(row, Border.BackgroundProperty, ThemeManager.SlabKey);
        Avalonia.Automation.AutomationProperties.SetName(row, station.Name);

        return row;
    }

    /// <summary>The controlling faction, with "Fleet carrier" for Spansh's FleetCarrier.</summary>
    public static string Faction(StationProfile station) => station.ControllingFaction switch
    {
        null => Dash,
        "FleetCarrier" => "Fleet carrier",
        { } faction => faction,
    };

    private static TextBlock Ellipsed(string text, double size, FontWeight weight, string key)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = size,
            FontWeight = weight,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        RoutingKit.Themed(block, TextBlock.ForegroundProperty, key);

        return block;
    }

    private static TextBlock Mono(string text, string key)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Control,
        };
        RoutingKit.Themed(block, TextBlock.ForegroundProperty, key);

        return block;
    }

    private static TextBlock Figure(int count, string key)
    {
        var block = Mono(count.ToString(CultureInfo.InvariantCulture), key);
        block.FontSize = TypeScale.Meta;

        return block;
    }
}

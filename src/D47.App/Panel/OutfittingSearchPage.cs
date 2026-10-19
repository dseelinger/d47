using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Knowledge;
using static D47.App.Panel.SystemSearchPage;

namespace D47.App.Panel;

/// <summary>Search › Outfitting: stations that sell every module added, and the ship if one is, through <c>search_outfitting</c>.</summary>
public sealed class OutfittingSearchPage : GalaxySearchPage
{
    public const string RootKey = "search.outfitting";

    public const string SummaryText = "Stations that sell everything you add, nearest first.";

    public const string ItemsHelp = "A station that sells every module you add, and the ship if you add one.";

    public const string NothingText = "Nothing matches.";

    public const string AddName = "AddItem";

    public const string NothingName = "NothingMatches";

    /// <summary>Marks an added item's tile; pressing it removes the item.</summary>
    public const string ItemClass = "item-tile";

    public const int DefaultDistance = 50;

    public const string HeadName = "OutfittingHead";

    private const string Columns = "1.5*,*,64,84,92,76";

    private const string HeadsetColumns = "1.5*,*,64,84,92";

    private const string Dash = "—";

    private static readonly IReadOnlyList<string> Pads = ["Any", .. StationQuery.MinPads];

    private readonly Func<string?> _here;
    private readonly Action<string, long?> _openSystem;
    private readonly Func<string, Task<bool>>? _copy;

    private readonly TextBox _add = Field("add a module or ship");
    private readonly SystemNameSuggestions<OutfittingItem> _addNames;
    private readonly TextBlock _addLabel = Label("Add a module or ship", width: null);
    private readonly TextBlock _nothing = Prose(NothingText, TypeScale.Tip, ThemeManager.GreyKey);
    private readonly WrapPanel _tiles = new() { ItemSpacing = Gaps.Tile, LineSpacing = Gaps.Tile };
    private readonly TextBox _near = Field("near");
    private readonly SystemNameSuggestions<SystemNameMatch>? _nearNames;
    private readonly Amount _distance = new() { Value = DefaultDistance, Minimum = 1, Maximum = 500, Step = 10, Width = 132 };
    private readonly Segment _pad = new() { ItemsSource = Pads, SelectedIndex = 0, Height = TypeScale.MinimumTarget };
    private readonly TextBox _fromStar = Field("from the star at most", mono: true);
    private readonly CheckBox _surface = LabeledCheckBox.Caps("Surface stations");
    private readonly CheckBox _carriers = LabeledCheckBox.Caps("Fleet carriers");

    private IReadOnlyList<OutfittingItem> _items = [];

    public OutfittingSearchPage(
        GalaxySearchSurface searches,
        Func<bool> lookupsEnabled,
        Func<string?> here,
        Action<string, long?> openSystem,
        Func<string, Task<bool>>? copy,
        Action? openSettings,
        bool headset)
        : base(searches, lookupsEnabled, openSettings, headset)
    {
        _here = here;
        _openSystem = openSystem;
        _copy = copy;
        _nearNames = SuggestNames(_near);
        _addNames = SuggestItems(_add, Offer, Add);

        _surface.IsChecked = true;
        _surface.MinHeight = TypeScale.MinimumTarget;
        _carriers.MinHeight = TypeScale.MinimumTarget;
        _nothing.IsVisible = false;
        _nothing.Name = NothingName;
        _add.Name = AddName;
        _add.TextChanged += (_, _) => _nothing.IsVisible &= !string.IsNullOrWhiteSpace(_add.Text);
        _fromStar.PlaceholderText = Any;
        _fromStar.InnerRightContent = Label("LS", width: null);

        DrawItems();
    }

    public override string Tool => "search_outfitting";

    public override GalaxySearchKind Kind => GalaxySearchKind.Outfitting;

    public override string Summary => SummaryText;

    protected override string Noun => "Stations";

    /// <summary>The items the form asks for.</summary>
    public IReadOnlyList<OutfittingItem> Items => _items;

    /// <summary>Adds an item as the type-ahead does; a second ship, or an item already added, is left out.</summary>
    public void Add(OutfittingItem item)
    {
        if (_items.Any(kept => kept.Display == item.Display) || (item.IsShip && _items.Any(kept => kept.IsShip)))
        {
            return;
        }

        _items = [.. _items, item];
        DrawItems();
    }

    /// <summary>Removes an item, as its tile's ✕ does.</summary>
    public void Remove(OutfittingItem item)
    {
        _items = [.. _items.Where(kept => kept.Display != item.Display)];
        DrawItems();
    }

    /// <summary>Types into the add field.</summary>
    public void Type(string text) => _add.Text = text;

    /// <summary>The suggestions the add field lists.</summary>
    public SystemNameSuggestions<OutfittingItem> Suggestions => _addNames;

    /// <summary>Sets the Near field.</summary>
    public void Near(string system) => _near.Text = system;

    /// <summary>Sets the landing pad: Any, Medium or Large.</summary>
    public void Pad(string pad) => _pad.SelectedIndex = Math.Max(0, Pads.ToList().FindIndex(known => string.Equals(known, pad, StringComparison.OrdinalIgnoreCase)));

    public void TickSurface(bool ticked) => _surface.IsChecked = ticked;

    public void TickCarriers(bool ticked) => _carriers.IsChecked = ticked;

    public override IReadOnlyDictionary<string, string> FormArguments()
    {
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal);

        void Put(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                arguments[key] = value.Trim();
            }
        }

        Put("items", string.Join(", ", _items.Select(item => item.Display)));
        Put("near", _near.Text);

        if (_distance.Value is { } distance && distance != DefaultDistance)
        {
            arguments["max_distance"] = distance.ToString("0", CultureInfo.InvariantCulture);
        }

        Put("min_pad", _pad.SelectedIndex > 0 ? Pads[_pad.SelectedIndex] : null);
        Put("max_station_distance", _fromStar.Text);

        if (_surface.IsChecked != true)
        {
            arguments["surface_stations"] = "false";
        }

        if (_carriers.IsChecked == true)
        {
            arguments["include_carriers"] = "true";
        }

        return arguments;
    }

    public override void ClearForm()
    {
        _items = [];
        _add.Text = string.Empty;
        _near.Text = string.Empty;
        _fromStar.Text = string.Empty;
        _distance.Value = DefaultDistance;
        _pad.SelectedIndex = 0;
        _surface.IsChecked = true;
        _carriers.IsChecked = false;
        DrawItems();
    }

    protected override void FillForm(IReadOnlyDictionary<string, string> arguments)
    {
        ClearForm();

        string? Get(string key) => arguments.TryGetValue(key, out var value) ? value : null;

        Fill(_near, _nearNames, Get("near") ?? string.Empty);
        _fromStar.Text = Get("max_station_distance") ?? string.Empty;
        Pad(Get("min_pad") ?? "Any");
        _surface.IsChecked = !bool.TryParse(Get("surface_stations"), out var surface) || surface;
        _carriers.IsChecked = bool.TryParse(Get("include_carriers"), out var carriers) && carriers;

        foreach (var part in Get("items")?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [])
        {
            if (OutfittingItem.TryParse(part, out var item, out _))
            {
                Add(item);
            }
        }

        if (double.TryParse(Get("max_distance"), NumberStyles.Float, CultureInfo.InvariantCulture, out var distance))
        {
            _distance.Value = (decimal)Math.Clamp(distance, 1, 500);
        }
    }

    protected override bool Refusal(IReadOnlyDictionary<string, string> arguments)
    {
        string? Get(string key) => arguments.TryGetValue(key, out var value) ? value : null;

        var search = new OutfittingSearch
        {
            ReferenceSystem = Get("near") ?? "-",
            Items = Get("items"),
            MinPad = Get("min_pad"),
            MaxStationDistance = double.TryParse(Get("max_station_distance"), NumberStyles.Float, CultureInfo.InvariantCulture, out var furthest) ? furthest : null,
        };

        return !StationQuery.TryParse(search, out _, out _);
    }

    protected override (string Lead, string Reference)? Aside(IReadOnlyDictionary<string, string> arguments, string? reference)
    {
        var within = arguments.TryGetValue("max_distance", out var typed)
                     && double.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out var asked)
            ? asked
            : DefaultDistance;

        var from = reference
            ?? (arguments.TryGetValue("near", out var near) && near.Trim().Length > 0 ? near.Trim() : null)
            ?? "your system";

        return ($"WITHIN {within.ToString("0.##", CultureInfo.InvariantCulture)} LY OF ", from);
    }

    protected override int Count(GalaxySearchPosting posting) =>
        posting.Result is StationSearchResult result ? result.Stations.Count : 0;

    protected override Control? Table(GalaxySearchPosting posting, bool headset)
    {
        if (posting.Result is not StationSearchResult { Stations.Count: > 0 } result)
        {
            return null;
        }

        var columns = headset ? HeadsetColumns : Columns;

        List<SearchColumn> heads =
        [
            new("Station", "Type", null, false),
            new("System", null, null, false),
            new("LY", null, null, true),
            new("Pad", "Largest", null, false),
            new("From star", null, null, true),
        ];

        if (!headset)
        {
            heads.Add(new("Updated", null, null, true));
        }

        var rule = SearchHead(posting, HeadName, columns, heads);
        var rows = new StackPanel { Spacing = Gaps.Tile, Margin = new Thickness(0, 2, 0, 0) };

        foreach (var station in result.Stations)
        {
            rows.Children.Add(Row(station, columns, headset));
        }

        return new StackPanel { Children = { rule, rows } };
    }

    protected override Control BuildForm()
    {
        var form = new StackPanel { Spacing = 22 };

        var add = new StackPanel { Spacing = 4, Children = { _addLabel, _add, _addNames.List, _nothing } };

        form.Children.Add(Group(
            "Items",
            add,
            _tiles,
            Prose(ItemsHelp, TypeScale.Tip, ThemeManager.GreyKey)));

        form.Children.Add(Group(
            "Search out from",
            Labelled("Near", NearField(_near, _nearNames)),
            Labelled("Distance in LY", _distance)));

        Control[] ticks = Headset ? [Columns2("*,*", _surface, _carriers)] : [_surface, _carriers];

        form.Children.Add(Group(
            "The station",
            [Labelled("Landing pad", _pad), Labelled("From the star at most", _fromStar), .. ticks]));

        return form;
    }

    protected override object? Followed => _here();

    protected override void Follow() =>
        ShowHere(_near, _here());

    /// <summary>The catalogue items for what is typed, best rating first: no ship while one is added, nothing already added.</summary>
    private IReadOnlyList<OutfittingItem> Offer(string typed)
    {
        var hasShip = _items.Any(item => item.IsShip);

        IReadOnlyList<OutfittingItem> offered =
        [
            .. OutfittingItem.Suggest(typed, int.MaxValue)
                .Where(item => !(hasShip && item.IsShip) && _items.All(kept => kept.Display != item.Display))
                .OrderBy(item => item.Display.Length)
                .ThenBy(item => item.Rating, StringComparer.Ordinal)
                .ThenBy(item => item.Class)
                .Take(NameSuggestions.Most),
        ];

        Dispatcher.UIThread.Post(() => _nothing.IsVisible = offered.Count == 0 && typed.Trim().Length > 0);

        return offered;
    }

    private void DrawItems()
    {
        var hasShip = _items.Any(item => item.IsShip);
        _addLabel.Text = (hasShip ? "Add a module" : "Add a module or ship").ToUpperInvariant();
        _add.PlaceholderText = hasShip ? "Type a module" : "Type a module or ship";
        _nothing.IsVisible = false;
        _tiles.Children.Clear();

        foreach (var item in _items)
        {
            var tile = new Button
            {
                Content = $"{item.Display.ToUpperInvariant()}  ✕",
                Height = TypeScale.MinimumTarget,
                Classes = { ItemClass },
                Tag = item.Display,
            };
            Avalonia.Automation.AutomationProperties.SetName(tile, $"Remove {item.Display}");

            var kept = item;
            tile.Click += (_, _) => Remove(kept);
            _tiles.Children.Add(tile);
        }
    }

    private Control Row(StationSummary station, string columns, bool headset)
    {
        var name = Chrome(station.Name, TypeScale.Secondary, ThemeManager.AKey);
        name.Text = station.Name;

        var here = string.Equals(station.SystemName, _here(), StringComparison.OrdinalIgnoreCase);
        var system = Value(station.SystemName);
        system.VerticalAlignment = VerticalAlignment.Center;
        Themed(system, TextBlock.ForegroundProperty, here ? ThemeManager.CyanKey : ThemeManager.AKey);

        var systemCell = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { system } };

        if (_copy is { } copy)
        {
            var glyph = CopyGlyph.For(station.SystemName, copy);
            glyph.VerticalAlignment = VerticalAlignment.Center;
            systemCell.Children.Add(glyph);
        }

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(columns),
            ColumnSpacing = 12,
            Children =
            {
                Cell(Pair(name, Sub(station.Type)), 0),
                Cell(systemCell, 1),
                Cell(Right(Mono(station.Distance is { } ly ? ly.ToString("F2", CultureInfo.InvariantCulture) : Dash, TypeScale.Small, ThemeManager.AKey)), 2),
                Cell(Value(LargestPad(station)), 3),
                Cell(Right(Mono(station.DistanceToArrival is { } ls ? $"{ls.ToString("N0", CultureInfo.InvariantCulture)} LS" : Dash, TypeScale.Small, ThemeManager.WhiteKey)), 4),
            },
        };

        if (!headset)
        {
            grid.Children.Add(Cell(Right(Mono(
                station.UpdatedAt is { } at ? at.ToLocalTime().ToString("MM-dd", CultureInfo.InvariantCulture) : Dash,
                TypeScale.Small,
                ThemeManager.GreyKey)), 5));
        }

        var row = new Button
        {
            MinHeight = 54,
            Padding = new Thickness(12, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = grid,
            Classes = { RowClass },
            Tag = station.SystemName,
        };
        Avalonia.Automation.AutomationProperties.SetName(row, station.Name);

        var open = _openSystem;
        row.Click += (_, _) => open(station.SystemName, station.SystemAddress);

        return ListRow.Dress(row);
    }

    private static string LargestPad(StationSummary station) =>
        station.LargePads > 0 || station.HasLargePad ? "Large"
        : station.MediumPads > 0 ? "Medium"
        : station.SmallPads > 0 ? "Small"
        : Dash;
}

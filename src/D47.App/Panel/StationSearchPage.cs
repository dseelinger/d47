using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Knowledge;
using static D47.App.Panel.SystemSearchPage;

namespace D47.App.Panel;

/// <summary>Search › Stations: stations that match, nearest first, through <c>search_stations</c>.</summary>
public sealed class StationSearchPage : GalaxySearchPage
{
    public const string RootKey = "search.stations";

    public const string SummaryText = "Stations that match, nearest first.";

    public const string ChooseServicesName = "ChooseServices";

    /// <summary>Marks a chosen service's tile; pressing it removes the service.</summary>
    public const string ServiceClass = "service-tile";

    public const string ServicesKey = "search.stations.services";

    public const string ServicesHelp = "Stations that have every service you tick. Fleet carriers are left out unless the station type names them.";

    public const int DefaultDistance = 50;

    public const int DefaultLimit = 5;

    private const string Columns = "1.5*,*,64,84,92,1.5*,76";

    private const string HeadsetColumns = "1.5*,*,64,84,92,1.5*";

    private readonly Func<string?> _here;
    private readonly Action<IReadOnlyList<string>, Action<IReadOnlyList<string>>> _chooseServices;

    private readonly TextBox _near = Field("near");
    private readonly Amount _distance = new() { Value = DefaultDistance, Minimum = 1, Maximum = 500, Step = 10, Width = 132 };
    private readonly Amount _limit = new() { Value = DefaultLimit, Minimum = 1, Maximum = 20, Step = 1, Width = 132 };
    private readonly Dictionary<string, CheckBox> _types = StationQuery.StationTypes.ToDictionary(type => type.Name, type => Tile(type.Name), StringComparer.Ordinal);
    private readonly Stepper _pad = Choice(StationQuery.MinPads);
    private readonly TextBox _fromStar = Field("from the star at most", mono: true);
    private readonly WrapPanel _serviceTiles = new() { ItemSpacing = Gaps.Tile, LineSpacing = Gaps.Tile };
    private readonly Stepper _trader = Choice(StationQuery.TraderTypes);
    private readonly Stepper _broker = Choice(StationQuery.BrokerTypes);
    private readonly TextBox _controllingFaction = Field("controlling faction");
    private readonly TextBox _module = Field("module");
    private readonly TextBox _ship = Field("ship");
    private readonly Stepper _powerState = Choice(GalaxyFilters.Find("power_state")?.Choices ?? []);
    private readonly TextBox _populationFrom = Field("population from", mono: true);
    private readonly TextBox _populationTo = Field("population to", mono: true);
    private readonly CheckBox _colonised = LabeledCheckBox.Caps("Only colonised systems");

    private readonly Dictionary<string, (InlinePicker Picker, IReadOnlyList<string> Choices)> _pickers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _picked = new(StringComparer.Ordinal);

    private IReadOnlyList<string> _services = [];

    public StationSearchPage(
        GalaxySearchSurface searches,
        Func<bool> lookupsEnabled,
        Func<string?> here,
        Action<IReadOnlyList<string>, Action<IReadOnlyList<string>>> chooseServices,
        Action? openSettings,
        bool headset)
        : base(searches, lookupsEnabled, openSettings, headset)
    {
        _here = here;
        _chooseServices = chooseServices;

        _colonised.MinHeight = TypeScale.MinimumTarget;
        _controllingFaction.PlaceholderText = "Exact name";
        _fromStar.PlaceholderText = Any;
        _fromStar.InnerRightContent = Label("LS", width: null);
        _populationFrom.PlaceholderText = "0";
        _populationTo.PlaceholderText = Any;

        foreach (var (name, choices) in PickerChoices)
        {
            var picker = new InlinePicker { Label = name.Replace('_', ' '), ListMaxHeight = 240 };
            var key = name;

            picker.Picked += (_, id) => Pick(key, id);

            _pickers[name] = (picker, choices);
            Pick(name, Any);
        }

        DrawServices();
    }

    public override string Tool => "search_stations";

    public override GalaxySearchKind Kind => GalaxySearchKind.Stations;

    public override string Summary => SummaryText;

    protected override string Noun => "Stations";

    /// <summary>The services the form asks for.</summary>
    public IReadOnlyList<string> Services => _services;

    private static IReadOnlyList<(string Name, IReadOnlyList<string> Choices)> PickerChoices { get; } =
    [
        .. new[] { "allegiance", "government", "station_economy", "station_state", "state", "primary_economy", "power" }
            .Select(name => (name, Vocabulary(name))),
        ("module_class", OutfittingCatalogue.Classes),
        ("module_rating", OutfittingCatalogue.Ratings),
    ];

    /// <summary>
    /// Sets a dropdown field to <paramref name="value"/>, or to Any for a value it does not offer. A list of
    /// choices, as a spoken search can give a filter that takes several, is kept as given.
    /// </summary>
    public void Pick(string name, string value)
    {
        var (picker, choices) = _pickers[name];
        var chosen = choices.FirstOrDefault(choice => string.Equals(choice, value, StringComparison.OrdinalIgnoreCase));

        if (chosen is null
            && GalaxyFilters.Find(name) is { Several: true }
            && value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) is { Length: > 1 } several
            && several.All(part => choices.Contains(part, StringComparer.OrdinalIgnoreCase)))
        {
            chosen = string.Join(", ", several);
        }

        if (chosen is null)
        {
            _picked.Remove(name);
        }
        else
        {
            _picked[name] = chosen;
        }

        picker.Show(
            [new InlinePickerOption(Any, Any, ThemeManager.GreyKey), .. choices.Select(choice => new InlinePickerOption(choice, choice, ThemeManager.WhiteKey))],
            chosen ?? Any,
            chosen ?? Any,
            chosen is null ? ThemeManager.GreyKey : ThemeManager.WhiteKey,
            null);
    }

    /// <summary>Sets a text field by its tool argument name.</summary>
    public void Type(string name, string value) => TextField(name).Text = value;

    /// <summary>Ticks or clears a station type's tile.</summary>
    public void TickType(string type, bool ticked) => _types[type].IsChecked = ticked;

    /// <summary>Sets the services the form asks for, as CHOOSE SERVICES does.</summary>
    public void ChooseServices(IReadOnlyList<string> services)
    {
        _services = [.. services.Select(Canonical).OfType<string>().Distinct(StringComparer.Ordinal)];
        DrawServices();
    }

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

        Put("near", _near.Text);

        if (_distance.Value is { } distance && distance != DefaultDistance)
        {
            arguments["max_distance"] = distance.ToString("0", CultureInfo.InvariantCulture);
        }

        Put("station_type", string.Join(", ", StationQuery.StationTypes.Where(type => _types[type.Name].IsChecked == true).Select(type => type.Name)));
        Put("min_pad", Selected(_pad));
        Put("max_station_distance", _fromStar.Text);
        Put("services", string.Join(", ", _services));
        Put("material_trader", Selected(_trader));
        Put("technology_broker", Selected(_broker));
        Put("module", _module.Text);
        Put("ship", _ship.Text);
        Put("controlling_faction", _controllingFaction.Text);
        Put("power_state", Selected(_powerState));
        Put("population", Range(_populationFrom.Text, _populationTo.Text));

        foreach (var (name, value) in _picked)
        {
            Put(name, value);
        }

        if (_colonised.IsChecked == true)
        {
            arguments["colonised"] = "true";
        }

        if (_limit.Value is { } limit && limit != DefaultLimit)
        {
            arguments["limit"] = limit.ToString("0", CultureInfo.InvariantCulture);
        }

        return arguments;
    }

    public override void ClearForm()
    {
        foreach (var box in (TextBox[])[_near, _fromStar, _controllingFaction, _module, _ship, _populationFrom, _populationTo])
        {
            box.Text = string.Empty;
        }

        foreach (var tile in _types.Values)
        {
            tile.IsChecked = false;
        }

        foreach (var stepper in (Stepper[])[_pad, _trader, _broker, _powerState])
        {
            stepper.SelectedIndex = 0;
        }

        _distance.Value = DefaultDistance;
        _limit.Value = DefaultLimit;
        _colonised.IsChecked = false;
        ChooseServices([]);

        foreach (var name in _pickers.Keys)
        {
            Pick(name, Any);
        }
    }

    protected override void FillForm(IReadOnlyDictionary<string, string> arguments)
    {
        ClearForm();

        string? Get(string key) => arguments.TryGetValue(key, out var value) ? value : null;

        _near.Text = Get("near") ?? string.Empty;
        _fromStar.Text = Get("max_station_distance") ?? string.Empty;
        _controllingFaction.Text = Get("controlling_faction") ?? string.Empty;
        _module.Text = Get("module") ?? string.Empty;
        _ship.Text = Get("ship") ?? string.Empty;
        (_populationFrom.Text, _populationTo.Text) = Split(Get("population"));
        _colonised.IsChecked = bool.TryParse(Get("colonised"), out var colonised) && colonised;

        _pad.SelectedIndex = Index(_pad.ItemsSource, Get("min_pad"));
        _trader.SelectedIndex = Index(_trader.ItemsSource, Get("material_trader"));
        _broker.SelectedIndex = Index(_broker.ItemsSource, Get("technology_broker"));
        _powerState.SelectedIndex = Index(_powerState.ItemsSource, Get("power_state"));

        foreach (var name in Items(Get("station_type")))
        {
            if (_types.Keys.FirstOrDefault(type => string.Equals(type, name, StringComparison.OrdinalIgnoreCase)) is { } type)
            {
                _types[type].IsChecked = true;
            }
        }

        ChooseServices([.. Items(Get("services"))]);

        if (double.TryParse(Get("max_distance"), NumberStyles.Float, CultureInfo.InvariantCulture, out var distance))
        {
            _distance.Value = (decimal)Math.Clamp(distance, 1, 500);
        }

        if (int.TryParse(Get("limit"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit))
        {
            _limit.Value = Math.Clamp(limit, 1, 20);
        }

        foreach (var name in _pickers.Keys)
        {
            Pick(name, Get(name) ?? Any);
        }
    }

    protected override bool Refusal(IReadOnlyDictionary<string, string> arguments)
    {
        string? Get(string key) => arguments.TryGetValue(key, out var value) ? value : null;

        var filters = GalaxyFilters.For(GalaxySearchKind.Stations)
            .Where(filter => arguments.ContainsKey(filter.Name))
            .ToDictionary(filter => filter.Name, filter => arguments[filter.Name], StringComparer.Ordinal);

        var search = new StationSearch
        {
            ReferenceSystem = Get("near") ?? "-",
            StationTypes = Get("station_type"),
            MinPad = Get("min_pad"),
            MaxStationDistance = double.TryParse(Get("max_station_distance"), NumberStyles.Float, CultureInfo.InvariantCulture, out var furthest) ? furthest : null,
            Services = Get("services"),
            MaterialTrader = Get("material_trader"),
            TechnologyBroker = Get("technology_broker"),
            Module = Get("module"),
            ModuleClass = Get("module_class"),
            ModuleRating = Get("module_rating"),
            Ship = Get("ship"),
            Filters = filters,
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

    /// <summary>The services of <paramref name="station"/> among those <paramref name="asked"/> for, by the names the form offers.</summary>
    public static IReadOnlyList<string> AskedFor(StationSummary station, IReadOnlyDictionary<string, string> asked) =>
    [
        .. Items(asked.GetValueOrDefault("services"))
            .Select(Canonical)
            .OfType<string>()
            .Where(name => station.Services.Any(service =>
                string.Equals(service, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(service, StationQuery.ServiceNames.First(known => known.Name == name).Field, StringComparison.OrdinalIgnoreCase))),
    ];

    protected override Control? Table(GalaxySearchPosting posting, bool headset)
    {
        if (posting.Result is not StationSearchResult { Stations.Count: > 0 } result)
        {
            return null;
        }

        var columns = headset ? HeadsetColumns : Columns;

        var heads = new Grid { ColumnDefinitions = new ColumnDefinitions(columns), ColumnSpacing = 12, Margin = new Thickness(12, 0, 12, 6) };

        foreach (var (top, sub, column, right) in new (string, string?, int, bool)[]
                 {
                     ("Station", "Type", 0, false),
                     ("System", "Controlled by", 1, false),
                     ("LY", null, 2, true),
                     ("Pad", "Largest", 3, false),
                     ("From star", null, 4, true),
                     ("Asked for", null, 5, false),
                     ("Updated", null, 6, true),
                 })
        {
            if (headset && column == 6)
            {
                continue;
            }

            var head = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Children = { Chrome(top, TypeScale.Caption, ThemeManager.AKey) } };

            if (sub is not null)
            {
                head.Children.Add(Chrome(sub, TypeScale.Caption, ThemeManager.GreyKey));
            }

            if (right)
            {
                head.HorizontalAlignment = HorizontalAlignment.Right;
            }

            Grid.SetColumn(head, column);
            heads.Children.Add(head);
        }

        var rule = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Child = heads };
        Themed(rule, Border.BorderBrushProperty, ThemeManager.LineKey);

        var rows = new StackPanel { Spacing = Gaps.Tile, Margin = new Thickness(0, 2, 0, 0) };

        foreach (var station in result.Stations)
        {
            rows.Children.Add(Row(station, AskedFor(station, posting.Arguments), columns, headset));
        }

        return new StackPanel { Children = { rule, rows } };
    }

    protected override Control BuildForm()
    {
        var form = new StackPanel { Spacing = 22 };

        form.Children.Add(Group(
            "Search out from",
            Labelled("Near", _near),
            Columns2("*,132", Labelled("Distance in LY", _distance), Labelled("How many", _limit))));

        var types = new WrapPanel { ItemSpacing = Gaps.Tile, LineSpacing = Gaps.Tile };

        foreach (var tile in _types.Values)
        {
            types.Children.Add(tile);
        }

        var choose = new Button { Name = ChooseServicesName, Content = "CHOOSE SERVICES", Height = TypeScale.MinimumTarget, HorizontalAlignment = HorizontalAlignment.Left };
        choose.Click += (_, _) => _chooseServices(_services, ChooseServices);

        form.Children.Add(Group(
            "The station",
            Labelled("Type · none ticked means all but carriers", types),
            Labelled("Landing pad", _pad),
            Labelled("From the star at most", _fromStar),
            Labelled("Services", new StackPanel { Spacing = 8, Children = { choose, _serviceTiles } }),
            Columns2("*,*", Labelled("Material trader", _trader), Labelled("Technology broker", _broker)),
            Labelled("Its economy", _pickers["station_economy"].Picker),
            Labelled("Its state", _pickers["station_state"].Picker),
            Columns2(
                "*,*",
                Labelled("Allegiance", _pickers["allegiance"].Picker),
                Labelled("Government", _pickers["government"].Picker)),
            Labelled("Controlling faction's state", _pickers["state"].Picker),
            Labelled("Controlling faction", _controllingFaction)));

        form.Children.Add(Group(
            "Sold there",
            Labelled("Module", _module),
            Columns2(
                "*,*",
                Labelled("Class", _pickers["module_class"].Picker),
                Labelled("Rating", _pickers["module_rating"].Picker)),
            Labelled("Ship", _ship)));

        form.Children.Add(Group(
            "Its system",
            Labelled("Economy", _pickers["primary_economy"].Picker),
            Labelled("Power", _pickers["power"].Picker),
            Labelled("Powerplay state", _powerState),
            Labelled("Population", Span(_populationFrom, _populationTo, null)),
            _colonised));

        return form;
    }

    protected override object? Followed => _here();

    protected override void Follow() =>
        _near.PlaceholderText = _here() is { } system ? $"Your system · {system}" : "Your system";

    private void DrawServices()
    {
        _serviceTiles.Children.Clear();

        foreach (var service in _services)
        {
            var tile = new Button
            {
                Content = $"{service.ToUpperInvariant()}  ✕",
                Height = TypeScale.MinimumTarget,
                Classes = { ServiceClass },
                Tag = service,
            };
            Avalonia.Automation.AutomationProperties.SetName(tile, $"Remove {service}");

            var name = service;
            tile.Click += (_, _) => ChooseServices([.. _services.Where(kept => kept != name)]);
            _serviceTiles.Children.Add(tile);
        }
    }

    private Control Row(StationSummary station, IReadOnlyList<string> asked, string columns, bool headset)
    {
        var name = Chrome(station.Name, TypeScale.Secondary, ThemeManager.AKey);
        name.Text = station.Name;

        var system = station.ControllingFaction is { } controlling
            ? station.State is { } state ? $"{controlling} · {state}" : controlling
            : Dash;

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(columns),
            ColumnSpacing = 12,
            Children =
            {
                Cell(Pair(name, Sub(station.Type)), 0),
                Cell(Pair(Value(station.SystemName), Sub(system)), 1),
                Cell(Right(Mono(station.Distance is { } ly ? ly.ToString("F2", CultureInfo.InvariantCulture) : Dash, TypeScale.Small, ThemeManager.AKey)), 2),
                Cell(Value(LargestPad(station)), 3),
                Cell(Right(Mono(station.DistanceToArrival is { } ls ? $"{ls.ToString("N0", CultureInfo.InvariantCulture)} LS" : Dash, TypeScale.Small, ThemeManager.WhiteKey)), 4),
                Cell(Wrapped(asked.Count == 0 ? Dash : string.Join(", ", asked)), 5),
            },
        };

        if (!headset)
        {
            grid.Children.Add(Cell(Right(Mono(
                station.UpdatedAt is { } at ? at.ToLocalTime().ToString("MM-dd", CultureInfo.InvariantCulture) : Dash,
                TypeScale.Small,
                ThemeManager.GreyKey)), 6));
        }

        var row = new Border { MinHeight = 54, Padding = new Thickness(12, 6), Child = grid, Classes = { RowClass } };
        return ListRow.Dress(row);
    }

    private static string LargestPad(StationSummary station) =>
        station.LargePads > 0 || station.HasLargePad ? "Large"
        : station.MediumPads > 0 ? "Medium"
        : station.SmallPads > 0 ? "Small"
        : Dash;

    private static TextBlock Wrapped(string text)
    {
        var block = Value(text);
        block.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        block.TextTrimming = Avalonia.Media.TextTrimming.None;
        return block;
    }

    private TextBox TextField(string name) => name switch
    {
        "near" => _near,
        "controlling_faction" => _controllingFaction,
        "module" => _module,
        "ship" => _ship,
        "max_station_distance" => _fromStar,
        "population from" => _populationFrom,
        "population to" => _populationTo,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "No such text field."),
    };

    private static string? Canonical(string name) =>
        StationQuery.ServiceNames.FirstOrDefault(known => string.Equals(known.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))?.Name;

    private static IEnumerable<string> Items(string? list) =>
        list?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];

    private static IReadOnlyList<string> Vocabulary(string name) => GalaxyFilters.Find(name)?.Choices ?? [];

    private static string? Selected(Stepper stepper) => stepper.SelectedIndex > 0 ? stepper.SelectedItem : null;

    private static Stepper Choice(IReadOnlyList<string> choices) =>
        new() { ItemsSource = [Any, .. choices], Wraps = false, MaxWidth = double.PositiveInfinity };

    private static CheckBox Tile(string name)
    {
        var tile = LabeledCheckBox.Caps(name);
        tile.MinHeight = TypeScale.MinimumTarget;
        return tile;
    }

    private const string Dash = "—";
}

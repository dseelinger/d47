using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Knowledge;
using static D47.App.Panel.SystemSearchPage;

namespace D47.App.Panel;

/// <summary>Search › Stations: stations that match, through <c>search_stations</c>.</summary>
public sealed class StationSearchPage : GalaxySearchPage
{
    public const string RootKey = "search.stations";

    public const string ChooseServicesName = "ChooseServices";

    /// <summary>Marks a chosen service's tile; pressing it removes the service.</summary>
    public const string ServiceClass = "service-tile";

    public const string ServicesKey = "search.stations.services";

    public const string ServicesHelp = "Stations that have every service you tick. Fleet carriers are left out unless the station type names them.";

    public const int DefaultDistance = 500;

    public const string HeadName = "StationHead";

    public const string TableId = "Search.Stations";

    private readonly Func<string?> _here;
    private readonly Action<StationSummary>? _open;
    private readonly Action<IReadOnlyList<string>, Action<IReadOnlyList<string>>> _chooseServices;

    private readonly TextBox _near = Field("near");
    private readonly SystemNameSuggestions<SystemNameMatch>? _nearNames;
    private readonly SystemNameSuggestions<string>? _controllingFactionNames;
    private readonly Amount _distance = new() { Value = DefaultDistance, Minimum = 1, Maximum = 99999, Step = 10, Format = "#,0" };
    private readonly MaxReportAge _age = new(72);
    private readonly Dictionary<string, CheckBox> _types = StationQuery.StationTypeNames.ToDictionary(name => name, name => Tile(name), StringComparer.Ordinal);
    private readonly Stepper _pad = Choice(StationQuery.MinPads);
    private readonly TextBox _minDistance = Field("minimum distance", mono: true);
    private readonly TextBox _fromStar = Field("from the star at most", mono: true);
    private readonly WrapPanel _serviceTiles = new() { ItemSpacing = Gaps.Tile, LineSpacing = Gaps.Tile };
    private readonly Stepper _trader = Choice(StationQuery.TraderTypes);
    private readonly Stepper _broker = Choice(StationQuery.BrokerTypes);
    private readonly TextBox _controllingFaction = Field("controlling faction");
    private readonly TextBox _module = Field("module");
    private readonly TextBox _ship = Field("ship");
    private readonly Stepper _powerState = Choice(GalaxyFilters.Find("power_state")?.Choices ?? []);
    private readonly MinimumPopulation _population = new(Field("minimum population", mono: true));
    private readonly CheckBox _colonised = LabeledCheckBox.Caps("Only colonised systems");

    private readonly Dictionary<string, (InlinePicker Picker, IReadOnlyList<string> Choices)> _pickers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _picked = new(StringComparer.Ordinal);

    private IReadOnlyList<string> _services = [];

    private readonly TextBlock _origin = Prose(string.Empty, TypeScale.Tip, ThemeManager.GreyKey);
    private IReadOnlyDictionary<string, string>? _originArguments;

    public StationSearchPage(
        GalaxySearchSurface searches,
        Func<string?> here,
        Action<IReadOnlyList<string>, Action<IReadOnlyList<string>>> chooseServices,
        Action<StationSummary>? open,
        Func<string, Task<bool>>? copy,
        bool headset)
        : base(searches, headset)
    {
        _here = here;
        _open = open;
        Copy = copy;
        _nearNames = SuggestNames(_near);
        _controllingFactionNames = SuggestFactions(_controllingFaction);
        _chooseServices = chooseServices;

        _colonised.MinHeight = TypeScale.MinimumTarget;
        _controllingFaction.PlaceholderText = "Exact name";
        _fromStar.PlaceholderText = Any;
        _fromStar.InnerRightContent = Label("LS", width: null);

        foreach (var (name, choices) in PickerChoices)
        {
            AddPicker(_pickers, _picked, name, choices, several: true);
        }

        DrawServices();
    }

    public override string Tool => "search_stations";

    protected override string SortTableId => TableId;

    public override GalaxySearchKind Kind => GalaxySearchKind.Stations;

    public override string Summary => "";

    protected override string Noun => "Stations";
    protected override string NounOne => "Station";

    private static IReadOnlyList<(string Name, IReadOnlyList<string> Choices)> PickerChoices { get; } =
    [
        .. new[] { "allegiance", "government", "station_economy", "station_state", "state", "primary_economy", "power", "acquiring_power" }
            .Select(name => (name, Vocabulary(name))),
        ("module_class", OutfittingCatalogue.Classes),
        ("module_rating", OutfittingCatalogue.Ratings),
        ("module_mount", OutfittingItem.MountNames),
    ];

    /// <summary>Sets a text field by its tool argument name.</summary>
    public void Type(string name, string value) => TextField(name).Text = value;

    /// <summary>Sets the services the form asks for, as CHOOSE SERVICES does.</summary>
    public void ChooseServices(IReadOnlyList<string> services)
    {
        _services = [.. services.Select(Canonical).OfType<string>().Distinct(StringComparer.Ordinal)];
        DrawServices();
    }

    /// <summary>
    /// Fills the form with a rare run's <paramref name="arguments"/> and runs them, saying where they came from
    /// for as long as the form still holds them.
    /// </summary>
    public void RunRareRun(IReadOnlyDictionary<string, string> arguments, string rare, bool powerOpen)
    {
        _origin.Text = $"Filled in from {rare}'s rare run."
                       + (powerOpen ? " You are not pledged to a Power, so Acquiring power is left open." : string.Empty);
        _originArguments = arguments;
        Run(arguments);
        Follow();
    }

    public override IReadOnlyDictionary<string, string> FormArguments()
    {
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal);

        PutNear(arguments, _near, _distance, DefaultDistance);
        Put(arguments, "min_distance", _minDistance.Text);

        Put(arguments, "station_type", string.Join(", ", StationQuery.StationTypeNames.Where(name => _types[name].IsChecked == true)));
        Put(arguments, "min_pad", Selected(_pad));
        Put(arguments, "max_station_distance", _fromStar.Text);
        Put(arguments, "services", string.Join(", ", _services));
        Put(arguments, "material_trader", Selected(_trader));
        Put(arguments, "technology_broker", Selected(_broker));
        Put(arguments, "module", _module.Text);
        Put(arguments, "ship", _ship.Text);
        Put(arguments, "controlling_faction", _controllingFaction.Text);
        Put(arguments, "power_state", Selected(_powerState));
        Put(arguments, "population", _population.Asked);

        foreach (var (name, value) in _picked)
        {
            Put(arguments, name, value);
        }

        if (_colonised.IsChecked == true)
        {
            arguments["colonised"] = "true";
        }

        if (_age.Asked is { } age)
        {
            arguments["max_report_age_hours"] = age;
        }

        return arguments;
    }

    public override void ClearForm()
    {
        foreach (var box in (TextBox[])[_near, _minDistance, _fromStar, _controllingFaction, _module, _ship])
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
        _age.Reset();
        _population.Set(null);
        _colonised.IsChecked = false;
        ChooseServices([]);

        ResetPickers(_pickers, _picked, null, several: true);
    }

    protected override void FillForm(IReadOnlyDictionary<string, string> arguments)
    {
        ClearForm();

        string? Get(string key) => arguments.TryGetValue(key, out var value) ? value : null;

        Fill(_near, _nearNames, Get("near") ?? string.Empty);
        _age.Set(Get("max_report_age_hours"));
        _minDistance.Text = Get("min_distance") ?? string.Empty;
        _fromStar.Text = Get("max_station_distance") ?? string.Empty;
        Fill(_controllingFaction, _controllingFactionNames, Get("controlling_faction") ?? string.Empty);
        _module.Text = Get("module") ?? string.Empty;
        _ship.Text = Get("ship") ?? string.Empty;
        _population.Set(Get("population"));
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

        FillDistance(arguments, _distance);

        ResetPickers(_pickers, _picked, arguments, several: true);
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
            ModuleMount = Get("module_mount"),
            Ship = Get("ship"),
            OrderBy = Get("order_by"),
            Order = Get("order"),
            Filters = filters,
        };

        return !StationQuery.TryParse(search, out _, out _);
    }

    protected override (string Lead, string Reference)? Aside(IReadOnlyDictionary<string, string> arguments, string? reference)
    {
        return WithinAside(arguments, reference, DefaultDistance);
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

    protected override int Total(GalaxySearchPosting posting) =>
        posting.Result is StationSearchResult result ? result.Total : 0;

    protected override bool Counted(GalaxySearchPosting posting) =>
        posting.Result is not StationSearchResult { Uncounted: true };

    protected override IReadOnlyList<object> Rows(GalaxySearchPosting posting) =>
        posting.Result is StationSearchResult result ? [.. result.Stations] : [];

    protected override PagedTable? Paged(GalaxySearchPosting posting, bool headset)
    {
        if (posting.Result is not StationSearchResult { Stations.Count: > 0 })
        {
            return null;
        }

        var table = Arrange(TableId, HeadName, string.Empty, posting, () => Fields(headset));

        return new PagedTable(ColumnHead(table), item => Row((StationSummary)item, table));
    }

    /// <summary>Every field the table can show; Updated is not shown by default in the headset.</summary>
    private IReadOnlyList<TableField<object>> Fields(bool headset) =>
    [
        SearchField("Station", new TableColumn("Station", ColumnFit.Fill) { Min = 120 }, "Station", row => NameCell((StationSummary)row), "name", "Type", filters: [SearchValues.One<StationSummary>("Type", station => station.Type, "No type reported")]),
        SearchField("System", new TableColumn("System", ColumnFit.Fill) { Min = 100 }, "System", row => SystemCell((StationSummary)row), "system", "Controlled by", filters: [SearchValues.One<StationSummary>("System", station => station.SystemName, "No system reported"), SearchValues.One<StationSummary>("Controlled by", ControlledBy, "No faction reported")]),
        SearchField("Distance", new TableColumn("Distance"), "LY", row => Centered(Right(Mono(Fixed(((StationSummary)row).Distance), TypeScale.Small, ThemeManager.AKey))), SearchSorts.Distance, right: true),
        SearchField("Pad", new TableColumn("Pad"), "Pad", row => Centered(Value(LargestPad((StationSummary)row))), second: "Largest", local: LocalSort.Figure<StationSummary>(station => LargestPadRank(station)), filters: [SearchValues.One<StationSummary>("Largest pad", PadValue, "No pad reported")]),
        SearchField("FromStar", new TableColumn("FromStar"), "From star", row => Centered(Right(Mono(Light(((StationSummary)row).DistanceToArrival), TypeScale.Small, ThemeManager.WhiteKey))), "from_star", right: true),
        SearchField("AskedFor", new TableColumn("AskedFor", ColumnFit.Fill) { Min = 100 }, "Asked for", row => Centered(Wrapped(string.Join(", ", Asked((StationSummary)row)))), local: LocalSort.Text<StationSummary>(station => string.Join(", ", Asked(station))), filters: [SearchValues.Many<StationSummary>("Asked for", station => AskedFor(station, Posted.Arguments), "None asked for")]),
        SearchField("Updated", new TableColumn("Updated"), "Updated", row => Centered(Right(Mono(Day(((StationSummary)row).UpdatedAt), TypeScale.Small, ThemeManager.GreyKey))), "updated", right: true, byDefault: !headset),
        SearchField("Pads", new TableColumn("Pads"), "Pads", row => PadsCell((StationSummary)row), byDefault: false, local: LocalSort.Figure<StationSummary>(station => (station.SmallPads ?? 0) + (station.MediumPads ?? 0) + (station.LargePads ?? 0))),
        SearchField("Services", new TableColumn("Services", ColumnFit.Fill) { Min = 140 }, "Services", row => TableCells.List(((StationSummary)row).Services), byDefault: false, local: LocalSort.Text<StationSummary>(station => string.Join(", ", station.Services)), filters: [SearchValues.Many<StationSummary>("Services", station => station.Services, "No services reported")]),
        SearchField("MaterialTrader", new TableColumn("MaterialTrader"), "Material trader", row => TableCells.Text(((StationSummary)row).TraderType), byDefault: false, local: LocalSort.Text<StationSummary>(station => station.TraderType), filters: [SearchValues.One<StationSummary>("Material trader", station => station.TraderType, "No material trader")]),
        SearchField("TechnologyBroker", new TableColumn("TechnologyBroker"), "Technology broker", row => TableCells.Text(((StationSummary)row).TechnologyBroker), byDefault: false, local: LocalSort.Text<StationSummary>(station => station.TechnologyBroker), filters: [SearchValues.One<StationSummary>("Technology broker", station => station.TechnologyBroker, "No technology broker")]),
        SearchField("StockSeen", new TableColumn("StockSeen"), "Stock seen", row => Centered(Right(Mono(Day(((StationSummary)row).StockLastSeen), TypeScale.Small, ThemeManager.GreyKey))), right: true, byDefault: false, local: LocalSort.When<StationSummary>(station => station.StockLastSeen)),
        SearchField("Power", new TableColumn("Power"), "Power", row => TableCells.Text(((StationSummary)row).ControllingPower), byDefault: false, local: LocalSort.Text<StationSummary>(station => station.ControllingPower), filters: [SearchValues.One<StationSummary>("Power", station => station.ControllingPower, "No Power reported")]),
        SearchField("PowerState", new TableColumn("PowerState"), "Power state", row => TableCells.Text(((StationSummary)row).PowerState), byDefault: false, local: LocalSort.Text<StationSummary>(station => station.PowerState), filters: [SearchValues.One<StationSummary>("Power state", station => station.PowerState, "No state reported")]),
    ];

    private IReadOnlyList<string> Asked(StationSummary station)
    {
        var asked = AskedFor(station, Posted.Arguments);

        return asked.Count == 0 ? [Dash] : asked;
    }

    private static Control NameCell(StationSummary station)
    {
        var name = Chrome(station.Name, TypeScale.Secondary, ThemeManager.AKey);
        name.Text = station.Name;

        return Centered(Pair(name, Sub(station.Type)));
    }

    /// <summary>Who the System cell's second line names: a stronghold carrier's Power, else the controlling faction.</summary>
    internal static string? ControlledBy(StationSummary station) =>
        station.IsStrongholdCarrier && station.ControllingPower is { Length: > 0 } carrierPower ? carrierPower : station.ControllingFaction;

    private Control SystemCell(StationSummary station)
    {
        var systemName = Value(station.SystemName);
        ThemeInk.Bind(systemName, TextBlock.ForegroundProperty, string.Equals(station.SystemName, _here(), StringComparison.OrdinalIgnoreCase) ? ThemeManager.CyanKey : ThemeManager.AKey);

        var system = station.IsStrongholdCarrier && station.ControllingPower is { Length: > 0 } carrierPower
            ? carrierPower
            : station.ControllingFaction is { } controlling
            ? station.ReportedState is { } state ? $"{controlling} · {state}" : controlling
            : Dash;

        return Centered(Pair(CopyGlyph.Beside(SystemLink.Look(systemName), station.SystemName, Copy), Sub(system)));
    }

    /// <summary>"S 2 · M 3 · L 1" for the sizes the service counts; a dash where it counts none.</summary>
    internal static Control PadsCell(StationSummary station)
    {
        var sizes = new[] { ("S", station.SmallPads), ("M", station.MediumPads), ("L", station.LargePads) }
            .Where(size => size.Item2 is not null)
            .Select(size => $"{size.Item1} {size.Item2!.Value.ToString(CultureInfo.InvariantCulture)}");

        return TableCells.Text(string.Join(" · ", sizes));
    }

    /// <summary>Light seconds with thousands separators, or a dash.</summary>
    internal static string Light(double? ls) => ls is { } value ? $"{value.ToString("N0", CultureInfo.InvariantCulture)} LS" : Dash;

    protected override Control BuildForm()
    {
        var form = new StackPanel { Spacing = 22 };

        _origin.IsVisible = false;
        form.Children.Add(_origin);

        form.Children.Add(Group(
            "Search out from",
            Labelled("Near", NearField(_near, _nearNames)),
            Labelled("Distance in LY", _distance),
            Labelled("At least this far in LY", _minDistance),
            _age.Field("Max report age")));

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
            Labelled("Controlling faction", Beneath(_controllingFaction, _controllingFactionNames))));

        form.Children.Add(Group(
            "Sold there",
            Labelled("Module", _module),
            Columns2(
                "*,*",
                Labelled("Class", _pickers["module_class"].Picker),
                Labelled("Rating", _pickers["module_rating"].Picker)),
            Labelled("Mount", _pickers["module_mount"].Picker),
            Labelled("Ship", _ship)));

        form.Children.Add(Group(
            "Its system",
            Labelled("Economy", _pickers["primary_economy"].Picker),
            Labelled("Power", _pickers["power"].Picker),
            Labelled("Acquiring power", _pickers["acquiring_power"].Picker),
            Labelled("Powerplay state", _powerState),
            Labelled("Minimum population", _population),
            _colonised));

        return form;
    }

    protected override object? Followed => _here();

    protected override void Follow()
    {
        ShowHere(_near, _here());

        _origin.IsVisible = _originArguments is { } filled
                            && FormArguments() is var now
                            && now.Count == filled.Count
                            && filled.All(pair => now.TryGetValue(pair.Key, out var value) && value == pair.Value);
    }

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

    private Control Row(StationSummary station, ArrangedTable<object> table)
    {
        var grid = table.Row(station);

        if (station.SystemAddress is null || _open is null)
        {
            var still = new Border { MinHeight = 54, Padding = new Thickness(12, 6), Child = grid, Classes = { RowClass } };
            return ListRow.Dress(still);
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
            Tag = station.Name,
        };
        Avalonia.Automation.AutomationProperties.SetName(row, $"{station.Name} in {station.SystemName}");

        var open = _open;
        row.Click += (_, _) => open(station);

        return ListRow.Dress(row);
    }

    internal static double? LargestPadRank(StationSummary station) =>
        station.LargePads > 0 || station.HasLargePad ? 3
        : station.MediumPads > 0 ? 2
        : station.SmallPads > 0 ? 1
        : null;

    /// <summary>"Large", "Medium" or "Small", the largest pad the station is known to have; null for none known.</summary>
    internal static string? PadValue(StationSummary station) =>
        station.LargePads > 0 || station.HasLargePad ? "Large"
        : station.MediumPads > 0 ? "Medium"
        : station.SmallPads > 0 ? "Small"
        : null;

    private static string LargestPad(StationSummary station) => PadValue(station) ?? Dash;

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
        "min_distance" => _minDistance,
        "minimum population" => _population.Box,
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

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Knowledge;
using static D47.App.Panel.SystemSearchPage;

namespace D47.App.Panel;

/// <summary>Search › Bodies: bodies that match, sorted from the column headers, through <c>find_body</c>.</summary>
public sealed class BodySearchPage : GalaxySearchPage
{
    public const string RootKey = "search.bodies";

    public const int DefaultDistance = 500;

    public const string HeadName = "BodyHead";

    public const string TableId = "Search.Bodies";

    private const string Dash = "—";

    private readonly Func<string?> _here;
    private readonly Action<BodySummary>? _open;

    private readonly TextBox _near = Field("near");
    private readonly SystemNameSuggestions<SystemNameMatch>? _nearNames;
    private readonly DistancePicker _distance = new(DefaultDistance, offersAny: true);
    private readonly CheckBox _landable = Tile("Landable");
    private readonly CheckBox _terraformable = Tile("Terraforming candidate");
    private readonly CheckBox _tidallyLocked = Tile("Tidally locked");
    private readonly CheckBox _ringsExactly = Tile("Only these ring types");
    private readonly TextBox _codexEntry = Field("codex entry");
    private readonly TextBox _volcanism = Field("volcanism");
    private readonly TextBox _atmosphere = Field("atmosphere");
    private readonly TextBox _gravityFrom = Field("gravity from", mono: true);
    private readonly TextBox _gravityTo = Field("gravity to", mono: true);
    private readonly TextBox _temperatureFrom = Field("temperature from", mono: true);
    private readonly TextBox _temperatureTo = Field("temperature to", mono: true);
    private readonly RangeReadout _gravityReadout;
    private readonly RangeReadout _temperatureReadout;
    private readonly TextBox _fromStar = Field("from the star at most", mono: true);
    private readonly TextBox _signalCount = Field("surface signal count", mono: true);
    private readonly TextBox _hotspotCount = Field("hotspot count", mono: true);

    private readonly Dictionary<string, (InlinePicker Picker, IReadOnlyList<string> Choices)> _pickers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _picked = new(StringComparer.Ordinal);

    private Dictionary<string, string> _carried = new(StringComparer.Ordinal);
    private Dictionary<string, string>? _carriedWith;

    public BodySearchPage(
        GalaxySearchSurface searches,
        Func<string?> here,
        Action<BodySummary>? open,
        Func<string, Task<bool>>? copy,
        bool headset)
        : base(searches, headset)
    {
        _here = here;
        _open = open;
        Copy = copy;
        _nearNames = SuggestNames(_near);

        _fromStar.PlaceholderText = Any;
        _fromStar.InnerRightContent = Label("LS", width: null);
        _gravityReadout = new RangeReadout(_gravityFrom, _gravityTo, (min, max) => RangeReadout.Measure(min, max, "Gravity", "G"));
        _temperatureReadout = new RangeReadout(_temperatureFrom, _temperatureTo, (min, max) => RangeReadout.Measure(min, max, "Temperature", "K"));
        _signalCount.PlaceholderText = Any;
        _hotspotCount.PlaceholderText = Any;
        _codexEntry.PlaceholderText = Any;
        _codexEntry.TextChanged += (_, _) => ShowDistanceDefault();
        _volcanism.PlaceholderText = Any;
        _atmosphere.PlaceholderText = Any;

        foreach (var (name, choices) in PickerChoices)
        {
            AddPicker(_pickers, _picked, name, choices, several: false);
        }

        _pickers["region"].Picker.Picked += (_, _) => ShowDistanceDefault();
        _pickers["body_type"].Picker.Picked += (_, _) => ShowDistanceDefault();
    }

    public override string Tool => "find_body";

    protected override string SortTableId => TableId;

    public override GalaxySearchKind Kind => GalaxySearchKind.Bodies;

    public override string Summary => "";

    protected override string Noun => "Bodies";
    protected override string NounOne => "Body";

    private static IReadOnlyList<(string Name, IReadOnlyList<string> Choices)> PickerChoices { get; } =
    [
        ("region", GalacticRegions.Names),
        ("body_type", BodyCatalogue.Subtypes),
        ("luminosity", BodyCatalogue.Luminosities),
        ("material", BodyCatalogue.SurfaceMaterials),
        ("signal", BodyCatalogue.Signals),
        ("hotspot", BodyCatalogue.RingSignals),
        ("ring_type", [.. BodyCatalogue.RingTypes, BodyCatalogue.NoRings]),
        ("reserve_level", BodyCatalogue.ReserveLevels),
        ("power", Choices("power")),
        ("power_state", Choices("power_state")),
    ];

    /// <summary>Sets a text field by its tool argument name.</summary>
    public void Type(string name, string value)
    {
        TextField(name).Text = value;
        ShowDistanceDefault();
    }

    /// <summary>
    /// The form's fields, and the arguments it has no field for — a ring set, or terraformable false — from the search
    /// that filled it, until a field is changed.
    /// </summary>
    public override IReadOnlyDictionary<string, string> FormArguments()
    {
        var arguments = Shown();

        if (_carriedWith is { } filled && Same(arguments, filled))
        {
            foreach (var (name, value) in _carried)
            {
                arguments.TryAdd(name, value);
            }
        }

        return arguments;
    }

    private static bool Same(IReadOnlyDictionary<string, string> one, IReadOnlyDictionary<string, string> other) =>
        one.Count == other.Count && one.All(pair => other.TryGetValue(pair.Key, out var value) && value == pair.Value);

    private Dictionary<string, string> Shown()
    {
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal);

        Put(arguments, "near", _near.Text);

        if (_distance.Argument(sendChosenDefault: true) is { } distance)
        {
            arguments["max_distance"] = distance;
        }

        foreach (var (name, value) in _picked)
        {
            Put(arguments, name, value);
        }

        Put(arguments, "signal_count", _signalCount.Text);
        Put(arguments, "hotspot_count", _hotspotCount.Text);
        Put(arguments, "codex_entry", _codexEntry.Text);
        Put(arguments, "volcanism", _volcanism.Text);
        Put(arguments, "atmosphere", _atmosphere.Text);
        Put(arguments, "gravity", Range(_gravityFrom.Text, _gravityTo.Text));
        Put(arguments, "temperature", Range(_temperatureFrom.Text, _temperatureTo.Text));
        Put(arguments, "max_arrival_distance", _fromStar.Text);

        foreach (var name in Flags)
        {
            if (FlagField(name).IsChecked == true)
            {
                arguments[name] = "true";
            }
        }

        return arguments;
    }

    public override void ClearForm()
    {
        foreach (var box in (TextBox[])[_near, _codexEntry, _volcanism, _atmosphere, _gravityFrom, _gravityTo, _temperatureFrom, _temperatureTo, _fromStar, _signalCount, _hotspotCount])
        {
            box.Text = string.Empty;
        }

        foreach (var name in Flags)
        {
            FlagField(name).IsChecked = false;
        }

        _distance.Reset();
        _carried = new(StringComparer.Ordinal);
        _carriedWith = null;

        ResetPickers(_pickers, _picked, null, several: false);
        ShowDistanceDefault();
    }

    protected override void FillForm(IReadOnlyDictionary<string, string> arguments)
    {
        ClearForm();

        string? Get(string key) => arguments.TryGetValue(key, out var value) ? value : null;

        Fill(_near, _nearNames, Get("near") ?? string.Empty);
        _codexEntry.Text = Get("codex_entry") ?? string.Empty;
        _volcanism.Text = Get("volcanism") ?? string.Empty;
        _atmosphere.Text = Get("atmosphere") ?? string.Empty;
        _signalCount.Text = Get("signal_count") ?? string.Empty;
        _hotspotCount.Text = Get("hotspot_count") ?? string.Empty;
        _fromStar.Text = Get("max_arrival_distance") ?? string.Empty;
        (_gravityFrom.Text, _gravityTo.Text) = Split(Get("gravity"));
        (_temperatureFrom.Text, _temperatureTo.Text) = Split(Get("temperature"));

        foreach (var name in Flags)
        {
            FlagField(name).IsChecked = bool.TryParse(Get(name), out var ticked) && ticked;
        }

        FillDistance(arguments, _distance);

        var shown = new Dictionary<string, string>(arguments, StringComparer.Ordinal);

        if (Get("region") is { } region && GalacticRegions.Match(region) is { } named)
        {
            shown["region"] = named;
        }

        ResetPickers(_pickers, _picked, shown, several: false);
        ShowDistanceDefault();

        var fields = Shown();
        _carried = Carried
            .Where(name => !string.IsNullOrWhiteSpace(Get(name)) && !fields.ContainsKey(name))
            .ToDictionary(name => name, name => Get(name)!, StringComparer.Ordinal);
        _carriedWith = _carried.Count > 0 ? fields : null;
    }

    /// <summary>The arguments a search can hold in a value the form has no field for.</summary>
    private static readonly string[] Carried = ["ring_type", "terraformable"];

    protected override bool Refusal(IReadOnlyDictionary<string, string> arguments)
    {
        string? Get(string key) => arguments.TryGetValue(key, out var value) ? value : null;
        int? Whole(string key) => int.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : null;
        bool? Flag(string key) => bool.TryParse(Get(key), out var value) ? value : null;

        var filters = GalaxyFilters.For(GalaxySearchKind.Bodies)
            .Where(filter => arguments.ContainsKey(filter.Name))
            .ToDictionary(filter => filter.Name, filter => arguments[filter.Name], StringComparer.Ordinal);

        var request = new BodyRequest
        {
            ReferenceSystem = Get("near"),
            Subtype = Get("body_type"),
            Signal = Get("signal"),
            SignalCount = Whole("signal_count"),
            RingSignal = Get("hotspot"),
            RingSignalCount = Whole("hotspot_count"),
            RingType = Get("ring_type"),
            RingsExactly = Flag("rings_exactly"),
            ReserveLevel = Get("reserve_level"),
            Landable = Flag("landable"),
            Terraformable = Flag("terraformable"),
            Luminosity = Get("luminosity"),
            CodexEntry = Get("codex_entry"),
            Region = Get("region"),
            Volcanism = Get("volcanism"),
            Atmosphere = Get("atmosphere"),
            TidallyLocked = Flag("tidally_locked"),
            Gravity = Get("gravity"),
            Temperature = Get("temperature"),
            MaxArrivalDistance = double.TryParse(Get("max_arrival_distance"), NumberStyles.Float, CultureInfo.InvariantCulture, out var furthest) ? furthest : null,
            Material = Get("material"),
            OrderBy = Get("order_by"),
            Order = Get("order"),
            Filters = filters,
        };

        return !BodyQuery.TryParse(request, out _, out _);
    }

    protected override (string Lead, string Reference)? Aside(IReadOnlyDictionary<string, string> arguments, string? reference)
    {
        if (!arguments.ContainsKey("max_distance")
            && (Text(arguments, "codex_entry") is not null || (Text(arguments, "region") is not null && Text(arguments, "body_type") is not null)))
        {
            return ("NEAREST TO ", AsideFrom(arguments, reference));
        }

        return WithinAside(arguments, reference, DefaultDistance);
    }

    private static string? Text(IReadOnlyDictionary<string, string> arguments, string key) =>
        arguments.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    /// <summary>With no distance chosen, the dropdown shows what the search will use: Any for a codex entry or a body type in a region, else 500.</summary>
    private void ShowDistanceDefault() =>
        _distance.ShowDefault(any: !string.IsNullOrWhiteSpace(_codexEntry.Text) || (_picked.ContainsKey("region") && _picked.ContainsKey("body_type")));

    protected override int Count(GalaxySearchPosting posting) =>
        posting.Result is BodySearchResult result ? result.Bodies.Count : 0;

    /// <summary>The material the answer is ordered by, when the search asked to order by one.</summary>
    public static string? RichestIn(IReadOnlyDictionary<string, string> asked) =>
        string.Equals(asked.GetValueOrDefault("order_by"), "material", StringComparison.OrdinalIgnoreCase)
            ? Material(asked)
            : null;

    private static string? Material(IReadOnlyDictionary<string, string> asked) =>
        asked.TryGetValue("material", out var spoken) && !string.IsNullOrWhiteSpace(spoken)
            ? BodyCatalogue.MatchSurfaceMaterial(spoken) ?? spoken.Trim()
            : null;

    protected override int Total(GalaxySearchPosting posting) =>
        posting.Result is BodySearchResult result ? result.Total : 0;

    protected override IReadOnlyList<object> Rows(GalaxySearchPosting posting) =>
        posting.Result is BodySearchResult result ? [.. result.Bodies] : [];

    /// <summary>Without <c>limit</c>, except that an order by material shows every body it chooses from.</summary>
    protected override IReadOnlyDictionary<string, string> Sent(IReadOnlyDictionary<string, string> arguments)
    {
        var sent = new Dictionary<string, string>(base.Sent(arguments), StringComparer.Ordinal);

        if (string.Equals(sent.GetValueOrDefault("order_by"), SearchSorts.Material, StringComparison.OrdinalIgnoreCase))
        {
            sent["limit"] = BodyQuery.RichestOf.ToString(CultureInfo.InvariantCulture);
        }

        return sent;
    }

    protected override PagedTable? Paged(GalaxySearchPosting posting, bool headset)
    {
        if (posting.Result is not BodySearchResult { Bodies.Count: > 0 })
        {
            return null;
        }

        var material = Material(posting.Arguments);
        var table = Arrange(TableId, HeadName, material ?? string.Empty, posting, () => Fields(headset, material));

        var head = new StackPanel { Spacing = 8 };

        if (RichestIn(posting.Arguments) is { } richest)
        {
            head.Children.Add(Prose(
                $"The richest in {richest} among the nearest {BodyQuery.RichestOf} that carry it.",
                TypeScale.Tip,
                ThemeManager.GreyKey));
        }

        head.Children.Add(ColumnHead(table));

        return new PagedTable(head, item => Row((BodySummary)item, table));
    }

    /// <summary>
    /// Every field the table can show. The asked-for material's Share column is required while the material is asked
    /// for; Gravity is not shown by default in the headset.
    /// </summary>
    private IReadOnlyList<TableField<object>> Fields(bool headset, string? material)
    {
        List<TableField<object>> fields =
        [
            SearchField("Body", new TableColumn("Body", ColumnFit.Fill) { Min = 120 }, "Body", row => Centered(TableCells.Wraps(Chrome(((BodySummary)row).Name, TypeScale.Secondary, ThemeManager.AKey))), "name"),
            SearchField("Type", new TableColumn("Type", ColumnFit.Fill) { Min = 120 }, "Type", row => TypeCell((BodySummary)row), local: LocalSort.Text<BodySummary>(body => body.Subtype), filters: [SearchValues.One<BodySummary>("Type", body => body.Subtype, "No type reported")]),
            SearchField("System", new TableColumn("System", ColumnFit.Fill) { Min = 100 }, "System", row => SystemCell((BodySummary)row), "system", filters: [SearchValues.One<BodySummary>("System", body => body.SystemName, "No system reported")]),
            SearchField("Distance", new TableColumn("Distance"), "LY", row => Centered(Right(Mono(Fixed(((BodySummary)row).Distance), TypeScale.Small, ThemeManager.AKey))), SearchSorts.Distance, right: true),
            SearchField("FromStar", new TableColumn("FromStar"), "From star", row => Centered(Right(Mono(StationSearchPage.Light(((BodySummary)row).DistanceToArrival), TypeScale.Small, ThemeManager.WhiteKey))), "from_star", right: true),
            SearchField("Landable", new TableColumn("Landable"), "Landable", row => Centered(Value(((BodySummary)row).IsLandable ? "Yes" : "No")), local: LocalSort.Flag<BodySummary>(body => body.IsLandable), filters: [SearchValues.Flag<BodySummary>("Landable", body => body.IsLandable, "Not known")]),
        ];

        if (material is not null)
        {
            fields.Add(SearchField(
                "Share",
                new TableColumn("Share"),
                material,
                row => Centered(Right(Mono(
                    ((BodySummary)row).Share(material) is { } share ? $"{share.ToString("F2", CultureInfo.InvariantCulture)}%" : Dash,
                    TypeScale.Small,
                    ThemeManager.AKey))),
                SearchSorts.Material,
                "Share",
                right: true) with { Required = true });
        }

        fields.AddRange(
        [
            SearchField(
                "Gravity",
                new TableColumn("Gravity"),
                "Gravity",
                row => Centered(Right(Mono(
                    ((BodySummary)row).Gravity is { } g ? $"{g.ToString("0.00", CultureInfo.InvariantCulture)} g" : Dash,
                    TypeScale.Small,
                    ThemeManager.GreyKey))),
                "gravity",
                right: true,
                byDefault: !headset),
            SearchField("Atmosphere", new TableColumn("Atmosphere"), "Atmosphere", row => TableCells.Text(((BodySummary)row).Atmosphere), byDefault: false, local: LocalSort.Text<BodySummary>(body => body.Atmosphere), filters: [SearchValues.One<BodySummary>("Atmosphere", body => body.Atmosphere, "No atmosphere reported")]),
            SearchField("MappingValue", new TableColumn("MappingValue"), "Mapping value", row => TableCells.Number(((BodySummary)row).MappingValue), right: true, byDefault: false, local: LocalSort.Figure<BodySummary>(body => body.MappingValue)),
            SearchField("Reserves", new TableColumn("Reserves"), "Reserves", row => TableCells.Text(((BodySummary)row).ReserveLevel), byDefault: false, local: LocalSort.Text<BodySummary>(body => body.ReserveLevel), filters: [SearchValues.One<BodySummary>("Reserves", body => body.ReserveLevel, "No reserves reported")]),
            SearchField("Rings", new TableColumn("Rings", ColumnFit.Fill) { Min = 120 }, "Rings", row => TableCells.List([.. ((BodySummary)row).Rings.Select(ring => ring.Name)]), byDefault: false, local: LocalSort.Text<BodySummary>(body => string.Join(", ", body.Rings.Select(ring => ring.Name))), filters: [SearchValues.Many<BodySummary>("Rings", body => body.Rings.Select(ring => ring.Type ?? RingTypeNotKnown), "No rings")]),
            SearchField("Signals", new TableColumn("Signals", ColumnFit.Fill) { Min = 120 }, "Signals", row => TableCells.List([.. ((BodySummary)row).Signals.Select(signal => $"{signal.Kind} {signal.Count.ToString(CultureInfo.InvariantCulture)}")]), byDefault: false, local: LocalSort.Text<BodySummary>(body => string.Join(", ", body.Signals.Select(signal => signal.Kind))), filters: [SearchValues.Many<BodySummary>("Signals", body => body.Signals.Select(signal => signal.Kind), "No signals")]),
            SearchField(
                "SurfaceTemperature",
                new TableColumn("SurfaceTemperature"),
                "Surface temperature",
                row => Centered(Right(Mono(
                    ((BodySummary)row).SurfaceTemperature is { } kelvin ? $"{kelvin.ToString("N0", CultureInfo.InvariantCulture)} K" : Dash,
                    TypeScale.Small,
                    SurfaceHeat.IsHostile(((BodySummary)row).SurfaceTemperature, ((BodySummary)row).IsLandable) ? ThemeManager.RedKey : ThemeManager.WhiteKey))),
                right: true,
                byDefault: false,
                local: LocalSort.Figure<BodySummary>(body => body.SurfaceTemperature)),
            SearchField("Terraforming", new TableColumn("Terraforming"), "Terraforming", row => TableCells.Text(((BodySummary)row).TerraformingState), byDefault: false, local: LocalSort.Text<BodySummary>(body => body.TerraformingState), filters: [SearchValues.One<BodySummary>("Terraforming", body => body.TerraformingState, "No terraforming reported")]),
            SearchField("TidallyLocked", new TableColumn("TidallyLocked"), "Tidally locked", row => TableCells.Flag(((BodySummary)row).IsTidallyLocked), byDefault: false, local: LocalSort.Flag<BodySummary>(body => body.IsTidallyLocked), filters: [SearchValues.Flag<BodySummary>("Tidally locked", body => body.IsTidallyLocked, "Not known")]),
            SearchField("Volcanism", new TableColumn("Volcanism"), "Volcanism", row => TableCells.Text(((BodySummary)row).Volcanism), byDefault: false, local: LocalSort.Text<BodySummary>(body => body.Volcanism), filters: [SearchValues.One<BodySummary>("Volcanism", body => body.Volcanism, "No volcanism reported")]),
        ]);

        return fields;
    }

    /// <summary>The Rings filter's entry for a ring whose type is not reported.</summary>
    private const string RingTypeNotKnown = "Type not known";

    /// <summary>The type, with TYPE UNCONFIRMED under it when the galaxy record's class has no subclass.</summary>
    internal static Control TypeCell(BodySummary body)
    {
        var type = string.IsNullOrWhiteSpace(body.Subtype)
            ? TableCells.Missing()
            : Typeset.Text(body.Subtype, TypeScale.BodyType, ThemeManager.WhiteKey, wrap: true, vertical: VerticalAlignment.Center);

        if (!body.TypeUnconfirmed)
        {
            return type;
        }

        var mark = Chrome("Type unconfirmed", TypeScale.MetaSmall, ThemeManager.GreyKey);
        mark.TextWrapping = TextWrapping.Wrap;
        mark.TextTrimming = TextTrimming.None;

        return new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { type, mark } };
    }

    private Control SystemCell(BodySummary body)
    {
        var system = Value(body.SystemName);
        ThemeInk.Bind(system, TextBlock.ForegroundProperty, string.Equals(body.SystemName, _here(), StringComparison.OrdinalIgnoreCase) ? ThemeManager.CyanKey : ThemeManager.AKey);

        return Centered(CopyGlyph.Beside(SystemLink.Look(system), body.SystemName, Copy));
    }

    protected override Control BuildForm()
    {
        var form = new StackPanel { Spacing = 22 };

        form.Children.Add(Group(
            "Search out from",
            Labelled("Near", NearField(_near, _nearNames)),
            Labelled("Distance in LY", _distance.Picker),
            Labelled("Region", _pickers["region"].Picker)));

        var flags = new WrapPanel { ItemSpacing = Gaps.Tile, LineSpacing = Gaps.Tile, Children = { _landable, _terraformable, _tidallyLocked } };

        form.Children.Add(Group(
            "The body",
            Labelled("Type", _pickers["body_type"].Picker),
            Labelled("Luminosity", _pickers["luminosity"].Picker),
            Labelled("Surface material", _pickers["material"].Picker),
            flags,
            Labelled("Codex entry", _codexEntry),
            Columns2("*,*", Labelled("Volcanism", _volcanism), Labelled("Atmosphere", _atmosphere)),
            Labelled("Gravity in G", Span(_gravityFrom, _gravityTo, _gravityReadout)),
            Labelled("Temperature in K", Span(_temperatureFrom, _temperatureTo, _temperatureReadout)),
            Labelled("From the star at most", _fromStar)));

        form.Children.Add(Group(
            "Signals and rings",
            Labelled("Surface signal", _pickers["signal"].Picker),
            Labelled("Exactly how many", _signalCount),
            Labelled("Ring hotspot", _pickers["hotspot"].Picker),
            Labelled("Exactly how many", _hotspotCount),
            Labelled("Ring type", _pickers["ring_type"].Picker),
            _ringsExactly,
            Labelled("Reserves", _pickers["reserve_level"].Picker)));

        form.Children.Add(Group(
            "Its system",
            Labelled("Power", _pickers["power"].Picker),
            Labelled("Powerplay state", _pickers["power_state"].Picker)));

        return form;
    }

    protected override object? Followed => _here();

    protected override void Follow() =>
        ShowHere(_near, _here());

    private static readonly string[] Flags = ["landable", "terraformable", "tidally_locked", "rings_exactly"];

    private Control Row(BodySummary body, ArrangedTable<object> table)
    {
        var grid = table.Row(body);

        if (body.SystemAddress is null || _open is null)
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
            Tag = body.Name,
        };
        Avalonia.Automation.AutomationProperties.SetName(row, $"{body.Name} in {body.SystemName}");

        var open = _open;
        row.Click += (_, _) => open(body);

        return ListRow.Dress(row);
    }

    private TextBox TextField(string name) => name switch
    {
        "near" => _near,
        "codex_entry" => _codexEntry,
        "volcanism" => _volcanism,
        "atmosphere" => _atmosphere,
        "signal_count" => _signalCount,
        "hotspot_count" => _hotspotCount,
        "max_arrival_distance" => _fromStar,
        "gravity from" => _gravityFrom,
        "gravity to" => _gravityTo,
        "temperature from" => _temperatureFrom,
        "temperature to" => _temperatureTo,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "No such text field."),
    };

    private CheckBox FlagField(string name) => name switch
    {
        "landable" => _landable,
        "terraformable" => _terraformable,
        "tidally_locked" => _tidallyLocked,
        "rings_exactly" => _ringsExactly,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "No such flag."),
    };

    private static CheckBox Tile(string name)
    {
        var tile = LabeledCheckBox.Caps(name);
        tile.MinHeight = TypeScale.MinimumTarget;
        return tile;
    }
}

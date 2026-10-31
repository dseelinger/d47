using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Knowledge;
using static D47.App.Panel.SystemSearchPage;

namespace D47.App.Panel;

/// <summary>Search › Bodies: bodies that match, sorted from the column headers, through <c>find_body</c>.</summary>
public sealed class BodySearchPage : GalaxySearchPage
{
    public const string RootKey = "search.bodies";

    public const int DefaultDistance = 50;

    public const int DefaultLimit = ShowUpTo.Default;

    public const string HeadName = "BodyHead";

    public const string TableId = "Search.Bodies";

    private const string Dash = "—";

    private readonly Func<string?> _here;

    private readonly TextBox _near = Field("near");
    private readonly SystemNameSuggestions<SystemNameMatch>? _nearNames;
    private readonly Amount _distance = new() { Value = DefaultDistance, Minimum = 1, Maximum = (decimal)BodyQuery.MaxDistanceCeiling, Step = 10, Format = "#,0" };
    private readonly ShowUpTo _limit = new();
    private readonly CheckBox _landable = Tile("Landable");
    private readonly CheckBox _terraformable = Tile("Terraforming candidate");
    private readonly CheckBox _tidallyLocked = Tile("Tidally locked");
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

    public BodySearchPage(
        GalaxySearchSurface searches,
        Func<bool> lookupsEnabled,
        Func<string?> here,
        Action? openSettings,
        bool headset)
        : base(searches, lookupsEnabled, openSettings, headset)
    {
        _here = here;
        _nearNames = SuggestNames(_near);

        _fromStar.PlaceholderText = Any;
        _fromStar.InnerRightContent = Label("LS", width: null);
        _gravityReadout = new RangeReadout(_gravityFrom, _gravityTo, (min, max) => RangeReadout.Measure(min, max, "Gravity", "G"));
        _temperatureReadout = new RangeReadout(_temperatureFrom, _temperatureTo, (min, max) => RangeReadout.Measure(min, max, "Temperature", "K"));
        _signalCount.PlaceholderText = Any;
        _hotspotCount.PlaceholderText = Any;
        _codexEntry.PlaceholderText = Any;
        _volcanism.PlaceholderText = Any;
        _atmosphere.PlaceholderText = Any;

        foreach (var (name, choices) in PickerChoices)
        {
            AddPicker(_pickers, _picked, name, choices, several: false);
        }
    }

    public override string Tool => "find_body";

    public override GalaxySearchKind Kind => GalaxySearchKind.Bodies;

    public override string Summary => "";

    protected override string Noun => "Bodies";
    protected override string NounOne => "Body";

    private static IReadOnlyList<(string Name, IReadOnlyList<string> Choices)> PickerChoices { get; } =
    [
        ("body_type", BodyCatalogue.Subtypes),
        ("material", BodyCatalogue.SurfaceMaterials),
        ("signal", BodyCatalogue.Signals),
        ("hotspot", BodyCatalogue.RingSignals),
        ("ring_type", BodyCatalogue.RingTypes),
        ("reserve_level", BodyCatalogue.ReserveLevels),
        ("power", Choices("power")),
        ("power_state", Choices("power_state")),
    ];

    /// <summary>Sets a dropdown field to <paramref name="value"/>, or to Any for a value it does not offer.</summary>
    public void Pick(string name, string value) => ShowPick(_pickers, _picked, name, value, several: false);

    /// <summary>The count SHOW UP TO shows.</summary>
    public int Limit
    {
        get => _limit.Value;
        set => _limit.Value = value;
    }

    /// <summary>Sets a text field by its tool argument name.</summary>
    public void Type(string name, string value) => TextField(name).Text = value;

    /// <summary>Ticks or clears a flag by its tool argument name.</summary>
    public void TickFlag(string name, bool ticked) => FlagField(name).IsChecked = ticked;

    public override IReadOnlyDictionary<string, string> FormArguments()
    {
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal);

        PutNear(arguments, _near, _distance, DefaultDistance);

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

        PutLimit(arguments, _limit, DefaultLimit);

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

        _distance.Value = DefaultDistance;
        _limit.Value = DefaultLimit;

        ResetPickers(_pickers, _picked, null, several: false);
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

        FillDistance(arguments, _distance, BodyQuery.MaxDistanceCeiling);

        FillLimit(arguments, _limit);

        ResetPickers(_pickers, _picked, arguments, several: false);
    }

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
            ReserveLevel = Get("reserve_level"),
            Landable = Flag("landable"),
            Terraformable = Flag("terraformable"),
            CodexEntry = Get("codex_entry"),
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
        return WithinAside(arguments, reference, DefaultDistance);
    }

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

    protected override Control? Table(GalaxySearchPosting posting, bool headset)
    {
        if (posting.Result is not BodySearchResult { Bodies.Count: > 0 } result)
        {
            return null;
        }

        var material = Material(posting.Arguments);
        var shareColumn = material is null ? -1 : 5;
        var gravityColumn = headset ? -1 : shareColumn < 0 ? 5 : 6;

        List<SearchColumn> heads =
        [
            new("Body", "Type", "name", false),
            new("System", null, "system", false),
            new("LY", null, SearchSorts.Distance, true),
            new("From star", null, "from_star", true),
            new("Landable", null, null, false),
        ];

        if (shareColumn >= 0)
        {
            heads.Add(new(material!, "Share", SearchSorts.Material, true));
        }

        if (gravityColumn >= 0)
        {
            heads.Add(new("Gravity", null, "gravity", true));
        }

        List<TableColumn> shown =
        [
            new("Body", ColumnFit.Fill) { Min = 120 },
            new("System", ColumnFit.Fill) { Min = 100 },
            new("Distance"),
            new("FromStar"),
            new("Landable"),
        ];

        if (shareColumn >= 0)
        {
            shown.Add(new("Share"));
        }

        if (gravityColumn >= 0)
        {
            shown.Add(new("Gravity"));
        }

        var columns = NewTable(TableId, shown);
        var rule = SearchHead(posting, HeadName, columns, heads);

        var rows = new StackPanel { Spacing = Gaps.Tile, Margin = new Thickness(0, 2, 0, 0) };

        foreach (var body in result.Bodies)
        {
            rows.Children.Add(Row(body, material, columns, shareColumn, gravityColumn));
        }

        var table = new StackPanel { Spacing = 8 };

        if (RichestIn(posting.Arguments) is { } richest)
        {
            table.Children.Add(Prose(
                $"The richest in {richest} among the nearest {BodyQuery.RichestOf} that carry it.",
                TypeScale.Tip,
                ThemeManager.GreyKey));
        }

        table.Children.Add(rule);
        table.Children.Add(rows);
        return table;
    }

    protected override Control BuildForm()
    {
        var form = new StackPanel { Spacing = 22 };

        form.Children.Add(Group(
            "Search out from",
            Labelled("Near", NearField(_near, _nearNames)),
            Labelled("Distance in LY", _distance),
            _limit.Field("Bodies")));

        var flags = new WrapPanel { ItemSpacing = Gaps.Tile, LineSpacing = Gaps.Tile, Children = { _landable, _terraformable, _tidallyLocked } };

        form.Children.Add(Group(
            "The body",
            Labelled("Type", _pickers["body_type"].Picker),
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

    private static readonly string[] Flags = ["landable", "terraformable", "tidally_locked"];

    private Control Row(BodySummary body, string? material, TableLayout columns, int shareColumn, int gravityColumn)
    {
        var name = Chrome(body.Name, TypeScale.Secondary, ThemeManager.AKey);

        var system = Value(body.SystemName);
        ThemeInk.Bind(system, TextBlock.ForegroundProperty, string.Equals(body.SystemName, _here(), StringComparison.OrdinalIgnoreCase) ? ThemeManager.CyanKey : ThemeManager.AKey);

        List<Control> cells =
        [
                Cell(Pair(name, Sub(body.Subtype)), 0),
                Cell(system, 1),
                Cell(Right(Mono(body.Distance is { } ly ? ly.ToString("F2", CultureInfo.InvariantCulture) : Dash, TypeScale.Small, ThemeManager.AKey)), 2),
                Cell(Right(Mono(body.DistanceToArrival is { } ls ? $"{ls.ToString("N0", CultureInfo.InvariantCulture)} LS" : Dash, TypeScale.Small, ThemeManager.WhiteKey)), 3),
                Cell(Value(body.IsLandable ? "Yes" : "No"), 4),
        ];

        if (shareColumn >= 0)
        {
            cells.Add(Cell(Right(Mono(
                body.Share(material!) is { } share ? $"{share.ToString("F2", CultureInfo.InvariantCulture)}%" : Dash,
                TypeScale.Small,
                ThemeManager.AKey)), shareColumn));
        }

        if (gravityColumn >= 0)
        {
            cells.Add(Cell(Right(Mono(
                body.Gravity is { } g ? $"{g.ToString("0.00", CultureInfo.InvariantCulture)} g" : Dash,
                TypeScale.Small,
                ThemeManager.GreyKey)), gravityColumn));
        }

        var row = new Border { MinHeight = 54, Padding = new Thickness(12, 6), Child = columns.Row(cells), Classes = { RowClass } };
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
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "No such flag."),
    };

    private static CheckBox Tile(string name)
    {
        var tile = LabeledCheckBox.Caps(name);
        tile.MinHeight = TypeScale.MinimumTarget;
        return tile;
    }
}

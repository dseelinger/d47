using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>Search › Systems: star systems that match, nearest first, through <c>search_systems</c>.</summary>
public sealed class SystemSearchPage : GalaxySearchPage
{
    public const string RootKey = "search.systems";

    public const string Any = "Any";

    /// <summary>The table's columns: system, light years, allegiance, economy, population, power, reported.</summary>
    public const string HeadName = "SystemHead";

    public const string TableId = "Search.Systems";

    private const string Dash = "—";

    private readonly Func<string?> _here;
    private readonly Action<SystemSummary>? _open;

    private readonly TextBox _near = Field("near");
    private readonly SystemNameSuggestions<SystemNameMatch>? _nearNames;
    private readonly SystemNameSuggestions<string>? _controllingFactionNames;
    private readonly SystemNameSuggestions<string>? _factionNames;
    private readonly TextBox _distanceFrom = Field("distance from", mono: true);
    private readonly TextBox _distanceTo = Field("distance to", mono: true);
    private readonly MaxReportAge _age = new();
    private readonly RangeReadout _distanceReadout;

    private readonly MinimumPopulation _population = new(Field("minimum population", mono: true));
    private readonly CheckBox _colonised = LabeledCheckBox.Caps("Only colonised systems");
    private readonly TextBox _controllingFaction = Field("controlling faction");
    private readonly TextBox _faction = Field("a faction present");
    private readonly Stepper _powerState;

    private readonly Dictionary<string, (InlinePicker Picker, IReadOnlyList<string> Choices)> _pickers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _picked = new(StringComparer.Ordinal);

    public SystemSearchPage(
        GalaxySearchSurface searches,
        Func<string?> here,
        Action<SystemSummary>? open,
        Func<string, Task<bool>>? copy,
        bool headset)
        : base(searches, headset)
    {
        _here = here;
        Copy = copy;
        _nearNames = SuggestNames(_near);
        _controllingFactionNames = SuggestFactions(_controllingFaction);
        _factionNames = SuggestFactions(_faction);
        _open = open;

        _powerState = new Stepper
        {
            ItemsSource = [Any, .. Choices("power_state")],
            Wraps = false,
            MaxWidth = double.PositiveInfinity,
        };

        _colonised.MinHeight = TypeScale.MinimumTarget;
        _controllingFaction.PlaceholderText = "Exact name";
        _distanceReadout = new RangeReadout(_distanceFrom, _distanceTo, (min, max) => RangeReadout.Distance(min, max, Reference()));
        _near.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                _distanceReadout.Update();
            }
        };

        foreach (var name in (string[])["allegiance", "government", "primary_economy", "security", "state", "faction_state", "faction_government", "power", "acquiring_power"])
        {
            AddPicker(_pickers, _picked, name, Choices(name), several: true);
        }
    }

    public override string Tool => "search_systems";

    protected override string SortTableId => TableId;

    public override GalaxySearchKind Kind => GalaxySearchKind.Systems;

    public override string Summary => "";

    protected override string Noun => "Systems";
    protected override string NounOne => "System";

    /// <summary>Sets a text field by its tool argument name.</summary>
    public void Type(string name, string value) => TextField(name).Text = value;

    public override IReadOnlyDictionary<string, string> FormArguments()
    {
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal);

        Put(arguments, "near", _near.Text);
        Put(arguments, "distance", Range(_distanceFrom.Text, _distanceTo.Text));
        Put(arguments, "population", _population.Asked);
        Put(arguments, "controlling_faction", _controllingFaction.Text);
        Put(arguments, "faction", _faction.Text);
        Put(arguments, "power_state", _powerState.SelectedIndex > 0 ? _powerState.SelectedItem : null);

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
        foreach (var box in (TextBox[])[_near, _distanceFrom, _distanceTo, _controllingFaction, _faction])
        {
            box.Text = string.Empty;
        }

        _population.Set(null);
        _age.Reset();
        _powerState.SelectedIndex = 0;
        _colonised.IsChecked = false;

        ResetPickers(_pickers, _picked, null, several: true);
    }

    protected override void FillForm(IReadOnlyDictionary<string, string> arguments)
    {
        ClearForm();

        string? Get(string key) => arguments.TryGetValue(key, out var value) ? value : null;

        Fill(_near, _nearNames, Get("near") ?? string.Empty);
        (_distanceFrom.Text, _distanceTo.Text) = Split(Get("distance"));
        _population.Set(Get("population"));
        Fill(_controllingFaction, _controllingFactionNames, Get("controlling_faction") ?? string.Empty);
        Fill(_faction, _factionNames, Get("faction") ?? string.Empty);
        _age.Set(Get("max_report_age_hours"));
        _colonised.IsChecked = bool.TryParse(Get("colonised"), out var colonised) && colonised;
        _powerState.SelectedIndex = Index(_powerState.ItemsSource, Get("power_state"));

        ResetPickers(_pickers, _picked, arguments, several: true);
    }

    protected override bool Refusal(IReadOnlyDictionary<string, string> arguments)
    {
        var requested = arguments
            .Where(pair => pair.Key is not ("near" or "limit" or "order_by" or "order"))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        return !GalaxyCriteria.TryParse(GalaxySearchKind.Systems, requested, out _, out _)
               || !SearchSorts.TryParse(GalaxySearchKind.Systems, arguments.GetValueOrDefault("order_by"), arguments.GetValueOrDefault("order"), out _, out _);
    }

    protected override (string Lead, string Reference)? Aside(IReadOnlyDictionary<string, string> arguments, string? reference)
    {
        if (!arguments.TryGetValue("distance", out var distance)
            || !GalaxyCriteria.TryParseRange(distance, out var min, out var max))
        {
            return null;
        }

        var from = AsideFrom(arguments, reference);

        var lead = (min, max) switch
        {
            ({ } low, { } high) => $"{Number(low)}–{Number(high)} LY FROM ",
            (null, { } high) => $"WITHIN {Number(high)} LY OF ",
            ({ } low, null) => $"BEYOND {Number(low)} LY OF ",
            _ => null,
        };

        return lead is null ? null : (lead, from);
    }

    protected override int Count(GalaxySearchPosting posting) =>
        posting.Result is GalaxySearchResult result ? result.Systems.Count : 0;

    protected override int Total(GalaxySearchPosting posting) =>
        posting.Result is GalaxySearchResult result ? result.Total : 0;

    protected override IReadOnlyList<object> Rows(GalaxySearchPosting posting) =>
        posting.Result is GalaxySearchResult result ? [.. result.Systems] : [];

    protected override PagedTable? Paged(GalaxySearchPosting posting, bool headset)
    {
        if (posting.Result is not GalaxySearchResult { Systems.Count: > 0 })
        {
            return null;
        }

        var table = Arrange(TableId, HeadName, string.Empty, posting, () => Fields(headset));

        return new PagedTable(SearchTableFields.Head(table), item => Row((SystemSummary)item, table));
    }

    /// <summary>Every field the table can show; Reported is not shown by default in the headset.</summary>
    private IReadOnlyList<TableField<object>> Fields(bool headset) =>
    [
        SearchField("System", new TableColumn("System", ColumnFit.Fill) { Min = 120 }, "System", row => SystemCell((SystemSummary)row), "name", "Controlled by"),
        SearchField("Distance", new TableColumn("Distance"), "LY", row => Centered(Right(Mono(Fixed(((SystemSummary)row).Distance), TypeScale.Small, ThemeManager.AKey))), SearchSorts.Distance, right: true),
        SearchField("Allegiance", new TableColumn("Allegiance"), "Allegiance", row => Centered(Pair(Value(((SystemSummary)row).Allegiance), Sub(((SystemSummary)row).Government))), "allegiance", "Government"),
        SearchField("Economy", new TableColumn("Economy"), "Economy", row => Centered(Pair(Value(((SystemSummary)row).PrimaryEconomy), Sub(((SystemSummary)row).Security))), "economy", "Security"),
        SearchField(
            "Population",
            new TableColumn("Population"),
            "Population",
            row => Centered(Right(Mono(((SystemSummary)row).Population is { } people ? people.ToString("N0", CultureInfo.InvariantCulture) : Dash, TypeScale.Small, ThemeManager.WhiteKey))),
            "population",
            right: true),
        SearchField("Power", new TableColumn("Power"), "Power", row => Centered(PowerCell((SystemSummary)row)), "power", "State"),
        SearchField(
            "Reported",
            new TableColumn("Reported"),
            "Reported",
            row => Centered(Right(Mono(Day(((SystemSummary)row).ReportedAt), TypeScale.Small, ThemeManager.GreyKey))),
            "updated",
            right: true,
            byDefault: !headset),
        SearchField("Permit", new TableColumn("Permit"), "Permit", row => ((SystemSummary)row) is { KnownPermit: false } ? TableCells.Missing() : TableCells.Flag(((SystemSummary)row).NeedsPermit), byDefault: false, local: LocalSort.Flag<SystemSummary>(system => system.KnownPermit == false ? null : system.NeedsPermit)),
        SearchField("Stations", new TableColumn("Stations"), "Stations", row => TableCells.Number(((SystemSummary)row).StationCount), right: true, byDefault: false, local: LocalSort.Figure<SystemSummary>(system => system.StationCount)),
        SearchField("AtWar", new TableColumn("AtWar"), "At war", row => TableCells.Flag(((SystemSummary)row).AtWar), byDefault: false, local: LocalSort.Flag<SystemSummary>(system => system.AtWar)),
    ];

    private Control SystemCell(SystemSummary system)
    {
        var here = string.Equals(system.Name, _here(), StringComparison.OrdinalIgnoreCase);
        var name = Chrome(system.Name, TypeScale.Secondary, here ? ThemeManager.CyanKey : ThemeManager.AKey);
        name.Text = system.Name;

        var faction = system.ControllingFaction is { } controlling
            ? system.Factions.FirstOrDefault(f => string.Equals(f.Name, controlling, StringComparison.OrdinalIgnoreCase))?.State is { } state
                ? $"{controlling} · {state}"
                : controlling
            : Dash;

        return Centered(Pair(CopyGlyph.Beside(SystemLink.Look(name), system.Name, Copy), Sub(faction)));
    }

    protected override Control BuildForm()
    {
        var form = new StackPanel { Spacing = 22 };

        form.Children.Add(Group(
            "Search out from",
            Labelled("Near", NearField(_near, _nearNames)),
            Labelled("Distance in light years", Span(_distanceFrom, _distanceTo, _distanceReadout)),
            _age.Field("Max report age")));

        form.Children.Add(Group(
            "The system",
            Columns2(
                "*,*",
                Labelled("Allegiance", _pickers["allegiance"].Picker),
                Labelled("Government", _pickers["government"].Picker)),
            Columns2(
                "*,*",
                Labelled("Economy", _pickers["primary_economy"].Picker),
                Labelled("Security", _pickers["security"].Picker)),
            Labelled("Minimum population", _population),
            _colonised));

        form.Children.Add(Group(
            "Factions",
            Labelled("Controlling faction's state", _pickers["state"].Picker),
            Labelled("Controlling faction", Beneath(_controllingFaction, _controllingFactionNames)),
            Labelled("A faction present", Beneath(_faction, _factionNames)),
            Columns2(
                "*,*",
                Labelled("Its state", _pickers["faction_state"].Picker),
                Labelled("Its government", _pickers["faction_government"].Picker))));

        form.Children.Add(Group(
            "Powerplay",
            Labelled("Power", _pickers["power"].Picker),
            Columns2(
                "*,*",
                Labelled("Powerplay state", _powerState),
                Labelled("Acquiring power", _pickers["acquiring_power"].Picker))));

        return form;
    }

    protected override object? Followed => _here();

    protected override void Follow()
    {
        ShowHere(_near, _here());
        _distanceReadout.Update();
    }

    private string Reference() =>
        _near.Text is { } near && near.Trim().Length > 0 ? near.Trim() : _here() ?? "your system";

    private Control Row(SystemSummary system, ArrangedTable<object> table)
    {
        var grid = table.Row(system);

        if (system.SystemAddress is null || _open is null)
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
            Tag = system.Name,
        };
        Avalonia.Automation.AutomationProperties.SetName(row, system.Name);

        var open = _open;
        row.Click += (_, _) => open(system);

        return ListRow.Dress(row);
    }

    private static StackPanel PowerCell(SystemSummary system)
    {
        if (system.Acquiring.Count == 0)
        {
            return Pair(Value(system.ControllingPower), Sub(system.PowerState));
        }

        var acquiring = string.Join(
            " · ",
            system.Acquiring.Select(conflict => $"{conflict.Power} {PowerConflicts.Percent(conflict.Progress)}"));

        return Pair(Value(system.ControllingPower ?? acquiring), Sub(system.ControllingPower is null ? system.PowerState : $"{system.PowerState} · {acquiring}"));
    }

    internal static IReadOnlyList<string> Choices(string name) => GalaxyFilters.Find(name)?.Choices ?? [];

    private TextBox TextField(string name) => name switch
    {
        "near" => _near,
        "controlling_faction" => _controllingFaction,
        "faction" => _faction,
        "distance from" => _distanceFrom,
        "distance to" => _distanceTo,
        "minimum population" => _population.Box,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "No such text field."),
    };

    /// <summary>The form's span written as the tool reads one: "50", "10-50" or "10-".</summary>
    public static string? Range(string? from, string? to)
    {
        var low = from?.Trim() ?? string.Empty;
        var high = to?.Trim() ?? string.Empty;

        return (low.Length, high.Length) switch
        {
            (0, 0) => null,
            (0, _) => high,
            (_, 0) => $"{low}-",
            _ => $"{low}-{high}",
        };
    }

    internal static (string From, string To) Split(string? range)
    {
        if (range is null || !GalaxyCriteria.TryParseRange(range, out var min, out var max))
        {
            return (string.Empty, string.Empty);
        }

        return (min is { } low ? Number(low) : string.Empty, max is { } high ? Number(high) : string.Empty);
    }

    internal static int Index(IReadOnlyList<string> items, string? value)
    {
        if (value is null)
        {
            return 0;
        }

        for (var index = 1; index < items.Count; index++)
        {
            if (string.Equals(items[index], value, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return 0;
    }

    internal static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    internal static TextBox Field(string name, bool mono = false)
    {
        var box = new TextBox
        {
            Height = TypeScale.MinimumTarget,
            VerticalContentAlignment = VerticalAlignment.Center,
            FontSize = TypeScale.Search,
        };

        if (mono)
        {
            box.FontFamily = new FontFamily(Fonts.MonoFamily);
        }

        Avalonia.Automation.AutomationProperties.SetName(box, name);
        return box;
    }

    internal static Control Group(string name, params Control[] children)
    {
        var head = TitleText.GroupRow(TitleText.Build(name, TypeScale.Section, TitleRank.Group));
        var group = new StackPanel { Spacing = 10, Children = { head } };

        foreach (var child in children)
        {
            group.Children.Add(child);
        }

        return group;
    }

    internal static Control Labelled(string label, Control control) =>
        new StackPanel { Spacing = 4, Children = { Label(label, width: null), control } };

    internal static Grid Columns2(string columns, Control left, Control right)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(columns), ColumnSpacing = 12, Children = { left, right } };
        Grid.SetColumn(right, 1);
        return grid;
    }

    internal static StackPanel Span(TextBox from, TextBox to, RangeReadout readout)
    {
        from.InnerLeftContent = SearchText.Prefix("From");
        to.InnerLeftContent = SearchText.Prefix("Up to");
        Grid.SetColumn(to, 1);

        var boxes = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 12, Children = { from, to } };
        return new StackPanel { Spacing = 6, Children = { boxes, readout.Line } };
    }

    internal static StackPanel Pair(Control top, TextBlock bottom) =>
        new() { VerticalAlignment = VerticalAlignment.Center, Children = { top, bottom } };

    internal static TextBlock Value(string? text)
    {
        var block = new TextBlock
        {
            Text = text ?? Dash,
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Tip,
            FontWeight = FontWeight.Medium,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        ThemeInk.Bind(block, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);
        return block;
    }

    internal static TextBlock Sub(string? text)
    {
        var block = new TextBlock
        {
            Text = text ?? Dash,
            FontFamily = Fonts.ProseFamily,
            FontSize = TypeScale.Caption,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        ThemeInk.Bind(block, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        return block;
    }

    internal static T Centered<T>(T control)
        where T : Control
    {
        control.VerticalAlignment = VerticalAlignment.Center;
        return control;
    }

    /// <summary>Light years to two places, or a dash.</summary>
    internal static string Fixed(double? ly) => ly is { } value ? value.ToString("F2", CultureInfo.InvariantCulture) : Dash;

    /// <summary>The day as MM-dd in local time, or a dash.</summary>
    internal static string Day(DateTimeOffset? at) => at is { } value ? value.ToLocalTime().ToString("MM-dd", CultureInfo.InvariantCulture) : Dash;

    internal static TextBlock Right(TextBlock block)
    {
        block.HorizontalAlignment = HorizontalAlignment.Right;
        block.TextAlignment = TextAlignment.Right;
        return block;
    }
}

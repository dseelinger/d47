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

    public const string SummaryText = "Star systems that match, nearest first.";

    public const string Any = "Any";

    /// <summary>The tool's own default for how many to return.</summary>
    public const int DefaultLimit = 5;

    /// <summary>The table's columns: system, light years, allegiance, economy, population, power, reported.</summary>
    private const string Columns = "1.5*,64,*,*,104,*,76";

    private const string HeadsetColumns = "1.5*,64,*,*,104,*";

    private const string Dash = "—";

    private readonly Func<string?> _here;
    private readonly Action<SystemSummary>? _open;

    private readonly TextBox _near = Field("near");
    private readonly SystemNameSuggestions? _nearNames;
    private readonly TextBox _distanceFrom = Field("distance from", mono: true);
    private readonly TextBox _distanceTo = Field("distance to", mono: true);
    private readonly Amount _limit = new() { Value = DefaultLimit, Minimum = 1, Maximum = 20, Step = 1, Width = 132 };

    private readonly TextBox _populationFrom = Field("population from", mono: true);
    private readonly TextBox _populationTo = Field("population to", mono: true);
    private readonly CheckBox _colonised = LabeledCheckBox.Caps("Only colonised systems");
    private readonly TextBox _controllingFaction = Field("controlling faction");
    private readonly TextBox _faction = Field("a faction present");
    private readonly Stepper _powerState;

    private readonly Dictionary<string, (InlinePicker Picker, IReadOnlyList<string> Choices)> _pickers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _picked = new(StringComparer.Ordinal);

    public SystemSearchPage(
        GalaxySearchSurface searches,
        Func<bool> lookupsEnabled,
        Func<string?> here,
        Action<SystemSummary>? open,
        Action? openSettings,
        bool headset)
        : base(searches, lookupsEnabled, openSettings, headset)
    {
        _here = here;
        _nearNames = SuggestNames(_near);
        _open = open;

        _powerState = new Stepper
        {
            ItemsSource = [Any, .. Choices("power_state")],
            Wraps = false,
            MaxWidth = double.PositiveInfinity,
        };

        _colonised.MinHeight = TypeScale.MinimumTarget;
        _controllingFaction.PlaceholderText = "Exact name";
        _distanceFrom.PlaceholderText = "0";
        _populationFrom.PlaceholderText = "0";
        _populationTo.PlaceholderText = Any;

        foreach (var name in (string[])["allegiance", "government", "primary_economy", "security", "state", "faction_state", "faction_government", "power"])
        {
            var picker = new InlinePicker { Label = name.Replace('_', ' '), ListMaxHeight = 240 };
            var choices = Choices(name);
            var key = name;

            picker.Picked += (_, id) =>
            {
                Pick(key, id);
            };

            _pickers[name] = (picker, choices);
            Pick(name, Any);
        }
    }

    public override string Tool => "search_systems";

    public override GalaxySearchKind Kind => GalaxySearchKind.Systems;

    public override string Summary => SummaryText;

    protected override string Noun => "Systems";

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
        Put("distance", Range(_distanceFrom.Text, _distanceTo.Text));
        Put("population", Range(_populationFrom.Text, _populationTo.Text));
        Put("controlling_faction", _controllingFaction.Text);
        Put("faction", _faction.Text);
        Put("power_state", _powerState.SelectedIndex > 0 ? _powerState.SelectedItem : null);

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
        foreach (var box in (TextBox[])[_near, _distanceFrom, _distanceTo, _populationFrom, _populationTo, _controllingFaction, _faction])
        {
            box.Text = string.Empty;
        }

        _limit.Value = DefaultLimit;
        _powerState.SelectedIndex = 0;
        _colonised.IsChecked = false;

        foreach (var name in _pickers.Keys)
        {
            Pick(name, Any);
        }
    }

    protected override void FillForm(IReadOnlyDictionary<string, string> arguments)
    {
        ClearForm();

        string? Get(string key) => arguments.TryGetValue(key, out var value) ? value : null;

        Fill(_near, _nearNames, Get("near") ?? string.Empty);
        (_distanceFrom.Text, _distanceTo.Text) = Split(Get("distance"));
        (_populationFrom.Text, _populationTo.Text) = Split(Get("population"));
        _controllingFaction.Text = Get("controlling_faction") ?? string.Empty;
        _faction.Text = Get("faction") ?? string.Empty;
        _colonised.IsChecked = bool.TryParse(Get("colonised"), out var colonised) && colonised;
        _powerState.SelectedIndex = Index(_powerState.ItemsSource, Get("power_state"));

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
        var requested = arguments
            .Where(pair => pair.Key is not ("near" or "limit"))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        return !GalaxyCriteria.TryParse(GalaxySearchKind.Systems, requested, out var criteria, out _) || criteria.Count == 0;
    }

    protected override (string Lead, string Reference)? Aside(IReadOnlyDictionary<string, string> arguments, string? reference)
    {
        if (!arguments.TryGetValue("distance", out var distance)
            || !GalaxyCriteria.TryParseRange(distance, out var min, out var max))
        {
            return null;
        }

        var from = reference
            ?? (arguments.TryGetValue("near", out var near) && near.Trim().Length > 0 ? near.Trim() : null)
            ?? "your system";

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

    protected override Control? Table(GalaxySearchPosting posting, bool headset)
    {
        if (posting.Result is not GalaxySearchResult { Systems.Count: > 0 } result)
        {
            return null;
        }

        var columns = headset ? HeadsetColumns : Columns;

        var heads = new Grid { ColumnDefinitions = new ColumnDefinitions(columns), ColumnSpacing = 12, Margin = new Thickness(12, 0, 12, 6) };

        foreach (var (top, sub, column, right) in new (string, string?, int, bool)[]
                 {
                     ("System", "Controlled by", 0, false),
                     ("LY", null, 1, true),
                     ("Allegiance", "Government", 2, false),
                     ("Economy", "Security", 3, false),
                     ("Population", null, 4, true),
                     ("Power", "State", 5, false),
                     ("Reported", null, 6, true),
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
        var here = _here();

        foreach (var system in result.Systems)
        {
            rows.Children.Add(Row(system, columns, headset, string.Equals(system.Name, here, StringComparison.OrdinalIgnoreCase)));
        }

        return new StackPanel { Children = { rule, rows } };
    }

    protected override Control BuildForm()
    {
        var form = new StackPanel { Spacing = 22 };

        form.Children.Add(Group(
            "Search out from",
            Labelled("Near", Beneath(_near, _nearNames)),
            Columns2(
                "*,132",
                Labelled("Distance", Span(_distanceFrom, _distanceTo, "LY")),
                Labelled("How many", _limit))));

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
            Labelled("Population", Span(_populationFrom, _populationTo, null)),
            _colonised));

        form.Children.Add(Group(
            "Factions",
            Labelled("Controlling faction's state", _pickers["state"].Picker),
            Labelled("Controlling faction", _controllingFaction),
            Labelled("A faction present", _faction),
            Columns2(
                "*,*",
                Labelled("Its state", _pickers["faction_state"].Picker),
                Labelled("Its government", _pickers["faction_government"].Picker))));

        form.Children.Add(Group(
            "Powerplay",
            Labelled("Power", _pickers["power"].Picker),
            Labelled("Powerplay state", _powerState)));

        return form;
    }

    protected override object? Followed => _here();

    protected override void Follow() =>
        _near.PlaceholderText = _here() is { } system ? $"Your system · {system}" : "Your system";

    private Control Row(SystemSummary system, string columns, bool headset, bool here)
    {
        var name = Chrome(system.Name, TypeScale.Secondary, here ? ThemeManager.CyanKey : ThemeManager.AKey);
        name.Text = system.Name;

        var faction = system.ControllingFaction is { } controlling
            ? system.Factions.FirstOrDefault(f => string.Equals(f.Name, controlling, StringComparison.OrdinalIgnoreCase))?.State is { } state
                ? $"{controlling} · {state}"
                : controlling
            : Dash;

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(columns),
            ColumnSpacing = 12,
            Children =
            {
                Cell(Pair(name, Sub(faction)), 0),
                Cell(Right(Mono(system.Distance is { } ly ? ly.ToString("F2", CultureInfo.InvariantCulture) : Dash, TypeScale.Small, ThemeManager.AKey)), 1),
                Cell(Pair(Value(system.Allegiance), Sub(system.Government)), 2),
                Cell(Pair(Value(system.PrimaryEconomy), Sub(system.Security)), 3),
                Cell(Right(Mono(system.Population is { } people ? people.ToString("N0", CultureInfo.InvariantCulture) : Dash, TypeScale.Small, ThemeManager.WhiteKey)), 4),
                Cell(Pair(Value(system.ControllingPower), Sub(system.PowerState)), 5),
            },
        };

        if (!headset)
        {
            grid.Children.Add(Cell(Right(Mono(
                system.ReportedAt is { } at ? at.ToLocalTime().ToString("MM-dd", CultureInfo.InvariantCulture) : Dash,
                TypeScale.Small,
                ThemeManager.GreyKey)), 6));
        }

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

    internal static IReadOnlyList<string> Choices(string name) => GalaxyFilters.Find(name)?.Choices ?? [];

    private TextBox TextField(string name) => name switch
    {
        "near" => _near,
        "controlling_faction" => _controllingFaction,
        "faction" => _faction,
        "distance from" => _distanceFrom,
        "distance to" => _distanceTo,
        "population from" => _populationFrom,
        "population to" => _populationTo,
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

    internal static Grid Span(TextBox from, TextBox to, string? unit)
    {
        var dash = Mono("–", TypeScale.Small, ThemeManager.GreyKey);
        dash.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(dash, 1);
        Grid.SetColumn(to, 2);

        if (unit is not null)
        {
            var suffix = Label(unit, width: null);
            suffix.Margin = new Thickness(0, 0, 10, 0);
            to.InnerRightContent = suffix;
        }

        return new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,*"), ColumnSpacing = 6, Children = { from, dash, to } };
    }

    internal static StackPanel Pair(TextBlock top, TextBlock bottom) =>
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
        Themed(block, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);
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
        Themed(block, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        return block;
    }

    internal static T Cell<T>(T control, int column)
        where T : Control
    {
        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, column);
        return control;
    }

    internal static TextBlock Right(TextBlock block)
    {
        block.HorizontalAlignment = HorizontalAlignment.Right;
        block.TextAlignment = TextAlignment.Right;
        return block;
    }
}

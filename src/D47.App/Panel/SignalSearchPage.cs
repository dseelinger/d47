using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Knowledge;
using static D47.App.Panel.SystemSearchPage;

namespace D47.App.Panel;

/// <summary>Search › Signals: the nearest signals of a kind or name, from the daily index and the Commander's own scans, through <c>find_signal</c>.</summary>
public sealed class SignalSearchPage : GalaxySearchPage
{
    public const string RootKey = "search.signals";

    public const string SummaryText = "Signals that match, nearest first.";

    public const string Help = "Signals from the daily index and from your own scans.";

    public const string NoSources =
        "You have no signal scans of your own yet. Turn on “Signal index” in Settings and D47 will search the daily index of signals too.";

    public const string NameName = "SignalName";

    public const string Unset = "Choose a signal";

    public const int DefaultDistance = 100;

    public const string HeadName = "SignalHead";

    public const string TableId = "Search.Signals";

    private static readonly TableColumn[] Columns =
    [
        new("Signal", ColumnFit.Fill) { Min = 100 },
        new("System", ColumnFit.Fill) { Min = 100 },
        new("Distance"),
        new("Seen"),
        new("Source"),
    ];

    private const string YourScan = "YOUR SCAN";

    private const string FromIndex = "INDEX";

    private readonly Func<string?> _here;
    private readonly Action<string, long?> _openSystem;
    private readonly Func<string, Task<bool>>? _copy;

    private readonly InlinePicker _signal = new() { Label = "Signal", ListMaxHeight = 320 };
    private readonly TextBox _name = Field("part of a signal's name");
    private readonly TextBox _near = Field("near");
    private readonly SystemNameSuggestions<SystemNameMatch>? _nearNames;
    private readonly Amount _distance = new() { Value = DefaultDistance, Minimum = 1, Maximum = 99999, Step = 10, Format = "#,0" };

    private readonly MaxReportAge _age = new();

    private string? _picked;
    private bool _filling;

    public SignalSearchPage(
        GalaxySearchSurface searches,
        Func<bool> namesEnabled,
        Func<string?> here,
        Action<string, long?> openSystem,
        Func<string, Task<bool>>? copy,
        Action? openSettings,
        bool headset)
        : base(searches, searches.SignalSources ?? (() => true), openSettings, headset, namesEnabled)
    {
        _here = here;
        _openSystem = openSystem;
        _copy = copy;
        _nearNames = SuggestNames(_near);

        _name.Name = NameName;
        _name.PlaceholderText = "memorial, unauthorised, Natalie Fraser";
        _name.TextChanged += (_, _) =>
        {
            if (!_filling && !string.IsNullOrWhiteSpace(_name.Text))
            {
                Pick(null);
            }
        };
        _signal.Picked += (_, id) => Pick(id);

        Pick(null);
    }

    public override string Tool => "find_signal";

    public override GalaxySearchKind Kind => GalaxySearchKind.Signals;

    public override string Summary => SummaryText;

    protected override string Noun => "Signals";
    protected override string NounOne => "Signal";

    protected override string OffSentence => NoSources;

    protected override string OffTip => "Turn on “Signal index” in Settings to search the daily index.";

    protected override string RunningText => "Searching signals";

    protected override string RefusedLabel => "SEARCH REFUSED";

    protected override string Origin => "INDEX AND YOUR SCANS";

    protected override string? SayPhrase => "where is the nearest hazardous RES";

    /// <summary>The kinds the picker offers, Any RES first.</summary>
    public static IReadOnlyList<string> Choices { get; } = [SignalSearch.AnyRes, .. SignalKind.All.Select(kind => kind.Name)];

    /// <summary>The kind the picker holds, or null while none is chosen.</summary>
    public string? Picked => _picked;

    /// <summary>The picker, for a test to open.</summary>
    public InlinePicker Picker => _signal;

    /// <summary>Chooses a kind as the picker does, which clears the name; null leaves the picker unset.</summary>
    public void Pick(string? kind)
    {
        _picked = Choices.FirstOrDefault(choice => string.Equals(choice, kind, StringComparison.OrdinalIgnoreCase));

        if (_picked is not null && !string.IsNullOrEmpty(_name.Text))
        {
            _filling = true;
            _name.Text = string.Empty;
            _filling = false;
        }

        _signal.Show(
            [.. Choices.Select(choice => new InlinePickerOption(choice, choice, ThemeManager.WhiteKey))],
            _picked ?? string.Empty,
            _picked ?? Unset,
            _picked is null ? ThemeManager.GreyKey : ThemeManager.WhiteKey,
            null);
    }

    /// <summary>Types into the name field.</summary>
    public void Type(string text) => _name.Text = text;

    /// <summary>Sets the Near field.</summary>
    public void Near(string system) => _near.Text = system;

    public override IReadOnlyDictionary<string, string> FormArguments()
    {
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal);

        var signal = _picked ?? _name.Text?.Trim();

        if (!string.IsNullOrEmpty(signal))
        {
            arguments["signal"] = signal;
        }

        if (!string.IsNullOrWhiteSpace(_near.Text))
        {
            arguments["near"] = _near.Text.Trim();
        }

        if (_distance.Value is { } distance && distance != DefaultDistance)
        {
            arguments["max_distance"] = distance.ToString("0", CultureInfo.InvariantCulture);
        }

        if (_age.Asked is { } age)
        {
            arguments["max_report_age_hours"] = age;
        }

        return arguments;
    }

    public override void ClearForm()
    {
        _filling = true;
        _name.Text = string.Empty;
        _near.Text = string.Empty;
        _filling = false;
        _distance.Value = DefaultDistance;
        _age.Reset();
        Pick(null);
    }

    protected override void FillForm(IReadOnlyDictionary<string, string> arguments)
    {
        ClearForm();

        string? Get(string key) => arguments.TryGetValue(key, out var value) ? value : null;

        Fill(_near, _nearNames, Get("near") ?? string.Empty);
        _age.Set(Get("max_report_age_hours"));

        if (double.TryParse(Get("max_distance"), NumberStyles.Float, CultureInfo.InvariantCulture, out var distance))
        {
            _distance.Value = (decimal)Math.Clamp(distance, 1, 99999);
        }

        if (Get("signal")?.Trim() is { Length: > 0 } signal)
        {
            Pick(signal);

            if (_picked is null)
            {
                _filling = true;
                _name.Text = signal;
                _filling = false;
            }
        }
    }

    protected override bool Refusal(IReadOnlyDictionary<string, string> arguments) => true;

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
        posting.Result is SignalSearchResult result ? result.Signals.Count : 0;

    protected override Control? Table(GalaxySearchPosting posting, bool headset)
    {
        if (posting.Result is not SignalSearchResult { Signals.Count: > 0 } result)
        {
            return null;
        }

        var columns = NewTable(TableId, Columns);

        List<SearchColumn> heads =
        [
            new("Signal", null, null, false),
            new("System", null, null, false),
            new("LY", null, null, true),
            new("Seen", null, null, false),
            new("Source", null, null, false),
        ];

        var rule = SearchHead(posting, HeadName, columns, heads);
        var rows = new StackPanel { Spacing = Gaps.Tile, Margin = new Thickness(0, 2, 0, 0) };
        var now = DateTimeOffset.UtcNow;

        foreach (var finding in result.Signals)
        {
            rows.Children.Add(Row(finding, columns, now));
        }

        return new StackPanel { Children = { rule, rows } };
    }

    protected override Control BuildForm()
    {
        var form = new StackPanel { Spacing = 22 };

        form.Children.Add(Group(
            "The signal",
            Prose(Help, TypeScale.Tip, ThemeManager.GreyKey),
            Labelled("Signal", _signal),
            Labelled("Or any part of its name", _name)));

        form.Children.Add(Group(
            "Search out from",
            Labelled("Near", NearField(_near, _nearNames)),
            Labelled("Up to, in LY", _distance),
            _age.Field("Max report age")));

        return form;
    }

    protected override object? Followed => _here();

    protected override void Follow() =>
        ShowHere(_near, _here());

    private Control Row(SignalFinding finding, TableLayout columns, DateTimeOffset now)
    {
        var here = string.Equals(finding.System, _here(), StringComparison.OrdinalIgnoreCase);
        var system = Value(finding.System);
        system.VerticalAlignment = VerticalAlignment.Center;
        Themed(system, TextBlock.ForegroundProperty, here ? ThemeManager.CyanKey : ThemeManager.AKey);

        var systemCell = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { system } };

        if (_copy is { } copy)
        {
            var glyph = CopyGlyph.For(finding.System, copy);
            glyph.VerticalAlignment = VerticalAlignment.Center;
            systemCell.Children.Add(glyph);
        }

        var grid = columns.Row(
            [
                Cell(Value(finding.Signal), 0),
                Cell(systemCell, 1),
                Cell(Right(Mono(finding.Distance.ToString("F2", CultureInfo.InvariantCulture), TypeScale.Small, ThemeManager.AKey)), 2),
                Cell(Value(MissionsPage.Ago(now - finding.LastSeen)), 3),
                Cell(Chrome(finding.OwnScan ? YourScan : FromIndex, TypeScale.Small, finding.OwnScan ? ThemeManager.CyanKey : ThemeManager.GreyKey), 4),
            ]);

        var row = new Button
        {
            MinHeight = 54,
            Padding = new Thickness(12, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = grid,
            Classes = { RowClass },
            Tag = finding.System,
        };
        Avalonia.Automation.AutomationProperties.SetName(row, $"{finding.Signal} in {finding.System}");

        var open = _openSystem;
        row.Click += (_, _) => open(finding.System, finding.SystemAddress == 0 ? null : finding.SystemAddress);

        return ListRow.Dress(row);
    }
}

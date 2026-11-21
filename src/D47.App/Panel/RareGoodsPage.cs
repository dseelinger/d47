using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Knowledge;
using static D47.App.Panel.SystemSearchPage;

namespace D47.App.Panel;

/// <summary>Search › Rare goods: the rares within reach of a system, through <c>list_rares</c>, with a rare run from each.</summary>
public sealed class RareGoodsPage : UserControl, IPageSummary
{
    public const string RootKey = "search.rares";

    public const string SummaryText = "Rare goods for sale, most profit first, with a rare run from each.";

    public const string RunName = "RunRareSearch";

    public const string RareRunName = "RareRun";

    public const string RowsName = "RareRows";

    public const string HintName = "RareHint";

    public const string HeadingName = "RareHeading";

    public const string HeadName = "RareHead";

    public const string TableId = "Search.Rares";

    public const int DefaultDistance = 200;

    public const string DefaultOrder = "profit";

    public const string Hint =
        "Profit is an estimate per tonne for selling far from the source; it grows with distance up to about 200 ly. "
        + "Prices and supply are reported by other Commanders.";

    private const string Dash = "—";

    private readonly GalaxySearchSurface _searches;
    private readonly Func<string?> _here;
    private readonly Func<string?> _power;
    private readonly Action<RareListingRow> _rareRun;
    private readonly bool _headset;
    private readonly Func<string, Task<bool>>? _copy;

    private readonly TextBox _near = Field("near");
    private readonly SystemNameSuggestions<SystemNameMatch>? _nearNames;
    private readonly Amount _distance = new() { Value = DefaultDistance, Minimum = 1, Maximum = 99999, Step = 10, Format = "#,0" };
    private readonly Button _run = new() { Name = RunName, Content = "SEARCH", Height = TypeScale.MinimumTarget, VerticalAlignment = VerticalAlignment.Bottom };
    private readonly ContentControl _answer = new();
    private readonly Control _form;
    private readonly D47.App.Voice.WaitSound? _waitSound;
    private readonly WaitingState _waiting = new(WaitForm.Body, "Asking for the rare goods…") { IsVisible = false };

    private CancellationTokenSource? _inFlight;
    private (string Text, DateTimeOffset At)? _failure;
    private object? _seen;
    private bool _asked;
    private readonly SearchTableFields _sortFields = new(GalaxySearchKind.Rares);
    private ArrangedTable<object>? _table;
    private GalaxySearchPosting? _posted;
    private IReadOnlyDictionary<string, string>? _filterFor;

    public RareGoodsPage(
        GalaxySearchSurface searches,
        Func<string?> here,
        Func<string?> pledgedPower,
        Action<RareListingRow> rareRun,
        Func<string, Task<bool>>? copy,
        bool headset,
        D47.App.Voice.WaitSound? waitSound = null)
    {
        _waitSound = waitSound;
        _searches = searches;
        _here = here;
        _power = pledgedPower;
        _rareRun = rareRun;
        _copy = copy;
        _headset = headset;

        if (searches.PrefixNames is { } lookup)
        {
            SystemNameSuggestions<SystemNameMatch>? names = null;
            names = new SystemNameSuggestions<SystemNameMatch>(
                _near, lookup, match => match.Name, () => true, match => names!.SetText(match.Name), searches.SuggestionPause);
            _nearNames = names;
        }

        _near.Classes.Add("here");
        _form = Form();
        _run.Click += (_, _) => _ = RunAsync(FormArguments());

        Content = new Border { Padding = new Thickness(0, 16, 0, 0), Child = _answer };
    }

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    public string Summary => SummaryText;

    public ColumnWidthMemory? ColumnWidths { get; init; }

    /// <summary>Where the columns the table shows are remembered; null keeps the table's own.</summary>
    public ColumnArrangementMemory? ColumnArrangements { get; init; }

    /// <summary>What is drawn where the rares go.</summary>
    public GalaxySearchView View { get; private set; }

    /// <summary>The sentence drawn for the current state, where it has one.</summary>
    public string? Sentence { get; private set; }

    /// <summary>The rares drawn, in order.</summary>
    public IReadOnlyList<RareListingRow> Rows { get; private set; } = [];

    /// <summary>The arguments the form holds: the page asks for the most profit first.</summary>
    public IReadOnlyDictionary<string, string> FormArguments()
    {
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(_near.Text))
        {
            arguments["near"] = _near.Text.Trim();
        }

        if (_distance.Value is { } value && value != DefaultDistance)
        {
            arguments["max_distance"] = value.ToString("0", CultureInfo.InvariantCulture);
        }

        arguments["order_by"] = DefaultOrder;

        return arguments;
    }

    /// <summary>Sets the Near field's text.</summary>
    public void Type(string text) => _near.Text = text;

    /// <summary>The Near field's text.</summary>
    public string NearText => _near.Text ?? string.Empty;

    /// <summary>Runs the form's search; the answer arrives through the board.</summary>
    public async Task RunAsync(IReadOnlyDictionary<string, string> arguments)
    {
        if (_inFlight is not null)
        {
            return;
        }

        var inFlight = _inFlight = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        _asked = true;
        _failure = null;
        Draw();

        try
        {
            ToolResult? result = null;

            await Waiting.While(
                _waiting,
                _waitSound,
                async () => result = await _searches.Registry
                    .InvokeAsync(
                        "list_rares",
                        new ToolArguments(new Dictionary<string, string>(arguments, StringComparer.Ordinal)),
                        inFlight.Token,
                        ToolCaller.Commander)
                    .ConfigureAwait(true))
                .ConfigureAwait(true);

            if (result!.IsError)
            {
                _failure = (result.Content, DateTimeOffset.Now);
            }
        }
        catch (OperationCanceledException)
        {
            _failure = ("The rare list took too long to answer.", DateTimeOffset.Now);
        }
        finally
        {
            _inFlight = null;
            inFlight.Dispose();
            Draw();
        }
    }

    /// <summary>Redraws when the Commander's system changed or an answer landed; true when it did.</summary>
    public bool Tick()
    {
        if (Equals(Stamp(), _seen))
        {
            return false;
        }

        Draw();
        return true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _filterFor = null;
        _table?.StopArranging();

        if (_table is { Filtering: true } filtering)
        {
            filtering.ToggleFiltering();
        }
        _searches.Board.Posted += OnPosted;

        if (_searches.Board.Last(GalaxySearchKind.Rares) is { } last)
        {
            FillForm(last.Arguments);
            _asked = true;
        }

        Draw();

        if (!_asked)
        {
            _ = RunAsync(FormArguments());
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _searches.Board.Posted -= OnPosted;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnPosted(GalaxySearchKind kind)
    {
        if (kind != GalaxySearchKind.Rares)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            _failure = null;
            _asked = true;

            if (_searches.Board.Last(GalaxySearchKind.Rares) is { } last)
            {
                FillForm(last.Arguments);
            }

            Draw();
        });
    }

    private void FillForm(IReadOnlyDictionary<string, string> arguments)
    {
        _near.Text = arguments.GetValueOrDefault("near") ?? string.Empty;

        _distance.Value = arguments.TryGetValue("max_distance", out var typed)
                          && double.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out var asked)
            ? D47.Core.Decimals.FromDouble(Math.Clamp(asked, 1, 99999))
            : DefaultDistance;
    }

    private object Stamp() =>
        (_here(), _searches.Board.Last(GalaxySearchKind.Rares)?.AskedAt, _inFlight is not null, _failure?.At);

    private void Draw()
    {
        _seen = Stamp();
        _near.PlaceholderText = (_here() is { } here ? $"Your system · {here}" : "Your system").ToUpperInvariant();

        var last = _searches.Board.Last(GalaxySearchKind.Rares);
        var result = last?.Result as RareSearchResult;
        Rows = result?.Rows ?? [];

        View = _inFlight is not null ? GalaxySearchView.Running
            : _failure is not null ? GalaxySearchView.Unavailable
            : last is null ? GalaxySearchView.Unasked
            : Rows.Count == 0 ? GalaxySearchView.NoResults
            : GalaxySearchView.Answer;

        Sentence = _failure?.Text;

        (_form.Parent as Avalonia.Controls.Panel)?.Children.Remove(_form);
        _run.IsEnabled = _inFlight is null;

        var top = new StackPanel { Spacing = 20 };
        top.Children.Add(_form);

        switch (View)
        {
            case GalaxySearchView.Running:
                (_waiting.Parent as Avalonia.Controls.Panel)?.Children.Remove(_waiting);
                top.Children.Add(_waiting);
                _answer.Content = LoadoutPages.Scrolling(top);
                break;

            case GalaxySearchView.Unavailable:
                top.Children.Add(new Notice(NoticeLevel.Error)
                {
                    Label = "RARE GOODS UNAVAILABLE",
                    Text = _failure!.Value.Text,
                    Detail = _failure.Value.At.ToString("HH:mm", CultureInfo.InvariantCulture),
                });
                _answer.Content = LoadoutPages.Scrolling(top);
                break;

            case GalaxySearchView.NoResults:
                top.Children.Add(Heading(last!, result!));
                top.Children.Add(Typeset.Prose(
                    $"No rare good is sold within {Distance(last!)} ly of {From(last!)}.",
                    TypeScale.Secondary,
                    ThemeManager.WhiteKey,
                    wrap: true));
                _answer.Content = LoadoutPages.Scrolling(top);
                break;

            case GalaxySearchView.Answer:
                top.Children.Add(Heading(last!, result!));
                _answer.Content = Table(top, last!, result!);
                break;

            default:
                _answer.Content = LoadoutPages.Scrolling(top);
                break;
        }
    }

    private Control Form()
    {
        var near = new StackPanel { Spacing = 2, Children = { _near } };

        if (_nearNames is not null)
        {
            near.Children.Add(_nearNames.List);
        }

        var fields = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(_headset ? "*" : "*,Auto,Auto"),
            ColumnSpacing = 16,
            RowSpacing = 12,
            MaxWidth = 900,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var nearBox = Labelled("Near", near);
        var distanceBox = Labelled("Max distance in light years", _distance);

        if (_headset)
        {
            fields.RowDefinitions = new RowDefinitions("Auto,Auto,Auto");
            Grid.SetRow(distanceBox, 1);
            Grid.SetRow(_run, 2);
            _run.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        else
        {
            Grid.SetColumn(distanceBox, 1);
            Grid.SetColumn(_run, 2);
            _run.HorizontalAlignment = HorizontalAlignment.Left;
        }

        fields.Children.Add(nearBox);
        fields.Children.Add(distanceBox);
        fields.Children.Add(_run);

        return Group("Search out from", fields);
    }

    private static string From(GalaxySearchPosting last) =>
        last.Reference ?? (last.Arguments.TryGetValue("near", out var near) && near.Trim().Length > 0 ? near.Trim() : "your system");

    private static string Distance(GalaxySearchPosting last) =>
        last.Arguments.TryGetValue("max_distance", out var typed)
        && double.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out var asked)
            ? Number(asked)
            : Number(DefaultDistance);

    private Control Heading(GalaxySearchPosting last, RareSearchResult result)
    {
        var heading = Typeset.Prose(
            $"Rare goods within {Distance(last)} ly of {From(last)}", TypeScale.Section, ThemeManager.AKey, wrap: true);
        heading.Name = HeadingName;

        var count = result.Total == 1 ? "1 rare." : $"{result.Total} rares.";
        var hint = Typeset.Prose(
            $"{count} {Hint}" + (result.NoIndex ? " The rare index could not be fetched, so supply and prices are not known." : string.Empty),
            TypeScale.Tip,
            ThemeManager.GreyKey,
            wrap: true);
        hint.Name = HintName;

        return new StackPanel { Spacing = 4, Children = { heading, hint } };
    }

    private Control Table(StackPanel top, GalaxySearchPosting last, RareSearchResult result)
    {
        var table = Arranged(last);
        IReadOnlyList<object> rows = [.. result.Rows];

        var list = new ItemsControl
        {
            Name = RowsName,
            ItemsSource = ColumnFilter.Empty(table, rows) is { } empty
                ? new Control[] { empty }
                : table.Filter(rows).Select(row => Spaced(Row((RareListingRow)row, table))).ToArray(),
            IsVisible = !ColumnFilter.HidesRows(table),
        };

        if (ColumnArranger.Line(table, rows) is { } line)
        {
            top.Children.Add(line);
        }

        if (ColumnFilter.Bar(table, rows) is { } filter)
        {
            top.Children.Add(filter);
        }

        if (ColumnArranger.Bar(table) is { } bar)
        {
            top.Children.Add(bar);
        }

        return LoadoutPages.HeaderAndList(top, SearchTableFields.Head(table), list, _headset ? null : table.Layout, out _);
    }

    private static Border Spaced(Control row) => new() { Margin = new Thickness(0, 2, 0, Gaps.Tile - 2), Child = row };

    /// <summary>
    /// The table, built once so its COLUMNS and FILTER bars stay open across redraws, showing the sort <paramref name="last"/>
    /// was asked in. A new answer clears the filter.
    /// </summary>
    private ArrangedTable<object> Arranged(GalaxySearchPosting last)
    {
        _posted = last;

        if (_table is null)
        {
            _table = new ArrangedTable<object>(TableId, Fields(), ColumnArrangements, _headset ? null : ColumnWidths, 12, headName: HeadName);
            _table.Changed += Draw;
            _table.Sorted += () => _ = RunAsync(_sortFields.Sorted(_posted!.Arguments, _table));
        }
        else
        {
            _table.Refresh();
        }

        if (!ReferenceEquals(_filterFor, last.Arguments))
        {
            _table.ClearFilters();
            _filterFor = last.Arguments;
        }

        _sortFields.Show(_table, last.Arguments);

        return _table;
    }

    /// <summary>Every field the table can show; RARE RUN is a control column at the right.</summary>
    private IReadOnlyList<TableField<object>> Fields() =>
    [
        _sortFields.Field("Name", new TableColumn("Name", ColumnFit.Fill) { Min = 140 }, "Name", row => NameCell((RareListingRow)row), "name"),
        _sortFields.Field("Station", new TableColumn("Station", ColumnFit.Fill) { Min = 190 }, "Station", row => StationCell((RareListingRow)row), "station", "(System)", filters: [SearchValues.One<RareListingRow>("Station", rare => rare.Rare.Station, "No station reported"), SearchValues.One<RareListingRow>("System", rare => rare.Rare.System, "No system reported")]),
        _sortFields.Field(
            "FromStar",
            new TableColumn("FromStar") { Min = 118 },
            "From star",
            row => Centered(Right(Mono(((RareListingRow)row).Rare.DistanceToArrival is { } ls ? $"{ls.ToString("N0", CultureInfo.InvariantCulture)} ls" : Dash, ThemeManager.AKey))),
            "from_star",
            right: true),
        _sortFields.Field(
            "Distance",
            new TableColumn("Distance") { Min = 60 },
            "LY",
            row => Centered(Right(Mono(((RareListingRow)row).Distance.ToString("0.0", CultureInfo.InvariantCulture), ThemeManager.AKey))),
            SearchSorts.Distance,
            right: true),
        _sortFields.Field(
            "Supply",
            new TableColumn("Supply") { Min = 56 },
            "Supply",
            row => Centered(Right(Mono(((RareListingRow)row).Supply is { } supply ? supply.ToString("N0", CultureInfo.InvariantCulture) : Dash, StockInk((RareListingRow)row)))),
            "supply",
            right: true),
        _sortFields.Field("Price", new TableColumn("Price") { Min = 64 }, "Price", row => Centered(Right(Mono(Figure(((RareListingRow)row).Price), ThemeManager.AKey))), "price", "Buy", right: true),
        _sortFields.Field("Profit", new TableColumn("Profit") { Min = 96 }, "Profit", row => Centered(Right(Mono(Figure(((RareListingRow)row).Profit), ThemeManager.AKey))), "profit", "Est. per tonne", right: true),
        _sortFields.Field(
            "Total",
            new TableColumn("Total") { Min = 96 },
            "Total",
            row => Centered(Right(Mono(((RareListingRow)row).Total is { } total ? total.ToString("N0", CultureInfo.InvariantCulture) : Dash, StockInk((RareListingRow)row)))),
            "total",
            "Supply sold",
            right: true),
        _sortFields.Field(
            "Reported",
            new TableColumn("Reported") { Min = 100 },
            "Reported",
            row => Centered(Right(Typeset.Prose(
                ((RareListingRow)row).ReportedAt is { } at ? MissionsPage.Ago(DateTimeOffset.UtcNow - at) : Dash,
                TypeScale.Small,
                ThemeManager.GreyKey))),
            "reported",
            right: true),
        _sortFields.Field("SellsFor", new TableColumn("SellsFor") { Min = 96 }, "Sells for", row => Centered(Right(Mono(Figure(((RareListingRow)row).FarPrice), ThemeManager.AKey))), second: "Far away", right: true, byDefault: false, local: LocalSort.Figure<RareListingRow>(rare => rare.FarPrice)),
        new TableField<object>("RareRun", new TableColumn("RareRun") { Min = 116 }, row => RunCell((RareListingRow)row)),
    ];

    private static string StockInk(RareListingRow rare) => rare.Supply is > 0 ? ThemeManager.AKey : ThemeManager.GreyKey;

    private static TextBlock NameCell(RareListingRow rare)
    {
        var name = Value(rare.Rare.Name);
        name.FontSize = TypeScale.Secondary;

        return Centered(name);
    }

    private Control StationCell(RareListingRow rare)
    {
        var system = Typeset.Prose(
            rare.Rare.System,
            TypeScale.Tip,
            string.Equals(rare.Rare.System, _here(), StringComparison.OrdinalIgnoreCase) ? ThemeManager.CyanKey : ThemeManager.AKey,
            vertical: VerticalAlignment.Center);

        var station = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                Typeset.Prose(rare.Rare.Station, TypeScale.Tip, ThemeManager.WhiteKey, trimming: TextTrimming.CharacterEllipsis, vertical: VerticalAlignment.Center),
                CopyGlyph.LinkBeside(system, rare.Rare.System, _copy),
            },
        };

        return Centered(new Border { ClipToBounds = true, Child = station });
    }

    private Button RunCell(RareListingRow rare)
    {
        var tile = new Button
        {
            Name = RareRunName,
            Content = "RARE RUN",
            Height = TypeScale.MinimumTarget,
            Classes = { "on-row" },
            Tag = rare.Rare.Symbol,
        };
        Avalonia.Automation.AutomationProperties.SetName(tile, $"Rare run for {rare.Rare.Name}");
        tile.Click += (_, _) => _rareRun(rare);

        return Centered(tile);
    }

    private static Control Row(RareListingRow rare, ArrangedTable<object> table)
    {
        var grid = table.Row(rare);

        return ListRow.Dress(new Border { MinHeight = 54, Padding = new Thickness(12, 6), Child = grid, Classes = { GalaxySearchPage.RowClass } });
    }

    private static string Figure(int? value) =>
        value is { } figure ? figure.ToString("N0", CultureInfo.InvariantCulture) : Dash;

    private static TextBlock Mono(string text, string key) =>
        Typeset.Mono(text, TypeScale.Small, key, trimming: TextTrimming.CharacterEllipsis);
}

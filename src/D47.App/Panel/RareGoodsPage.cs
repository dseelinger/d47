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

    private static readonly TableColumn[] Columns =
    [
        new("Name", ColumnFit.Fill) { Min = 140 },
        new("Station", ColumnFit.Fill) { Min = 190 },
        new("FromStar") { Min = 118 },
        new("Distance") { Min = 60 },
        new("Supply") { Min = 56 },
        new("Price") { Min = 64 },
        new("Profit") { Min = 96 },
        new("Total") { Min = 96 },
        new("Reported") { Min = 100 },
        new("RareRun") { Min = 116 },
    ];

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

    private CancellationTokenSource? _inFlight;
    private (string Text, DateTimeOffset At)? _failure;
    private object? _seen;
    private bool _asked;

    public RareGoodsPage(
        GalaxySearchSurface searches,
        Func<string?> here,
        Func<string?> pledgedPower,
        Action<RareListingRow> rareRun,
        Func<string, Task<bool>>? copy,
        bool headset)
    {
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
            var result = await _searches.Registry
                .InvokeAsync(
                    "list_rares",
                    new ToolArguments(new Dictionary<string, string>(arguments, StringComparer.Ordinal)),
                    inFlight.Token,
                    ToolCaller.Commander)
                .ConfigureAwait(true);

            if (result.IsError)
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
            ? (decimal)Math.Clamp(asked, 1, 99999)
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
                top.Children.Add(Typeset.Prose("Asking for the rare goods…", TypeScale.Secondary, ThemeManager.GreyKey));
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
        var layout = new TableLayout(TableId, Columns, _headset ? null : ColumnWidths, 12);
        var now = DateTimeOffset.UtcNow;

        var list = new ItemsControl
        {
            Name = RowsName,
            ItemsSource = result.Rows.Select(row => Spaced(Row(row, layout, now))).ToArray(),
        };

        top.Children.Add(Head(last, layout));

        return LoadoutPages.HeaderAndList(top, list, out _);
    }

    private static Border Spaced(Control row) => new() { Margin = new Thickness(0, 2, 0, Gaps.Tile - 2), Child = row };

    /// <summary>One column of the header: its label, a second line, the <c>order_by</c> it sorts on, and which side its figures sit on.</summary>
    private sealed record HeadColumn(string Label, string? Second, string? Sort, bool Right);

    private static readonly HeadColumn[] Heads =
    [
        new("Name", null, "name", false),
        new("Station", "(System)", "station", false),
        new("From star", null, "from_star", true),
        new("LY", null, SearchSorts.Distance, true),
        new("Supply", null, "supply", true),
        new("Price", "Buy", "price", true),
        new("Profit", "Est. per tonne", "profit", true),
        new("Total", "Supply sold", "total", true),
        new("Reported", null, "reported", true),
        new(string.Empty, null, null, false),
    ];

    private Control Head(GalaxySearchPosting last, TableLayout layout)
    {
        var fields = SearchSorts.For(GalaxySearchKind.Rares);

        var specs = Heads
            .Select(column => fields.FirstOrDefault(field => field.Name == column.Sort) is { } field
                ? SortColumn<object>.Remote(column.Label, field.FirstDescending ? SortDirection.Descending : SortDirection.Ascending)
                  with { Second = column.Second }
                : SortColumn<object>.Plain(column.Label) with { Second = column.Second })
            .ToList();

        var head = new SortHeader<object>(HeadName, specs);
        var nearest = Array.FindIndex(Heads, column => column.Sort == SearchSorts.Distance);

        var current = new SortOrder(nearest, SortDirection.Ascending);

        if (SearchSorts.Read(GalaxySearchKind.Rares, last.Arguments) is { } asked
            && Array.FindIndex(Heads, column => column.Sort == asked.Field.Name) is var shown and >= 0)
        {
            current = new SortOrder(shown, asked.Descending ? SortDirection.Descending : SortDirection.Ascending);
        }

        head.Show(current);
        head.Changed += () => SortBy(last, head.Order);

        var grid = layout.Header(head, [.. Heads.Select(column => column.Right ? HorizontalAlignment.Right : HorizontalAlignment.Left)]);
        grid.Margin = new Thickness(12, 0, 12, 6);

        var rule = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Child = grid };
        ThemeInk.Bind(rule, Border.BorderBrushProperty, ThemeManager.LineKey);

        return rule;
    }

    /// <summary>Asks again sorted by <paramref name="order"/>; null is nearest first.</summary>
    private void SortBy(GalaxySearchPosting last, SortOrder? order)
    {
        var arguments = new Dictionary<string, string>(last.Arguments, StringComparer.Ordinal);
        arguments.Remove("order_by");
        arguments.Remove("order");

        if (order is { } sort && Heads[sort.Column].Sort is { } field)
        {
            arguments["order_by"] = field;
            arguments["order"] = sort.Direction == SortDirection.Ascending ? "asc" : "desc";
        }

        _ = RunAsync(arguments);
    }

    private Control Row(RareListingRow rare, TableLayout layout, DateTimeOffset now)
    {
        var name = Value(rare.Rare.Name);
        name.FontSize = TypeScale.Secondary;

        var station = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                Typeset.Prose(rare.Rare.Station, TypeScale.Tip, ThemeManager.WhiteKey, trimming: TextTrimming.CharacterEllipsis, vertical: VerticalAlignment.Center),
                Typeset.Prose(
                    $"({rare.Rare.System})",
                    TypeScale.Tip,
                    string.Equals(rare.Rare.System, _here(), StringComparison.OrdinalIgnoreCase) ? ThemeManager.CyanKey : ThemeManager.AKey,
                    vertical: VerticalAlignment.Center),
            },
        };

        var supplied = rare.Supply is > 0;
        var stockInk = supplied ? ThemeManager.AKey : ThemeManager.GreyKey;

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

        var grid = layout.Row(
            [
                Cell(name, 0),
                Cell(new Border { ClipToBounds = true, Child = CopyGlyph.Beside(station, rare.Rare.System, _copy) }, 1),
                Cell(Right(Mono(rare.Rare.DistanceToArrival is { } ls ? $"{ls.ToString("N0", CultureInfo.InvariantCulture)} ls" : Dash, ThemeManager.AKey)), 2),
                Cell(Right(Mono(rare.Distance.ToString("0.0", CultureInfo.InvariantCulture), ThemeManager.AKey)), 3),
                Cell(Right(Mono(rare.Supply is { } supply ? supply.ToString("N0", CultureInfo.InvariantCulture) : Dash, stockInk)), 4),
                Cell(Right(Mono(Figure(rare.Price), ThemeManager.AKey)), 5),
                Cell(Right(Mono(Figure(rare.Profit), ThemeManager.AKey)), 6),
                Cell(Right(Mono(rare.Total is { } total ? total.ToString("N0", CultureInfo.InvariantCulture) : Dash, stockInk)), 7),
                Cell(Right(Typeset.Prose(
                    rare.ReportedAt is { } at ? MissionsPage.Ago(now - at) : Dash,
                    TypeScale.Small,
                    ThemeManager.GreyKey)), 8),
                Cell(tile, 9),
            ]);

        return ListRow.Dress(new Border { MinHeight = 54, Padding = new Thickness(12, 6), Child = grid, Classes = { GalaxySearchPage.RowClass } });
    }

    private static string Figure(int? value) =>
        value is { } figure ? figure.ToString("N0", CultureInfo.InvariantCulture) : Dash;

    private static TextBlock Mono(string text, string key) =>
        Typeset.Mono(text, TypeScale.Small, key, trimming: TextTrimming.CharacterEllipsis);
}

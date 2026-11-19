using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>Commander › Ledgers › Codex › a region › Discoveries: the entries the codex table lists for the region, missing or on the record, under a status, category and sub-category filter.</summary>
public sealed class CodexDiscoveriesPage : UserControl, IPageSummary, IFilterablePage
{
    private const double SearchWidth = 96;

    public const string TableId = "Codex.Discoveries";

    private readonly Func<CommanderGameState?> _state;
    private readonly Func<HistoryState> _history;
    private readonly int? _regionId;
    private readonly CodexTable _table;
    private readonly bool _headset;
    private readonly PanelNavigator? _nav;
    private readonly Func<BodySearchLink?>? _bodySearch;
    private readonly CodexThumbnails? _thumbs;
    private readonly Dictionary<string, (CodexMissingRow Row, Border Square)> _squares = new(StringComparer.Ordinal);
    private readonly ArrangedTable<CodexMissingRow> _columns;
    private readonly ArrangedList _list;
    private readonly CodexDiscoveryFilters _filters = new();

    private CodexLedger? _seenLedger;
    private HistoryState _seenHistory;
    private string _query = string.Empty;
    private bool _seenSearch;

    public CodexDiscoveriesPage(
        Func<CommanderGameState?> state,
        Func<HistoryState> history,
        int? regionId,
        CodexTable? table = null,
        bool headset = false,
        PanelNavigator? nav = null,
        CodexPictureSet? pictures = null,
        ColumnWidthMemory? columnWidths = null,
        Func<BodySearchLink?>? bodySearch = null,
        ColumnArrangementMemory? columnArrangements = null)
    {
        _nav = nav;
        _bodySearch = bodySearch;
        _thumbs = pictures is null ? null : new CodexThumbnails(pictures);
        _thumbs?.Refresh();
        _state = state;
        _history = history;
        _regionId = regionId;
        _table = table ?? CodexCatalogue.Table;
        _headset = headset;

        _columns = new ArrangedTable<CodexMissingRow>(
            TableId, Fields(), columnArrangements, headset ? null : columnWidths, headset ? 12 : 16, CodexModel.MissingDefaultKey, "CodexMissingHead");
        _list = new ArrangedList(headset, "CodexMissingRows", item => item is CodexMissingRow row ? RowView(row) : null, _squares.Clear);
        _columns.Changed += () =>
        {
            _columns.Refresh();
            Draw();
        };
        _filters.Changed += (_, _) => Draw();

        Margin = new Thickness(14);
        Content = _list.View;
        Draw();
    }

    public string Summary => _headset ? CodexPage.HeadsetSummaryText : CodexPage.SummaryText;

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    public bool Filters => Build() is { Running: false, Known: > 0 };

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _thumbs?.Refresh();

        foreach (var (row, square) in _squares.Values)
        {
            Fill(square, row);
        }
    }

    public string FilterPlaceholder => CodexModel.DiscoveriesPlaceholder;

    public void Filter(string? query)
    {
        if (PageKit.Retarget(query, ref _query))
        {
            Draw();
        }
    }

    /// <summary>Redraws when the record or the history walk has changed.</summary>
    public bool Tick()
    {
        if (ReferenceEquals(_state()?.CodexLedger, _seenLedger) && _history() == _seenHistory && HasSearch() == _seenSearch)
        {
            return false;
        }

        Draw();

        return true;
    }

    private CodexDiscoveriesView Build() =>
        CodexModel.Discoveries(_state()?.CodexLedger ?? CodexLedger.None, _table, _regionId ?? -1, _history(), _filters.Filter, _query);

    private void Draw()
    {
        _seenLedger = _state()?.CodexLedger;
        _seenHistory = _history();

        var hasSearch = HasSearch();

        if (hasSearch != _seenSearch)
        {
            _seenSearch = hasSearch;
            _list.Forget();
        }

        var view = Build();

        _list.Top.Children.Clear();

        if (view.Running)
        {
            _list.Top.Children.Add(RoutingKit.Ink(CodexModel.RunningText, TypeScale.ControlLarge, ThemeManager.GreyKey, wrap: true));
            Rows([]);
            return;
        }

        _list.Top.Children.Add(Intro(view));

        if (view.Failed)
        {
            _list.Top.Children.Add(CodexKit.Warning(view.OnRecordSince));
        }

        var name = CodexModel.RegionName(_regionId);

        _list.Top.Children.Add(StarSystemPage.GroupHead(
            "Discoveries · " + name, CodexKit.Line(CodexModel.DiscoveriesGroupLine(_filters.Filter.Status))));
        _list.Top.Children.Add(CodexKit.Strip(Strip(view)));

        if (view.Known == 0)
        {
            _list.Top.Children.Add(CreditsKit.Message(view.Empty!));
            Rows([]);
            return;
        }

        _filters.Show(view.Categories, view.SubCategories, view.Shown, view.Known);
        _list.Top.Children.Add(_filters);
        _list.Top.Children.Add(Note(CodexModel.DiscoveriesNote(view.OnRecordSince, view.Uncovered)));

        if (view.Empty is { } empty)
        {
            _list.Top.Children.Add(CreditsKit.Message(empty));
            Rows([]);
            return;
        }

        if (view.Shown == 0)
        {
            _list.Top.Children.Add(CreditsKit.Message(CodexModel.NoMissingMatchText));
            Rows([]);
            return;
        }

        _list.Top.Children.Add(LedgerTables.Arranger(_columns));

        Rows(_columns.Sort(view.Rows));
    }

    private static StripCell[] Strip(CodexDiscoveriesView view) =>
    [
        new("Known", view.Known, ThemeManager.AKey, "in the codex table"),
        new("On record", view.Logged, ThemeManager.BlueKey, "logged at any time"),
        new("Missing", view.Missing, ThemeManager.AKey, "not on your record"),
        new("Subcategories", view.Subcategories, ThemeManager.AKey, "with missing entries"),
    ];

    private static TextBlock Note(string text)
    {
        var note = RoutingKit.Ink(text, TypeScale.Small, ThemeManager.GreyKey, wrap: true);
        note.Margin = new Thickness(0, 0, 0, 10);

        return note;
    }

    private static Grid Intro(CodexDiscoveriesView view)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 8) };
        row.Children.Add(RoutingKit.Ink(CodexModel.DiscoveriesLine, TypeScale.ControlLarge, ThemeManager.GreyKey, wrap: true));
        CodexKit.ShowSince(row, view.OnRecordSince, view.Failed);

        return row;
    }

    /// <summary>Shows <paramref name="items"/>, leaving the list and its scroll position alone when they are the items already shown.</summary>
    private void Rows(IReadOnlyList<CodexMissingRow> items) =>
        _list.Show(
            _columns,
            [.. items.Cast<object>()],
            new Thickness(LedgerTables.RowInset, 0, LedgerTables.RowInset + (_bodySearch is not null ? Gaps.Tile + SearchWidth : 0), 0));

    private IReadOnlyList<TableField<CodexMissingRow>> Fields()
    {
        var heads = CodexModel.MissingHeads();

        return
        [
            LedgerTables.Control(new TableColumn("Mark", ColumnFit.Fixed) { Fixed = 44 }, Thumbnail, heads[0]),
            StarSystemPage.Headed(heads[1], new TableColumn("Entry", ColumnFit.Fill), row => CreditsKit.Line(row.Name, Fonts.ProseFamily, TypeScale.ControlLarge, ThemeManager.WhiteKey)),
            StarSystemPage.Headed(heads[2], new TableColumn("Category", ColumnFit.Fill), row => CreditsKit.Line(row.Category, Fonts.ProseFamily, TypeScale.Small, ThemeManager.GreyKey)),
            StarSystemPage.Headed(heads[3], new TableColumn("SubCategory", ColumnFit.Fill), row => CreditsKit.Line(row.SubCategory, Fonts.ProseFamily, TypeScale.Small, ThemeManager.GreyKey)),
            StarSystemPage.Headed(heads[4], new TableColumn(CodexModel.MissingDefaultKey), row => CreditsKit.Mono(CodexModel.Number(row.Systems), TypeScale.Small, ThemeManager.AKey, right: false)),
            StarSystemPage.Headed(heads[5], new TableColumn("Status"), StatusCell),
        ];
    }

    private static TextBlock StatusCell(CodexMissingRow row)
    {
        var status = CreditsKit.Line(
            row.OnRecord ? "ON RECORD" : "MISSING", Fonts.ChromeFamily, TypeScale.Meta, row.OnRecord ? ThemeManager.BlueKey : ThemeManager.GreyKey);
        status.FontWeight = Avalonia.Media.FontWeight.SemiBold;

        return status;
    }

    private bool HasSearch() => _bodySearch?.Invoke() is not null;

    private Grid RowView(CodexMissingRow row)
    {
        var grid = _columns.Row(row);
        grid.VerticalAlignment = VerticalAlignment.Center;

        var button = ListRow.Dress(new Button
        {
            Name = "CodexMissing." + row.Key,
            Content = grid,
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });
        Avalonia.Automation.AutomationProperties.SetName(button, row.Name);
        button.Click += (_, _) => _nav?.Drill(CodexPage.EntryCrumb(_regionId, row.Key, row.Name));

        var line = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, Gaps.Tile) };
        line.Children.Add(button);

        if (SearchTile(row) is { } search)
        {
            Grid.SetColumn(search, 1);
            line.Children.Add(search);
        }

        return line;
    }

    /// <summary>The row's Search tile: for an entry the codex table lists that has a search.</summary>
    private Button? SearchTile(CodexMissingRow row)
    {
        if (_bodySearch?.Invoke() is not { } link
            || _table.Entry(row.Key) is not { } entry
            || CodexSearch.Arguments(entry, _regionId) is not { } arguments)
        {
            return null;
        }

        var tile = new Button
        {
            Name = "CodexSearch." + row.Key,
            Content = "Search",
            Width = SearchWidth,
            MinHeight = TypeScale.MinimumTarget,
            Margin = Gaps.TileBefore,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        Avalonia.Automation.AutomationProperties.SetName(tile, "Search " + row.Name);
        tile.Click += (_, _) => link.Run(arguments);

        return tile;
    }

    private Border Thumbnail(CodexMissingRow row)
    {
        var side = TypeScale.MinimumTarget;
        var square = new Border { Width = side, Height = side };
        ThemeInk.Bind(square, Border.BackgroundProperty, ThemeManager.SlabKey);

        if (_thumbs is not null)
        {
            _squares[row.Key] = (row, square);
            Fill(square, row);
        }

        return square;
    }

    /// <summary>The picture the entry page would show, else the family placeholder, else an empty square.</summary>
    private void Fill(Border square, CodexMissingRow row)
    {
        var side = TypeScale.MinimumTarget;
        var picture = _thumbs!.Picture(row.Key, _regionId);
        var bitmap = picture?.Bitmap ?? _thumbs.Family(row.PlaceholderFile);

        square.Child = bitmap is null
            ? null
            : new Image
            {
                Name = picture is not { } shown
                    ? "CodexThumbPlaceholder"
                    : shown.Choice.Source == CodexPictureSource.Local ? "CodexThumbLocal" : "CodexThumbStock",
                Source = bitmap,
                Stretch = Avalonia.Media.Stretch.Uniform,
                Width = side,
                Height = side,
            };
    }
}

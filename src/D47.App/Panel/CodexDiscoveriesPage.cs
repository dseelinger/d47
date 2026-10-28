using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>Commander › Ledgers › Codex › a region › Discoveries: the entries the codex table lists for the region, missing or on the record, under a status, category and sub-category filter.</summary>
public sealed class CodexDiscoveriesPage : UserControl, IPageSummary, IFilterablePage
{
    private const int ThumbnailPixels = 88;

    private const double SearchWidth = 96;

    public const string TableId = "Codex.Discoveries";

    private static readonly TableColumn[] ColumnList =
    [
        new("Mark", ColumnFit.Fixed) { Fixed = 44 },
        new("Entry", ColumnFit.Fill),
        new("Category", ColumnFit.Fill),
        new("SubCategory", ColumnFit.Fill),
        new("ReportedIn"),
        new("Status"),
    ];

    private readonly Func<CommanderGameState?> _state;
    private readonly Func<HistoryState> _history;
    private readonly int? _regionId;
    private readonly CodexTable _table;
    private readonly bool _headset;
    private TableLayout _layout;
    private readonly ColumnWidthMemory? _columnWidths;
    private readonly PanelNavigator? _nav;
    private readonly Func<BodySearchLink?>? _bodySearch;
    private readonly CodexPictureSet? _pictures;
    private readonly Dictionary<long, (Bitmap Bitmap, string Name)> _thumbs = [];
    private readonly Dictionary<string, Bitmap?> _families = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<long, (CodexMissingRow Row, Border Square)> _squares = [];
    private readonly SortHeader<CodexMissingRow> _head = new("CodexMissingHead", CodexModel.MissingHeads(), new SortOrder(CodexModel.MissingDefaultColumn, SortDirection.Descending));
    private readonly CodexDiscoveryFilters _filters = new();
    private readonly StackPanel _top = new();
    private readonly ItemsControl _list;
    private readonly ScrollViewer _scroll;

    private IReadOnlySet<CodexPictureKey> _local = new HashSet<CodexPictureKey>();
    private IReadOnlyList<CodexPictureRow> _stock = [];
    private IReadOnlyList<CodexMissingRow> _shown = [];
    private CodexLedger? _seenLedger;
    private HistoryState _seenHistory;
    private string _query = string.Empty;
    private bool? _seenLookups;

    public CodexDiscoveriesPage(
        Func<CommanderGameState?> state,
        Func<HistoryState> history,
        int? regionId,
        CodexTable? table = null,
        bool headset = false,
        PanelNavigator? nav = null,
        CodexPictureSet? pictures = null,
        ColumnWidthMemory? columnWidths = null,
        Func<BodySearchLink?>? bodySearch = null)
    {
        _nav = nav;
        _bodySearch = bodySearch;
        _pictures = pictures;
        Refresh();
        _state = state;
        _history = history;
        _regionId = regionId;
        _table = table ?? CodexCatalogue.Table;
        _headset = headset;
        _columnWidths = columnWidths;
        _layout = NewLayout();

        _head.Changed += Draw;
        _filters.Changed += (_, _) => Draw();

        _list = new ItemsControl
        {
            Name = "CodexMissingRows",
            ItemsPanel = new FuncTemplate<Avalonia.Controls.Panel?>(() => new VirtualizingStackPanel()),
            ItemTemplate = new FuncDataTemplate<object>((item, _) => item is CodexMissingRow row ? RowView(row) : null),
        };

        _scroll = new ScrollViewer
        {
            Content = new Border { Padding = new Thickness(0, 0, 14, 0), Child = _list },
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        _top.Margin = new Thickness(0, 0, 14, 0);
        grid.Children.Add(_top);
        Grid.SetRow(_scroll, 1);
        grid.Children.Add(_scroll);

        Margin = new Thickness(14);
        Content = grid;
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
        _head.Reset();
        Refresh();

        foreach (var (row, square) in _squares.Values)
        {
            Fill(square, row);
        }
    }

    public string FilterPlaceholder => CodexModel.DiscoveriesPlaceholder;

    public void Filter(string? query)
    {
        var trimmed = query?.Trim() ?? string.Empty;

        if (trimmed == _query)
        {
            return;
        }

        _query = trimmed;
        Draw();
    }

    /// <summary>Redraws when the record or the history walk has changed.</summary>
    public bool Tick()
    {
        if (ReferenceEquals(_state()?.CodexLedger, _seenLedger) && _history() == _seenHistory && Lookups() == _seenLookups)
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

        var lookups = Lookups();

        if (lookups != _seenLookups)
        {
            _seenLookups = lookups;
            _shown = [];
        }

        var view = Build();

        _top.Children.Clear();

        if (view.Running)
        {
            _top.Children.Add(RoutingKit.Ink(CodexModel.RunningText, TypeScale.ControlLarge, ThemeManager.GreyKey, wrap: true));
            Rows([]);
            return;
        }

        _top.Children.Add(Intro(view));

        if (view.Failed)
        {
            _top.Children.Add(CodexKit.Warning(view.OnRecordSince));
        }

        var name = CodexModel.RegionName(_regionId);

        _top.Children.Add(StarSystemPage.GroupHead(
            "Discoveries · " + name, CodexKit.Line(CodexModel.DiscoveriesGroupLine(_filters.Filter.Status))));
        _top.Children.Add(CodexKit.Strip(Strip(view)));

        if (view.Known == 0)
        {
            _top.Children.Add(CreditsKit.Message(view.Empty!));
            Rows([]);
            return;
        }

        _filters.Show(view.Categories, view.SubCategories, view.Shown, view.Known);
        _top.Children.Add(_filters);
        _top.Children.Add(Note(CodexModel.DiscoveriesNote(view.OnRecordSince, view.Uncovered)));

        if (view.Empty is { } empty)
        {
            _top.Children.Add(CreditsKit.Message(empty));
            Rows([]);
            return;
        }

        if (view.Shown == 0)
        {
            _top.Children.Add(CreditsKit.Message(CodexModel.NoMissingMatchText));
            Rows([]);
            return;
        }

        _layout = NewLayout();
        var header = CreditsKit.HeaderRow(_head, _layout, i => HorizontalAlignment.Left);

        if (_bodySearch is not null)
        {
            header.Margin = new Thickness(14, 0, 14 + Gaps.Tile + SearchWidth, 0);
        }

        _top.Children.Add(header);

        Rows(_head.Sort(view.Rows));
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
    private void Rows(IReadOnlyList<CodexMissingRow> items)
    {
        _scroll.IsVisible = items.Count > 0;

        if (items.Count > 0 && _shown.SequenceEqual(items))
        {
            return;
        }

        _shown = items;
        _squares.Clear();
        _list.ItemsSource = items;
    }

    private TableLayout NewLayout() => new(TableId, ColumnList, _columnWidths, _headset ? 12 : 16);

    private bool? Lookups() => _bodySearch?.Invoke()?.Enabled();

    private Grid RowView(CodexMissingRow row)
    {
        var grid = _layout.Row([]);
        grid.VerticalAlignment = VerticalAlignment.Center;

        var status = CreditsKit.Line(
            row.OnRecord ? "ON RECORD" : "MISSING", Fonts.ChromeFamily, TypeScale.Meta, row.OnRecord ? ThemeManager.BlueKey : ThemeManager.GreyKey);
        status.FontWeight = Avalonia.Media.FontWeight.SemiBold;

        CreditsKit.Place(grid, 0, Thumbnail(row));
        CreditsKit.Place(grid, 1, CreditsKit.Line(row.Name, Fonts.ProseFamily, TypeScale.ControlLarge, ThemeManager.WhiteKey));
        CreditsKit.Place(grid, 2, CreditsKit.Line(row.Category, Fonts.ProseFamily, TypeScale.Small, ThemeManager.GreyKey));
        CreditsKit.Place(grid, 3, CreditsKit.Line(row.SubCategory, Fonts.ProseFamily, TypeScale.Small, ThemeManager.GreyKey));
        CreditsKit.Place(grid, 4, CreditsKit.Mono(CodexModel.Number(row.Systems), TypeScale.Small, ThemeManager.AKey, right: false));
        CreditsKit.Place(grid, 5, status);

        var button = ListRow.Dress(new Button
        {
            Name = "CodexMissing." + row.EntryId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Content = grid,
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });
        Avalonia.Automation.AutomationProperties.SetName(button, row.Name);
        button.Click += (_, _) => _nav?.Drill(CodexPage.EntryCrumb(_regionId, row.EntryId, row.Name));

        var line = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, Gaps.Tile) };
        line.Children.Add(button);

        if (SearchTile(row) is { } search)
        {
            Grid.SetColumn(search, 1);
            line.Children.Add(search);
        }

        return line;
    }

    /// <summary>The row's Search tile: for an entry the codex table lists, disabled while the galaxy lookups are off.</summary>
    private Button? SearchTile(CodexMissingRow row)
    {
        if (_bodySearch?.Invoke() is not { } link || _table.Entry(row.EntryId) is not { } entry)
        {
            return null;
        }

        var enabled = link.Enabled();
        var tile = new Button
        {
            Name = "CodexSearch." + row.EntryId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Content = "Search",
            IsEnabled = enabled,
            Width = SearchWidth,
            MinHeight = TypeScale.MinimumTarget,
            Margin = Gaps.TileBefore,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        Avalonia.Automation.AutomationProperties.SetName(tile, "Search " + row.Name);
        ToolTip.SetShowOnDisabled(tile, true);
        ToolTip.SetTip(tile, enabled ? null : GalaxySearchPage.LookupsOffTip);
        tile.Click += (_, _) => link.Run(new Dictionary<string, string>(StringComparer.Ordinal) { ["codex_entry"] = entry.Name });

        return tile;
    }

    private Border Thumbnail(CodexMissingRow row)
    {
        var side = TypeScale.MinimumTarget;
        var square = new Border { Width = side, Height = side };
        RoutingKit.Themed(square, Border.BackgroundProperty, ThemeManager.SlabKey);

        if (_pictures is not null)
        {
            _squares[row.EntryId] = (row, square);
            Fill(square, row);
        }

        return square;
    }

    /// <summary>The picture the entry page would show, else the family placeholder, else an empty square.</summary>
    private void Fill(Border square, CodexMissingRow row)
    {
        var side = TypeScale.MinimumTarget;
        var picture = PictureOf(row.EntryId);
        var bitmap = picture?.Bitmap ?? Family(row.PlaceholderFile);

        square.Child = bitmap is null
            ? null
            : new Image
            {
                Name = picture?.Name ?? "CodexThumbPlaceholder",
                Source = bitmap,
                Stretch = Avalonia.Media.Stretch.Uniform,
                Width = side,
                Height = side,
            };
    }

    /// <summary>Re-reads which pictures exist and drops the thumbnails decoded before.</summary>
    private void Refresh()
    {
        if (_pictures is null)
        {
            return;
        }

        _thumbs.Clear();
        _local = _pictures.Store.LocalKeys();
        _stock = _pictures.Store.StockRows();
    }

    private (Bitmap Bitmap, string Name)? PictureOf(long entryId)
    {
        if (_thumbs.TryGetValue(entryId, out var cached))
        {
            return cached;
        }

        var store = _pictures!.Store;
        var choice = CodexPictures.Find(entryId, _regionId, _local, _stock);
        var bytes = choice is null
            ? null
            : choice.Source == CodexPictureSource.Local ? store.Local(choice.Key) : store.StockOnDisk(choice.Row!);

        if (Decode(bytes) is not { } bitmap)
        {
            return null;
        }

        var shown = (bitmap, choice!.Source == CodexPictureSource.Local ? "CodexThumbLocal" : "CodexThumbStock");
        _thumbs[entryId] = shown;

        return shown;
    }

    private Bitmap? Family(string? file)
    {
        if (file is null)
        {
            return null;
        }

        if (!_families.TryGetValue(file, out var bitmap))
        {
            _families[file] = bitmap = Decode(_pictures!.Placeholder(file));
        }

        return bitmap;
    }

    private static Bitmap? Decode(byte[]? bytes)
    {
        if (bytes is null)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(bytes);

            return Bitmap.DecodeToWidth(stream, ThumbnailPixels);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or NotSupportedException)
        {
            return null;
        }
    }
}

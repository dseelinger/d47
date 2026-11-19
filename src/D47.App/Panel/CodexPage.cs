using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>Commander › Ledgers › Codex › Regions: the codex entries in the journals on this PC, by region (#1290).</summary>
public sealed class CodexPage : UserControl, IPageSummary, IFilterablePage
{
    public const string PageKey = "codex";

    private const string RegionPrefix = "codex-region-";

    private const string DiscoveriesPrefix = "codex-discoveries-";

    private const string EntryPrefix = "codex-entry-";

    /// <summary>The key of the Regions page.</summary>
    public const string RegionsKey = "codex-regions";

    /// <summary>The key of the Possible firsts page; it shares no prefix with the others.</summary>
    public const string PossibleFirstsKey = "codex-possible-firsts";

    private const string ChoosePrefix = "codex-choose-";

    private const string CropPrefix = "codex-crop-";

    private const string RecropPrefix = "codex-recrop-";

    private const string NodePrefix = "codex-catalogue-";

    public const string SummaryText = "The codex entries you have logged, from your journals.";

    public const string HeadsetSummaryText = "The codex entries you have logged.";

    public const string TableId = "Codex.Regions";

    private readonly Func<CommanderGameState?> _state;
    private readonly Func<HistoryState> _history;
    private readonly Func<DateTimeOffset> _now;
    private readonly CodexChoice _choice;
    private readonly PanelNavigator _nav;
    private readonly bool _headset;
    private readonly ArrangedTable<CodexRegionRow> _columns;
    private readonly Segment _period = CodexKit.PeriodSegment();
    private readonly Grid _row;
    private readonly StackPanel _body = new();

    private CodexLedger? _seenLedger;
    private HistoryState _seenHistory;
    private long _seenMinute;
    private string _query = string.Empty;

    public CodexPage(
        Func<CommanderGameState?> state,
        Func<HistoryState> history,
        Func<DateTimeOffset> now,
        CodexChoice choice,
        PanelNavigator nav,
        bool headset = false,
        ColumnWidthMemory? columnWidths = null,
        ColumnArrangementMemory? columnArrangements = null)
    {
        _state = state;
        _history = history;
        _now = now;
        _choice = choice;
        _nav = nav;
        _headset = headset;

        _columns = new ArrangedTable<CodexRegionRow>(
            TableId, Fields(), columnArrangements, headset ? null : columnWidths, headset ? 12 : 16, headName: "CodexHead");
        _row = CodexKit.PeriodRow(_period);
        _columns.Changed += Draw;
        _period.SelectedIndex = choice.Period;
        _period.SelectionChanged += (_, _) =>
        {
            _choice.Period = _period.SelectedIndex;
            Draw();
        };

        Margin = new Thickness(14);
        Content = LoadoutPages.Scrolling(new StackPanel { Children = { _body } });
        Draw();
    }

    public string Summary => _headset ? HeadsetSummaryText : SummaryText;

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    public bool Filters => Build() is { Running: false } view && (view.Regions > 0 || view.OtherRegions > 0);

    public string FilterPlaceholder => CodexModel.Placeholder;

    /// <summary>The crumb of a region's page; null for a region D47 has no number for.</summary>
    public static NavCrumb RegionCrumb(int? regionId) =>
        new(RegionPrefix + (regionId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"), CodexModel.RegionName(regionId))
        {
            Whole = true,
        };

    public static bool IsRegion(string key) => key.StartsWith(RegionPrefix, StringComparison.Ordinal);

    /// <summary>The region number a region crumb is for; null for the unnumbered region.</summary>
    public static int? RegionOf(string key) =>
        int.TryParse(key[RegionPrefix.Length..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id)
            ? id
            : null;

    /// <summary>The crumb of a region's Discoveries page.</summary>
    public static NavCrumb DiscoveriesCrumb(int? regionId) =>
        new(
            DiscoveriesPrefix + (regionId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"),
            CodexModel.DiscoveriesCrumbWord)
        {
            Whole = true,
        };

    public static bool IsDiscoveries(string key) => key.StartsWith(DiscoveriesPrefix, StringComparison.Ordinal);

    /// <summary>The region number a Discoveries crumb is for; null for the unnumbered region.</summary>
    public static int? DiscoveriesOf(string key) =>
        int.TryParse(key[DiscoveriesPrefix.Length..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id)
            ? id
            : null;

    /// <summary>The crumb of the Possible firsts page.</summary>
    public static NavCrumb PossibleFirstsCrumb() => new(PossibleFirstsKey, CodexModel.PossibleFirstsCrumbWord) { Whole = true };

    public static bool IsPossibleFirsts(string key) => key == PossibleFirstsKey;

    /// <summary>The crumb of the Regions page.</summary>
    public static NavCrumb RegionsCrumb() => new(RegionsKey, CodexModel.RegionsCrumbWord) { Whole = true };

    public static bool IsRegions(string key) => key == RegionsKey;

    /// <summary>The crumb of a codex tree node's catalogue page, named for the node; the root's is the Codex ledger.</summary>
    public static NavCrumb NodeCrumb(CodexNode node) =>
        node.Parent is null
            ? LedgersPage.Ledger(PageKey, "Codex")
            : new(NodePrefix + node.Key, CodexCatalogueModel.Display(node.ShortName)) { Whole = true };

    /// <summary>The crumbs to a genus's catalogue page: the Codex root, its family, the genus; the root alone where the tree has no such group.</summary>
    public static IReadOnlyList<NavCrumb> GenusTrail(CodexTree tree, string genus) =>
    [
        NodeCrumb(tree.Root),
        .. CodexCatalogueModel.GenusNode(tree, genus) is { } node ? CodexCatalogueModel.PathTo(node).Select(NodeCrumb) : [],
    ];

    /// <summary>Whether the key is the Codex root, the catalogue, or a node's page under it.</summary>
    public static bool IsCatalogue(string key) => key == PageKey || key.StartsWith(NodePrefix, StringComparison.Ordinal);

    /// <summary>The tree key of the node a catalogue crumb is for; null when the key is not one.</summary>
    public static string? CatalogueNodeOf(string key) =>
        key == PageKey ? CodexTree.RootKey : key.StartsWith(NodePrefix, StringComparison.Ordinal) ? key[NodePrefix.Length..] : null;

    /// <summary>The crumb of one entry's page in a region, by its codex key, named for the entry.</summary>
    public static NavCrumb EntryCrumb(int? regionId, string key, string name) =>
        new(
            EntryPrefix + (regionId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none") + "-" + key,
            name)
        {
            Whole = true,
        };

    public static bool IsEntry(string key) => key.StartsWith(EntryPrefix, StringComparison.Ordinal);

    /// <summary>The crumb of the page that lists the captures to cut a picture from.</summary>
    public static NavCrumb ChooseCrumb(CodexPictureKey key) => new(ChoosePrefix + Tail(key), CodexModel.ChooseWord) { Whole = true };

    /// <summary>The crumb of the crop page, reached from the choice, or from Re-crop when <paramref name="kept"/>.</summary>
    public static NavCrumb CropCrumb(CodexPictureKey key, bool kept) =>
        new((kept ? RecropPrefix : CropPrefix) + Tail(key), CodexModel.CropWord) { Whole = true };

    public static bool IsChoose(string key) => key.StartsWith(ChoosePrefix, StringComparison.Ordinal);

    public static bool IsCrop(string key) =>
        key.StartsWith(CropPrefix, StringComparison.Ordinal) || key.StartsWith(RecropPrefix, StringComparison.Ordinal);

    /// <summary>Whether the key is a Choose a picture or a Crop page.</summary>
    public static bool IsPicturePage(string key) => IsChoose(key) || IsCrop(key);

    /// <summary>Whether a crop crumb is the Re-crop one, which cuts from the kept source.</summary>
    public static bool IsRecrop(string key) => key.StartsWith(RecropPrefix, StringComparison.Ordinal);

    /// <summary>The picture a Choose or Crop crumb is for; null when the key is not one.</summary>
    public static CodexPictureKey? PictureOf(string key)
    {
        var prefix = new[] { ChoosePrefix, CropPrefix, RecropPrefix }.FirstOrDefault(p => key.StartsWith(p, StringComparison.Ordinal));

        if (prefix is null || key[prefix.Length..].Split('-') is not [var region, var entry]
            || !int.TryParse(region, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var regionId)
            || !CodexKeys.IsValid(entry))
        {
            return null;
        }

        return new CodexPictureKey(entry, regionId);
    }

    private static string Tail(CodexPictureKey key) =>
        key.RegionId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + key.Key;

    /// <summary>The region number and codex key an entry crumb is for; null when the key is not one.</summary>
    public static (int? RegionId, string Key)? EntryOf(string key)
    {
        if (!IsEntry(key) || key[EntryPrefix.Length..].Split('-') is not [var region, var entry] || !CodexKeys.IsValid(entry))
        {
            return null;
        }

        return (int.TryParse(region, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id) ? id : null, entry);
    }

    public void Filter(string? query)
    {
        if (PageKit.Retarget(query, ref _query))
        {
            Draw();
        }
    }

    /// <summary>Redraws when the record, the history walk, the minute or the period chosen on the other page has changed.</summary>
    public bool Tick()
    {
        if (ReferenceEquals(_state()?.CodexLedger, _seenLedger)
            && _history() == _seenHistory
            && PageKit.Minute(_now()) == _seenMinute
            && _choice.Period == _period.SelectedIndex)
        {
            return false;
        }

        _period.SelectedIndex = _choice.Period;
        Draw();

        return true;
    }


    private CodexPeriod Period => CodexKit.PeriodOf(_period);

    private CodexRootView Build() =>
        CodexModel.Regions(_state()?.CodexLedger ?? CodexLedger.None, Period, _now(), _history(), _query);

    private void Draw()
    {
        _seenLedger = _state()?.CodexLedger;
        _seenHistory = _history();
        _seenMinute = PageKit.Minute(_now());

        var view = Build();

        _body.Children.Clear();
        _columns.Refresh();

        if (view.Running)
        {
            _body.Children.Add(RoutingKit.Ink(CodexModel.RunningText, TypeScale.ControlLarge, ThemeManager.GreyKey, wrap: true));
            return;
        }

        CodexKit.ShowSince(_row, view.OnRecordSince, view.Failed);
        _body.Children.Add(_row);

        if (view.Failed)
        {
            _body.Children.Add(CodexKit.Warning(view.OnRecordSince));
        }

        var shown = view.Rows.Count;
        var count = shown == view.Regions
            ? CodexModel.RegionCount(view.Regions)
            : $"{CodexModel.Number(shown)} of {CodexModel.RegionCount(view.Regions)}";

        _body.Children.Add(StarSystemPage.GroupHead(CodexModel.PeriodName(Period), CodexKit.Line(view.Line), count));

        if (view.Empty is { } empty)
        {
            _body.Children.Add(CreditsKit.Message(empty));
        }
        else
        {
            _body.Children.Add(CodexKit.Strip(view.Entries, view.New, view.Logged, "Regions", view.Regions));

            if (shown > 0)
            {
                _body.Children.Add(Table(view.Rows));
            }
            else if (view.Others.Count == 0)
            {
                _body.Children.Add(CreditsKit.Message(CodexModel.NoRegionMatchText));
            }
        }

        if (view.Others.Count > 0)
        {
            var others = view.Others.Count == view.OtherRegions
                ? CodexModel.RegionCount(view.OtherRegions)
                : $"{CodexModel.Number(view.Others.Count)} of {CodexModel.RegionCount(view.OtherRegions)}";

            _body.Children.Add(new StackPanel
            {
                Margin = new Thickness(0, 14, 0, 0),
                Spacing = Gaps.Tile,
                Children =
                {
                    StarSystemPage.GroupHead(CodexModel.OtherRegionsHead, CodexKit.Line(view.OtherLine), others),
                    Table(view.Others, view.Rows.Count == 0),
                },
            });
        }
        else if (view.Empty is not null && !string.IsNullOrEmpty(_query))
        {
            _body.Children.Add(CreditsKit.Message(CodexModel.NoRegionMatchText));
        }
    }

    private StackPanel Table(IReadOnlyList<CodexRegionRow> rows, bool header = true)
    {
        var columns = _columns;
        var list = new StackPanel { Spacing = Gaps.Tile };
        var table = new StackPanel { Spacing = Gaps.Tile };

        if (header)
        {
            table.Children.Add(LedgerTables.Arranger(columns));
            list.Children.Add(LedgerTables.Head(columns, new Thickness(LedgerTables.RowInset, 0)));
        }

        foreach (var row in columns.Sort(rows))
        {
            list.Children.Add(RowView(row, columns));
        }

        table.Children.Add(LedgerTables.Sideways(columns, list));

        return table;
    }

    private IReadOnlyList<TableField<CodexRegionRow>> Fields()
    {
        var heads = CodexModel.RegionHeads();

        return
        [
            StarSystemPage.Headed(heads[0], new TableColumn("Region", ColumnFit.Fill), NameOf),
            StarSystemPage.Headed(heads[1], new TableColumn("New"), row => PeriodFigure(row, row.New, ThemeManager.BlueKey), right: true),
            StarSystemPage.Headed(heads[2], new TableColumn("Logged"), row => PeriodFigure(row, row.Logged, ThemeManager.AKey), right: true),
            StarSystemPage.Headed(heads[3], new TableColumn("Entries"), row => PeriodFigure(row, row.Entries, ThemeManager.AKey), right: true),
            StarSystemPage.Headed(heads[4], new TableColumn("Discoveries"), RecordedCell, right: true),
            LedgerTables.Control(
                new TableColumn("Open", ColumnFit.Fixed) { Fixed = 16 },
                _ => CreditsKit.Line("›", Fonts.ChromeFamily, TypeScale.ControlLarge, ThemeManager.AKey),
                heads[5],
                right: true),
        ];
    }

    private static Control RecordedCell(CodexRegionRow row) =>
        row is { Recorded: { } recorded, Known: { } known }
            ? new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Children =
                {
                    CreditsKit.Mono(CodexModel.Number(recorded), TypeScale.Small, ThemeManager.WhiteKey, right: true),
                    CreditsKit.Mono(" / " + CodexModel.Number(known), TypeScale.Small, ThemeManager.GreyKey, right: true),
                },
            }
            : new Border();

    private Button RowView(CodexRegionRow row, ArrangedTable<CodexRegionRow> columns)
    {
        var grid = columns.Row(row);
        grid.VerticalAlignment = VerticalAlignment.Center;

        var button = ListRow.Dress(new Button
        {
            Name = "CodexRegion." + (row.RegionId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"),
            Content = grid,
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });
        Avalonia.Automation.AutomationProperties.SetName(button, row.Name);
        button.Click += (_, _) => _nav.Drill(RegionCrumb(row.RegionId));

        return button;
    }

    private static TextBlock PeriodFigure(CodexRegionRow row, int value, string key) =>
        row.Other
            ? CreditsKit.Mono("—", TypeScale.Small, ThemeManager.Grey2Key, right: true)
            : CreditsKit.Mono(CodexModel.Number(value), TypeScale.Small, key, right: true);

    private static StackPanel NameOf(CodexRegionRow row)
    {
        var cell = new StackPanel
        {
            Children =
            {
                CreditsKit.Line(row.Name, Fonts.ProseFamily, TypeScale.ControlLarge, row.Named ? ThemeManager.WhiteKey : ThemeManager.GreyKey),
            },
        };

        if (row.Detail.Length > 0)
        {
            cell.Children.Add(CreditsKit.Line(row.Detail, Fonts.ProseFamily, TypeScale.Small, ThemeManager.GreyKey));
        }

        return cell;
    }
}

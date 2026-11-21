using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>Commander › Ledgers › Codex › Possible firsts: every record in the region-first store, newest first.</summary>
public sealed class CodexPossibleFirstsPage : UserControl, IPageSummary, IFilterablePage
{
    public const string TableId = "Codex.PossibleFirsts";

    public const string SummaryText = "Codex entries you may be the first to log in their region.";

    public const string PreviousName = "CodexPossibleFirstsPrevious";

    public const string NextName = "CodexPossibleFirstsNext";

    /// <summary>The key of the column the default order follows.</summary>
    public const string LoggedKey = "Logged";

    private readonly Func<CommanderGameState?> _state;
    private readonly RegionFirstBook? _book;
    private readonly PanelNavigator? _nav;
    private readonly bool _headset;
    private readonly Func<string, Task<bool>>? _copy;
    private readonly ArrangedTable<RegionFirstRecord> _columns;
    private readonly StackPanel _body = new();

    private readonly RegionFirstPress? _press;
    private long _seenPress;
    private long _seenVersion;
    private string? _seenSystem;
    private string? _seenCommander;
    private string _query = string.Empty;
    private int _page;
    private readonly CodexThumbnails? _thumbs;

    public CodexPossibleFirstsPage(
        Func<CommanderGameState?> state,
        RegionFirstBook? book,
        PanelNavigator? nav = null,
        bool headset = false,
        ColumnWidthMemory? columnWidths = null,
        ColumnArrangementMemory? columnArrangements = null,
        Func<string, Task<bool>>? copy = null,
        RegionFirstPress? press = null,
        CodexPictureSet? pictures = null)
    {
        _press = press;
        _thumbs = pictures is null ? null : new CodexThumbnails(pictures);
        _thumbs?.Refresh();
        _state = state;
        _book = book;
        _nav = nav;
        _headset = headset;
        _copy = copy;

        _columns = new ArrangedTable<RegionFirstRecord>(
            TableId, Fields(), columnArrangements, headset ? null : columnWidths, headset ? 12 : 16, LoggedKey, "CodexPossibleFirstsHead");
        _columns.Sorted += () => _page = 0;
        _columns.FilterChanged += () => _page = 0;
        _columns.Changed += Draw;

        Margin = new Thickness(14);
        Content = LoadoutPages.Scrolling(new StackPanel { Children = { _body } });
        Draw();
    }

    public string Summary => SummaryText;

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    public bool Filters => Records().Count > 0;

    public string FilterPlaceholder => CodexModel.PossibleFirstsPlaceholder;

    public void Filter(string? query)
    {
        if (PageKit.Retarget(query, ref _query))
        {
            _page = 0;
            Draw();
        }
    }

    /// <summary>Redraws when a record is added or reported, or the current system or Commander changes.</summary>
    public bool Tick()
    {
        if ((_book?.Version ?? 0) == _seenVersion
            && (_press?.Version ?? 0) == _seenPress
            && _state()?.Location.StarSystem == _seenSystem
            && _state()?.Identity.FrontierId == _seenCommander)
        {
            return false;
        }

        Draw();

        return true;
    }

    private IReadOnlyList<RegionFirstRecord> Records() =>
        _book is not null && _state()?.Identity.FrontierId is { Length: > 0 } id ? _book.For(id) : [];

    /// <summary>Redraws when the pictures on disk have changed since the last draw.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _columns.Reopen();

        if (_thumbs?.Refresh() == true)
        {
            Draw();
        }
    }

    private void Draw()
    {
        _seenVersion = _book?.Version ?? 0;
        _seenPress = _press?.Version ?? 0;
        _seenSystem = _state()?.Location.StarSystem;
        _seenCommander = _state()?.Identity.FrontierId;

        var records = Records();
        var shown = CodexModel.PossibleFirstsMatching(records, _query);

        _body.Children.Clear();
        _columns.Refresh();

        _body.Children.Add(RoutingKit.Ink(CodexModel.PossibleFirstsLine(records), TypeScale.Small, ThemeManager.GreyKey, wrap: true));

        if (shown.Count == 0)
        {
            _body.Children.Add(CreditsKit.Message(CodexModel.PossibleFirstsNoMatchText));
            return;
        }

        if (ColumnFilter.Empty(_columns, shown) is { } hidden)
        {
            _body.Children.Add(LedgerTables.Arranger(_columns, shown));
            _body.Children.Add(hidden);
            return;
        }

        var sorted = _columns.Shown(shown);

        IReadOnlyList<RegionFirstRecord> page = sorted;

        if (_headset)
        {
            _page = FactionModel.ClampPage(_page, sorted.Count);
            page = [.. sorted.Skip(_page * FactionModel.PageSize).Take(FactionModel.PageSize)];
        }

        var list = new StackPanel { Spacing = Gaps.Tile };
        list.Children.Add(LedgerTables.Head(_columns, new Thickness(LedgerTables.RowInset, 0)));

        foreach (var record in page)
        {
            var view = RowView(record);
            view.IsVisible = !ColumnFilter.HidesRows(_columns);
            list.Children.Add(view);
        }

        _body.Children.Add(LedgerTables.Arranger(_columns, shown));
        _body.Children.Add(LedgerTables.Sideways(_columns, list));

        if (_headset && sorted.Count > FactionModel.PageSize)
        {
            _body.Children.Add(Pager(sorted.Count));
        }
    }

    private IReadOnlyList<TableField<RegionFirstRecord>> Fields()
    {
        var heads = CodexModel.PossibleFirstHeads();

        return
        [
            CodexThumbnails.Column<RegionFirstRecord>(Square),
            StarSystemPage.Headed(heads[0], new TableColumn("Entry", ColumnFit.Fill), EntryCell)
                with { Filters = [ValueLists.One<RegionFirstRecord>("Kind", record => CodexModel.RegionFirstWord(record.First), "No kind")] },
            StarSystemPage.Headed(heads[1], new TableColumn("Region", ColumnFit.Fill), record =>
                CreditsKit.Line(record.Region ?? CodexModel.NoRegionName, Fonts.ProseFamily, TypeScale.ControlLarge, ThemeManager.WhiteKey))
                with { Filters = [ValueLists.One<RegionFirstRecord>("Region", record => record.Region ?? CodexModel.NoRegionName, "No region")] },
            StarSystemPage.Headed(heads[2], new TableColumn("System", ColumnFit.Fill), SystemCell)
                with
                {
                    Filters =
                    [
                        ValueLists.One<RegionFirstRecord>("System", record => record.System, "No system"),
                        ValueLists.One<RegionFirstRecord>("Body", record => CodexModel.BodyOf(record.System, record.Body), "No body"),
                    ],
                },
            StarSystemPage.Headed(heads[3], new TableColumn(LoggedKey), record =>
                CreditsKit.Mono(CodexModel.DateTime(record.At), TypeScale.Small, ThemeManager.WhiteKey, right: true), right: true),
            StarSystemPage.Headed(heads[4], new TableColumn("Report"), ReportCell)
                with { Filters = [ValueLists.One<RegionFirstRecord>("Report", record => record.Reported is null ? "Not reported" : "Reported", "No report")] },
            LedgerTables.Control(new TableColumn("ReportTile", ColumnFit.Fixed) { Fixed = 190 }, TileCell, heads[5]),
            LedgerTables.Control(new TableColumn("Open", ColumnFit.Fixed) { Fixed = TypeScale.MinimumTarget }, OpenCell, heads[6], right: true),
        ];
    }

    private Border Square(RegionFirstRecord record)
    {
        var key = CodexKeys.Of(record.EntryId);

        return CodexThumbnails.Square(
            _thumbs?.Entry(key, record.RegionId, CodexFamilies.FileOfTableName(CodexCatalogue.Table.Entry(key)?.SubCategory)));
    }

    private static StackPanel EntryCell(RegionFirstRecord record)
    {
        var kind = CreditsKit.Line(CodexModel.RegionFirstWord(record.First), Fonts.ChromeFamily, TypeScale.MetaSmall, ThemeManager.CyanKey);
        kind.FontWeight = Avalonia.Media.FontWeight.SemiBold;

        return new StackPanel
        {
            Children =
            {
                CreditsKit.Line(EntryName(record), Fonts.ProseFamily, TypeScale.ControlLarge, ThemeManager.WhiteKey),
                kind,
            },
        };
    }

    private static string EntryName(RegionFirstRecord record) =>
        record.Name ?? record.EntryId.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private StackPanel SystemCell(RegionFirstRecord record)
    {
        var cell = new StackPanel();
        var here = record.System is { } system && string.Equals(system, _state()?.Location.StarSystem, StringComparison.OrdinalIgnoreCase);

        if (record.System is { } name)
        {
            cell.Children.Add(CopyGlyph.LinkBeside(
                CreditsKit.Line(name, Fonts.ChromeFamily, TypeScale.ControlLarge, here ? ThemeManager.CyanKey : ThemeManager.AKey),
                name,
                _copy));
        }

        var body = CodexModel.BodyOf(record.System, record.Body);
        var position = CodexModel.PositionOf(record.Latitude, record.Longitude);

        if (!string.IsNullOrWhiteSpace(body) || position is not null)
        {
            var place = RoutingKit.Ink(string.Empty, TypeScale.Small, ThemeManager.GreyKey);
            place.TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;

            if (!string.IsNullOrWhiteSpace(body))
            {
                place.Inlines!.Add(new Run(body));
            }

            if (position is not null)
            {
                place.Inlines!.Add(new Run(string.IsNullOrWhiteSpace(body) ? string.Empty : " · "));
                var mono = CreditsKit.Colored(position, ThemeManager.GreyKey);
                mono.FontSize = TypeScale.Meta;
                place.Inlines.Add(mono);
            }

            cell.Children.Add(place);
        }

        return cell;
    }

    private Control TileCell(RegionFirstRecord record) =>
        _press is not null && RegionFirstPress.Offers(record) ? _press.Tile(record) : new Border();

    private Control ReportCell(RegionFirstRecord record)
    {
        if (_press?.Failure(record, inline: true) is { } failed)
        {
            return failed;
        }

        if (record.Reported is not { } reported)
        {
            return CreditsKit.Line("NOT REPORTED", Fonts.ChromeFamily, TypeScale.Meta, ThemeManager.GreyKey);
        }

        var state = RoutingKit.Ink(string.Empty, TypeScale.Meta, ThemeManager.BlueKey);
        state.Inlines!.Add(new Run("✓ REPORTED "));
        state.Inlines.Add(CreditsKit.Colored(CodexModel.Date(reported), ThemeManager.BlueKey));
        state.FontFamily = Fonts.ChromeFamily;
        state.FontWeight = Avalonia.Media.FontWeight.SemiBold;

        return state;
    }

    private Control OpenCell(RegionFirstRecord record)
    {
        var button = HullMarks.Glyph("›", "Open " + EntryName(record));
        button.Name = "CodexPossibleFirst.Open." + record.EntryId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "." + record.RegionId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        button.Click += (_, _) => _nav?.Drill(CodexPage.EntryCrumb(record.RegionId, CodexKeys.Of(record.EntryId), EntryName(record)));

        return button;
    }

    private Border RowView(RegionFirstRecord record)
    {
        var grid = _columns.Row(record);
        grid.VerticalAlignment = VerticalAlignment.Center;

        return ListRow.Dress(new Border
        {
            Name = "CodexPossibleFirst." + record.EntryId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "." + record.RegionId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Child = grid,
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 0, 0, 0),
        });
    }

    private Grid Pager(int total)
    {
        var readout = CreditsKit.Mono(FactionModel.PageReadout(_page, total), TypeScale.Tip, ThemeManager.GreyKey, right: false);
        readout.VerticalAlignment = VerticalAlignment.Center;

        var previous = new Button { Name = PreviousName, Content = "◄ PREVIOUS", Height = TypeScale.MinimumTarget, IsEnabled = _page > 0 };
        var next = new Button
        {
            Name = NextName,
            Content = "NEXT ►",
            Height = TypeScale.MinimumTarget,
            IsEnabled = FactionModel.ClampPage(_page + 1, total) > _page,
        };
        previous.Click += (_, _) =>
        {
            _page--;
            Draw();
        };
        next.Click += (_, _) =>
        {
            _page++;
            Draw();
        };

        var tiles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tile, Children = { previous, next } };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 8, 0, 0) };
        Grid.SetColumn(tiles, 1);
        grid.Children.Add(readout);
        grid.Children.Add(tiles);

        return grid;
    }
}

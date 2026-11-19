using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>
/// Commander › Ledgers › Codex and every level under it: one node of the codex tree, its child groups as rows
/// or as a segmented control over one kinds list, and its kinds, under the shared status and region filter.
/// </summary>
public sealed class CodexCataloguePage : UserControl, IPageSummary
{
    public const string GroupsTableId = "Codex.Catalogue.Groups";

    public const string KindsTableId = "Codex.Catalogue.Kinds";

    public const string PreviousName = "CodexCataloguePrevious";

    public const string NextName = "CodexCatalogueNext";

    private const double SearchWidth = 96;

    private readonly Func<CommanderGameState?> _state;
    private readonly Func<HistoryState> _history;
    private readonly CodexCatalogueChoice _choice;
    private readonly CodexTree _tree;
    private readonly CodexNode _node;
    private readonly bool _headset;
    private readonly PanelNavigator? _nav;
    private readonly ColumnWidthMemory? _columnWidths;
    private readonly ColumnArrangementMemory? _columnArrangements;
    private readonly Func<BodySearchLink?>? _bodySearch;
    private readonly Func<string, Task<bool>>? _copy;
    private readonly CodexChoice? _period;
    private readonly Func<DateTimeOffset>? _now;
    private readonly RegionFirstBook? _regionFirsts;
    private readonly CodexThumbnails? _thumbs;
    private readonly CodexCatalogueFilters _filters;
    private readonly IReadOnlyList<CodexNode> _segmentNodes;
    private readonly Segment? _segment;
    private readonly Control? _results;
    private readonly StackPanel _body = new();

    private ArrangedTable<CodexGroupRow>? _groupColumns;
    private ArrangedTable<CodexKindRow>? _kindColumns;
    private string? _columnsRegion;
    private int _groupPage;
    private int _kindPage;
    private CodexLedger? _seenLedger;
    private HistoryState _seenHistory;
    private string? _seenSystem;
    private int? _seenHere;
    private long _seenChoice = -1;
    private bool _seenSearch;
    private long _seenMinute;
    private long _seenFirsts;
    private int _seenPeriod = -1;
    private string _summary = string.Empty;

    public CodexCataloguePage(
        Func<CommanderGameState?> state,
        Func<HistoryState> history,
        CodexCatalogueChoice choice,
        CodexNode node,
        bool headset = false,
        PanelNavigator? nav = null,
        CodexPictureSet? pictures = null,
        ColumnWidthMemory? columnWidths = null,
        ColumnArrangementMemory? columnArrangements = null,
        Func<BodySearchLink?>? bodySearch = null,
        Func<string, Task<bool>>? copy = null,
        CodexTree? tree = null,
        CodexChoice? period = null,
        Func<DateTimeOffset>? now = null,
        RegionFirstBook? regionFirsts = null)
    {
        _period = period;
        _now = now;
        _regionFirsts = regionFirsts;
        _state = state;
        _history = history;
        _choice = choice;
        _node = node;
        _headset = headset;
        _nav = nav;
        _columnWidths = columnWidths;
        _columnArrangements = columnArrangements;
        _bodySearch = bodySearch;
        _copy = copy;
        _tree = tree ?? CodexCatalogue.Tree;
        _thumbs = pictures is null ? null : new CodexThumbnails(pictures);
        _thumbs?.Refresh();

        _filters = new CodexCatalogueFilters(choice, Here);
        _filters.Changed += (_, _) =>
        {
            _groupPage = 0;
            _kindPage = 0;
            Draw();
        };

        _segmentNodes = CodexCatalogueModel.SegmentsOf(node);

        if (_segmentNodes.Count > 0)
        {
            _segment = new Segment
            {
                Name = "CodexCatalogueSegment",
                ItemsSource = [.. _segmentNodes.Select(group => CodexCatalogueModel.Display(group.ShortName))],
                SelectedIndex = Math.Clamp(choice.Segment(node.Key), 0, _segmentNodes.Count - 1),
                Margin = new Thickness(0, 0, 0, 10),
            };
            _segment.SelectionChanged += (_, _) =>
            {
                if (_segment.SelectedIndex >= 0 && _segment.SelectedIndex != _choice.Segment(_node.Key))
                {
                    _choice.ChooseSegment(_node.Key, _segment.SelectedIndex);
                    _kindPage = 0;
                    Draw();
                }
            };

            if (headset)
            {
                _results = _filters.TakeResults();
            }
        }

        Margin = new Thickness(14);
        Content = LoadoutPages.Scrolling(new StackPanel { Children = { _body } });
        Draw();
    }

    public string Summary => _summary;

    public event EventHandler? SummaryChanged;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (_thumbs is not null)
        {
            _thumbs.Refresh();
            Draw();
        }
        else
        {
            Tick();
        }
    }

    /// <summary>Redraws when the record, the history walk, the current system or region, the shared filter or the search link has changed.</summary>
    public bool Tick()
    {
        if (ReferenceEquals(_state()?.CodexLedger, _seenLedger)
            && _history() == _seenHistory
            && _state()?.Location.StarSystem == _seenSystem
            && Here() == _seenHere
            && _choice.Version == _seenChoice
            && HasSearch() == _seenSearch
            && (!AtRoot || (PageKit.Minute(_now!()) == _seenMinute && (_regionFirsts?.Version ?? 0) == _seenFirsts && _period!.Period == _seenPeriod)))
        {
            return false;
        }

        Draw();

        return true;
    }

    /// <summary>The codex region of the Commander's position; null when it is not known.</summary>
    private int? Here() =>
        _state()?.Location.StarPos is { } position && GalacticRegions.Find(position) is { } name ? GalacticRegions.IdOf(name) : null;

    /// <summary>Whether this is the Codex root, which carries the Possible firsts and Regions rows under its tables.</summary>
    private bool AtRoot => _node.Parent is null && _period is not null && _now is not null;

    private bool HasSearch() => _bodySearch?.Invoke() is not null;

    private void Draw()
    {
        var state = _state();
        _seenLedger = state?.CodexLedger;
        _seenHistory = _history();
        _seenSystem = state?.Location.StarSystem;
        _seenHere = Here();
        _seenChoice = _choice.Version;
        _seenSearch = HasSearch();

        if (AtRoot)
        {
            _seenMinute = PageKit.Minute(_now!());
            _seenFirsts = _regionFirsts?.Version ?? 0;
            _seenPeriod = _period!.Period;
        }

        _body.Children.Clear();

        if (_seenHistory is HistoryState.Pending or HistoryState.Running)
        {
            _body.Children.Add(RoutingKit.Ink(CodexModel.RunningText, TypeScale.ControlLarge, ThemeManager.GreyKey, wrap: true));
            return;
        }

        var ledger = state?.CodexLedger ?? CodexLedger.None;
        var scope = _filters.Scope;
        var view = CodexCatalogueModel.View(ledger, _tree, _node, scope, _choice.Status, _choice.Segment(_node.Key));

        SetSummary(CodexCatalogueModel.Summary(view, _headset));
        Columns(scope);

        _filters.Show(view.Shown, view.Counted);
        _body.Children.Add(_filters);

        if (scope is { } region)
        {
            var uncovered = CodexDiscoveries.For(ledger, CodexCatalogue.Table, region).Uncovered.Count;
            _body.Children.Add(Note(CodexModel.DiscoveriesNote(ledger.OnRecordSince, uncovered)));
        }

        if (_segment is not null)
        {
            _body.Children.Add(SegmentRow(view));
        }

        var any = false;

        if (view.Groups.Count > 0)
        {
            any = true;
            _body.Children.Add(GroupTable(view.Groups));
        }

        if (view.Kinds.Count > 0)
        {
            any = true;
            _body.Children.Add(KindTable(view.Kinds, scope));
        }

        if (!any)
        {
            _body.Children.Add(CreditsKit.Message(CodexCatalogueModel.NoMatchText));
        }

        if (AtRoot)
        {
            _body.Children.Add(RootRows(state, ledger));
        }
    }

    /// <summary>The Possible firsts row, where there is a record, directly above the Regions row; neither follows the filter.</summary>
    private StackPanel RootRows(CommanderGameState? state, CodexLedger ledger)
    {
        var rows = new StackPanel { Spacing = 2 };

        if (FirstsRow(state) is { } firsts)
        {
            rows.Children.Add(firsts);
        }

        rows.Children.Add(RegionsRow(ledger));

        return rows;
    }

    private Button RegionsRow(CodexLedger ledger)
    {
        var period = (CodexPeriod)Math.Clamp(_period!.Period, 0, 2);
        var view = CodexModel.Regions(ledger, period, _now!(), _history());
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 16 };

        CreditsKit.Place(grid, 0, new StackPanel
        {
            Children =
            {
                CreditsKit.Label(CodexModel.RegionsCrumbWord),
                RoutingKit.Ink(CodexModel.RegionsRowLine, TypeScale.Small, ThemeManager.GreyKey, wrap: true),
            },
        });
        CreditsKit.Place(grid, 1, CreditsKit.Mono(CodexModel.RegionsFigure(view, period), TypeScale.Small, ThemeManager.AKey, right: true));
        CreditsKit.Place(grid, 2, CreditsKit.Line("›", Fonts.ChromeFamily, TypeScale.ControlLarge, ThemeManager.AKey));

        var button = ListRow.Dress(new Button
        {
            Name = "CodexRegions",
            Content = grid,
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });
        Avalonia.Automation.AutomationProperties.SetName(button, CodexModel.RegionsCrumbWord);
        button.Click += (_, _) => _nav?.Drill(CodexPage.RegionsCrumb());

        return button;
    }

    /// <summary>The Possible firsts row; null with no record in the store.</summary>
    private Control? FirstsRow(CommanderGameState? state)
    {
        var records = _regionFirsts is not null && state?.Identity.FrontierId is { Length: > 0 } id ? _regionFirsts.For(id) : [];

        if (records.Count == 0)
        {
            return null;
        }

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 16 };

        var label = CreditsKit.Label(CodexModel.PossibleFirstsCrumbWord);
        ThemeInk.Bind(label, TextBlock.ForegroundProperty, ThemeManager.CyanKey);

        CreditsKit.Place(grid, 0, new StackPanel
        {
            Children = { label, RoutingKit.Ink(CodexModel.PossibleFirstsRowLine, TypeScale.Small, ThemeManager.GreyKey, wrap: true) },
        });
        CreditsKit.Place(grid, 1, CreditsKit.Mono(CodexModel.PossibleFirstsFigures(records), TypeScale.Small, ThemeManager.AKey, right: true));
        CreditsKit.Place(grid, 2, CreditsKit.Line("›", Fonts.ChromeFamily, TypeScale.ControlLarge, ThemeManager.AKey));

        var button = ListRow.Dress(new Button
        {
            Name = "CodexPossibleFirsts",
            Content = grid,
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });
        Avalonia.Automation.AutomationProperties.SetName(button, CodexModel.PossibleFirstsCrumbWord);
        button.Click += (_, _) => _nav?.Drill(CodexPage.PossibleFirstsCrumb());

        return button;
    }

    private void SetSummary(string summary)
    {
        if (summary != _summary)
        {
            _summary = summary;
            SummaryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static TextBlock Note(string text)
    {
        var note = RoutingKit.Ink(text, TypeScale.Small, ThemeManager.GreyKey, wrap: true);
        note.Margin = new Thickness(0, 0, 0, 10);

        return note;
    }

    /// <summary>The segmented control: with status lines on the desktop; in the headset, sharing its row with the results line.</summary>
    private Control SegmentRow(CodexCatalogueView view)
    {
        var segment = _segment!;

        if (segment.Parent is Avalonia.Controls.Panel old)
        {
            old.Children.Remove(segment);
        }

        segment.SelectedIndex = view.Segment;

        if (!_headset)
        {
            segment.Statuses = [.. view.Segments.Select(item => (ChoiceStatus?)new ChoiceStatus(CodexCatalogueModel.SegmentLine(item), ChoiceTone.Grey))];
            segment.Width = 280.0 * view.Segments.Count;
            segment.HorizontalAlignment = HorizontalAlignment.Left;

            return segment;
        }

        segment.Width = 220.0 * view.Segments.Count;
        segment.Margin = default;

        var results = _results!;

        if (results.Parent is Avalonia.Controls.Panel held)
        {
            held.Children.Remove(results);
        }

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12, Margin = new Thickness(0, 0, 0, 10) };
        row.Children.Add(segment);
        Grid.SetColumn(results, 1);
        row.Children.Add(results);

        return row;
    }

    /// <summary>Builds the two tables again when the region their heads name has changed.</summary>
    private void Columns(int? scope)
    {
        var region = scope is { } id ? CodexModel.RegionName(id) : null;

        if (_groupColumns is not null && _kindColumns is not null && region == _columnsRegion)
        {
            _groupColumns.Refresh();
            _kindColumns.Refresh();
            return;
        }

        _columnsRegion = region;
        var widths = _headset ? null : _columnWidths;

        _groupColumns = new ArrangedTable<CodexGroupRow>(
            GroupsTableId, GroupFields(region), _columnArrangements, widths, _headset ? 12 : 16, headName: "CodexGroupHead");
        _kindColumns = new ArrangedTable<CodexKindRow>(
            KindsTableId, KindFields(region), _columnArrangements, widths, _headset ? 12 : 16, CodexCatalogueModel.SystemsKey, "CodexKindHead");
        _groupColumns.Changed += Draw;
        _kindColumns.Changed += Draw;
    }

    private StackPanel GroupTable(IReadOnlyList<CodexGroupRow> rows)
    {
        var columns = _groupColumns!;
        var sorted = columns.Sort(rows);
        var list = new StackPanel { Name = "CodexGroupRows", Spacing = Gaps.Tile };
        list.Children.Add(LedgerTables.Head(columns, new Thickness(LedgerTables.RowInset, 0)));

        foreach (var row in Paged(sorted, ref _groupPage))
        {
            list.Children.Add(GroupView(row, columns));
        }

        var table = new StackPanel { Spacing = Gaps.Tile, Margin = new Thickness(0, 0, 0, 14) };
        table.Children.Add(LedgerTables.Arranger(columns));
        table.Children.Add(LedgerTables.Sideways(columns, list));

        if (_headset && sorted.Count > FactionModel.PageSize)
        {
            table.Children.Add(Pager(sorted.Count, _groupPage, page => _groupPage = page, "Groups"));
        }

        return table;
    }

    private StackPanel KindTable(IReadOnlyList<CodexKindRow> rows, int? scope)
    {
        var columns = _kindColumns!;
        var sorted = columns.Sort(rows);
        var list = new StackPanel { Name = "CodexKindRows", Spacing = Gaps.Tile };
        var right = LedgerTables.RowInset + (_bodySearch is not null ? Gaps.Tile + SearchWidth : 0) + Gaps.Tile + TypeScale.MinimumTarget;
        list.Children.Add(LedgerTables.Head(columns, new Thickness(LedgerTables.RowInset, 0, right, 0)));

        foreach (var row in Paged(sorted, ref _kindPage))
        {
            list.Children.Add(KindView(row, columns, scope));
        }

        var table = new StackPanel { Spacing = Gaps.Tile };
        table.Children.Add(LedgerTables.Arranger(columns));
        table.Children.Add(LedgerTables.Sideways(columns, list));

        if (_headset && sorted.Count > FactionModel.PageSize)
        {
            table.Children.Add(Pager(sorted.Count, _kindPage, page => _kindPage = page, "Kinds"));
        }

        return table;
    }

    /// <summary>All the rows on the desktop; in the headset, one page of 25.</summary>
    private IReadOnlyList<T> Paged<T>(IReadOnlyList<T> sorted, ref int page)
    {
        if (!_headset)
        {
            return sorted;
        }

        page = FactionModel.ClampPage(page, sorted.Count);

        return [.. sorted.Skip(page * FactionModel.PageSize).Take(FactionModel.PageSize)];
    }

    private Grid Pager(int total, int page, Action<int> turn, string table)
    {
        var readout = CreditsKit.Mono(FactionModel.PageReadout(page, total), TypeScale.Tip, ThemeManager.GreyKey, right: false);
        readout.VerticalAlignment = VerticalAlignment.Center;

        var previous = new Button { Name = PreviousName + "." + table, Content = "◄ PREVIOUS", Height = TypeScale.MinimumTarget, IsEnabled = page > 0 };
        var next = new Button
        {
            Name = NextName + "." + table,
            Content = "NEXT ►",
            Height = TypeScale.MinimumTarget,
            IsEnabled = FactionModel.ClampPage(page + 1, total) > page,
        };
        previous.Click += (_, _) =>
        {
            turn(page - 1);
            Draw();
        };
        next.Click += (_, _) =>
        {
            turn(page + 1);
            Draw();
        };

        var tiles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tile, Children = { previous, next } };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 8, 0, 0) };
        Grid.SetColumn(tiles, 1);
        grid.Children.Add(readout);
        grid.Children.Add(tiles);

        return grid;
    }

    private IReadOnlyList<TableField<CodexGroupRow>> GroupFields(string? region)
    {
        var heads = CodexCatalogueModel.GroupHeads(_node.Parent is null ? "Families" : CodexCatalogueModel.Display(_node.ShortName), region);

        return
        [
            LedgerTables.Control<CodexGroupRow>(new TableColumn("Picture", ColumnFit.Fixed) { Fixed = TypeScale.MinimumTarget }, GroupSquare),
            StarSystemPage.Headed(heads[0], new TableColumn("Name", ColumnFit.Fill), GroupName),
            StarSystemPage.Headed(heads[1], new TableColumn("Kinds"), row => Figure(row.Kinds, ThemeManager.AKey), right: true),
            StarSystemPage.Headed(heads[2], new TableColumn("OnRecord"), row => Figure(row.OnRecord, ThemeManager.BlueKey), right: true),
            StarSystemPage.Headed(heads[3], new TableColumn("Missing"), row => Figure(row.Missing, ThemeManager.AKey), right: true),
            LedgerTables.Control<CodexGroupRow>(
                new TableColumn("Open", ColumnFit.Fixed) { Fixed = 16 },
                _ => CreditsKit.Line("›", Fonts.ChromeFamily, TypeScale.ControlLarge, ThemeManager.AKey),
                right: true),
        ];
    }

    private IReadOnlyList<TableField<CodexKindRow>> KindFields(string? region)
    {
        var heads = CodexCatalogueModel.KindHeads(region);
        var firstFound = new TableColumn("FirstFound", ColumnFit.Fill);

        return
        [
            LedgerTables.Control<CodexKindRow>(new TableColumn("Picture", ColumnFit.Fixed) { Fixed = TypeScale.MinimumTarget }, KindSquare),
            StarSystemPage.Headed(heads[0], new TableColumn("Kind", ColumnFit.Fill), KindName),
            StarSystemPage.Headed(heads[1], new TableColumn(CodexCatalogueModel.SystemsKey), row => SystemsCell(row, region), right: true),
            _headset
                ? LedgerTables.Added("FirstFound", firstFound, row => FirstFoundCell(row, region), heads[2])
                : StarSystemPage.Headed(heads[2], firstFound, row => FirstFoundCell(row, region)),
            StarSystemPage.Headed(heads[3], new TableColumn("Status"), StatusCell),
        ];
    }

    private static TextBlock Figure(int value, string key) =>
        CreditsKit.Mono(CodexModel.Number(value), TypeScale.ControlLarge, key, right: true);

    private static StackPanel GroupName(CodexGroupRow row)
    {
        var cell = new StackPanel
        {
            Children = { CreditsKit.Line(row.Name, Fonts.ProseFamily, TypeScale.Secondary, ThemeManager.WhiteKey) },
        };

        if (row.Line is { } line)
        {
            cell.Children.Add(CreditsKit.Line(line, Fonts.ProseFamily, TypeScale.Small, ThemeManager.GreyKey));
        }

        return cell;
    }

    private StackPanel KindName(CodexKindRow row)
    {
        var picture = Picture(row);
        var line = CreditsKit.Line(
            CodexCatalogueModel.PictureLine(picture.Line).ToUpperInvariant(),
            Fonts.ChromeFamily,
            TypeScale.MetaSmall,
            picture.Line == CodexPictureChoiceLine.Yours ? ThemeManager.CyanKey : ThemeManager.GreyKey);
        line.Name = "CodexPictureLine";
        line.FontWeight = Avalonia.Media.FontWeight.SemiBold;
        line.LetterSpacing = TypeScale.MetaSmall * Fonts.ChromeTracking;

        return new StackPanel
        {
            Spacing = 1,
            Children =
            {
                CreditsKit.Line(row.Name, Fonts.ProseFamily, TypeScale.Secondary, ThemeManager.WhiteKey),
                line,
            },
        };
    }

    private StackPanel SystemsCell(CodexKindRow row, string? region)
    {
        var cell = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { Figure(row.Systems, ThemeManager.AKey) },
        };

        if (region is not null && !_headset)
        {
            cell.Children.Add(CreditsKit.Mono(CodexModel.Number(row.SystemsAllRegions), TypeScale.Meta, ThemeManager.GreyKey, right: true));
        }

        return cell;
    }

    private StackPanel FirstFoundCell(CodexKindRow row, string? region)
    {
        if (row.FirstFind is not { } first)
        {
            return new StackPanel
            {
                Children =
                {
                    CreditsKit.Line(
                        region is null ? CodexCatalogueModel.NotFound : CodexCatalogueModel.NotFoundHere,
                        Fonts.ProseFamily,
                        TypeScale.Small,
                        ThemeManager.GreyKey),
                },
            };
        }

        var here = _state()?.Location.StarSystem is { } current
                   && string.Equals(first.System, current, StringComparison.OrdinalIgnoreCase);
        var place = RoutingKit.Ink(string.Empty, TypeScale.Secondary, here ? ThemeManager.CyanKey : ThemeManager.AKey);
        place.TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;
        place.Inlines!.Add(new Avalonia.Controls.Documents.Run(first.System ?? CodexModel.UnknownSystemText));

        if (CodexModel.BodyOf(first.System, first.Body) is { Length: > 0 } body)
        {
            var run = CreditsKit.Colored(" " + body, ThemeManager.GreyKey);
            run.FontFamily = new Avalonia.Media.FontFamily(Fonts.ProseFamily);
            run.FontSize = TypeScale.Small;
            place.Inlines.Add(run);
        }

        var when = CodexModel.DateTime(first.At)
                   + (region is null && first.RegionId is { } at ? " · " + CodexModel.RegionName(at) : string.Empty);

        return new StackPanel
        {
            Spacing = 1,
            Children =
            {
                first.System is { } system ? CopyGlyph.LinkBeside(place, system, _copy) : place,
                CreditsKit.Mono(when, TypeScale.Meta, ThemeManager.GreyKey, right: false),
            },
        };
    }

    private StackPanel StatusCell(CodexKindRow row)
    {
        var status = CreditsKit.Line(
            row.OnRecord ? "ON RECORD" : "MISSING", Fonts.ChromeFamily, TypeScale.Meta, row.OnRecord ? ThemeManager.BlueKey : ThemeManager.GreyKey);
        status.FontWeight = Avalonia.Media.FontWeight.SemiBold;

        var cell = new StackPanel { Children = { status } };

        if (_headset && row.FirstFind is { } first)
        {
            cell.Children.Add(CreditsKit.Mono(CodexModel.Date(first.At), TypeScale.Meta, ThemeManager.GreyKey, right: false));
        }

        return cell;
    }

    private Button GroupView(CodexGroupRow row, ArrangedTable<CodexGroupRow> columns)
    {
        var grid = columns.Row(row);
        grid.VerticalAlignment = VerticalAlignment.Center;

        var button = ListRow.Dress(new Button
        {
            Name = "CodexGroup." + row.Node.Key,
            Content = grid,
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(LedgerTables.RowInset, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });
        Avalonia.Automation.AutomationProperties.SetName(button, row.Name);
        button.Click += (_, _) => _nav?.Drill(CodexPage.NodeCrumb(row.Node));

        return button;
    }

    private Grid KindView(CodexKindRow row, ArrangedTable<CodexKindRow> columns, int? scope)
    {
        var grid = columns.Row(row);
        grid.VerticalAlignment = VerticalAlignment.Center;

        var button = ListRow.Dress(new Button
        {
            Name = "CodexKind." + row.Entry.Key,
            Content = grid,
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(LedgerTables.RowInset, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });
        Avalonia.Automation.AutomationProperties.SetName(button, row.Name);
        button.Click += (_, _) => Open(row, scope);

        var open = new Button
        {
            Name = "CodexKindOpen." + row.Entry.Key,
            Theme = Application.Current?.FindResource("D47.GlyphButton") as ControlTheme,
            Content = Glyphs.Text("›", TypeScale.Glyph),
            Width = TypeScale.MinimumTarget,
            MinHeight = TypeScale.MinimumTarget,
            Margin = Gaps.TileBefore,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        Avalonia.Automation.AutomationProperties.SetName(open, "Open " + row.Name);
        open.Click += (_, _) => Open(row, scope);

        var line = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        line.Children.Add(button);

        if (_bodySearch is not null)
        {
            var slot = SearchTile(row, scope) ?? (Control)new Border { Width = SearchWidth, Margin = Gaps.TileBefore };
            Grid.SetColumn(slot, 1);
            line.Children.Add(slot);
        }

        Grid.SetColumn(open, 2);
        line.Children.Add(open);

        return line;
    }

    /// <summary>Opens the kind's entry page in the filter's region; with every region, in the region the Commander is in.</summary>
    private void Open(CodexKindRow row, int? scope) =>
        _nav?.Drill(CodexPage.EntryCrumb(scope ?? Here(), row.Entry.Key, row.Entry.Name));

    /// <summary>The row's Search tile, for a kind the service can search.</summary>
    private Button? SearchTile(CodexKindRow row, int? scope)
    {
        if (_bodySearch?.Invoke() is not { } link || CodexSearch.Arguments(row.Entry, scope) is not { } arguments)
        {
            return null;
        }

        var tile = new Button
        {
            Name = "CodexSearch." + row.Entry.Key,
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

    private (CodexPictureChoiceLine Line, Avalonia.Media.Imaging.Bitmap? Bitmap) Picture(CodexKindRow row)
    {
        if (_thumbs?.Picture(row.Entry.Key, _filters.Scope) is not { } shown)
        {
            return (CodexPictureChoiceLine.Placeholder, _thumbs?.Family(row.PlaceholderFile));
        }

        var line = shown.Choice.FromGroup
            ? CodexPictureChoiceLine.Group
            : shown.Choice.Source == CodexPictureSource.Local ? CodexPictureChoiceLine.Yours : CodexPictureChoiceLine.Stock;

        return (line, shown.Bitmap);
    }

    private Border KindSquare(CodexKindRow row) => Square(Picture(row).Bitmap);

    private Border GroupSquare(CodexGroupRow row) =>
        Square(_thumbs?.Picture(row.Node.Key, _filters.Scope)?.Bitmap ?? _thumbs?.Family(row.PlaceholderFile));

    private static Border Square(Avalonia.Media.Imaging.Bitmap? bitmap)
    {
        var side = TypeScale.MinimumTarget;
        var square = new Border { Width = side, Height = side };
        ThemeInk.Bind(square, Border.BackgroundProperty, ThemeManager.SlabKey);

        if (bitmap is not null)
        {
            square.Child = new Image
            {
                Name = "CodexThumb",
                Source = bitmap,
                Stretch = Avalonia.Media.Stretch.Uniform,
                Width = side,
                Height = side,
            };
        }

        return square;
    }
}

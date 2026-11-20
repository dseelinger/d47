using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>Commander › Ledgers › Codex › a region: one pressable row per entry in the period, grouped by subcategory (#1290).</summary>
public sealed class CodexRegionPage : UserControl, IPageSummary, IFilterablePage
{
    public const string TableId = "Codex.Entries";

    private readonly Func<CommanderGameState?> _state;
    private readonly Func<HistoryState> _history;
    private readonly Func<DateTimeOffset> _now;
    private readonly CodexChoice _choice;
    private readonly int? _regionId;
    private readonly bool _headset;
    private readonly Func<string, Task<bool>>? _copy;
    private readonly PanelNavigator? _nav;
    private readonly CodexTable _table;
    private readonly ArrangedTable<CodexEntryRow> _columns;
    private readonly ArrangedList _list;
    private readonly Segment _period = CodexKit.PeriodSegment();
    private readonly Grid _periodRow;
    private CodexLedger? _seenLedger;
    private HistoryState _seenHistory;
    private long _seenMinute;
    private string? _seenSystem;
    private string _query = string.Empty;
    private readonly RegionFirstBook? _regionFirsts;
    private readonly CodexCatalogueChoice? _catalogue;
    private long _seenFirsts;
    private readonly CodexThumbnails? _thumbs;

    public CodexRegionPage(
        Func<CommanderGameState?> state,
        Func<HistoryState> history,
        Func<DateTimeOffset> now,
        CodexChoice choice,
        int? regionId,
        bool headset = false,
        PanelNavigator? nav = null,
        CodexTable? table = null,
        ColumnWidthMemory? columnWidths = null,
        Func<string, Task<bool>>? copy = null,
        ColumnArrangementMemory? columnArrangements = null,
        RegionFirstBook? regionFirsts = null,
        CodexCatalogueChoice? catalogue = null,
        CodexPictureSet? pictures = null)
    {
        _catalogue = catalogue;
        _thumbs = pictures is null ? null : new CodexThumbnails(pictures);
        _thumbs?.Refresh();
        _regionFirsts = regionFirsts;
        _copy = copy;
        _state = state;
        _history = history;
        _now = now;
        _choice = choice;
        _regionId = regionId;
        _headset = headset;
        _nav = nav;
        _table = table ?? CodexCatalogue.Table;

        _periodRow = CodexKit.PeriodRow(_period);
        _columns = new ArrangedTable<CodexEntryRow>(
            TableId, Fields(), columnArrangements, headset ? null : columnWidths, headset ? 12 : 16, headName: "CodexEntryHead");
        _list = new ArrangedList(
            headset,
            "CodexRows",
            item => item switch
            {
                CodexGroup group => GroupView(group),
                CodexEntryRow row => RowView(row),
                _ => null,
            });
        _columns.Changed += () =>
        {
            _columns.Refresh();
            Draw();
        };
        _period.SelectedIndex = choice.Period;
        _period.SelectionChanged += (_, _) =>
        {
            _choice.Period = _period.SelectedIndex;
            Draw();
        };

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

    public bool Filters => Build() is { Running: false, Entries: > 0 };

    public string FilterPlaceholder => CodexModel.Placeholder;

    public void Filter(string? query)
    {
        if (PageKit.Retarget(query, ref _query))
        {
            Draw();
        }
    }

    /// <summary>Redraws when the record, the history walk, the minute, the current system or the period chosen on the other page has changed.</summary>
    public bool Tick()
    {
        if (ReferenceEquals(_state()?.CodexLedger, _seenLedger)
            && _history() == _seenHistory
            && PageKit.Minute(_now()) == _seenMinute
            && _state()?.Location.StarSystem == _seenSystem
            && (_regionFirsts?.Version ?? 0) == _seenFirsts
            && _choice.Period == _period.SelectedIndex)
        {
            return false;
        }

        _period.SelectedIndex = _choice.Period;
        Draw();

        return true;
    }


    /// <summary>Redraws every row when the pictures on disk have changed since the last draw.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (_thumbs?.Refresh() == true)
        {
            _list.Forget();
            Draw();
        }
    }

    private CodexPeriod Period => CodexKit.PeriodOf(_period);

    private CodexRegionView Build()
    {
        var state = _state();

        return CodexModel.Entries(
            state?.CodexLedger ?? CodexLedger.None,
            Period,
            _regionId,
            _now(),
            _history(),
            state?.Location.StarSystem,
            (address, body) => state?.Scans.For(address, body)?.BodyName,
            _query,
            CodexModel.RegionFirstLookup(_regionFirsts, state));
    }

    private void Draw()
    {
        _seenLedger = _state()?.CodexLedger;
        _seenHistory = _history();
        _seenMinute = PageKit.Minute(_now());
        _seenSystem = _state()?.Location.StarSystem;
        _seenFirsts = _regionFirsts?.Version ?? 0;

        var view = Build();

        _list.Top.Children.Clear();

        if (view.Running)
        {
            _list.Top.Children.Add(RoutingKit.Ink(CodexModel.RunningText, TypeScale.ControlLarge, ThemeManager.GreyKey, wrap: true));
            Rows([]);
            return;
        }

        CodexKit.ShowSince(_periodRow, view.OnRecordSince, view.Failed);
        _list.Top.Children.Add(_periodRow);

        if (view.Failed)
        {
            _list.Top.Children.Add(CodexKit.Warning(view.OnRecordSince));
        }

        var count = view.Shown == view.Entries
            ? CodexModel.EntryCount(view.Entries)
            : $"{CodexModel.Number(view.Shown)} of {CodexModel.EntryCount(view.Entries)}";

        _list.Top.Children.Add(StarSystemPage.GroupHead(
            CodexModel.PeriodName(Period) + " · " + CodexModel.RegionName(_regionId), CodexKit.Line(view.Line), count));

        if (view.Empty is { } empty)
        {
            _list.Top.Children.Add(CreditsKit.Message(empty));
            _list.Top.Children.Add(DiscoveriesRow());
            Rows([]);
            return;
        }

        _list.Top.Children.Add(CodexKit.Strip(view.Entries, view.New, view.Logged, "Subcategories", view.Subcategories));
        _list.Top.Children.Add(DiscoveriesRow());

        if (view.Shown == 0)
        {
            _list.Top.Children.Add(CreditsKit.Message(CodexModel.NoEntryMatchText));
            Rows([]);
            return;
        }

        _list.Top.Children.Add(LedgerTables.Arranger(_columns));

        var items = new List<object>();

        foreach (var group in view.Groups)
        {
            items.Add(group);
            items.AddRange(_columns.Sort(group.Rows));
        }

        Rows(items);
    }

    private Control DiscoveriesRow()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 16 };
        var named = _regionId is not null;

        CreditsKit.Place(grid, 0, new StackPanel
        {
            Children =
            {
                CreditsKit.Label("Discoveries"),
                RoutingKit.Ink(
                    named ? CodexModel.DiscoveriesRowLine : CodexModel.DiscoveriesNoRegionLine,
                    TypeScale.Small,
                    ThemeManager.GreyKey,
                    wrap: true),
            },
        });

        if (_regionId is { } id)
        {
            var found = CodexDiscoveries.For(_state()?.CodexLedger ?? CodexLedger.None, _table, id);
            CreditsKit.Place(grid, 1, CreditsKit.Mono(CodexModel.DiscoveriesFigures(found), TypeScale.Small, ThemeManager.AKey, right: true));
            CreditsKit.Place(grid, 2, CreditsKit.Line("›", Fonts.ChromeFamily, TypeScale.ControlLarge, ThemeManager.AKey));
        }

        var button = ListRow.Dress(new Button
        {
            Name = "CodexDiscoveries",
            Content = grid,
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 6),
            Margin = new Thickness(0, 0, 0, 8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = named,
        });
        Avalonia.Automation.AutomationProperties.SetName(button, "Discoveries");
        button.Click += (_, _) => OpenMissing();

        return button;
    }

    /// <summary>Opens the Codex catalogue set to the kinds missing in this region.</summary>
    private void OpenMissing()
    {
        if (_regionId is not { } id)
        {
            return;
        }

        if (_catalogue is not null)
        {
            var here = _state()?.Location.StarPos is { } position && GalacticRegions.Find(position) is { } name ? GalacticRegions.IdOf(name) : null;

            _catalogue.Status = CodexKindStatus.Missing;

            if (id == here)
            {
                _catalogue.Here = true;
            }
            else
            {
                _catalogue.Other = id;
            }
        }

        _nav?.GoTo(LedgersPage.Ledger(CodexPage.PageKey, "Codex"));
    }

    /// <summary>Shows <paramref name="items"/>, leaving the list and its scroll position alone when they are the items already shown.</summary>
    private void Rows(IReadOnlyList<object> items) =>
        _list.Show(_columns, items, new Thickness(LedgerTables.RowInset, 0));

    private IReadOnlyList<TableField<CodexEntryRow>> Fields()
    {
        var heads = CodexModel.EntryHeads();

        return
        [
            CodexThumbnails.Column<CodexEntryRow>(row => CodexThumbnails.Square(_thumbs?.Entry(row.Key, _regionId, row.PlaceholderFile))),
            StarSystemPage.Headed(heads[0], new TableColumn("Entry", ColumnFit.Fill), row => CreditsKit.Line(row.Name, Fonts.ProseFamily, TypeScale.ControlLarge, ThemeManager.WhiteKey)),
            StarSystemPage.Headed(heads[1], new TableColumn("System", ColumnFit.Fill), row => SystemCell(row, _copy)),
            StarSystemPage.Headed(heads[2], new TableColumn("Time"), row => CreditsKit.Mono(CodexModel.DateTime(row.At), TypeScale.Small, ThemeManager.GreyKey, right: false)),
            StarSystemPage.Headed(heads[3], new TableColumn("Status"), StatusCell),
        ];
    }

    private static DockPanel GroupView(CodexGroup group)
    {
        var count = CreditsKit.Mono(CodexModel.Number(group.Count), TypeScale.Small, ThemeManager.GreyKey, right: true);
        DockPanel.SetDock(count, Dock.Right);

        return new DockPanel
        {
            Margin = new Thickness(14, 12, 14, 4),
            Children = { count, CreditsKit.Label(group.Label) },
        };
    }

    private Button RowView(CodexEntryRow row)
    {
        var grid = _columns.Row(row);
        grid.VerticalAlignment = VerticalAlignment.Center;

        var button = ListRow.Dress(new Button
        {
            Name = "CodexEntry." + row.Key,
            Content = grid,
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 6),
            Margin = new Thickness(0, 0, 0, Gaps.Tile),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });
        Avalonia.Automation.AutomationProperties.SetName(button, row.Name);
        button.Click += (_, _) => _nav?.Drill(CodexPage.EntryCrumb(_regionId, row.Key, row.Name));

        return button;
    }

    internal static StackPanel SystemCell(CodexEntryRow row, Func<string, Task<bool>>? copy = null)
    {
        var cell = new StackPanel();

        if (row.System is { } system)
        {
            cell.Children.Add(CopyGlyph.LinkBeside(
                CreditsKit.Line(system, Fonts.ChromeFamily, TypeScale.ControlLarge, row.IsHere ? ThemeManager.CyanKey : ThemeManager.AKey),
                system,
                copy));
        }

        if (row.Place.Length > 0)
        {
            var place = RoutingKit.Ink(string.Empty, TypeScale.Small, ThemeManager.GreyKey);
            place.TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;

            if (!string.IsNullOrWhiteSpace(row.Body))
            {
                place.Inlines!.Add(new Run(row.Body));
            }

            if (row.Position is { } position)
            {
                place.Inlines!.Add(new Run(row.Body is null ? string.Empty : " · "));
                var mono = CreditsKit.Colored(position, ThemeManager.GreyKey);
                mono.FontSize = TypeScale.Meta;
                place.Inlines.Add(mono);
            }

            cell.Children.Add(place);
        }

        return cell;
    }

    private static StackPanel StatusCell(CodexEntryRow row)
    {
        var status = CreditsKit.Line(
            CodexEntryPage.StatusWord(row),
            Fonts.ChromeFamily,
            TypeScale.Meta,
            CodexEntryPage.StatusInk(row));
        status.FontWeight = Avalonia.Media.FontWeight.SemiBold;

        var cell = new StackPanel { Children = { status } };

        if (row.First is { } first)
        {
            var note = RoutingKit.Ink(string.Empty, TypeScale.Small, ThemeManager.GreyKey);
            note.Inlines!.Add(new Run("first "));
            note.Inlines.Add(CreditsKit.Colored(CodexModel.Date(first), ThemeManager.GreyKey));
            cell.Children.Add(note);
        }
        else if (row.Origin is { } origin)
        {
            cell.Children.Add(CreditsKit.Line(origin, Fonts.ProseFamily, TypeScale.Small, ThemeManager.GreyKey));
        }

        return cell;
    }
}

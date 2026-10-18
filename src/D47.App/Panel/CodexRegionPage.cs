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

namespace D47.App.Panel;

/// <summary>Commander › Ledgers › Codex › a region: one row per entry in the period, grouped by subcategory (#1290).</summary>
public sealed class CodexRegionPage : UserControl, IPageSummary, IFilterablePage
{
    private static readonly string[] PanelColumns = ["*", "360", "160", "200"];
    private static readonly string[] HeadsetColumns = ["*", "250", "136", "150"];

    private readonly Func<CommanderGameState?> _state;
    private readonly Func<HistoryState> _history;
    private readonly Func<DateTimeOffset> _now;
    private readonly CodexChoice _choice;
    private readonly int? _regionId;
    private readonly bool _headset;
    private readonly SortHeader<CodexEntryRow> _head = new("CodexEntryHead", CodexModel.EntryHeads());
    private readonly Segment _period = CodexKit.PeriodSegment();
    private readonly Grid _periodRow;
    private readonly StackPanel _top = new();
    private readonly ItemsControl _list;
    private readonly ScrollViewer _scroll;

    private IReadOnlyList<object> _shown = [];
    private CodexLedger? _seenLedger;
    private HistoryState _seenHistory;
    private long _seenMinute;
    private string? _seenSystem;
    private string _query = string.Empty;

    public CodexRegionPage(
        Func<CommanderGameState?> state,
        Func<HistoryState> history,
        Func<DateTimeOffset> now,
        CodexChoice choice,
        int? regionId,
        bool headset = false)
    {
        _state = state;
        _history = history;
        _now = now;
        _choice = choice;
        _regionId = regionId;
        _headset = headset;

        _periodRow = CodexKit.PeriodRow(_period);
        _head.Changed += Draw;
        _period.SelectedIndex = choice.Period;
        _period.SelectionChanged += (_, _) =>
        {
            _choice.Period = _period.SelectedIndex;
            Draw();
        };

        _list = new ItemsControl
        {
            Name = "CodexRows",
            ItemsPanel = new FuncTemplate<Avalonia.Controls.Panel?>(() => new VirtualizingStackPanel()),
            ItemTemplate = new FuncDataTemplate<object>((item, _) => item switch
            {
                CodexGroup group => GroupView(group),
                CodexEntryRow row => RowView(row),
                _ => null,
            }),
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

    public bool Filters => Build() is { Running: false, Entries: > 0 };

    public string FilterPlaceholder => CodexModel.Placeholder;

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

    /// <summary>Redraws when the record, the history walk, the minute, the current system or the period chosen on the other page has changed.</summary>
    public bool Tick()
    {
        if (ReferenceEquals(_state()?.CodexLedger, _seenLedger)
            && _history() == _seenHistory
            && Minute(_now()) == _seenMinute
            && _state()?.Location.StarSystem == _seenSystem
            && _choice.Period == _period.SelectedIndex)
        {
            return false;
        }

        _period.SelectedIndex = _choice.Period;
        Draw();

        return true;
    }

    private static long Minute(DateTimeOffset now) => now.ToUnixTimeSeconds() / 60;

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
            _query);
    }

    private void Draw()
    {
        _seenLedger = _state()?.CodexLedger;
        _seenHistory = _history();
        _seenMinute = Minute(_now());
        _seenSystem = _state()?.Location.StarSystem;

        var view = Build();

        _top.Children.Clear();

        if (view.Running)
        {
            _top.Children.Add(RoutingKit.Ink(CodexModel.RunningText, TypeScale.ControlLarge, ThemeManager.GreyKey, wrap: true));
            Rows([]);
            return;
        }

        CodexKit.ShowSince(_periodRow, view.OnRecordSince, view.Failed);
        _top.Children.Add(_periodRow);

        if (view.Failed)
        {
            _top.Children.Add(CodexKit.Warning(view.OnRecordSince));
        }

        var count = view.Shown == view.Entries
            ? CodexModel.EntryCount(view.Entries)
            : $"{CodexModel.Number(view.Shown)} of {CodexModel.EntryCount(view.Entries)}";

        _top.Children.Add(StarSystemPage.GroupHead(
            CodexModel.PeriodName(Period) + " · " + CodexModel.RegionName(_regionId), CodexKit.Line(view.Line), count));

        if (view.Empty is { } empty)
        {
            _top.Children.Add(CreditsKit.Message(empty));
            Rows([]);
            return;
        }

        _top.Children.Add(CodexKit.Strip(view.Entries, view.New, view.Logged, "Subcategories", view.Subcategories));

        if (view.Shown == 0)
        {
            _top.Children.Add(CreditsKit.Message(CodexModel.NoEntryMatchText));
            Rows([]);
            return;
        }

        _top.Children.Add(CreditsKit.HeaderRow(_head, NewGrid(), i => HorizontalAlignment.Left));

        var items = new List<object>();

        foreach (var group in view.Groups)
        {
            items.Add(group);
            items.AddRange(_head.Sort(group.Rows));
        }

        Rows(items);
    }

    /// <summary>Shows <paramref name="items"/>, leaving the list and its scroll position alone when they are the items already shown.</summary>
    private void Rows(IReadOnlyList<object> items)
    {
        _scroll.IsVisible = items.Count > 0;

        if (items.Count > 0 && _shown.SequenceEqual(items))
        {
            return;
        }

        _shown = items;
        _list.ItemsSource = items;
    }

    private Grid NewGrid() => new()
    {
        ColumnDefinitions = new ColumnDefinitions(string.Join(',', _headset ? HeadsetColumns : PanelColumns)),
        ColumnSpacing = _headset ? 12 : 16,
    };

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

    private Border RowView(CodexEntryRow row)
    {
        var grid = NewGrid();
        grid.VerticalAlignment = VerticalAlignment.Center;

        CreditsKit.Place(grid, 0, CreditsKit.Line(row.Name, Fonts.ProseFamily, TypeScale.ControlLarge, ThemeManager.WhiteKey));
        CreditsKit.Place(grid, 1, SystemCell(row));
        CreditsKit.Place(grid, 2, CreditsKit.Mono(CodexModel.DateTime(row.At), TypeScale.Small, ThemeManager.GreyKey, right: false));
        CreditsKit.Place(grid, 3, StatusCell(row));

        var border = new Border
        {
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 6),
            Margin = new Thickness(0, 0, 0, Gaps.Tile),
            Child = grid,
        };
        RoutingKit.Themed(border, Border.BackgroundProperty, ThemeManager.SlabKey);

        return border;
    }

    private static StackPanel SystemCell(CodexEntryRow row)
    {
        var cell = new StackPanel();

        if (row.System is { } system)
        {
            cell.Children.Add(CreditsKit.Line(
                system, Fonts.ChromeFamily, TypeScale.ControlLarge, row.IsHere ? ThemeManager.CyanKey : ThemeManager.AKey));
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
            row.IsNew ? "NEW" : "ALREADY LOGGED",
            Fonts.ChromeFamily,
            TypeScale.Meta,
            row.IsNew ? ThemeManager.BlueKey : ThemeManager.GreyKey);
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

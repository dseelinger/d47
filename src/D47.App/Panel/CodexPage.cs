using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>Commander › Ledgers › Codex: the codex entries in the journals on this PC, by region (#1290).</summary>
public sealed class CodexPage : UserControl, IPageSummary, IFilterablePage
{
    public const string PageKey = "codex";

    private const string RegionPrefix = "codex-region-";

    private const string DiscoveriesPrefix = "codex-discoveries-";

    private const string EntryPrefix = "codex-entry-";

    public const string SummaryText = "The codex entries you have logged, from your journals.";

    public const string HeadsetSummaryText = "The codex entries you have logged.";

    private static readonly string[] PanelColumns = ["*", "120", "120", "120", "150", "16"];
    private static readonly string[] HeadsetColumns = ["*", "80", "80", "80", "110", "16"];

    private readonly Func<CommanderGameState?> _state;
    private readonly Func<HistoryState> _history;
    private readonly Func<DateTimeOffset> _now;
    private readonly CodexChoice _choice;
    private readonly PanelNavigator _nav;
    private readonly bool _headset;
    private readonly SortHeader<CodexRegionRow> _head = new("CodexHead", CodexModel.RegionHeads());
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
        bool headset = false)
    {
        _state = state;
        _history = history;
        _now = now;
        _choice = choice;
        _nav = nav;
        _headset = headset;

        _row = CodexKit.PeriodRow(_period);
        _head.Changed += Draw;
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

    /// <summary>The crumb of one entry's page in a region, named for the entry.</summary>
    public static NavCrumb EntryCrumb(int? regionId, long entryId, string name) =>
        new(
            EntryPrefix + (regionId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none") + "-"
            + entryId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            name)
        {
            Whole = true,
        };

    public static bool IsEntry(string key) => key.StartsWith(EntryPrefix, StringComparison.Ordinal);

    /// <summary>The region number and EntryID an entry crumb is for; null when the key is not one.</summary>
    public static (int? RegionId, long EntryId)? EntryOf(string key)
    {
        if (!IsEntry(key) || key[EntryPrefix.Length..].Split('-') is not [var region, var entry]
            || !long.TryParse(entry, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var entryId))
        {
            return null;
        }

        return (int.TryParse(region, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id) ? id : null, entryId);
    }

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

    /// <summary>Redraws when the record, the history walk, the minute or the period chosen on the other page has changed.</summary>
    public bool Tick()
    {
        if (ReferenceEquals(_state()?.CodexLedger, _seenLedger)
            && _history() == _seenHistory
            && Minute(_now()) == _seenMinute
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

    private CodexRootView Build() =>
        CodexModel.Regions(_state()?.CodexLedger ?? CodexLedger.None, Period, _now(), _history(), _query);

    private void Draw()
    {
        _seenLedger = _state()?.CodexLedger;
        _seenHistory = _history();
        _seenMinute = Minute(_now());

        var view = Build();

        _body.Children.Clear();

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
        var table = new StackPanel { Spacing = Gaps.Tile };

        if (header)
        {
            table.Children.Add(CreditsKit.HeaderRow(_head, NewGrid(), i => i == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right));
        }

        foreach (var row in _head.Sort(rows))
        {
            table.Children.Add(RowView(row));
        }

        return table;
    }

    private Grid NewGrid() => new()
    {
        ColumnDefinitions = new ColumnDefinitions(string.Join(',', _headset ? HeadsetColumns : PanelColumns)),
        ColumnSpacing = _headset ? 12 : 16,
    };

    private Button RowView(CodexRegionRow row)
    {
        var grid = NewGrid();
        grid.VerticalAlignment = VerticalAlignment.Center;

        CreditsKit.Place(grid, 0, NameOf(row));
        CreditsKit.Place(grid, 1, PeriodFigure(row, row.New, ThemeManager.BlueKey));
        CreditsKit.Place(grid, 2, PeriodFigure(row, row.Logged, ThemeManager.AKey));
        CreditsKit.Place(grid, 3, PeriodFigure(row, row.Entries, ThemeManager.AKey));

        if (row is { Recorded: { } recorded, Known: { } known })
        {
            CreditsKit.Place(grid, 4, new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Children =
                {
                    CreditsKit.Mono(CodexModel.Number(recorded), TypeScale.Small, ThemeManager.WhiteKey, right: true),
                    CreditsKit.Mono(" / " + CodexModel.Number(known), TypeScale.Small, ThemeManager.GreyKey, right: true),
                },
            });
        }

        CreditsKit.Place(grid, 5, CreditsKit.Line("›", Fonts.ChromeFamily, TypeScale.ControlLarge, ThemeManager.AKey));

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

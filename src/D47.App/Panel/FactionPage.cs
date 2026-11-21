using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>What Search › Faction draws.</summary>
public enum FactionView
{
    /// <summary>No faction yet: the name field with its suggestions.</summary>
    Empty,

    /// <summary>The headset's name field, opened over an answer with CHANGE FACTION.</summary>
    Picking,

    Loading,

    Failed,

    Answered,
}

/// <summary>Search › Faction: where one minor faction is present and the state it is in, from a Spansh report and the Commander's own readings.</summary>
public sealed class FactionPage : UserControl, IPageSummary, IPageSection
{
    public const string RootKey = "search.faction";

    public const string SummaryText = "Where a minor faction is present, and the state it is in.";

    public const string SquadronHelp = "Your squadron's faction. Type another name to look it up.";

    public const string OtherHelp = "Type another name to look it up.";

    public const string EmptyHelp = "Type a minor faction's name. Choosing a minor faction in Find opens it here too.";

    public const string UnknownText =
        "You are in a squadron, but D47 does not know which minor faction it backs yet. "
        + "It learns the faction when you are in one of its systems, or when you say “my squadron's faction is …”.";

    public const string SearchingText = "SEARCHING THE FACTION'S SYSTEMS AND STATIONS";

    public const string NoSystemText = "No system of this faction has that name.";

    public const string UnreadableText = "The galaxy search answered with something I couldn't read.";

    public const string NoReportText = "The galaxy search has no minor faction by that name present in any system.";

    public const string FailedLabel = "GALAXY SEARCH UNAVAILABLE";

    public const string TimedOutText = "The galaxy search took too long to answer.";

    public const string FieldName = "FactionField";
    public const string RefreshName = "FactionRefresh";
    public const string ChangeName = "FactionChange";
    public const string BackName = "FactionBack";
    public const string RetryName = "FactionRetry";
    public const string SectionsName = "FactionSections";
    public const string SystemsFindName = "FactionSystemsFind";
    public const string PreviousName = "FactionPrevious";
    public const string NextName = "FactionNext";
    public const string TableId = "Search.Faction";
    public const string HeadName = "FactionHead";
    public const string ProgressName = "FactionProgress";
    public const string ConflictsHeadName = "FactionConflictsHead";
    public const string AssetsHeadName = "FactionAssetsHead";
    public const string AssetsFindName = "FactionAssetsFind";
    public const string ConflictsTableId = "Search.Faction.Conflicts";
    public const string AssetsTableId = "Search.Faction.Assets";

    public static readonly IReadOnlyList<string> Sections = ["OVERVIEW", "SYSTEMS", "CONFLICTS", "ASSETS"];

    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(150);

    private readonly GalaxySearchSurface _searches;
    private readonly bool _headset;
    private readonly TextBox _field = SystemSearchPage.Field("minor faction");
    private readonly SystemNameSuggestions<string>? _names;
    private readonly TextBlock _unknown;
    private readonly Grid _fieldLine = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
    private readonly TextBlock _helpBeside = SearchText.Line();
    private readonly TextBlock _helpBelow = SearchText.Line();
    private readonly Button _back = new() { Name = BackName, Content = "BACK", Height = TypeScale.MinimumTarget, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Border _headSlot = new();
    private readonly Border _noteSlot = new();
    private readonly Border _switchSlot = new();
    private readonly Border _content = new();
    private readonly TextBlock _stamp = new() { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
    private readonly Segment _sections;
    private readonly TextBox _find = SystemSearchPage.Field("find a system");
    private readonly TextBlock _count;
    private readonly Border _table = new();
    private readonly Border _systemsTile = new() { Margin = new Thickness(Gaps.Tile, 0, 0, 0) };
    private readonly Border _assetsTile = new() { Margin = new Thickness(Gaps.Tile, 0, 0, 0) };
    private readonly Grid _systemsView = new() { RowDefinitions = new RowDefinitions("Auto,*") };
    private readonly Func<DateTimeOffset> _now;
    private ArrangedTable<FactionRow>? _systemColumns;
    private ArrangedTable<FactionConflict>? _conflictColumns;
    private ArrangedTable<FactionStation>? _assetColumns;
    private DateTimeOffset _drawnAt;
    private string? _drawnHere;
    private double _barWidth;
    private readonly Border _conflictsTable = new();
    private readonly Border _assetsTable = new();
    private readonly Grid _conflictsView = new() { RowDefinitions = new RowDefinitions("*,Auto") };
    private readonly Grid _assetsView = new() { RowDefinitions = new RowDefinitions("Auto,*") };
    private readonly TextBox _assetFind = SystemSearchPage.Field("find a station or system");
    private readonly InlinePicker _assetKind = new() { Label = "Kind of station", ListMaxHeight = 400, DetailMono = true };
    private readonly TextBlock _assetCount;

    private CancellationTokenSource? _fetch;
    private int _generation;
    private bool _chosen;
    private FactionView _view = FactionView.Empty;
    private FactionView _before = FactionView.Empty;
    private string? _faction;
    private FactionReport? _report;
    private string _failure = TimedOutText;
    private DateTimeOffset _failedAt;
    private int _section;
    private int _page;
    private bool _keepScroll;
    private ScrollViewer? _overviewScroll;
    private ScrollViewer? _systemsScroll;
    private FactionProgress? _progress;
    private string? _statusKey;
    private IReadOnlyList<FactionRow> _rows = [];
    private IReadOnlyList<FactionConflict> _conflicts = [];
    private IReadOnlyList<FactionStation> _stations = [];
    private string _kind = FactionAssets.AllId;
    private ScrollViewer? _conflictsScroll;
    private ScrollViewer? _assetsScroll;
    private object? _seen;

    public FactionPage(GalaxySearchSurface searches, bool headset, string? faction = null)
    {
        _searches = searches;
        _headset = headset;
        _now = () => Now();

        _field.Name = FieldName;
        _field.Width = 460;
        _field.HorizontalAlignment = HorizontalAlignment.Left;
        _field.PlaceholderText = "Type a minor faction's name";
        _field.InnerLeftContent = SearchText.Prefix("Faction");
        _field.KeyDown += OnFieldKey;

        if (searches.FactionNames is { } lookup)
        {
            SystemNameSuggestions<string>? names = null;
            names = new SystemNameSuggestions<string>(
                _field, lookup, name => name, () => true, name =>
                {
                    names!.SetText(name);
                    Choose(name);
                },
                searches.SuggestionPause);
            _names = names;
        }

        _unknown = RoutingKit.Ink(UnknownText, TypeScale.ControlLarge, ThemeManager.WhiteKey, wrap: true);
        _unknown.MaxWidth = 760;

        _helpBeside.Margin = new Thickness(16, 0, 0, 0);
        _helpBeside.VerticalAlignment = VerticalAlignment.Top;
        _helpBeside.Padding = new Thickness(0, 14, 0, 0);

        var fieldHost = new StackPanel { Spacing = 2, Width = 460, HorizontalAlignment = HorizontalAlignment.Left, Children = { _field } };

        if (_names is not null)
        {
            fieldHost.Children.Add(_names.List);
        }

        Grid.SetColumn(_helpBeside, 1);
        _fieldLine.Children.Add(fieldHost);
        _fieldLine.Children.Add(_helpBeside);

        _back.Click += (_, _) =>
        {
            _view = _before;
            Draw();
        };

        _sections = new Segment
        {
            Name = SectionsName,
            ItemsSource = Sections,
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = headset ? TypeScale.MinimumTarget : Segment.StatusHeight,
        };
        _sections.SelectionChanged += (_, _) =>
        {
            StopArranging();
            _section = _sections.SelectedIndex;
            _page = 0;
            DrawContent();
            ReportSection();
        };

        _count = Chrome(string.Empty, TypeScale.Meta, FontWeight.Medium, ThemeManager.GreyKey);
        _count.HorizontalAlignment = HorizontalAlignment.Right;
        _count.VerticalAlignment = VerticalAlignment.Center;

        _find.Name = SystemsFindName;
        _find.Width = 340;
        _find.HorizontalAlignment = HorizontalAlignment.Left;
        _find.PlaceholderText = "Find a system";
        _find.TextChanged += (_, _) =>
        {
            _page = 0;
            DrawSystems();
        };

        var toolbar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, 0, 0, 6) };
        Grid.SetColumn(_count, 1);
        Grid.SetColumn(_systemsTile, 2);
        toolbar.Children.Add(_find);
        toolbar.Children.Add(_count);
        toolbar.Children.Add(_systemsTile);
        Grid.SetRow(_table, 1);
        _systemsView.Children.Add(toolbar);
        _systemsView.Children.Add(_table);

        _assetCount = Chrome(string.Empty, TypeScale.Meta, FontWeight.Medium, ThemeManager.GreyKey);
        _assetCount.HorizontalAlignment = HorizontalAlignment.Right;
        _assetCount.VerticalAlignment = VerticalAlignment.Center;
        _assetFind.Name = AssetsFindName;
        _assetFind.Width = 340;
        _assetFind.HorizontalAlignment = HorizontalAlignment.Left;
        _assetFind.PlaceholderText = "Find a station or system";
        _assetFind.TextChanged += (_, _) =>
        {
            _page = 0;
            DrawAssets();
        };
        _assetKind.Width = 300;
        _assetKind.HorizontalAlignment = HorizontalAlignment.Left;
        _assetKind.VerticalAlignment = VerticalAlignment.Top;
        _assetKind.Margin = new Thickness(12, 0, 0, 0);
        _assetKind.Picked += (_, id) =>
        {
            _kind = id;
            _page = 0;
            DrawAssets();
        };

        var assetBar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), Margin = new Thickness(0, 0, 0, 6) };
        _assetFind.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(_assetKind, 1);
        Grid.SetColumn(_assetCount, 2);
        Grid.SetColumn(_assetsTile, 3);
        assetBar.Children.Add(_assetFind);
        assetBar.Children.Add(_assetKind);
        assetBar.Children.Add(_assetCount);
        assetBar.Children.Add(_assetsTile);
        Grid.SetRow(_assetsTable, 1);
        _assetsView.Children.Add(assetBar);
        _assetsView.Children.Add(_assetsTable);

        var conflictNote = CreditsKit.Line(FactionConflicts.NoteText, Fonts.ProseFamily, TypeScale.Small, ThemeManager.GreyKey);
        conflictNote.TextWrapping = TextWrapping.Wrap;
        conflictNote.Margin = new Thickness(0, 8, 0, 0);
        Grid.SetRow(conflictNote, 1);
        _conflictsView.Children.Add(_conflictsTable);
        _conflictsView.Children.Add(conflictNote);

        var top = new StackPanel
        {
            Spacing = 12,
            Children = { _unknown, _fieldLine, _helpBelow, _back },
        };

        var footer = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 10, 0, 0),
            Margin = new Thickness(0, 12, 0, 0),
            MinHeight = 32,
            Child = _stamp,
        };
        ThemeInk.Bind(footer, Border.BorderBrushProperty, ThemeManager.Line2Key);

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*,Auto"), Margin = new Thickness(0, 10, 0, 0) };
        _headSlot.Margin = new Thickness(0, 12, 0, 0);
        _noteSlot.Margin = new Thickness(0, 4, 0, 0);
        _switchSlot.Margin = new Thickness(0, 10, 0, 10);
        InRow(root, top, 0);
        InRow(root, _headSlot, 1);
        InRow(root, _noteSlot, 2);
        InRow(root, _switchSlot, 3);
        InRow(root, _content, 4);
        InRow(root, footer, 5);
        Content = root;

        Draw();

        if (faction is not null)
        {
            Choose(faction);
        }
        else
        {
            FollowSquadron();
        }
    }

    public string Summary => SummaryText;

    private readonly PageSectionWord _sectionWord = new();

    public string OpenSection => _sectionWord.Word;

    public event EventHandler? OpenSectionChanged
    {
        add => _sectionWord.Changed += value;
        remove => _sectionWord.Changed -= value;
    }

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    public ColumnWidthMemory? ColumnWidths { get; init; }

    /// <summary>Which columns the tables show; the headset's memory when the page is drawn there.</summary>
    public ColumnArrangementMemory? ColumnArrangements { get; init; }

    public Func<string, Task<bool>>? Copy { get; init; }

    /// <summary>The clock the page reads; a test sets it.</summary>
    public Func<DateTimeOffset> Now { get; init; } = () => DateTimeOffset.UtcNow;

    /// <summary>The section showing: 0 Overview, 1 Systems, 2 Conflicts, 3 Assets.</summary>
    public int Section => _section;

    /// <summary>The rows the Systems table draws now: the page on the headset, every match on the desktop.</summary>
    public int RowsDrawn { get; private set; }

    /// <summary>Opens the page on <paramref name="faction"/> and asks for its report.</summary>
    public void Show(string faction) => Choose(faction);

    /// <summary>Follows the squadron faction, the Commander's readings and the clock; true when it redrew.</summary>
    public bool Tick()
    {
        var state = _searches.GameState?.Invoke();
        var key = (_view, state?.Standings, state?.Squadron.IsMember, _searches.SquadronFaction?.Invoke(), state?.Location.StarSystem, _searches.BgsTick?.Invoke(), PageKit.Minute(_now()));

        if (Equals(key, _seen))
        {
            return false;
        }

        FollowSquadron();
        _keepScroll = true;
        Draw();
        _keepScroll = false;
        return true;
    }

    private static void InRow(Grid grid, Control child, int row)
    {
        Grid.SetRow(child, row);
        grid.Children.Add(child);
    }

    private string? Squadron() => _searches.SquadronFaction?.Invoke() is { Length: > 0 } name ? name : null;

    private bool SquadronUnknown() =>
        Squadron() is null && _searches.GameState?.Invoke()?.Squadron.IsMember == true;

    private void FollowSquadron()
    {
        if (!_chosen && _view == FactionView.Empty && Squadron() is { } squadron)
        {
            Begin(squadron, refresh: false);
        }
    }

    private void Choose(string faction)
    {
        _chosen = true;
        Begin(faction, refresh: false);
    }

    private void OnFieldKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(_field.Text))
        {
            e.Handled = true;
            Choose(_field.Text.Trim());
        }
    }

    private void Begin(string faction, bool refresh)
    {
        faction = faction.Trim();

        if (faction.Length == 0 || _searches.Faction is not { } fetch)
        {
            return;
        }

        var changed = !string.Equals(faction, _faction, StringComparison.OrdinalIgnoreCase);

        if (_fetch is { } old)
        {
            old.Cancel();
            old.Dispose();
        }

        var source = _fetch = new CancellationTokenSource(Budget);
        var generation = ++_generation;
        _progress = null;

        if (changed)
        {
            _report = null;
            StopArranging();
            _find.Text = string.Empty;
            _section = 0;
            _sections.SelectedIndex = 0;
            _page = 0;
        }

        _faction = faction;
        _view = FactionView.Loading;
        Draw();
        _ = AskAsync(fetch, faction, refresh, source, generation);
    }

    private async Task AskAsync(
        Func<string, bool, IProgress<FactionProgress>, CancellationToken, Task<FactionReport>> fetch,
        string faction,
        bool refresh,
        CancellationTokenSource source,
        int generation)
    {
        FactionReport? report = null;
        string? failure = null;
        var token = source.Token;
        var progress = new Progress<FactionProgress>(read =>
        {
            if (generation == _generation)
            {
                _progress = read;
                DrawSwitch();
            }
        });

        try
        {
            report = await Task.Run(() => fetch(faction, refresh, progress, token), token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            failure = TimedOutText;
        }
        catch (GalaxyUnavailableException ex)
        {
            failure = ex.Message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            failure = UnreadableText;
        }

        if (generation != _generation)
        {
            return;
        }

        if (report is not null)
        {
            _report = report;
            _view = FactionView.Answered;
        }
        else
        {
            _failure = failure ?? TimedOutText;
            _failedAt = DateTimeOffset.Now;
            _view = FactionView.Failed;
        }

        Draw();
    }

    private bool UnchangedSinceTick(string system) =>
        _searches.BgsTick?.Invoke() is { } tick
        && _searches.GameState?.Invoke()?.Standings.Since(system, tick) == SinceTick.Unchanged;

    private bool EmptyLike => _view is FactionView.Empty or FactionView.Picking;

    private void Draw()
    {
        var state = _searches.GameState?.Invoke();
        _seen = (_view, state?.Standings, state?.Squadron.IsMember, _searches.SquadronFaction?.Invoke(), state?.Location.StarSystem, _searches.BgsTick?.Invoke(), PageKit.Minute(_now()));

        var standings = state?.Standings ?? SystemStandings.Empty;
        _rows = _report is null ? [] : FactionRows.Of(_report, standings);
        _conflicts = _report is null ? [] : FactionConflictRows.Of(_report, standings);
        _stations = _report is null ? [] : FactionAssets.Of(_report);

        var emptyLike = EmptyLike;
        var squadron = Squadron();
        var isSquadron = !emptyLike && squadron is not null && string.Equals(squadron, _faction, StringComparison.OrdinalIgnoreCase);

        _unknown.IsVisible = emptyLike && SquadronUnknown();
        _fieldLine.IsVisible = !(_headset && !emptyLike);
        _helpBeside.IsVisible = !emptyLike;
        _helpBelow.IsVisible = emptyLike;
        _back.IsVisible = _view == FactionView.Picking;

        if (emptyLike)
        {
            _helpBelow.Text = EmptyHelp;

            Dispatcher.UIThread.Post(() => _field.Focus());
        }
        else
        {
            _helpBeside.Text = isSquadron ? SquadronHelp : OtherHelp;

            if (_fieldShows != _faction)
            {
                _fieldShows = _faction;
                _names?.SetText(_faction ?? string.Empty);
            }

            ThemeInk.Bind(_field, TextBox.ForegroundProperty, isSquadron ? ThemeManager.CyanKey : ThemeManager.WhiteKey);
        }

        DrawHead();
        DrawSwitch();
        DrawContent();
        DrawStamp();
    }

    private string? _fieldShows;

    private void DrawHead()
    {
        if (EmptyLike)
        {
            _headSlot.Child = null;
            _noteSlot.Child = null;
            return;
        }

        var loading = _view == FactionView.Loading;
        var answered = _view == FactionView.Answered;
        var overview = answered && _report is not null
            ? FactionModel.Overview(_report, Rows())
            : null;

        var title = Chrome(loading ? "SEARCHING" : _faction ?? string.Empty, 16, FontWeight.SemiBold, ThemeManager.AKey);
        var aside = Chrome(
            loading ? _faction ?? string.Empty : overview is null ? string.Empty : $"{overview.Government} · {overview.Allegiance}",
            TypeScale.Meta,
            FontWeight.Medium,
            ThemeManager.AKey);
        aside.Margin = new Thickness(12, 0, 0, 0);
        aside.TextTrimming = TextTrimming.CharacterEllipsis;

        var tiles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tile, VerticalAlignment = VerticalAlignment.Center };

        if (_headset && answered && _report is not null)
        {
            var age = CreditsKit.Line(FactionModel.FetchedText(FetchedAge()), Fonts.ProseFamily, TypeScale.Tip, ThemeManager.GreyKey);
            age.Margin = new Thickness(0, 0, 12, 0);
            age.VerticalAlignment = VerticalAlignment.Center;
            tiles.Children.Add(age);
        }

        if (_headset && _view is FactionView.Answered or FactionView.Failed)
        {
            var change = new Button { Name = ChangeName, Content = "CHANGE FACTION", Height = TypeScale.MinimumTarget };
            change.Click += (_, _) =>
            {
                _before = _view;
                _view = FactionView.Picking;
                _names?.SetText(string.Empty);
                _fieldShows = null;
                Draw();
            };
            tiles.Children.Add(change);
        }

        if (_view != FactionView.Failed)
        {
            var refresh = new Button { Name = RefreshName, Content = "REFRESH", Height = TypeScale.MinimumTarget, IsEnabled = !loading };
            refresh.Click += (_, _) =>
            {
                if (_faction is { } faction)
                {
                    Begin(faction, refresh: true);
                }
            };
            tiles.Children.Add(refresh);
        }

        var line = new DockPanel { Height = TypeScale.MinimumTarget };
        DockPanel.SetDock(tiles, Dock.Right);
        line.Children.Add(tiles);
        line.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { title, aside } });

        var head = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Child = line };
        ThemeInk.Bind(head, Border.BorderBrushProperty, ThemeManager.LineKey);
        _headSlot.Child = head;

        _noteSlot.Child = !_headset && answered && _report is not null
            ? CreditsKit.Line(FactionModel.FetchedText(FetchedAge()), Fonts.ProseFamily, TypeScale.ControlLarge, ThemeManager.GreyKey)
            : null;
    }

    private TimeSpan FetchedAge() =>
        _report?.FetchedAt is { } at ? _now() - at : TimeSpan.Zero;

    private void ReportSection() =>
        _sectionWord.Set(this, ReferenceEquals(_switchSlot.Child, _sections) && _section >= 0 && _section < Sections.Count ? Sections[_section] : string.Empty);

    private void DrawSwitch()
    {
        DrawSwitchSlot();
        ReportSection();
    }

    private void DrawSwitchSlot()
    {
        switch (_view)
        {
            case FactionView.Answered when _report is { Systems.Count: 0 }:
                _switchSlot.Child = CreditsKit.Message(NoReportText);
                break;

            case FactionView.Answered when _report is not null:
                var rows = Rows();
                var statusKey = $"{rows.Count}/{_conflicts.Count}/{_stations.Count}";

                if (_statusKey != statusKey && !_headset)
                {
                    _statusKey = statusKey;
                    _sections.Statuses =
                    [
                        new ChoiceStatus($"Present in {rows.Count}", ChoiceTone.Grey),
                        new ChoiceStatus($"{rows.Count} systems", ChoiceTone.Grey),
                        new ChoiceStatus($"{_conflicts.Count} {(_conflicts.Count == 1 ? "system" : "systems")}", ChoiceTone.Grey),
                        new ChoiceStatus($"{_stations.Count} {(_stations.Count == 1 ? "station" : "stations")}", ChoiceTone.Grey),
                    ];
                }

                _sections.SelectedIndex = _section;
                _switchSlot.Child = _sections;
                break;

            case FactionView.Loading:
                var slab = new Border
                {
                    MinHeight = 50,
                    Padding = new Thickness(14, 10),
                    Child = new StackPanel
                    {
                        VerticalAlignment = VerticalAlignment.Center,
                        Spacing = 4,
                        Children =
                        {
                            Chrome(SearchingText, TypeScale.Meta, FontWeight.SemiBold, ThemeManager.GreyKey),
                            ProgressLine(),
                        },
                    },
                };
                ThemeInk.Bind(slab, Border.BackgroundProperty, ThemeManager.SlabKey);
                _switchSlot.Child = slab;
                break;

            case FactionView.Failed:
                var notice = new Notice(NoticeLevel.Error)
                {
                    Label = FailedLabel,
                    Text = _failure,
                    Detail = _failedAt.ToString("HH:mm", CultureInfo.InvariantCulture),
                };
                var retry = new Button { Name = RetryName, Content = "RETRY", Height = TypeScale.MinimumTarget };
                retry.Click += (_, _) =>
                {
                    if (_faction is { } faction)
                    {
                        Begin(faction, refresh: true);
                    }
                };
                notice.Actions.Add(retry);
                _switchSlot.Child = notice;
                break;

            default:
                _switchSlot.Child = null;
                break;
        }
    }

    /// <summary>"SYSTEMS READ 120 OF 590".</summary>
    public static string ProgressText(FactionProgress read) =>
        $"SYSTEMS READ {read.Read.ToString("N0", CultureInfo.InvariantCulture)} OF {read.Total.ToString("N0", CultureInfo.InvariantCulture)}";

    /// <summary>The wait mark before the first page arrives, then the count of systems read.</summary>
    private Control ProgressLine()
    {
        if (_progress is not { } read)
        {
            return new WaitingState(WaitForm.Line, GalaxySearchPage.RetrievingText) { Name = ProgressName };
        }

        var line = CreditsKit.Mono(ProgressText(read), 11, ThemeManager.GreyKey, right: false);
        line.Name = ProgressName;

        return line;
    }

    private IReadOnlyList<FactionRow> Rows() => _rows;

    private void DrawContent()
    {
        if (_view != FactionView.Answered || _report is null or { Systems.Count: 0 })
        {
            _content.Child = null;
            RowsDrawn = 0;
            return;
        }

        if (_section == 0)
        {
            _content.Child = Overview();
            RowsDrawn = 0;
            return;
        }

        switch (_section)
        {
            case 2:
                _content.Child = _conflictsView;
                RowsDrawn = 0;
                DrawConflicts();
                break;

            case 3:
                _content.Child = _assetsView;
                RowsDrawn = 0;
                DrawAssets();
                break;

            default:
                _content.Child = _systemsView;
                DrawSystems();
                break;
        }
    }

    private Control Overview()
    {
        var report = _report!;
        var overview = FactionModel.Overview(report, Rows());

        var tiles = new List<Control>
        {
            StatTile.Build("Government", overview.Government, StatInk.Name),
            StatTile.Build("Allegiance", overview.Allegiance, StatInk.Name),
            StatTile.Tile("Present in", Counted(overview.Present, "system")),
            StatTile.Tile("Systems controlled", Counted(overview.Controlled, "system")),
            StatTile.Tile("Stations controlled", Counted(overview.Stations, "station")),
            StatTile.Tile("In a conflict, active or pending", Counted(overview.InConflict, "system")),
        };

        var heading = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                Chrome("SYSTEMS IN EACH ACTIVE STATE", 15, FontWeight.SemiBold, ThemeManager.WhiteKey),
                CreditsKit.Line("A system can be in more than one state. States no system is in are left out.", Fonts.ProseFamily, TypeScale.Tip, ThemeManager.GreyKey),
            },
        };

        var groups = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 28,
            RowSpacing = 20,
        };

        for (var i = 0; i < overview.Groups.Count; i++)
        {
            var block = Group(overview.Groups[i]);
            Grid.SetColumn(block, i % 2);
            Grid.SetRow(block, i / 2);
            groups.Children.Add(block);
        }

        var stack = new StackPanel { Spacing = 20, Margin = new Thickness(0, 0, 14, 0), Children = { StatTile.Grid(tiles, maxColumns: 3), heading, groups } };

        var saved = _keepScroll ? _overviewScroll?.Offset : null;
        var scroll = _overviewScroll = LoadoutPages.Scrolling(stack);
        Restore(scroll, saved);

        return scroll;
    }

    private static void Restore(ScrollViewer scroll, Vector? offset)
    {
        if (offset is { } saved && saved != default)
        {
            Dispatcher.UIThread.Post(() => scroll.Offset = saved, DispatcherPriority.Background);
        }
    }

    private static TextBlock Counted(int number, string noun)
    {
        var block = new TextBlock
        {
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = StatTile.ValueSize,
            FontWeight = FontWeight.Medium,
        };
        ThemeInk.Bind(block, TextBlock.ForegroundProperty, ThemeManager.AKey);
        block.Inlines ??= [];
        block.Inlines.Add(Fonts.Mono(number.ToString("N0", CultureInfo.InvariantCulture)));
        block.Inlines.Add(new Run(" " + noun + (number == 1 ? string.Empty : "s")));

        return block;
    }

    private static Control Group(FactionStateGroup group)
    {
        var stack = new StackPanel { Spacing = Gaps.Tile };
        stack.Children.Add(StarSystemPage.GroupHead(group.Name, group.Description));

        if (group.States.Count == 0)
        {
            stack.Children.Add(CreditsKit.Line("No system is in one of these states.", Fonts.ProseFamily, TypeScale.ControlLarge, ThemeManager.GreyKey));
            return stack;
        }

        foreach (var state in group.States)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,70,56"), ColumnSpacing = 12, MinHeight = 40 };
            Place(row, CreditsKit.Line(state.Name, Fonts.ProseFamily, 15, ThemeManager.WhiteKey), 0);
            Place(row, CreditsKit.Mono(state.Count.ToString(CultureInfo.InvariantCulture), 14, ThemeManager.AKey, right: true), 1);
            Place(row, CreditsKit.Mono(state.Share, 12, ThemeManager.GreyKey, right: true), 2);

            var slab = new Border { Padding = new Thickness(14, 0), Child = row };
            ThemeInk.Bind(slab, Border.BackgroundProperty, ThemeManager.SlabKey);
            stack.Children.Add(slab);
        }

        return stack;
    }

    private static void Place(Grid grid, Control cell, int column)
    {
        cell.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }

    private TableColumn Fill(string key, double desktop, double headset) =>
        new(key, ColumnFit.Fill) { Min = _headset ? headset : desktop };

    private TableColumn Sized(string key, double desktop, double headset) =>
        new(key) { Min = _headset ? headset : desktop };

    private TableColumn Fixed(string key, double desktop, double headset) =>
        new(key, ColumnFit.Fixed) { Fixed = _headset ? headset : desktop };

    private ArrangedTable<T> Arranged<T>(string id, IReadOnlyList<TableField<T>> fields, string headName, string? sortedByDefault, Action redraw, Action? sorted = null)
    {
        var table = new ArrangedTable<T>(
            id, fields, ColumnArrangements, _headset ? null : ColumnWidths, _headset ? 12 : 16, sortedByDefault, headName);

        if (sorted is not null)
        {
            table.Sorted += sorted;
        }

        table.Changed += redraw;
        table.FilterChanged += () => _page = 0;

        return table;
    }

    private ArrangedTable<FactionRow> SystemColumns =>
        _systemColumns ??= Arranged(TableId, SystemFields(), HeadName, "Influence", DrawSystems, () => _page = 0);

    private ArrangedTable<FactionConflict> ConflictColumns =>
        _conflictColumns ??= Arranged(ConflictsTableId, ConflictFields(), ConflictsHeadName, null, DrawConflicts);

    private ArrangedTable<FactionStation> AssetColumns =>
        _assetColumns ??= Arranged(AssetsTableId, AssetFields(), AssetsHeadName, "System", DrawAssets, () => _page = 0);

    private void StopArranging()
    {
        _systemColumns?.Reopen();
        _conflictColumns?.Reopen();
        _assetColumns?.Reopen();
    }

    /// <summary>
    /// The FILTER and COLUMNS bars, ready for the table's header to follow; with <paramref name="line"/>, the tiles on a line of
    /// their own above them, otherwise the filter's head line. <paramref name="rows"/> are every row the table has.
    /// </summary>
    private static StackPanel ArrangerAbove<T>(ArrangedTable<T> columns, IReadOnlyList<T> rows, bool line = false)
    {
        var top = new StackPanel();

        if (line)
        {
            top.Children.Add(LedgerTables.Arranger(columns, rows));

            return top;
        }

        columns.Observe(rows);

        if (ColumnFilter.Head(columns, rows, null) is { } words)
        {
            words.Margin = new Thickness(0, 0, 0, 6);
            top.Children.Add(words);
        }

        if (ColumnFilter.Bar(columns, rows) is { } filter)
        {
            top.Children.Add(filter);
        }

        if (ColumnArranger.Bar(columns) is { } bar)
        {
            top.Children.Add(bar);
        }

        return top;
    }

    /// <summary>CLEAR FILTERS while rows are hidden, then FILTER and COLUMNS, for the toolbar.</summary>
    private static StackPanel Tiles<T>(ArrangedTable<T> columns, IReadOnlyList<T> rows)
    {
        columns.Observe(rows);
        var tiles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tile };

        if (columns.Hidden(rows) > 0)
        {
            tiles.Children.Add(ColumnFilter.HeadClear(columns));
        }

        if (ColumnFilter.Tile(columns) is { } filter)
        {
            tiles.Children.Add(filter);
        }

        if (ColumnArranger.Tile(columns) is { } tile)
        {
            tiles.Children.Add(tile);
        }

        return tiles;
    }

    private TableGrid HeadOf<T>(ArrangedTable<T> columns)
    {
        var header = columns.Header();
        header.MinHeight = TypeScale.MinimumTarget;
        header.Margin = new Thickness(14, 0);

        return header;
    }

    /// <summary>In the headset the header is kept as wide as the rows, so both report one width to the table.</summary>
    private void FollowRows(TableGrid header, ScrollViewer rows)
    {
        if (!_headset)
        {
            return;
        }

        // The headset's rows sit in the scroller's gutter and the stack's own right margin; the header meets the first of them.
        // The bar is always shown, so its width never changes the rows' width between one draw and the next.
        rows.VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Visible;

        void Follow()
        {
            if (rows.Bounds.Width > 0)
            {
                _barWidth = rows.Bounds.Width - rows.Viewport.Width;
            }

            header.Margin = new Thickness(12, 0, 12 + 14 + _barWidth, 0);
        }

        Follow();
        rows.ScrollChanged += (_, _) => Follow();
    }

    private static TableField<T> Added<T>(string key, TableColumn column, Func<T, Control> cell, SortColumn<T> sort) =>
        new(key, column, row =>
        {
            var control = cell(row);
            control.VerticalAlignment = VerticalAlignment.Center;

            return control;
        })
        { Label = sort.Label, Sort = sort, ByDefault = false };

    private IReadOnlyList<TableField<FactionRow>> SystemFields()
    {
        var heads = FactionModel.Heads(() => _now());

        return
        [
            StarSystemPage.Headed(heads[0], Fill("System", 170, 130), row => CopyGlyph.LinkBeside(SystemName(row.System), row.System, Copy))
                with { Filters = [ValueLists.One<FactionRow>("System", row => row.System, "No system")] },
            StarSystemPage.Headed(heads[1], Sized("Influence", 96, 76), InfluenceCell, right: true),
            StarSystemPage.Headed(heads[2], Fill("Active", 100, 90), row => States(row.Active, StateInk(row)))
                with { Filters = [ValueLists.Many<FactionRow>("Active", row => row.Active, "No state")] },
            StarSystemPage.Headed(heads[3], Fill("Pending", 100, 90), row => States(row.Pending, StateInk(row)))
                with { Filters = [ValueLists.Many<FactionRow>("Pending", row => row.Pending, "No state")] },
            StarSystemPage.Headed(heads[4], Fill("Recovering", 110, 90), row => States(row.Recovering, StateInk(row)))
                with { Filters = [ValueLists.Many<FactionRow>("Recovering", row => row.Recovering, "No state")] },
            StarSystemPage.Headed(heads[5], Sized("Controls", 84, 64), ControlsCell)
                with { Filters = [ValueLists.Flag<FactionRow>("Controls", row => row.Controls, "Not known")] },
            StarSystemPage.Headed(
                heads[6],
                Sized("Updated", 190, 132),
                row => Updated(row, _drawnAt, FactionModel.IsOld(row, _drawnAt), row.OwnReading && UnchangedSinceTick(row.System)),
                right: true),
            Added(
                "InConflict",
                Sized("InConflict", 96, 96),
                row => TableCells.Flag(row.InConflict),
                SortColumn<FactionRow>.Figure<int>("In conflict", row => row.InConflict ? 1 : 0, SortDirection.Descending))
                with { Filters = [ValueLists.Flag<FactionRow>("In conflict", row => row.InConflict, "Not known")] },
        ];
    }

    private TextBlock SystemName(string system)
    {
        var name = CreditsKit.Line(
            system,
            Fonts.ChromeFamily,
            TypeScale.ControlLarge,
            string.Equals(system, _drawnHere, StringComparison.OrdinalIgnoreCase) ? ThemeManager.CyanKey : ThemeManager.AKey);
        name.FontWeight = FontWeight.Medium;

        return name;
    }

    private string StateInk(FactionRow row) => FactionModel.IsOld(row, _drawnAt) ? ThemeManager.GreyKey : ThemeManager.WhiteKey;

    private static Control InfluenceCell(FactionRow row) =>
        row.Influence is { } fraction
            ? CreditsKit.Mono((fraction * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%", TypeScale.ControlLarge, ThemeManager.AKey, right: true)
            : CreditsKit.Mono(FactionModel.Dash, TypeScale.ControlLarge, ThemeManager.GreyKey, right: true);

    private static Control ControlsCell(FactionRow row)
    {
        var controls = CreditsKit.Line(row.Controls ? "✓" : string.Empty, Fonts.ChromeFamily, 15, ThemeManager.WhiteKey);
        controls.HorizontalAlignment = HorizontalAlignment.Center;

        return controls;
    }

    private void DrawSystems()
    {
        if (_report is null)
        {
            return;
        }

        var columns = SystemColumns;
        columns.Refresh();

        _drawnAt = _now();
        _drawnHere = _searches.GameState?.Invoke()?.Location.StarSystem;
        var all = Rows();
        var matches = FactionModel.Matching(all, _find.Text);
        columns.Observe(all);
        var sorted = columns.Shown(matches);

        _count.Text = FactionModel.CountText(sorted.Count, all.Count);
        _page = FactionModel.ClampPage(_page, sorted.Count);

        _systemsTile.Child = Tiles(columns, all);
        var top = ArrangerAbove(columns, all);
        var header = HeadOf(columns);

        if (sorted.Count == 0)
        {
            RowsDrawn = 0;
            top.Children.Add(header);

            if (ColumnFilter.Empty(columns, all) is { } hidden)
            {
                top.Children.Add(hidden);
                _table.Child = top;
                return;
            }

            var none = CreditsKit.Line(NoSystemText, Fonts.ProseFamily, TypeScale.ControlLarge, ThemeManager.GreyKey);
            none.Margin = new Thickness(0, 12, 0, 0);
            top.Children.Add(none);
            _table.Child = top;
            return;
        }

        Control list;

        if (_headset)
        {
            var shown = sorted.Skip(_page * FactionModel.PageSize).Take(FactionModel.PageSize).ToList();
            var stack = new StackPanel { Spacing = Gaps.Tile, Margin = new Thickness(0, 0, 14, 0), IsVisible = !ColumnFilter.HidesRows(columns) };

            foreach (var row in shown)
            {
                stack.Children.Add(RowView(row, columns));
            }

            stack.Children.Add(Pager(sorted.Count, DrawSystems));
            RowsDrawn = shown.Count;
            list = stack;
        }
        else
        {
            RowsDrawn = sorted.Count;
            list = new ItemsControl
            {
                ItemsPanel = new FuncTemplate<Avalonia.Controls.Panel?>(() => new VirtualizingStackPanel { }),
                ItemTemplate = new FuncDataTemplate<FactionRow>((row, _) => row is null ? null : Spaced(RowView(row, columns))),
                ItemsSource = sorted,
            };
        }

        var offset = _keepScroll ? _systemsScroll?.Offset : null;
        _table.Child = LoadoutPages.HeaderAndList(top, header, list, _headset ? null : columns.Layout, out var scroll);
        FollowRows(header, scroll);
        _systemsScroll = scroll;
        Restore(scroll, offset);
    }

    /// <summary>A list of rows that scroll, with the headset's 25-row pages and the desktop's virtualised list.</summary>
    private Control Body<T>(IReadOnlyList<T> sorted, Func<T, Border> row, Action redraw, bool hidden = false)
    {
        _page = FactionModel.ClampPage(_page, sorted.Count);

        if (_headset)
        {
            var shown = sorted.Skip(_page * FactionModel.PageSize).Take(FactionModel.PageSize).ToList();
            var stack = new StackPanel { Spacing = Gaps.Tile, Margin = new Thickness(0, 0, 14, 0), IsVisible = !hidden };

            foreach (var item in shown)
            {
                stack.Children.Add(row(item));
            }

            if (sorted.Count > FactionModel.PageSize)
            {
                stack.Children.Add(Pager(sorted.Count, redraw));
            }

            RowsDrawn = shown.Count;
            return stack;
        }

        RowsDrawn = sorted.Count;

        return new ItemsControl
        {
            ItemsPanel = new FuncTemplate<Avalonia.Controls.Panel?>(() => new VirtualizingStackPanel { }),
            ItemTemplate = new FuncDataTemplate<T>((item, _) => item is null ? null : Spaced(row(item))),
            ItemsSource = sorted,
        };
    }

    private IReadOnlyList<TableField<FactionConflict>> ConflictFields()
    {
        var heads = FactionConflicts.Heads;

        return
        [
            StarSystemPage.Headed(heads[0], Fill("System", 160, 120), conflict => CopyGlyph.LinkBeside(SystemName(conflict.System), conflict.System, Copy))
                with { Filters = [ValueLists.One<FactionConflict>("System", conflict => conflict.System, "No system")] },
            StarSystemPage.Headed(heads[1], Fixed("Conflict", 150, 100), KindCell)
                with
                {
                    Filters =
                    [
                        ValueLists.One<FactionConflict>("Type", conflict => conflict.Type, "No type"),
                        ValueLists.One<FactionConflict>("Status", conflict => conflict.Status, "No status"),
                    ],
                },
            StarSystemPage.Headed(heads[2], Fill("Opponent", 220, 170), Opponent)
                with { Filters = [ValueLists.Many<FactionConflict>("Opponent", OpponentNames, "Opponent not known")] },
            StarSystemPage.Headed(heads[3], Fixed("Days won", 120, 84), DaysCell, right: true),
            StarSystemPage.Headed(heads[4], Fill("At stake", 220, 170), StakeCell)
                with
                {
                    Filters =
                    [
                        ValueLists.One<FactionConflict>("Ours", conflict => conflict.Visited ? conflict.OurStake : null, "No stake read"),
                        ValueLists.One<FactionConflict>("Theirs", conflict => conflict.Visited ? conflict.TheirStake : null, "No stake read"),
                    ],
                },
        ];
    }

    private static IEnumerable<string?> OpponentNames(FactionConflict conflict) => conflict.Kind switch
    {
        OpponentKind.Named => [conflict.Opponent],
        OpponentKind.OneOf => conflict.Candidates,
        _ => [],
    };

    private void DrawConflicts()
    {
        if (_report is null)
        {
            return;
        }

        var columns = ConflictColumns;
        columns.Refresh();

        var top = ArrangerAbove(columns, _conflicts, line: true);
        var header = HeadOf(columns);

        if (_conflicts.Count == 0)
        {
            RowsDrawn = 0;
            top.Children.Add(header);
            var none = CreditsKit.Line(FactionConflicts.NoneText, Fonts.ProseFamily, TypeScale.ControlLarge, ThemeManager.GreyKey);
            none.Margin = new Thickness(0, 12, 0, 0);
            top.Children.Add(none);
            _conflictsTable.Child = top;
            return;
        }

        _drawnHere = _searches.GameState?.Invoke()?.Location.StarSystem;
        if (ColumnFilter.Empty(columns, _conflicts) is { } hidden)
        {
            RowsDrawn = 0;
            top.Children.Add(header);
            top.Children.Add(hidden);
            _conflictsTable.Child = top;
            return;
        }

        var list = Body(columns.Shown(_conflicts), conflict => ConflictView(conflict, columns), DrawConflicts, ColumnFilter.HidesRows(columns));
        var offset = _keepScroll ? _conflictsScroll?.Offset : null;
        _conflictsTable.Child = LoadoutPages.HeaderAndList(top, header, list, _headset ? null : columns.Layout, out var scroll);
        FollowRows(header, scroll);
        _conflictsScroll = scroll;
        Restore(scroll, offset);
    }

    private static Border ConflictView(FactionConflict conflict, ArrangedTable<FactionConflict> columns)
    {
        var border = new Border { MinHeight = 50, Padding = new Thickness(12, 4), Child = columns.Row(conflict) };
        ThemeInk.Bind(border, Border.BackgroundProperty, ThemeManager.TileKey);

        return border;
    }

    private static StackPanel KindCell(FactionConflict conflict) =>
        new()
        {
            Children =
            {
                CreditsKit.Line(conflict.Type, Fonts.ProseFamily, TypeScale.ControlLarge, ThemeManager.WhiteKey),
                Chrome(conflict.Status, TypeScale.MetaSmall, FontWeight.Medium, ThemeManager.GreyKey),
            },
        };

    private Control DaysCell(FactionConflict conflict)
    {
        Control days = conflict.Visited
            ? CreditsKit.Mono(conflict.DaysText, TypeScale.ControlLarge, ThemeManager.WhiteKey, right: true)
            : CreditsKit.Mono(FactionModel.Dash, TypeScale.ControlLarge, ThemeManager.GreyKey, right: true);

        if (conflict.Visited && UnchangedSinceTick(conflict.System))
        {
            var note = RoutingKit.Ink(FactionModel.UnchangedSinceTick, TypeScale.Meta, ThemeManager.GreyKey, wrap: true);
            note.MaxWidth = double.PositiveInfinity;
            note.HorizontalAlignment = HorizontalAlignment.Right;
            note.TextAlignment = TextAlignment.Right;
            days = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, Children = { days, note } };
        }

        return days;
    }

    private static Control StakeCell(FactionConflict conflict) =>
        conflict.Visited
            ? new StackPanel
            {
                Children = { Stake("Ours: ", conflict.OurStake), Stake("Theirs: ", conflict.TheirStake) },
            }
            : CreditsKit.Line(FactionModel.Dash, Fonts.ProseFamily, TypeScale.Small, ThemeManager.GreyKey);

    private static Control Opponent(FactionConflict conflict)
    {
        switch (conflict.Kind)
        {
            case OpponentKind.Named:
                return RoutingKit.Ink(conflict.Opponent, TypeScale.ControlLarge, ThemeManager.WhiteKey, wrap: true);

            case OpponentKind.OneOf:
                var names = RoutingKit.Ink(conflict.CandidateText, TypeScale.Meta, ThemeManager.GreyKey, wrap: true);
                names.MaxWidth = double.PositiveInfinity;

                return new StackPanel
                {
                    Children = { CreditsKit.Line(conflict.OneOfText, Fonts.ProseFamily, TypeScale.ControlLarge, ThemeManager.WhiteKey), names },
                };

            default:
                return CreditsKit.Line(FactionConflicts.OpponentUnknownText, Fonts.ProseFamily, TypeScale.ControlLarge, ThemeManager.GreyKey);
        }
    }

    private static TextBlock Stake(string side, string name)
    {
        var line = new TextBlock { FontFamily = new FontFamily(Fonts.ProseFamily), FontSize = TypeScale.Small, TextWrapping = TextWrapping.Wrap };
        var label = new Run(side);
        var held = new Run(name.Length == 0 ? FactionModel.Dash : name);
        label.Bind(TextElement.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(ThemeManager.GreyKey));
        held.Bind(TextElement.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(name.Length == 0 ? ThemeManager.GreyKey : ThemeManager.WhiteKey));
        line.Inlines ??= [];
        line.Inlines.Add(label);
        line.Inlines.Add(held);

        return line;
    }

    private IReadOnlyList<TableField<FactionStation>> AssetFields()
    {
        var heads = FactionAssets.Heads;

        return
        [
            StarSystemPage.Headed(
                heads[0],
                Fill("Station", 240, 170),
                station =>
                {
                    var name = CreditsKit.Line(station.Name, Fonts.ChromeFamily, TypeScale.ControlLarge, ThemeManager.WhiteKey);
                    name.FontWeight = FontWeight.Medium;

                    return CopyGlyph.Beside(name, station.Name, Copy);
                }),
            StarSystemPage.Headed(heads[1], Fill("System", 160, 120), station => CopyGlyph.LinkBeside(SystemName(station.System), station.System, Copy))
                with { Filters = [ValueLists.One<FactionStation>("System", station => station.System, "No system")] },
            StarSystemPage.Headed(
                heads[2],
                Fixed("Type", 190, 140),
                station =>
                {
                    var type = FactionAssets.TypeLabel(station);

                    return CreditsKit.Line(type ?? FactionModel.Dash, Fonts.ProseFamily, TypeScale.ControlLarge, type is null ? ThemeManager.GreyKey : ThemeManager.WhiteKey);
                })
                with { Filters = [ValueLists.One<FactionStation>("Type", FactionAssets.TypeLabel, "No type")] },
            StarSystemPage.Headed(
                heads[3],
                Fixed("Pad", 80, 56),
                station =>
                {
                    var pad = FactionAssets.PadLetter(station.LargestPad);

                    return CreditsKit.Mono(pad ?? FactionModel.Dash, TypeScale.ControlLarge, pad is null ? ThemeManager.GreyKey : ThemeManager.WhiteKey, right: true);
                },
                right: true)
                with { Filters = [ValueLists.One<FactionStation>("Pad", station => FactionAssets.PadLetter(station.LargestPad), "No pad")] },
            StarSystemPage.Headed(
                heads[4],
                Fixed("Distance", 150, 104),
                station => station.DistanceToArrival is { } distance
                    ? CreditsKit.Mono(FactionAssets.Distance(distance), TypeScale.ControlLarge, ThemeManager.AKey, right: true)
                    : CreditsKit.Mono(FactionModel.Dash, TypeScale.ControlLarge, ThemeManager.GreyKey, right: true),
                right: true),
        ];
    }

    private void DrawAssets()
    {
        if (_report is null)
        {
            return;
        }

        var options = FactionAssets.Options(_stations);

        if (options.All(option => option.Id != _kind))
        {
            _kind = FactionAssets.AllId;
        }

        var current = options.First(option => option.Id == _kind);
        _assetKind.Show(
            [.. options.Select(option => new InlinePickerOption(option.Id, option.Label, ThemeManager.WhiteKey, option.Count.ToString("N0", CultureInfo.InvariantCulture)))],
            _kind,
            current.Label,
            ThemeManager.WhiteKey,
            " · " + current.Count.ToString("N0", CultureInfo.InvariantCulture));

        var columns = AssetColumns;
        columns.Refresh();

        var matches = FactionAssets.Matching(_stations, _assetFind.Text, _kind);
        columns.Observe(_stations);
        var sorted = columns.Shown(matches);
        _drawnHere = _searches.GameState?.Invoke()?.Location.StarSystem;
        _assetCount.Text = FactionAssets.CountText(sorted.Count, _stations.Count);

        _assetsTile.Child = Tiles(columns, _stations);
        var top = ArrangerAbove(columns, _stations);
        var header = HeadOf(columns);

        if (sorted.Count == 0)
        {
            RowsDrawn = 0;
            top.Children.Add(header);

            if (ColumnFilter.Empty(columns, _stations) is { } hidden)
            {
                top.Children.Add(hidden);
                _assetsTable.Child = top;
                return;
            }

            var none = CreditsKit.Line(FactionAssets.NoMatchText, Fonts.ProseFamily, TypeScale.ControlLarge, ThemeManager.GreyKey);
            none.Margin = new Thickness(0, 12, 0, 0);
            top.Children.Add(none);
            _assetsTable.Child = top;
            return;
        }

        var list = Body(sorted, station => AssetView(station, columns), DrawAssets, ColumnFilter.HidesRows(columns));
        var offset = _keepScroll ? _assetsScroll?.Offset : null;
        _assetsTable.Child = LoadoutPages.HeaderAndList(top, header, list, _headset ? null : columns.Layout, out var scroll);
        FollowRows(header, scroll);
        _assetsScroll = scroll;
        Restore(scroll, offset);
    }

    private static Border AssetView(FactionStation station, ArrangedTable<FactionStation> columns)
    {
        var border = new Border { MinHeight = 50, Padding = new Thickness(12, 0), Child = columns.Row(station) };
        ThemeInk.Bind(border, Border.BackgroundProperty, ThemeManager.TileKey);

        return border;
    }

    private static Border Spaced(Control row) => new() { Margin = new Thickness(0, 0, 0, Gaps.Tile), Child = row };

    private Grid Pager(int total, Action redraw)
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
            redraw();
        };
        next.Click += (_, _) =>
        {
            _page++;
            redraw();
        };

        var tiles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tile, Children = { previous, next } };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 8, 0, 0) };
        Grid.SetColumn(tiles, 1);
        grid.Children.Add(readout);
        grid.Children.Add(tiles);

        return grid;
    }

    private static Border RowView(FactionRow row, ArrangedTable<FactionRow> columns)
    {
        var border = new Border { MinHeight = 50, Padding = new Thickness(12, 0), Child = columns.Row(row) };
        ThemeInk.Bind(border, Border.BackgroundProperty, ThemeManager.TileKey);

        return border;
    }

    private static TextBlock States(IReadOnlyList<string> states, string ink)
    {
        var block = RoutingKit.Ink(states.Count == 0 ? FactionModel.Dash : string.Join(", ", states), TypeScale.ControlLarge, states.Count == 0 ? ThemeManager.GreyKey : ink, wrap: true);
        block.MaxWidth = double.PositiveInfinity;

        return block;
    }

    private static StackPanel Updated(FactionRow row, DateTimeOffset now, bool old, bool unchanged)
    {
        var key = row.OwnReading ? ThemeManager.CyanKey : old ? ThemeManager.WarnKey : ThemeManager.GreyKey;
        var age = CreditsKit.Line(FactionModel.Age(row, now, unchanged), Fonts.ProseFamily, TypeScale.Tip, key);
        age.HorizontalAlignment = HorizontalAlignment.Right;
        age.TextAlignment = TextAlignment.Right;

        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, Children = { age } };

        if (old)
        {
            var label = Chrome("OLD REPORT", 11, FontWeight.Medium, ThemeManager.WarnKey);
            label.HorizontalAlignment = HorizontalAlignment.Right;
            stack.Children.Add(label);
        }

        return stack;
    }

    private void DrawStamp()
    {
        _stamp.Inlines ??= [];
        _stamp.Inlines.Clear();

        var word = _view switch
        {
            FactionView.Answered => "ANSWERED",
            FactionView.Loading => "SEARCHING",
            FactionView.Failed => "FAILED",
            _ => null,
        };

        if (word is null)
        {
            return;
        }

        var words = new Run($"GALAXY SEARCH · {word} ") { FontFamily = new FontFamily(Fonts.ChromeFamily), FontSize = TypeScale.Meta, FontWeight = FontWeight.Medium };
        words.Bind(TextElement.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(ThemeManager.GreyKey));
        _stamp.Inlines.Add(words);

        if (_view is FactionView.Answered or FactionView.Failed)
        {
            var at = _view == FactionView.Failed ? _failedAt : (_report?.FetchedAt ?? _now()).ToLocalTime();
            var time = new Run(at.ToString("HH:mm", CultureInfo.InvariantCulture)) { FontFamily = new FontFamily(Fonts.MonoFamily), FontSize = TypeScale.Meta };
            time.Bind(TextElement.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(ThemeManager.CyanKey));
            _stamp.Inlines.Add(time);
        }
    }

    private static TextBlock Chrome(string text, double size, FontWeight weight, string key)
    {
        var block = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = size,
            FontWeight = weight,
            LetterSpacing = size * Fonts.ChromeTracking,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ThemeInk.Bind(block, TextBlock.ForegroundProperty, key);

        return block;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (EmptyLike)
        {
            Dispatcher.UIThread.Post(() => _field.Focus());
        }
    }
}

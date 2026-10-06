using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Automation;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Reactive;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Checklists;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>The checklist, as a tab of the panel (Phase 25, "The checklist leaves its window").</summary>
public sealed class ChecklistPage : UserControl, IFilterablePage, IPageSummary
{
    /// <summary>The filter entry that means no filter.</summary>
    public const string Everything = "everything";

    /// <summary>The crumb the suggestions page is pushed as.</summary>
    public const string SuggestionsKey = "checklist.suggestions";

    /// <summary>The crumb the All lists page is pushed as.</summary>
    public const string AllKey = "checklist.all";

    /// <summary>The start of a one-list crumb's key; the list's id follows it.</summary>
    public const string ListKeyPrefix = "checklist.list:";

    /// <summary>How many lists the mini panel shows before counting the rest.</summary>
    public const int MiniRows = 4;

    /// <summary>The All lists level: every line, the filter stepper, no Add.</summary>
    public static NavCrumb AllLists => new(AllKey, "All lists") { Whole = true };

    /// <summary>The level that shows one list.</summary>
    public static NavCrumb ListCrumb(ChecklistList list)
    {
        ArgumentNullException.ThrowIfNull(list);

        return new NavCrumb(ListKeyPrefix + list.Id, list.Name) { Whole = true };
    }

    /// <summary>Which of the three checklist levels a page draws.</summary>
    private enum Level
    {
        Lists,
        One,
        All,
    }

    private readonly Level _level;

    /// <summary>The list a <see cref="Level.One"/> page draws, by <see cref="ChecklistList.Id"/>.</summary>
    private readonly string? _listId;

    /// <summary>The list's name as the crumb was pushed, for when the list has gone.</summary>
    private readonly string _listWord = string.Empty;

    /// <summary>The title, kind line and counts over a list page.</summary>
    private readonly StackPanel _heading = new();

    /// <summary>The bar of controls; page chrome, so its visibility is never assigned here.</summary>
    private readonly WrapPanel _bar;

    /// <summary>On a ship list: only lines an engineer in this system can do.</summary>
    private readonly CheckBox _hereOnly;

    private readonly Button _add;

    private readonly ChecklistService _checklists;
    private readonly PanelNavigator _nav;
    private readonly PanelPrompts _prompts;

    /// <summary>The Commander's long arcs (Phase 34, "The checklist points at the arc").</summary>
    private readonly D47.Core.Goals.GoalBook? _goals;

    private readonly Action? _backfill;

    private readonly Func<DateTimeOffset> _now;

    /// <summary>The goals, rebuilt with the page because a goal's figure moves with the journal.</summary>
    private readonly StackPanel _arcs = new() { Spacing = 2 };

    /// <summary>The goals mode: the height below the bar, scrolling only when the goals are taller.</summary>
    private readonly ScrollViewer _band = new()
    {
        Name = "GoalsView",
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        IsVisible = false,
    };

    /// <summary>Switches the page between the list and the goals (#646).</summary>
    private readonly CheckBox _arcsToggle;

    private readonly StackPanel _list = new() { Spacing = 2 };

    private readonly ScrollViewer _listView = new()
    {
        Name = "ChecklistView",
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
    };

    /// <summary>Suggestions and Add, hidden in goals mode.</summary>
    private readonly StackPanel _right = new() { Orientation = Orientation.Horizontal, Spacing = Gaps.Tile };
    private readonly Notice _problems = new() { IsVisible = false };

    private readonly Stepper _scopeCombo = new()
    {
        Name = "ChecklistScope",
        MinHeight = TouchTarget,
    };

    /// <summary>The filter key behind each item in <see cref="_scopeCombo"/>, by index.</summary>
    private List<string> _scopeKeys = [Everything];

    /// <summary>Whether <see cref="_scopeCombo"/>'s selection is being written rather than chosen.</summary>
    private bool _settlingScope;

    /// <summary>
    /// Include Partial Grades (change-requests.md 35): also show work an engineer here can start and
    /// somebody else has to finish. A left cluster, so the checkbox leads its label.
    /// </summary>
    private readonly CheckBox _partial;

    /// <summary>
    /// The bar's controls, held so the one above can be taken out of the tree entirely rather than
    /// hidden.
    /// </summary>
    private readonly WrapPanel _controls = new() { ItemSpacing = Gaps.Tile, LineSpacing = Gaps.Tile };

    private readonly Button _suggestions = new()
    {
        VerticalAlignment = VerticalAlignment.Top,
        IsVisible = false,
    };

    /// <summary>Bulk-removes every Done line on the whole checklist (#259).</summary>
    private readonly Button _deleteCompleted = new()
    {
        Content = "Delete completed",
        VerticalAlignment = VerticalAlignment.Top,
        Classes = { "destructive" },
    };

    private IDisposable? _sized;
    private bool _mini;

    /// <summary>The filter and the search text.</summary>
    private string Chosen => _checklists.Filter;

    private string Query => _checklists.Query;

    /// <summary>Which line is selected.</summary>
    private ChecklistItemId? Selected => _checklists.Selected;

    /// <summary>Whether the page is in goals mode.</summary>
    private bool _showArcs;

    /// <summary>The floor under anything on this page a ray has to hit, in pixels.</summary>
    private const double TouchTarget = 30;

    /// <summary>Which arc is open, by key.</summary>
    private string? _openArc;

    /// <param name="crumb">The level this page draws: the Checklist root, <see cref="AllLists"/>, or a
    /// <see cref="ListCrumb"/>.</param>
    public ChecklistPage(
        ChecklistService checklists,
        PanelNavigator nav,
        PanelPrompts prompts,
        D47.Core.Goals.GoalBook? goals = null,
        Action? backfill = null,
        Func<DateTimeOffset>? now = null,
        NavCrumb? crumb = null)
    {
        _checklists = checklists;
        _nav = nav;
        _prompts = prompts;
        _goals = goals;
        _backfill = backfill;
        _now = now ?? (() => DateTimeOffset.Now);

        if (crumb?.Key == AllKey)
        {
            _level = Level.All;
        }
        else if (crumb?.Key is { } key && key.StartsWith(ListKeyPrefix, StringComparison.Ordinal))
        {
            _level = Level.One;
            _listId = key[ListKeyPrefix.Length..];
            _listWord = crumb.Word;
        }

        _arcsToggle = LabeledCheckBox.Caps(string.Empty);
        _arcsToggle.IsVisible = false;

        _partial = LabeledCheckBox.Caps("Include Partial Grades");
        _hereOnly = LabeledCheckBox.Caps("Only what engineers here do");
        _hereOnly.MinHeight = TypeScale.MinimumTarget;

        AutomationProperties.SetName(_scopeCombo, "Checklist scope");
        _scopeCombo.SelectionChanged += (_, _) => OnScopeChanged();

        // Through the service, like the filter beside it: shared across surfaces and remembered.
        _partial.IsCheckedChanged += (_, _) => _checklists.IncludePartial(_partial.IsChecked == true);

        _hereOnly.IsCheckedChanged += (_, _) => Rebuild();

        _suggestions.Click += (_, _) =>
            _nav.Drill(new NavCrumb(SuggestionsKey, "Suggestions"));

        // The checkbox owns the flag rather than mirroring it: nothing else writes _showArcs, so RebuildArcs
        // never assigns IsChecked back and there is no loop to break.
        _arcsToggle.IsCheckedChanged += (_, _) =>
        {
            _showArcs = _arcsToggle.IsChecked == true;
            Rebuild();
        };

        _add = D47.App.Controls.Glyphs.Quiet(new Button(), "ADD A LINE", "Add a line");
        _add.VerticalAlignment = VerticalAlignment.Top;

        _add.Click += (_, _) => AddLine();

        _deleteCompleted.Click += (_, _) => DeleteCompletedItems();

        // A WrapPanel, because a DockPanel with a filling StackPanel does not shrink and the two groups drew
        // over each other below about 700 pixels.
        var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 10), ItemSpacing = 8, LineSpacing = 8 }
            .AsChrome();

        switch (_level)
        {
            case Level.Lists:
                _controls.Children.Add(_arcsToggle);
                _right.Children.Add(_suggestions);
                break;

            case Level.One:
                _controls.Children.Add(_hereOnly);
                _right.Children.Add(_add);
                _right.Children.Add(_deleteCompleted);
                break;

            default:
                _controls.Children.Add(_scopeCombo);
                _right.Children.Add(_deleteCompleted);
                break;
        }

        // The filter group first, so a bar that wraps drops the buttons to the second row rather than the
        // thing the page is filtered by.
        bar.Children.Add(_controls);
        bar.Children.Add(_right);

        _bar = bar;

        var root = new DockPanel { Margin = new Thickness(14) };

        DockPanel.SetDock(_heading, Dock.Top);
        DockPanel.SetDock(bar, Dock.Top);
        DockPanel.SetDock(_problems, Dock.Top);

        _problems.Margin = new Thickness(0, 0, 0, 10);

        _band.Content = _arcs;
        _listView.Content = _list;

        root.Children.Add(_heading);
        root.Children.Add(bar);
        root.Children.Add(_problems);

        // One of the two fills the height below the bar; the other is hidden.
        root.Children.Add(new Grid { Children = { _listView, _band } });

        Content = root;

        Rebuild();
    }

    /// <summary>
    /// Starts listening, and catches up on anything missed while this page was not on screen
    /// (remediation.md 11, item 3).
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _sized = this.GetSelfAndVisualAncestors()
            .OfType<PanelView>()
            .FirstOrDefault()
            ?.GetObservable(PanelView.ModeProperty)
            .Subscribe(new AnonymousObserver<PanelMode>(OnSurface));

        _checklists.List.Changed += OnChanged;
        _checklists.Proposals.Changed += OnChanged;

        // The filter is shared between the surfaces, so the one that did not change it still has to redraw —
        // which is the whole of what the report was about.
        _checklists.FilterChanged += OnChanged;

        // The engineer filter answers from where the ship is, and none of the three above moves when it does
        // (#93).
        _checklists.HereChanged += OnChanged;

        // A pin set or cleared on the Engineers tab redraws this one too, wherever the Commander toggled it
        // (#113).
        _checklists.PinnedChanged += OnChanged;

        if (_goals is not null)
        {
            _goals.Store.Changed += OnChanged;
        }

        // The world moved while this was off screen, which is the ordinary case for a page that is reparented
        // by a reflow.
        Rebuild();
    }

    /// <summary>The surface's one search box: every list from the root, every line on All lists; a list
    /// page has none.</summary>
    public bool Filters => _level != Level.One;

    public string FilterPlaceholder => _level == Level.Lists ? "Search every list" : "Search this page";

    public double? FilterWidth => _level == Level.Lists ? 360 : null;

    public void Filter(string? query)
    {
        // Through the service, which raises the change back at every surface — this page included, so there
        // is no rebuild here.
        _checklists.Search(query);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _sized?.Dispose();
        _sized = null;

        _checklists.List.Changed -= OnChanged;
        _checklists.Proposals.Changed -= OnChanged;
        _checklists.FilterChanged -= OnChanged;
        _checklists.HereChanged -= OnChanged;
        _checklists.PinnedChanged -= OnChanged;

        if (_goals is not null)
        {
            _goals.Store.Changed -= OnChanged;
        }
    }

    /// <summary>How many lines the filter or the search left, while either is narrowing the list.</summary>
    public string Summary { get; private set; } = string.Empty;

    public event EventHandler? SummaryChanged;

    private void Summarise(string summary)
    {
        if (summary == Summary)
        {
            return;
        }

        Summary = summary;
        SummaryChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The surface went mini, or came back.</summary>
    private void OnSurface(PanelMode mode)
    {
        var mini = mode == PanelMode.Mini;

        if (mini != _mini)
        {
            _mini = mini;
            Rebuild();
        }
    }

    /// <summary>The suggestions page, built for the crumb the button above pushes (Phase 25).</summary>
    public Control BuildSuggestions()
    {
        var page = new StackPanel { Spacing = 2, Margin = new Thickness(14) };

        void Fill()
        {
            page.Children.Clear();

            var title = TitleText.Block(TitleText.Build(
                "Suggestions",
                TypeScale.Heading,
                TitleRank.Screen));

            title.Margin = new Thickness(0, 0, 0, _mini ? 4 : 10);
            page.Children.Add(title);

            var pending = _checklists.Proposals.PendingFor(_checklists.Document.CommanderFid);

            if (pending.Count == 0)
            {
                page.Children.Add(Muted(
                    "Nothing waiting. D47 puts proposals here rather than on your list, and they stay "
                    + "here until you accept them."));

                return;
            }

            foreach (var proposal in pending)
            {
                page.Children.Add(Proposal(proposal, Fill));
            }
        }

        Fill();

        var scroller = new ScrollViewer
        {
            Content = page,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        // The store raises this from whichever thread wrote, so the hop is not optional.
        void Follow() => Dispatcher.UIThread.Post(Fill);

        // Accepting from this page refreshed it and nothing else did, which is why the gap was
        // invisible until a spoken yes accepted the same proposal from somewhere else: the card stayed on
        // screen after the line was already on the list (remediation.md 11, item 3).
        scroller.AttachedToVisualTree += (_, _) =>
        {
            _checklists.Proposals.Changed += Follow;
            Fill();
        };

        scroller.DetachedFromVisualTree += (_, _) => _checklists.Proposals.Changed -= Follow;

        return scroller;
    }

    /// <summary>
    /// The store raises this from whichever thread wrote — the tick loop, most often — and every
    /// control here belongs to the UI thread, so the hop is not optional.
    /// </summary>
    private void OnChanged() => Dispatcher.UIThread.Post(Rebuild);

    private void Rebuild()
    {
        _list.Children.Clear();
        _heading.Children.Clear();

        var document = _checklists.Document;
        var pending = _checklists.Proposals.PendingFor(document.CommanderFid);

        // **Items, not proposals** (remediation.md 15, item 12).
        var waiting = pending.Sum(proposal => Math.Max(1, proposal.Items.Count));

        _suggestions.IsVisible = pending.Count > 0;
        _suggestions.Content = $"Suggestions ({waiting.ToString(CultureInfo.InvariantCulture)})";

        if (_level == Level.All)
        {
            SettleScope();
        }

        var goalsMode = _level == Level.Lists && _showArcs && _goals is not null;

        _listView.IsVisible = !goalsMode;
        // The bar itself is chrome and is never assigned, so an output-only surface's style can hide it.
        _controls.IsVisible = !(_mini && _level == Level.Lists);
        _right.IsVisible = _controls.IsVisible;
        _bar.Margin = new Thickness(0, 0, 0, _controls.IsVisible ? 10 : 0);

        var list = _level == Level.One ? _checklists.Lists().FirstOrDefault(found => found.Id == _listId) : null;

        _hereNow = HereActive(list);

        // The partial-grades box goes with whichever engineers-here filter this level has.
        var offerPartial = _hereNow
                           && (_checklists.IncludePartialGrades || _checklists.HasPartialWorkHere());

        _partial.IsChecked = _checklists.IncludePartialGrades;

        if (offerPartial && !_controls.Children.Contains(_partial))
        {
            _controls.Children.Insert(1, _partial);
        }
        else if (!offerPartial)
        {
            _controls.Children.Remove(_partial);
        }

        if (_level == Level.Lists)
        {
            RebuildArcs();
        }

        // The list keeps its filter, query and selection in the service, so unticking redraws it as it was.
        if (goalsMode)
        {
            Summarise(string.Empty);
            _problems.IsVisible = false;
            return;
        }

        switch (_level)
        {
            case Level.Lists when Query.Length > 0:
                DrawLines(_checklists.Arranged().Where(Matches).ToList());
                break;

            case Level.Lists:
                Summarise(string.Empty);
                DrawLists();
                break;

            case Level.One:
                Summarise(string.Empty);
                DrawOne(list);
                break;

            default:
                DrawAllHeading();
                _deleteCompleted.IsEnabled = _checklists.HasCompleted;
                DrawLines(_checklists.Arranged().Where(Matches).ToList());
                break;
        }

        ShowProblems();
    }

    /// <summary>Whether the lines drawn are filtered to what an engineer here can do.</summary>
    private bool _hereNow;

    /// <summary>Whether this level is filtered to what an engineer here can do.</summary>
    private bool HereActive(ChecklistList? list) => _level switch
    {
        Level.All => Chosen == ChecklistService.HereKey,
        Level.One => list?.Scope.Group == ChecklistGroup.Ship && _hereOnly.IsChecked == true,
        _ => false,
    };

    /// <summary>Points the filter stepper at the service's filter.</summary>
    private void SettleScope()
    {
        // Flat: FilterAxes' headings group the choices by the question they answer, but the stepper
        // draws no heading between its own items.
        var keys = new List<string> { Everything };
        var words = new List<string> { "Everything" };

        foreach (var filter in _checklists.FilterAxes())
        {
            keys.Add(filter.Key);
            words.Add(filter.Word);
        }

        // A chosen filter can drop out of FilterAxes() while it is still selected — every Done line
        // under it deleted, say — and the stepper still has to show something for it rather than
        // going blank.
        if (!keys.Contains(Chosen))
        {
            keys.Add(Chosen);
            words.Add(Chosen);
        }

        _scopeKeys = keys;

        // Guarded, or setting the selection below would read as a choice and call back into Choose.
        _settlingScope = true;

        try
        {
            if (!_scopeCombo.ItemsSource.SequenceEqual(words))
            {
                _scopeCombo.ItemsSource = words;
            }

            _scopeCombo.SelectedIndex = keys.IndexOf(Chosen);
        }
        finally
        {
            _settlingScope = false;
        }
    }

    /// <summary>Lines in the order given, open first and a Done head over the rest, each led by its list's name.</summary>
    private void DrawLines(IReadOnlyList<ChecklistItem> live)
    {
        var open = live.Where(item => !item.IsComplete).ToList();
        var done = live.Where(item => item.IsComplete).ToList();

        if (live.Count == 0)
        {
            _list.Children.Add(Muted(EmptyMessage()));

            // Mini has no search box to type the query away (#94): the button that clears the filter,
            // not just the sentence that names it.
            if (Query.Length > 0 || Chosen != Everything)
            {
                _list.Children.Add(ClearFilterButton());
            }
        }

        var summary = string.Empty;

        if (open.Count > 0 && (Chosen != Everything || Query.Length > 0))
        {
            var ships = open
                .Where(item => item.Scope.Group == ChecklistGroup.Ship)
                .Select(item => item.Scope.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var lines = open.Count == 1 ? "1 line" : $"{open.Count.ToString(CultureInfo.InvariantCulture)} lines";

            summary = ships > 1
                ? $"{lines}, across {ships.ToString(CultureInfo.InvariantCulture)} ships."
                : $"{lines}.";
        }

        Summarise(summary);

        var names = _checklists.Lists().ToDictionary(list => list.Id, list => list.Name, StringComparer.Ordinal);

        string? Lead(ChecklistItem item) =>
            ChecklistLists.IdOf(item) is { } id && names.TryGetValue(id, out var name)
                ? name
                : null;

        foreach (var item in open)
        {
            _list.Children.Add(Line(item, Lead(item)));
        }

        if (done.Count > 0)
        {
            // Below the line and counted, never removed.
            _list.Children.Add(ListRow.Head($"Done ({done.Count})"));

            foreach (var item in done)
            {
                _list.Children.Add(Line(item, Lead(item)));
            }
        }
    }

    /// <summary>The list of lists: a row per list under its group's head, and All lists at the end.</summary>
    private void DrawLists()
    {
        var lists = _checklists.Lists();

        if (lists.Count == 0)
        {
            _list.Children.Add(Muted(EmptyMessage()));
            return;
        }

        if (_mini)
        {
            foreach (var list in lists.Take(MiniRows))
            {
                _list.Children.Add(CompactRow(list));
            }

            if (lists.Count > MiniRows)
            {
                var rest = lists.Count - MiniRows;

                var more = Muted(rest == 1 ? "1 more list" : $"{rest.ToString(CultureInfo.InvariantCulture)} more lists");
                more.Name = "ChecklistMoreLists";
                more.FontSize = TypeScale.Caption;
                more.Margin = new Thickness(0, 4, 0, 0);

                _list.Children.Add(more);
            }

            return;
        }

        ChecklistListGroup? group = null;

        foreach (var list in lists)
        {
            if (list.Group != group)
            {
                group = list.Group;
                _list.Children.Add(ListRow.Head(GroupWord(list.Group)));
            }

            _list.Children.Add(ListRowButton(list));
        }

        var all = _checklists.Arranged();
        var allOpen = all.Count(item => !item.IsComplete);

        var hint = Muted("Every line in one list, in your order");
        hint.FontSize = TypeScale.Tip;
        hint.TextWrapping = TextWrapping.NoWrap;
        hint.TextTrimming = TextTrimming.CharacterEllipsis;

        var total = Pressable(
            $"All lists, {allOpen.ToString(CultureInfo.InvariantCulture)} open, "
            + $"{(all.Count - allOpen).ToString(CultureInfo.InvariantCulture)} done",
            Columns(
                Named("All lists", null, sentence: false),
                hint,
                Counts(allOpen, all.Count - allOpen)),
            () => _nav.Drill(AllLists));

        total.Name = "ChecklistAllLists";
        total.Margin = new Thickness(0, 10, 0, 0);

        _list.Children.Add(total);
    }

    /// <summary>One row of the list of lists: name and kind, the next open line, the counts.</summary>
    private Button ListRowButton(ChecklistList list)
    {
        var quiet = list.Group == ChecklistListGroup.NothingOpen;

        var name = ListRow.Name(new TextBlock { Text = list.Name, TextTrimming = TextTrimming.CharacterEllipsis });

        if (quiet)
        {
            Themed(name, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        }
        else if (list.IsHere)
        {
            Themed(name, TextBlock.ForegroundProperty, ThemeManager.CyanKey);
        }

        var who = new StackPanel { Spacing = 1, Children = { name } };

        if (list.Kind.Length > 0)
        {
            who.Children.Add(ListRow.Sub(new TextBlock { Text = list.Kind, TextTrimming = TextTrimming.CharacterEllipsis }));
        }

        var next = new TextBlock
        {
            Text = list.Next ?? "Every line done",
            FontSize = TypeScale.Tip,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        Themed(next, TextBlock.ForegroundProperty, quiet ? ThemeManager.GreyKey : ThemeManager.WhiteKey);

        var row = Pressable(
            Spoken(list),
            Columns(who, next, quiet ? DoneMark(list.Done) : Counts(list.Open, list.Done)),
            () => _nav.Drill(ListCrumb(list)));

        if (quiet)
        {
            Themed(row, TemplatedControl.BackgroundProperty, ThemeManager.SlabKey);
        }

        return row;
    }

    /// <summary>
    /// The mini panel's row: name, next line, open count. A pressed border rather than a button, because an
    /// output-only mini surface hides every button.
    /// </summary>
    private Border CompactRow(ChecklistList list)
    {
        var name = ListRow.Name(new TextBlock
        {
            Text = list.Name,
            FontSize = TypeScale.Small,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        });

        if (list.IsHere)
        {
            Themed(name, TextBlock.ForegroundProperty, ThemeManager.CyanKey);
        }

        var next = new TextBlock
        {
            Text = list.Next ?? "Every line done",
            FontSize = TypeScale.Caption,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Themed(next, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var count = new TextBlock
        {
            Text = list.Open.ToString(CultureInfo.InvariantCulture),
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Caption,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Themed(count, TextBlock.ForegroundProperty, ThemeManager.AKey);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("130,*,54"), ColumnSpacing = 10 };

        Grid.SetColumn(next, 1);
        Grid.SetColumn(count, 2);
        grid.Children.Add(name);
        grid.Children.Add(next);
        grid.Children.Add(count);

        var row = ListRow.Dress(new Border { Child = grid, MinHeight = TypeScale.MinimumTarget });

        AutomationProperties.SetName(row, Spoken(list));
        row.PointerPressed += (_, _) => _nav.Drill(ListCrumb(list));

        return row;
    }

    /// <summary>A row's accessible name: the list and its counts.</summary>
    private static string Spoken(ChecklistList list) =>
        $"{list.Name}, {list.Open.ToString(CultureInfo.InvariantCulture)} open, "
        + $"{list.Done.ToString(CultureInfo.InvariantCulture)} done";

    private static string GroupWord(ChecklistListGroup group) => group switch
    {
        ChecklistListGroup.Yours => "Yours",
        ChecklistListGroup.Ships => "Ships",
        ChecklistListGroup.Systems => "Systems",
        ChecklistListGroup.SuitsAndWeapons => "Suits and weapons",
        _ => "Nothing open",
    };

    /// <summary>A pressable list row, 44px or taller, named for a screen reader.</summary>
    private static Button Pressable(string name, Control content, Action pressed)
    {
        var button = ListRow.Dress(new Button
        {
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            MinHeight = TypeScale.MinimumTarget,
        });

        AutomationProperties.SetName(button, name);
        button.Click += (_, _) => pressed();

        return button;
    }

    /// <summary>The three columns of a list row: who, next, counts.</summary>
    private static Grid Columns(Control who, Control next, Control counts)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("260,*,170"), ColumnSpacing = 16 };

        who.VerticalAlignment = VerticalAlignment.Center;
        next.VerticalAlignment = VerticalAlignment.Center;
        counts.VerticalAlignment = VerticalAlignment.Center;

        Grid.SetColumn(next, 1);
        Grid.SetColumn(counts, 2);
        grid.Children.Add(who);
        grid.Children.Add(next);
        grid.Children.Add(counts);

        return grid;
    }

    /// <summary>The open count in the accent and the done count in grey, mono, right-aligned.</summary>
    private static TextBlock Counts(int open, int done)
    {
        var block = new TextBlock
        {
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Small,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextWrapping = TextWrapping.NoWrap,
        };

        var opened = new Run($"{open.ToString(CultureInfo.InvariantCulture)} open");
        Themed(opened, TextElement.ForegroundProperty, ThemeManager.AKey);
        block.Inlines!.Add(opened);

        if (done > 0)
        {
            var closed = new Run($" · {done.ToString(CultureInfo.InvariantCulture)} done");
            Themed(closed, TextElement.ForegroundProperty, ThemeManager.GreyKey);
            block.Inlines.Add(closed);
        }

        return block;
    }

    /// <summary>A Nothing open row's count: "✓ {n} done" in Blue.</summary>
    private static TextBlock DoneMark(int done)
    {
        var block = new TextBlock
        {
            Text = $"✓ {done.ToString(CultureInfo.InvariantCulture)} done",
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Small,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        Themed(block, TextBlock.ForegroundProperty, ThemeManager.BlueKey);
        return block;
    }

    /// <summary>A list page's title: the name, a line under it, and the counts at the right.</summary>
    private void Heading(string name, string? kind, string? kindKey, int open, int done)
    {
        var title = TitleText.Build(name, TypeScale.Title, TitleRank.Screen);
        title.Name = "ChecklistListTitle";

        var text = new StackPanel { Spacing = 2, Children = { title } };

        if (kind is { Length: > 0 })
        {
            var line = new TextBlock
            {
                Name = "ChecklistListKind",
                Text = kind.ToUpperInvariant(),
                FontFamily = new FontFamily(Fonts.ChromeFamily),
                FontSize = TypeScale.Tip,
                FontWeight = FontWeight.Medium,
                TextWrapping = TextWrapping.Wrap,
            };

            Themed(line, TextBlock.ForegroundProperty, kindKey ?? ThemeManager.GreyKey);
            text.Children.Add(line);
        }

        var counts = Counts(open, done);
        counts.VerticalAlignment = VerticalAlignment.Bottom;

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 16,
            Margin = new Thickness(0, 0, 0, 10),
        };

        Grid.SetColumn(counts, 1);
        row.Children.Add(text);
        row.Children.Add(counts);

        _heading.Children.Add(row);
    }

    private void DrawAllHeading()
    {
        var all = _checklists.Arranged();
        var open = all.Count(item => !item.IsComplete);

        Heading("All lists", null, null, open, all.Count - open);
    }

    /// <summary>One list: its title, its controls and its lines.</summary>
    private void DrawOne(ChecklistList? list)
    {
        if (list is null)
        {
            Heading(_listWord, null, null, 0, 0);

            _hereOnly.IsVisible = false;
            _add.IsVisible = false;
            _deleteCompleted.IsVisible = false;

            _list.Children.Add(Muted("Nothing is on this list now."));
            return;
        }

        var ship = list.Scope.Group == ChecklistGroup.Ship;

        var kind = list.IsHere
            ? string.Join(" · ", new[] { list.Kind, ship ? "Flying now" : "You are here" }.Where(part => part.Length > 0))
            : list.Kind;

        Heading(list.Name, kind, list.IsHere ? ThemeManager.CyanKey : null, list.Open, list.Done);

        _hereOnly.IsVisible = ship;
        _add.IsVisible = _checklists.CanAdd(list);
        _deleteCompleted.IsVisible = true;
        _deleteCompleted.IsEnabled = list.Done > 0;

        var lines = _checklists.Lines(list);

        if (_hereNow)
        {
            lines = [.. lines.Where(_checklists.OfferedHere)];
        }

        if (lines.Count == 0)
        {
            _list.Children.Add(Muted("No engineer in this system does any line on this list."));
        }

        var done = lines.Where(item => item.IsComplete).ToList();

        foreach (var item in lines.Where(item => !item.IsComplete))
        {
            _list.Children.Add(Line(item, null, placed: true));
        }

        if (done.Count > 0)
        {
            _list.Children.Add(ListRow.Head($"Done ({done.Count})"));

            foreach (var item in done)
            {
                _list.Children.Add(Line(item, null, placed: true));
            }
        }
    }

    /// <summary>The goals mode (Phase 34, "The checklist points at the arc").</summary>
    private void RebuildArcs()
    {
        _arcs.Children.Clear();

        if (_goals is null)
        {
            _arcsToggle.IsVisible = false;
            _band.IsVisible = false;
            return;
        }

        var standings = _goals.Standings;
        var running = standings.Count(standing => !standing.IsDone);

        _arcsToggle.IsVisible = true;

        // The count, whichever way the box is set (#203).
        _arcsToggle.Content = $"Goals ({running} running)";
        _band.IsVisible = _showArcs;

        if (!_showArcs)
        {
            return;
        }

        if (standings.Count == 0)
        {
            _arcs.Children.Add(Muted("Every goal is removed."));
        }

        foreach (var standing in standings)
        {
            _arcs.Children.Add(Arc(standing));
        }

        // The button that gives every arc its age.
        if (_goals.Mine is null && _backfill is not null)
        {
            var read = new Button
            {
                Content = "Read my journals",
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 4, 0, 0),
            };

            read.Click += (_, _) => _backfill();

            _arcs.Children.Add(Muted(
                "Nothing here has an age yet, and the milestones have no figures. Reading back "
                + "through the journals already on this disk is what gives them one."));

            _arcs.Children.Add(read);
        }

        AddRemoved();
    }

    /// <summary>The Removed list: each removed goal with a Recover button. Nothing is drawn when none is removed.</summary>
    private void AddRemoved()
    {
        var removed = _goals!.Removed;

        if (removed.Count == 0)
        {
            return;
        }

        var names = _goals.Everything()
            .Where(standing => removed.Contains(standing.Arc.Key, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (names.Count == 0)
        {
            return;
        }

        _arcs.Children.Add(Muted("Removed"));

        foreach (var standing in names)
        {
            var recover = new Button
            {
                Content = "Recover",
                HorizontalAlignment = HorizontalAlignment.Right,
            };

            AutomationProperties.SetName(recover, $"Recover {standing.Arc.Name}");

            recover.Click += (_, _) =>
            {
                Say(_goals.Recover(standing.Arc.Key));
                Rebuild();
            };

            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            var name = Named(standing.Arc.Name, null, sentence: false);

            Grid.SetColumn(recover, 1);
            row.Children.Add(name);
            row.Children.Add(recover);

            _arcs.Children.Add(ListRow.Dress(new Border { Child = row }, selected: false));
        }
    }

    /// <summary>
    /// One arc: what it is, how far along, how long it has run — and, when open, what to do about it.
    /// </summary>
    private Control Arc(D47.Core.Goals.GoalStanding standing)
    {
        var open = standing.Arc.Key == _openArc;
        var body = new StackPanel { Spacing = 2 };

        body.Children.Add(Named(standing.Arc.Name, standing.IsDone ? Met : null, sentence: false));

        // The figure, then the bars — and no bar at all where the fraction is unknown.
        body.Children.Add(ListRow.Sub(new TextBlock { Text = Caption(standing), TextWrapping = TextWrapping.Wrap }));

        foreach (var bar in Bars(standing))
        {
            bar.Margin = new Thickness(0, 4, 0, 0);
            body.Children.Add(bar);
        }

        if (open)
        {
            body.Children.Add(Step(standing));
        }

        var card = ListRow.Dress(new Border { Child = body }, selected: open);

        AutomationProperties.SetName(card, standing.Arc.Name);

        card.PointerPressed += (_, _) =>
        {
            _openArc = open ? null : standing.Arc.Key;
            Rebuild();
        };

        return card;
    }

    /// <summary>
    /// A rank goal's ladder bar and rung bar, each labelled; any other goal's single bar, unlabelled. A finished
    /// goal draws a full ladder and no rung.
    /// </summary>
    private static IEnumerable<Control> Bars(D47.Core.Goals.GoalStanding standing)
    {
        var ladder = standing.IsDone ? 1 : standing.Fraction;

        if (standing.Arc.Top is not { } top)
        {
            if (ladder is { } only)
            {
                yield return D47.App.Controls.Gauge.Track(only, ThemeManager.AKey);
            }

            yield break;
        }

        if (ladder is { } fraction)
        {
            yield return Labelled($"To {top}", Percent(fraction), fraction);
        }

        if (standing.IsDone || standing.NextRank is not { } next)
        {
            yield break;
        }

        yield return standing.Rung is { } rung
            ? Labelled($"To {next}", Percent(rung), rung)
            : D47.App.Controls.Gauge.Heading($"To {next}", "not known yet", ThemeManager.GreyKey);
    }

    private static StackPanel Labelled(string label, string value, double fill) => new()
    {
        Spacing = 2,
        Children =
        {
            D47.App.Controls.Gauge.Heading(label, value, ThemeManager.AKey),
            D47.App.Controls.Gauge.Track(fill, ThemeManager.AKey),
        },
    };

    private static string Percent(double fraction) =>
        ((int)Math.Floor(fraction * 100)).ToString(CultureInfo.InvariantCulture) + "%";

    /// <summary>The caption under an arc: where it stands, where the figure came from, and its age.</summary>
    private string Caption(D47.Core.Goals.GoalStanding standing)
    {
        var parts = new List<string>();

        if (standing.IsDone)
        {
            parts.Add("done");
        }
        else if (standing.Note is { Length: > 0 } note)
        {
            parts.Add(note);
        }
        else if (standing.Have is { } have && standing.Need is { } need)
        {
            parts.Add($"{have:N0} of {need:N0}");
        }
        else
        {
            // Never "0".
            parts.Add("not known yet");
        }

        if (standing.Source == D47.Core.Goals.GoalSource.Mined && standing.AsOf is { } stamp)
        {
            parts.Add($"as of {stamp:d MMM yyyy}");
        }

        if (!standing.IsDone && standing.Age(_now()) is { TotalDays: >= 1 } age)
        {
            parts.Add($"running {(int)age.TotalDays} days");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>
    /// What to do about this arc today, and the two things a Commander can do about the arc itself.
    /// </summary>
    private Control Step(D47.Core.Goals.GoalStanding standing)
    {
        var panel = new StackPanel { Spacing = 6, Margin = new Thickness(0, 8, 0, 0) };

        var step = _goals?.Next(standing.Arc.Key);

        panel.Children.Add(ListRow.NameInk(new TextBlock
        {
            Text = step?.Say ?? standing.Arc.Done,
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
        }));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tile };

        if (step is { CanPropose: true })
        {
            var promote = new Button { Content = "Suggest a line" };

            promote.Click += (_, _) =>
            {
                Say(_goals!.Promote(standing.Arc.Key));
                Rebuild();
            };

            buttons.Children.Add(promote);
        }

        var remove = new Button { Content = "Remove" };

        remove.Click += (_, _) =>
        {
            Say(_goals!.Remove(standing.Arc.Key));
            _openArc = null;
            Rebuild();
        };

        buttons.Children.Add(remove);
        panel.Children.Add(buttons);

        return panel;
    }

    private bool Matches(ChecklistItem item)
    {
        // Searched on what is drawn as well as on what is stored, so a Commander can type the name of the
        // ship on the caption or the module on the line and find it.
        if (Query.Length > 0
            && !item.Text.Contains(Query, StringComparison.OrdinalIgnoreCase)
            && !item.Scope.ToString().Contains(Query, StringComparison.OrdinalIgnoreCase)
            && !_checklists.Said(item).Contains(Query, StringComparison.OrdinalIgnoreCase)
            && !_checklists.Where(item).Contains(Query, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Chosen == Everything)
        {
            return true;
        }

        // The one filter that is about where the Commander is rather than about the line (change-requests.md
        // 32).
        if (Chosen == ChecklistService.HereKey)
        {
            return _checklists.OfferedHere(item);
        }

        // The other filter about where the line could be finished rather than about the line itself —
        // anywhere, this time, because a pin does not care what system the Commander is standing in (#113).
        if (Chosen == ChecklistService.PinnedKey)
        {
            return _checklists.OfferedPinned(item);
        }

        // One engineer's own unlock, named by id in the key rather than by a spelling of its own (#265).
        if (ChecklistService.EngineerIdFor(Chosen) is { } engineerId)
        {
            return _checklists.OfferedEngineer(item, engineerId);
        }

        return Chosen.Equals(item.Kind.ToString(), StringComparison.OrdinalIgnoreCase)
               || Chosen.Equals(item.Source.ToString(), StringComparison.OrdinalIgnoreCase)
               || Chosen.Equals(ChecklistScope.Word(item.Scope.Group), StringComparison.OrdinalIgnoreCase)
               || Chosen.Equals(item.IsComplete ? "complete" : "open", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// One line: what it is, which scope it belongs to, and — when it is the selected one — the pair of
    /// movers.
    /// </summary>
    /// <param name="lead">The list's name, leading the caption, on a page that mixes lists.</param>
    /// <param name="placed">Whether the page already names the line's list, so the caption does not.</param>
    private Control Line(ChecklistItem item, string? lead = null, bool placed = false)
    {
        var selected = Selected is { } chosen && chosen.Same(item.Id);

        var body = new StackPanel { Spacing = 2 };

        // What it says now rather than what was stored: a derived line resolves its slot to the module
        // sitting in it, so "Slot01_Size7" reads as "7A Shield Generator".
        var said = _checklists.Said(item);

        CheckBox? checkbox = null;

        if (item.TicksByHand)
        {
            body.Children.Add(Named(said, null, sentence: true));

            checkbox = new CheckBox
            {
                Content = "completed",
                IsChecked = item.IsComplete,
                MinHeight = TouchTarget,
                VerticalAlignment = VerticalAlignment.Center,
            };

            checkbox.Click += (_, _) =>
            {
                var change = checkbox.IsChecked == true
                    ? _checklists.Complete(item.Id)
                    : _checklists.Uncomplete(item.Id);

                if (!change.Changed)
                {
                    Say(change.Report);
                }
            };
        }
        else
        {
            // No switch at all, rather than a disabled one.
            body.Children.Add(Named(said, item.IsComplete ? Met : NotMet, sentence: true));
        }

        // Scope on the line rather than as a heading over a group of them, and named rather than numbered:
        // "ship 51" is d47's key for the ship and nobody's name for one, so a page of finished rolls said
        // which id they were on and not which ship (reported 2026-08-21).
        var aside = new List<string>();

        if (!placed && lead is null)
        {
            aside.Add(_checklists.Where(item));
        }

        // And the arc it came from, where one proposed it (Phase 34).
        if (item.Goal is { Length: > 0 } goal)
        {
            // Through the book rather than the catalogue, so a line from a goal the Commander invented names
            // it as they wrote it rather than as a key.
            aside.Add("towards " + (_goals?.Find(goal)?.Arc.Name ?? goal));
        }

        // How far an engineer here can take it, where that is why the line is on the page at all
        // (change-requests.md 35).
        if (_hereNow
            && _checklists.IncludePartialGrades
            && _checklists.PartlyHere(item) is { } reach)
        {
            aside.Add(reach);
        }

        // And why a line this engineer does cannot be rolled today (#205).
        if (_hereNow && _checklists.RankHere(item) is { } standing)
        {
            aside.Add(standing);
        }

        ChecklistVerdict? verdict = null;

        if (ChecklistNextAction.For(item.State) is { } next)
        {
            aside.Add(next);
        }
        else if (!item.TicksByHand && _checklists.Verdict(item) is { } read)
        {
            verdict = read;
            aside.Add(read.Says);
        }

        var says = string.Join(" · ", aside);

        // A notice only where something is wrong.
        if (ChecklistNextAction.IsWrong(item.State))
        {
            body.Children.Add(new Notice(inline: true)
            {
                Text = lead is null ? says : string.Join(" · ", new[] { lead, says }.Where(part => part.Length > 0)),
            });
        }
        else if (lead is not null)
        {
            body.Children.Add(Led(lead, says));
        }
        else if (says.Length > 0)
        {
            body.Children.Add(Secondary(says));
        }

        // The same measure CriteriaFor reads for the Engineers pages, so the two cannot disagree (#17).
        if (verdict?.Measure is { } measure)
        {
            body.Children.Add(LoadoutPages.MeasureBar(item.IsComplete ? 1 : measure.Fill));
        }

        // On a line of their own under the text, so they wrap rather than clip on a narrow panel.
        if (selected)
        {
            var movers = Movers(item);
            movers.Margin = new Thickness(0, 4, 0, 0);

            body.Children.Add(movers);
        }

        var row = new DockPanel();

        // Centred on the text beside it rather than on its first line (#271).
        if (checkbox is not null)
        {
            checkbox.Margin = new Thickness(10, 0, 0, 0);

            DockPanel.SetDock(checkbox, Dock.Right);
            row.Children.Add(checkbox);
        }

        row.Children.Add(body);

        var card = ListRow.Dress(new Border { Child = row }, selected);

        // Selecting is what grows the movers, so the whole card takes the press rather than a handle
        // somewhere on it.
        card.PointerPressed += (_, _) =>
        {
            _checklists.Select(selected ? null : item.Id);
            Rebuild();
        };

        return card;
    }

    /// <summary>The four reorders, on the selected line (reported 2026-08-21).</summary>
    private Control Movers(ChecklistItem item)
    {
        var top = Mover(ChecklistMove.Top, "Move to the top", item);
        var up = Mover(ChecklistMove.Up, "Move up", item);
        var down = Mover(ChecklistMove.Down, "Move down", item);
        var bottom = Mover(ChecklistMove.Bottom, "Move to the bottom", item);

        var movers = new WrapPanel
        {
            ItemSpacing = Gaps.Tile,
            LineSpacing = Gaps.Tile,
            Children = { top, up, down, bottom },
        };

        // Rewording and removing belong to the selection for the same reason the movers do: four controls on
        // every one of several hundred rows is four hundred things a ray can hit by accident (remediation.md
        // 10, items 13 and 14).
        if (item.Kind == ChecklistItemKind.Authored)
        {
            var edit = new Button
            {
                Content = "Edit",
                MinWidth = 0,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var drop = new Button
            {
                Content = "Delete",
                MinWidth = 0,
                VerticalAlignment = VerticalAlignment.Center,
                Classes = { "destructive" },
            };

            edit.Click += (_, _) => EditLine(item);
            drop.Click += (_, _) => DeleteLine(item);

            AutomationProperties.SetName(edit, "Edit this line");
            AutomationProperties.SetName(drop, "Delete this line");

            movers.Children.Add(edit);
            movers.Children.Add(drop);
        }

        return movers;
    }

    /// <summary>Rewords a line the Commander wrote (remediation.md 10, item 13).</summary>
    private void EditLine(ChecklistItem item)
    {
        _prompts.Enter(
            new EntryRequest(
                "checklist.edit",
                "Edit",
                "Edit this line",
                "Your own words. The line keeps its place, its tick and whatever it came from.",
                item.Text,
                EntrySurface.Voice,
                value => string.IsNullOrWhiteSpace(value)
                    ? EntryVerdict.No("A line with nothing written on it is not a line.")
                    : EntryVerdict.Ok),
            value =>
            {
                var change = _checklists.Reword(item.Id, value);

                if (!change.Changed)
                {
                    Say(change.Report);
                    return;
                }

                Rebuild();
            });
    }

    /// <summary>Takes a line off the list (remediation.md 10, item 14).</summary>
    private void DeleteLine(ChecklistItem item)
    {
        _prompts.Choose(
            new ChoiceRequest(
                "checklist.delete",
                "Delete",
                "Delete this line",
                $"\"{item.Text}\" would come off the list. There is no way back from this one.",
                [new ChoiceOption("keep", "Keep it"), new ChoiceOption("delete", "Delete it")],
                null,
                ChoiceSurface.Layer),
            option =>
            {
                if (option.Key != "delete")
                {
                    return;
                }

                var change = _checklists.Delete(item.Id);

                if (!change.Changed)
                {
                    Say(change.Report);
                    return;
                }

                Rebuild();
            });
    }

    /// <summary>
    /// Removes every Done line in one press: this list's on a list page, the whole checklist's on All lists,
    /// ignoring the current filter and search (#259).
    /// </summary>
    private void DeleteCompletedItems()
    {
        var list = _level == Level.One ? _checklists.Lists().FirstOrDefault(found => found.Id == _listId) : null;

        if (_level == Level.One && list is null)
        {
            return;
        }

        var count = list?.Done ?? _checklists.Document.Items.Count(item => item.IsComplete);

        if (count == 0)
        {
            return;
        }

        var where = list is null
            ? " would come off your whole checklist, including anything hidden by the current filter or search."
            : $" would come off {list.Name}. Other lists keep theirs.";

        _prompts.Choose(
            new ChoiceRequest(
                "checklist.delete-completed",
                "Delete completed",
                "Delete completed items",
                (count == 1 ? "1 completed line" : $"{count} completed lines")
                + where
                + " There is no way back from this one.",
                [new ChoiceOption("keep", "Keep them"), new ChoiceOption("delete", "Delete them")],
                null,
                ChoiceSurface.Layer),
            option =>
            {
                if (option.Key != "delete")
                {
                    return;
                }

                var change = list is null ? _checklists.DeleteCompleted() : _checklists.DeleteCompleted(list);

                if (!change.Changed)
                {
                    Say(change.Report);
                    return;
                }

                Rebuild();
            });
    }

    /// <summary>
    /// One mover: the glyph, the touch target, and the name a screen reader and a test both find it by.
    /// </summary>
    private Button Mover(ChecklistMove move, string name, ChecklistItem item)
    {
        var glyph = Arrow(move);

        var button = new Button
        {
            Content = glyph,
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // The glyph takes the button's own text colour rather than a theme key of its own.
        glyph.Bind(
            Avalonia.Controls.Shapes.Shape.FillProperty,
            button.GetObservable(TemplatedControl.ForegroundProperty));

        button.Click += (_, _) => Moved(item, move);
        AutomationProperties.SetName(button, name);

        return button;
    }

    /// <summary>The mover glyphs, drawn rather than typed.</summary>
    private static Avalonia.Controls.Shapes.Path Arrow(ChecklistMove move)
    {
        // One 12x12 box for all four, so the pair in the middle and the pair outside it line up on the same
        // baseline and read as one row of controls.
        var geometry = move switch
        {
            ChecklistMove.Up => "M 6,2 L 11,9.5 L 1,9.5 Z",
            ChecklistMove.Down => "M 1,2.5 L 11,2.5 L 6,10 Z",
            ChecklistMove.Top => "M 1,1 L 11,1 L 11,2.6 L 1,2.6 Z M 6,4 L 11,11 L 1,11 Z",
            _ => "M 1,1 L 11,1 L 6,8 Z M 1,9.4 L 11,9.4 L 11,11 L 1,11 Z",
        };

        return new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(geometry),
            Width = 12,
            Height = 12,
            Stretch = Stretch.None,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
    }

    private void Moved(ChecklistItem item, ChecklistMove move)
    {
        var change = _checklists.Move(item.Id, move);

        if (!change.Changed)
        {
            Say(change.Report);
            return;
        }

        Rebuild();
    }

    /// <summary>The Commander stepped to a scope.</summary>
    private void OnScopeChanged()
    {
        if (_settlingScope)
        {
            return;
        }

        var index = _scopeCombo.SelectedIndex;

        if (index >= 0 && index < _scopeKeys.Count)
        {
            _checklists.Choose(_scopeKeys[index]);
        }
    }

    /// <summary>The Commander's own line, said or typed.</summary>
    private void AddLine()
    {
        _prompts.Enter(
            new EntryRequest(
                "checklist.add",
                "Add",
                "Add a line",
                "Your own note. It gets a switch, because it is yours to tick.",
                string.Empty,
                EntrySurface.Voice,
                value => string.IsNullOrWhiteSpace(value)
                    ? EntryVerdict.No("There was nothing to add.")
                    : EntryVerdict.Ok),
            value =>
            {
                var scope = _level == Level.One
                            && _checklists.Lists().FirstOrDefault(found => found.Id == _listId) is { } list
                    ? list.Scope
                    : _checklists.ScopeFor(null, null);

                var change = _checklists.AddNote(scope, value);

                if (!change.Changed)
                {
                    Say(change.Report);
                    return;
                }

                Rebuild();
            });
    }

    /// <summary>One proposal, with what it would do to the list.</summary>
    private Control Proposal(ChecklistProposal proposal, Action refresh)
    {
        var actions = ProposalActions.Build(
            accept: () =>
            {
                Say(_checklists.Accept(proposal.Id));
                refresh();
            },
            decline: () =>
            {
                Say(_checklists.Decline(proposal.Id));
                refresh();
            },
            footer: "D47 proposed this and cannot accept it itself.");

        var body = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                ListRow.NameInk(new TextBlock { Text = proposal.Summary, TextWrapping = TextWrapping.Wrap }),
                actions,
            },
        };

        return ListRow.Dress(new Border { Padding = new Thickness(12), Child = body });
    }

    private void ShowProblems()
    {
        var problems = _checklists.List.Problems.Concat(_checklists.Proposals.Problems).ToList();

        _problems.IsVisible = problems.Count > 0;
        _problems.Text = string.Join("\n", problems.Select(problem => $"{problem.Where}: {problem.Reason}"));
    }

    /// <summary>A refusal is shown where the problems are, not swallowed.</summary>
    private void Say(string message)
    {
        Rebuild();
        _problems.IsVisible = true;
        _problems.Text = message;
    }

    /// <summary>
    /// Names the query and the scope that emptied the list — the same two values <see cref="Matches"/>
    /// reads — so a Commander in mini, where neither is on screen, can still tell what to undo (#94).
    /// </summary>
    private string EmptyMessage()
    {
        if (Query.Length == 0 && Chosen == Everything)
        {
            return "Nothing here yet. Say \"add buy limpets to my checklist\", or ask for a build plan, "
                   + "and D47 will propose it.";
        }

        // The filter's own word, from the same lookup the scope button's label uses, so the two cannot
        // drift apart.
        var scope = Chosen == Everything
            ? null
            : _checklists.FilterAxes().FirstOrDefault(filter => filter.Key == Chosen)?.Word ?? Chosen;

        return (Query.Length > 0, scope) switch
        {
            (true, null) => $"Nothing on your list matches '{Query}'.",
            (true, { } word) => $"Nothing in {word} matches '{Query}'.",
            (false, { } word) => $"Nothing on your list is in {word}.",
            _ => "Nothing here yet.",
        };
    }

    /// <summary>The way out of a filter that emptied the list, for the surface with no search box to clear.</summary>
    private Control ClearFilterButton()
    {
        var button = new Button
        {
            Content = "Clear filter",
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 0),
        };

        button.Click += (_, _) =>
        {
            _checklists.Search(null);
            _checklists.Choose(null);
        };

        return button;
    }

    private static TextBlock Muted(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(block, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        return block;
    }

    /// <summary>The status ladder's mark for a line or goal that is met, in Blue.</summary>
    private static readonly (string Glyph, string Key) Met = ("✓", ThemeManager.BlueKey);

    /// <summary>The mark for a derived line not yet met, in Grey.</summary>
    private static readonly (string Glyph, string Key) NotMet = ("•", ThemeManager.GreyKey);

    /// <summary>
    /// A row's name, led by <paramref name="mark"/> where one is given. A sentence, such as a checklist line,
    /// keeps its own case and prose type.
    /// </summary>
    private static Control Named(string text, (string Glyph, string Key)? mark, bool sentence)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        var name = sentence ? ListRow.NameInk(block) : ListRow.Name(block);

        if (mark is not var (glyph, key))
        {
            return name;
        }

        var sign = new TextBlock { Text = glyph, Width = 16 };
        Themed(sign, TextBlock.ForegroundProperty, key);
        DockPanel.SetDock(sign, Dock.Left);

        return new DockPanel { Children = { sign, name } };
    }

    /// <summary>A caption led by the line's list name, upper case in the accent.</summary>
    private static Control Led(string lead, string says)
    {
        var name = new TextBlock
        {
            Text = lead.ToUpperInvariant(),
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontWeight = FontWeight.SemiBold,
            FontSize = TypeScale.Caption,
            LetterSpacing = TypeScale.Caption * 0.04,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Themed(name, TextBlock.ForegroundProperty, ThemeManager.AKey);
        DockPanel.SetDock(name, Dock.Left);

        var row = new DockPanel { Children = { name } };

        if (says.Length > 0)
        {
            row.Children.Add(Secondary(" · " + says));
        }

        return row;
    }

    /// <summary>A line's caption: a sentence, so it keeps its case and prose type in the secondary ink.</summary>
    private static TextBlock Secondary(string text) =>
        ListRow.SecondaryInk(new TextBlock
        {
            Text = text,
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
        });

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, Application.Current!.Resources.GetResourceObservable(key));
}

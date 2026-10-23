using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.App.Windowing;
using D47.Core.Capabilities;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>What the search pages run their tools through, and where they keep the answers and named searches.</summary>
public sealed record GalaxySearchSurface(
    CapabilityRegistry Registry,
    GalaxySearchBoard Board,
    SavedSearchStore? Saved,
    Func<string> FrontierId,
    Func<IReadOnlyCollection<string>> PhrasesTaken)
{
    /// <summary>The Commander's past searches, or null for none kept.</summary>
    public SearchHistory? History { get; init; }

    /// <summary>Opens the history picker; set by the panel that hosts the pages.</summary>
    public Action? OpenHistory { get; init; }

    /// <summary>The system names that start with the given letters, for the Near fields; null for no suggestions.</summary>
    public Func<string, CancellationToken, Task<IReadOnlyList<SystemNameMatch>>>? PrefixNames { get; init; }

    /// <summary>The minor faction names that start with the given letters, for the faction fields; null for no suggestions.</summary>
    public Func<string, CancellationToken, Task<IReadOnlyList<string>>>? FactionNames { get; init; }

    /// <summary>How long typing has to stop before names are suggested.</summary>
    public TimeSpan SuggestionPause { get; init; } = NameSuggestions.DefaultPause;

    /// <summary>Whether a signal search has anything to read: the signal index is on or the Commander has scanned signals; null for always.</summary>
    public Func<bool>? SignalSources { get; init; }
}

/// <summary>What a search page draws where the answer goes.</summary>
public enum GalaxySearchView
{
    /// <summary>No search of this kind has been answered this session.</summary>
    Unasked,

    /// <summary>“Look things up in the galaxy” is off.</summary>
    LookupsOff,

    /// <summary>A search from this page is waiting on the service.</summary>
    Running,

    /// <summary>The service could not answer.</summary>
    Unavailable,

    /// <summary>The tool refused a filter the form sent.</summary>
    Refused,

    /// <summary>The last answer matched nothing.</summary>
    NoResults,

    /// <summary>The last answer, as a table.</summary>
    Answer,
}

/// <summary>
/// One search kind's page on the Search tab: the form on the left and, on the right, the kind's saved
/// searches and its last answer on <see cref="GalaxySearchBoard"/>, however it was asked. The headset draws
/// the form and the answer as two full-width views.
/// </summary>
public abstract class GalaxySearchPage : UserControl, IPageSummary, IPageChrome
{
    public const string LookupsOffText =
        "Turn on “Look things up in the galaxy” in Settings and D47 will search the galaxy for star systems, stations and bodies.";

    public const string RunName = "RunSearch";

    public const string ClearName = "ClearSearch";

    public const string RefreshName = "RefreshSearch";

    public const string SaveName = "SaveSearch";

    public const string EditName = "EditSearch";

    public const string RetryName = "RetrySearch";

    public const string AskedName = "AskedAgo";

    public const string SortedName = "SortedBy";

    public const string OpenSettingName = "OpenTheSetting";

    /// <summary>Marks a saved or quick search tile, for a test to find it by its name.</summary>
    public const string SavedClass = "saved-search";

    /// <summary>Marks a FORGET tile beside one of the Commander's own searches.</summary>
    public const string ForgetClass = "forget-search";

    /// <summary>Marks a row of the answer table.</summary>
    public const string RowClass = "search-row";

    /// <summary>How long a search may run.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(70);

    private readonly GalaxySearchSurface _searches;
    private readonly Func<bool> _lookupsEnabled;
    private readonly Func<bool> _namesEnabled;
    private readonly Action? _openSettings;
    private readonly bool _headset;

    private readonly Button _run = new() { Name = RunName, Content = "RUN SEARCH", Height = TypeScale.MinimumTarget, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Button _clear = new() { Name = ClearName, Content = "CLEAR", Height = TypeScale.MinimumTarget };
    private readonly ContentControl _answer = new();
    private readonly ContentControl _saved = new();
    private readonly TextBlock _stamp = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };

    private Control? _form;
    private Control? _formView;
    private Control? _answerView;
    private bool? _showingAnswer;
    private CancellationTokenSource? _inFlight;
    private IReadOnlyDictionary<string, string>? _running;
    private (GalaxySearchView View, string Text, DateTimeOffset At, IReadOnlyDictionary<string, string> Arguments)? _failure;
    private object? _seen;
    private GalaxySearchPosting? _filledFrom;
    private readonly HistoryTile _history;
    private GalaxySearchPosting? _fromHistory;

    protected GalaxySearchPage(GalaxySearchSurface searches, Func<bool> lookupsEnabled, Action? openSettings, bool headset, Func<bool>? namesEnabled = null)
    {
        _searches = searches;
        _lookupsEnabled = lookupsEnabled;
        _namesEnabled = namesEnabled ?? lookupsEnabled;
        _openSettings = openSettings;
        _headset = headset;
        _history = new HistoryTile(searches);

        _run.Click += (_, _) => _ = RunAsync(FormArguments());
        _clear.Click += (_, _) => ClearForm();

        ToolTip.SetShowOnDisabled(_run, true);
    }

    public event EventHandler? SummaryChanged;

    public Control? BarTool => _history.Button;

    public string? HelpTopic => null;

    /// <summary>The history entry drawn instead of the board's last answer, or null.</summary>
    public string? HistoryId { get; private set; }

    /// <summary>Whether the HISTORY tile can be pressed.</summary>
    public bool CanOpenHistory => _history.CanOpen;

    /// <summary>
    /// Draws <paramref name="entry"/>'s stored answer and arguments until the next answer of this kind lands.
    /// </summary>
    public void ShowEntry(SearchHistoryEntry entry)
    {
        if (SearchHistoryPicker.Posting(entry) is not { } posting || posting.Kind != Kind)
        {
            return;
        }

        HistoryId = entry.Id;
        _fromHistory = posting;
        _failure = null;

        FillForm(posting.Arguments);
        _showingAnswer = true;
        Draw();
    }

    /// <summary>The search tool this page runs.</summary>
    public abstract string Tool { get; }

    /// <summary>The kind of answer on the board this page draws.</summary>
    public abstract GalaxySearchKind Kind { get; }

    public abstract string Summary { get; }

    /// <summary>What is drawn where the answer goes.</summary>
    public GalaxySearchView View { get; private set; }

    /// <summary>The sentence drawn for the current state, where it has one.</summary>
    public string? Sentence { get; private set; }

    /// <summary>Whether the headset draws the answer view rather than the form view.</summary>
    public bool ShowsAnswer => _showingAnswer ?? false;

    /// <summary>The answer drawn, or null while none is.</summary>
    public GalaxySearchPosting? Shown { get; private set; }

    /// <summary>Whether RUN SEARCH can be pressed.</summary>
    public bool CanRun => _run.IsEnabled;

    /// <summary>Whether this page is drawn for the headset.</summary>
    protected bool Headset => _headset;

    /// <summary>The plural noun the answer head counts in, upper case.</summary>
    protected abstract string Noun { get; }

    /// <summary>The singular of <see cref="Noun"/>, upper case.</summary>
    protected abstract string NounOne { get; }

    /// <summary>The sentence drawn while the page cannot search.</summary>
    protected virtual string OffSentence => LookupsOffText;

    /// <summary>Why the page cannot search, for the RUN SEARCH tooltip.</summary>
    protected virtual string OffTip => "Turn on “Look things up in the galaxy” in Settings to search.";

    /// <summary>The label of the box drawn while a search runs.</summary>
    protected virtual string RunningText => "Asking the galaxy search";

    /// <summary>The label of the notice drawn when the tool refused the form.</summary>
    protected virtual string RefusedLabel => "FILTER REFUSED";

    /// <summary>Where the answer came from, in the footer stamp.</summary>
    protected virtual string Origin => "GALAXY SEARCH";

    /// <summary>What the footer says to ask by voice; null for the page's first quick search.</summary>
    protected virtual string? SayPhrase => null;

    /// <summary>What the page draws from the Commander's own state, such as their system; a change redraws it.</summary>
    protected virtual object? Followed => null;

    /// <summary>Brings the form up to date with <see cref="Followed"/>.</summary>
    protected virtual void Follow()
    {
    }

    /// <summary>Redraws when the setting or the Commander's system changed; true when it did.</summary>
    public bool Tick()
    {
        var tileChanged = _history.Refresh();

        if (Equals(Stamp(), _seen))
        {
            return tileChanged;
        }

        Draw();
        return true;
    }

    /// <summary>Runs <see cref="Tool"/> with <paramref name="arguments"/> as the Commander; the answer arrives through the board.</summary>
    public async Task RunAsync(IReadOnlyDictionary<string, string> arguments)
    {
        if (!_lookupsEnabled() || _inFlight is not null)
        {
            return;
        }

        var inFlight = _inFlight = new CancellationTokenSource(Budget);
        _running = arguments;
        _failure = null;
        _showingAnswer = true;
        Draw();

        try
        {
            var result = await _searches.Registry
                .InvokeAsync(
                    Tool,
                    new ToolArguments(new Dictionary<string, string>(arguments, StringComparer.Ordinal)),
                    inFlight.Token,
                    ToolCaller.Commander)
                .ConfigureAwait(true);

            if (result.IsError)
            {
                _failure = (
                    Refusal(arguments) ? GalaxySearchView.Refused : GalaxySearchView.Unavailable,
                    result.Content,
                    DateTimeOffset.Now,
                    arguments);
            }
        }
        catch (OperationCanceledException)
        {
            _failure = (GalaxySearchView.Unavailable, "The galaxy search took too long to answer.", DateTimeOffset.Now, arguments);
        }
        finally
        {
            _inFlight = null;
            _running = null;
            inFlight.Dispose();
            Draw();
        }
    }

    /// <summary>The form, built once.</summary>
    protected abstract Control BuildForm();

    /// <summary>Lists system names under <paramref name="field"/> as it is typed into; null when the surface offers none.</summary>
    protected SystemNameSuggestions<SystemNameMatch>? SuggestNames(TextBox field)
    {
        if (_searches.PrefixNames is not { } lookup)
        {
            return null;
        }

        SystemNameSuggestions<SystemNameMatch>? names = null;
        names = new SystemNameSuggestions<SystemNameMatch>(field, lookup, match => match.Name, _namesEnabled, match => names!.SetText(match.Name), _searches.SuggestionPause);

        return names;
    }

    /// <summary>Lists catalogue items under <paramref name="field"/> as it is typed into; nothing is sent anywhere.</summary>
    protected SystemNameSuggestions<OutfittingItem> SuggestItems(TextBox field, Func<string, IReadOnlyList<OutfittingItem>> lookup, Action<OutfittingItem> picked)
    {
        SystemNameSuggestions<OutfittingItem>? items = null;
        items = new SystemNameSuggestions<OutfittingItem>(
            field,
            (typed, _) => Task.FromResult(lookup(typed)),
            item => item.Display,
            () => true,
            item =>
            {
                items!.SetText(string.Empty);
                picked(item);
            },
            _searches.SuggestionPause,
            item => item.IsShip ? "Ship" : "Module");

        return items;
    }

    /// <summary>Lists faction names under <paramref name="field"/> as it is typed into; null when the surface offers none.</summary>
    protected SystemNameSuggestions<string>? SuggestFactions(TextBox field)
    {
        if (_searches.FactionNames is not { } lookup)
        {
            return null;
        }

        SystemNameSuggestions<string>? names = null;
        names = new SystemNameSuggestions<string>(field, lookup, name => name, _lookupsEnabled, name => names!.SetText(name), _searches.SuggestionPause);

        return names;
    }

    /// <summary>The field with its suggestions listed under it.</summary>
    protected static Control Beneath<T>(TextBox field, SystemNameSuggestions<T>? names) =>
        names is null ? field : new StackPanel { Spacing = 2, Children = { field, names.List } };

    /// <summary>The Near field with its help line and suggestions listed under it.</summary>
    protected static Control NearField(TextBox near, SystemNameSuggestions<SystemNameMatch>? names)
    {
        near.Classes.Add("here");
        var stack = new StackPanel { Spacing = 4, Children = { near, Prose("Follows you as you jump. Type a system name to search from there instead.", TypeScale.Tip, ThemeManager.GreyKey) } };
        if (names is not null)
        {
            stack.Children.Add(names.List);
        }

        return stack;
    }

    /// <summary>Shows the Commander's current system as the empty Near box's value.</summary>
    protected static void ShowHere(TextBox near, string? system) =>
        near.PlaceholderText = (system is null ? "Your system" : $"Your system · {system}").ToUpperInvariant();

    /// <summary>Sets the field's text without listing names for it.</summary>
    protected static void Fill<T>(TextBox field, SystemNameSuggestions<T>? names, string text)
    {
        if (names is null)
        {
            field.Text = text;
        }
        else
        {
            names.SetText(text);
        }
    }

    /// <summary>The tool arguments the form holds, as given; a field left empty is left out.</summary>
    public abstract IReadOnlyDictionary<string, string> FormArguments();

    /// <summary>Empties the form.</summary>
    public abstract void ClearForm();

    /// <summary>Sets the form to <paramref name="arguments"/>, so a search asked elsewhere shows what it asked.</summary>
    protected abstract void FillForm(IReadOnlyDictionary<string, string> arguments);

    /// <summary>Whether the tool would refuse these filters before asking the service.</summary>
    protected abstract bool Refusal(IReadOnlyDictionary<string, string> arguments);

    /// <summary>The words after the count in the answer head, such as “WITHIN 50 LY OF ”, or null for none.</summary>
    protected abstract (string Lead, string Reference)? Aside(IReadOnlyDictionary<string, string> arguments, string? reference);

    /// <summary>The answer's table, or null when it matched nothing.</summary>
    protected abstract Control? Table(GalaxySearchPosting posting, bool headset);

    /// <summary>How many the answer holds.</summary>
    protected abstract int Count(GalaxySearchPosting posting);

    /// <summary>One column of an answer table: its labels, the <c>order_by</c> name the service sorts it on (null for none), and which side its figures sit on.</summary>
    protected sealed record SearchColumn(string Label, string? Second, string? Sort, bool Right);

    /// <summary>
    /// The header row of an answer table. A column the service sorts on is pressed to re-run the search
    /// sorted by it; the sort is carried as <c>order_by</c> and <c>order</c> in the search's arguments.
    /// </summary>
    protected Control SearchHead(GalaxySearchPosting posting, string name, string gridColumns, IReadOnlyList<SearchColumn> columns)
    {
        var fields = SearchSorts.For(Kind);

        var specs = columns
            .Select(column => fields.FirstOrDefault(field => field.Name == column.Sort) is { } field
                ? SortColumn<object>.Remote(column.Label, field.FirstDescending ? SortDirection.Descending : SortDirection.Ascending)
                  with { Second = column.Second, OneWay = field.OneWay }
                : SortColumn<object>.Plain(column.Label) with { Second = column.Second })
            .ToList();

        var nearest = columns.ToList().FindIndex(column => column.Sort == SearchSorts.Distance);
        var head = new SortHeader<object>(name, specs);

        // A search with no sort is nearest first, so the first press of the distance column reverses it.
        var current = nearest < 0 ? (SortOrder?)null : new SortOrder(nearest, SortDirection.Ascending);

        if (SearchSorts.Read(Kind, posting.Arguments) is { } asked
            && columns.ToList().FindIndex(column => column.Sort == asked.Field.Name) is var shown and >= 0)
        {
            current = new SortOrder(shown, asked.Descending ? SortDirection.Descending : SortDirection.Ascending);
        }

        head.Show(current);

        head.Changed += () => SortBy(posting, columns, head.Order);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(gridColumns), ColumnSpacing = 12, Margin = new Thickness(12, 0, 12, 6) };

        for (var index = 0; index < columns.Count; index++)
        {
            var cell = head.Cell(index, columns[index].Right ? HorizontalAlignment.Right : HorizontalAlignment.Left);
            Grid.SetColumn(cell, index);
            grid.Children.Add(cell);
        }

        var rule = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Child = grid };
        Themed(rule, Border.BorderBrushProperty, ThemeManager.LineKey);

        return rule;
    }

    /// <summary>Runs <paramref name="posting"/>'s search again sorted by <paramref name="order"/>; null is nearest first.</summary>
    private void SortBy(GalaxySearchPosting posting, IReadOnlyList<SearchColumn> columns, SortOrder? order)
    {
        var arguments = new Dictionary<string, string>(Rerun(posting), StringComparer.Ordinal);
        arguments.Remove("order_by");
        arguments.Remove("order");

        if (order is { } sort && columns[sort.Column].Sort is { } field)
        {
            arguments["order_by"] = field;
            arguments["order"] = sort.Direction == SortDirection.Ascending ? "asc" : "desc";
        }

        _ = RunAsync(arguments);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _searches.Board.Posted += OnPosted;
        _history.Attach();

        if (_searches.Saved is { } saved)
        {
            saved.Changed += OnSavedChanged;
        }

        FillFromLast();
        Draw();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _searches.Board.Posted -= OnPosted;
        _history.Detach();

        if (_searches.Saved is { } saved)
        {
            saved.Changed -= OnSavedChanged;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnPosted(GalaxySearchKind kind)
    {
        if (kind != Kind)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            _failure = null;
            _fromHistory = null;
            HistoryId = null;
            _showingAnswer = true;
            FillFromLast();
            Draw();
        });
    }

    private void OnSavedChanged() => Dispatcher.UIThread.Post(Draw);

    /// <summary>Sets the form to a posting it has not shown yet, so typing survives the page being re-attached.</summary>
    private void FillFromLast()
    {
        if (_fromHistory is not null || _searches.Board.Last(Kind) is not { } last || ReferenceEquals(last, _filledFrom))
        {
            return;
        }

        _filledFrom = last;
        FillForm(last.Arguments);
    }

    private GalaxySearchPosting? Latest => _fromHistory ?? _searches.Board.Last(Kind);

    private object Stamp() => (_lookupsEnabled(), Latest, _searches.FrontierId(), Followed);

    /// <summary>Shows the form with the values the shown answer was asked with.</summary>
    private void Edit()
    {
        var asked = _failure?.Arguments ?? (Latest is { } last ? Rerun(last) : null);

        if (asked is not null)
        {
            FillForm(asked);
        }

        _showingAnswer = false;
        Draw();
    }

    private void Draw()
    {
        _seen = Stamp();
        Follow();
        _showingAnswer ??= Latest is not null;

        var lookups = _lookupsEnabled();
        var last = Latest;

        View = !lookups ? GalaxySearchView.LookupsOff
            : _running is not null ? GalaxySearchView.Running
            : _failure is { } failure ? failure.View
            : last is null ? GalaxySearchView.Unasked
            : Count(last) == 0 ? GalaxySearchView.NoResults
            : GalaxySearchView.Answer;

        Shown = View is GalaxySearchView.Answer or GalaxySearchView.NoResults ? last : null;
        Sentence = View switch
        {
            GalaxySearchView.LookupsOff => OffSentence,
            GalaxySearchView.Unavailable or GalaxySearchView.Refused => _failure!.Value.Text,
            GalaxySearchView.NoResults => Described(last!),
            _ => null,
        };

        _run.IsEnabled = lookups && _running is null;
        ToolTip.SetTip(_run, lookups ? null : OffTip);

        _saved.Content = SavedBlock();
        DrawStamp(last);
        _answer.Content = AnswerBlock(last);

        if (Content is null)
        {
            Content = _headset ? HeadsetLayout() : Layout();
        }

        if (_formView is not null && _answerView is not null)
        {
            _formView.IsVisible = !_showingAnswer!.Value;
            _answerView.IsVisible = _showingAnswer.Value;
        }
    }

    private Grid HeadsetLayout()
    {
        _form = BuildForm();

        var buttons = Buttons();
        var form = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Bottom);
        form.Children.Add(buttons);
        form.Children.Add(LoadoutPages.Scrolling(new StackPanel { Spacing = 14, Children = { _saved, _form } }));

        var answer = new DockPanel();
        var footer = Footer();
        DockPanel.SetDock(footer, Dock.Bottom);
        answer.Children.Add(footer);
        answer.Children.Add(LoadoutPages.Scrolling(_answer));

        _formView = form;
        _answerView = answer;

        return new Grid { Margin = new Thickness(0, 16, 0, 0), Children = { form, answer } };
    }

    private Grid Buttons()
    {
        var buttons = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = Gaps.Tile,
            Margin = new Thickness(0, 12, 0, 0),
            Children = { _run, _clear },
        };
        Grid.SetColumn(_clear, 1);

        return buttons;
    }

    private Grid Layout()
    {
        _form = BuildForm();

        var scroll = new ScrollViewer
        {
            Content = new Border { Padding = new Thickness(0, 0, 10, 0), Child = _form },
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };

        var buttons = Buttons();

        var left = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Bottom);
        left.Children.Add(buttons);
        left.Children.Add(scroll);

        var right = new DockPanel();
        var footer = Footer();
        DockPanel.SetDock(footer, Dock.Bottom);
        right.Children.Add(footer);

        var top = new StackPanel { Spacing = 14, Children = { _saved, _answer } };
        right.Children.Add(LoadoutPages.Scrolling(top));

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("360,*"),
            ColumnSpacing = 28,
            Margin = new Thickness(0, 16, 0, 0),
            Children = { left, right },
        };
        Grid.SetColumn(right, 1);

        return grid;
    }

    private Control SavedBlock()
    {
        var quick = QuickSearches.All.Where(search => search.Tool == Tool).ToList();
        var yours = _searches.Saved is { } store
            ? store.All().Where(search => search.Tool == Tool).ToList()
            : [];

        var block = new StackPanel { Spacing = 8 };

        block.Children.Add(ListRow.Head("Saved"));

        if (quick.Count == 0)
        {
            block.Children.Add(NoQuick());
        }
        else
        {
            var row = new WrapPanel { ItemSpacing = Gaps.Tile, LineSpacing = Gaps.Tile };
            row.Children.Add(Label("Quick", width: 56));

            foreach (var search in quick)
            {
                row.Children.Add(SavedTile(search.Name, search.Arguments));
            }

            block.Children.Add(row);
        }

        if (yours.Count > 0)
        {
            var row = new WrapPanel { ItemSpacing = Gaps.Tile, LineSpacing = Gaps.Tile };
            row.Children.Add(Label("Yours", width: 56));

            foreach (var search in yours)
            {
                var forget = new Button
                {
                    Content = "✕",
                    Width = TypeScale.MinimumTarget,
                    Height = TypeScale.MinimumTarget,
                    Padding = new Thickness(0),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Classes = { ForgetClass },
                    Tag = search.Name,
                };
                Avalonia.Automation.AutomationProperties.SetName(forget, $"Forget the {search.Name} search");
                forget.Click += (_, _) => Forget(search.Name);

                row.Children.Add(new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = Gaps.Tile,
                    Children = { SavedTile(search.Name, search.Arguments), forget },
                });
            }

            block.Children.Add(row);
        }

        return block;
    }

    private TextBlock NoQuick()
    {
        var line = Prose($"No quick searches for {Noun.ToLowerInvariant()}.", TypeScale.Tip, ThemeManager.GreyKey);
        line.VerticalAlignment = VerticalAlignment.Center;
        return line;
    }

    private Button SavedTile(string name, IReadOnlyDictionary<string, string> arguments)
    {
        var tile = new Button
        {
            Content = name.ToUpperInvariant(),
            Height = TypeScale.MinimumTarget,
            Classes = { SavedClass },
            Tag = name,
            IsEnabled = _lookupsEnabled() && _running is null,
        };

        tile.Click += (_, _) => _ = RunAsync(arguments);
        return tile;
    }

    private Control AnswerBlock(GalaxySearchPosting? last)
    {
        var block = new StackPanel { Spacing = 14 };

        switch (View)
        {
            case GalaxySearchView.Unasked:
                break;

            case GalaxySearchView.LookupsOff:
                block.Children.Add(Head(Noun, null, refresh: null, save: false));
                block.Children.Add(Prose(OffSentence, TypeScale.Secondary, ThemeManager.WhiteKey));

                if (_openSettings is { } open)
                {
                    var setting = new Button { Name = OpenSettingName, Content = "OPEN THE SETTING", Height = TypeScale.MinimumTarget, HorizontalAlignment = HorizontalAlignment.Left };
                    setting.Click += (_, _) => open();
                    block.Children.Add(setting);
                }

                break;

            case GalaxySearchView.Running:
                block.Children.Add(Head("Searching", Aside(_running!, null), refresh: null, save: false, disabled: true));

                var asking = new Border { MinHeight = 50, Padding = new Thickness(14, 0), Child = Label(RunningText, width: null) };
                Themed(asking, Border.BackgroundProperty, ThemeManager.SlabKey);
                block.Children.Add(asking);
                break;

            case GalaxySearchView.Unavailable:
                block.Children.Add(Head(Noun, null, refresh: null, save: false));

                var unavailable = new Notice(NoticeLevel.Error)
                {
                    Label = "GALAXY SEARCH UNAVAILABLE",
                    Text = Sentence,
                    Detail = _failure!.Value.At.ToString("HH:mm", CultureInfo.InvariantCulture),
                };

                var again = _failure.Value.Arguments;
                var retry = new Button { Name = RetryName, Content = "RETRY", Height = TypeScale.MinimumTarget };
                retry.Click += (_, _) => _ = RunAsync(again);
                unavailable.Actions.Add(retry);
                block.Children.Add(unavailable);
                break;

            case GalaxySearchView.Refused:
                if (_headset)
                {
                    block.Children.Add(Head(Noun, null, refresh: null, save: false));
                }

                block.Children.Add(new Notice(NoticeLevel.Error) { Label = RefusedLabel, Text = Sentence });
                break;

            case GalaxySearchView.NoResults:
                block.Children.Add(Head($"0 {Noun}", Aside(last!.Arguments, last.Reference), Rerun(last), save: true, asked: Asked(last), saved: last.Arguments, sorted: Sorted(last)));
                AddNotes(block, last);
                block.Children.Add(Prose(Sentence!, TypeScale.Secondary, ThemeManager.WhiteKey));
                break;

            case GalaxySearchView.Answer:
                block.Children.Add(Head(Count(last!) == 1 ? $"1 {NounOne}" : $"{Count(last!)} {Noun}", Aside(last!.Arguments, last.Reference), Rerun(last), save: true, asked: Asked(last), saved: last.Arguments, sorted: Sorted(last)));
                AddNotes(block, last);

                if (Table(last, _headset) is { } table)
                {
                    block.Children.Add(table);
                }

                break;
        }

        return block;
    }

    private static void AddNotes(StackPanel block, GalaxySearchPosting posting)
    {
        if (posting.Notes.Length > 0)
        {
            block.Children.Add(Prose(posting.Notes, TypeScale.Tip, ThemeManager.GreyKey));
        }
    }

    /// <summary>The answer's own sentence, without the notes it opens with.</summary>
    private static string Described(GalaxySearchPosting posting) =>
        posting.Notes.Length == 0 ? posting.Answer
        : posting.Answer.StartsWith(posting.Notes, StringComparison.Ordinal) ? posting.Answer[posting.Notes.Length..].Trim()
        : posting.Answer.EndsWith(posting.Notes, StringComparison.Ordinal) ? posting.Answer[..^posting.Notes.Length].Trim()
        : posting.Answer;

    /// <summary>The arguments REFRESH and SAVE use: a history entry's <c>near</c> is its stored reference when left out.</summary>
    private IReadOnlyDictionary<string, string> Rerun(GalaxySearchPosting posting) =>
        ReferenceEquals(posting, _fromHistory)
            ? SearchHistoryPicker.Rerun(posting.Arguments, posting.Reference)
            : posting.Arguments;

    /// <summary>The words for the order the answer was sorted in, or null for nearest first.</summary>
    private string? Sorted(GalaxySearchPosting posting) => SearchSorts.Read(Kind, posting.Arguments)?.Words;

    /// <summary>"Asked 6 hours ago" while a history entry is drawn, or null.</summary>
    private string? Asked(GalaxySearchPosting posting) =>
        ReferenceEquals(posting, _fromHistory) ? $"Asked {MissionsPage.Ago(DateTimeOffset.UtcNow - posting.AskedAt)}" : null;

    private Control Head(
        string count,
        (string Lead, string Reference)? aside,
        IReadOnlyDictionary<string, string>? refresh,
        bool save,
        bool disabled = false,
        string? asked = null,
        IReadOnlyDictionary<string, string>? saved = null,
        string? sorted = null)
    {
        var name = Chrome(count, TypeScale.Section, ThemeManager.AKey);
        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Bottom, Children = { name } };

        if (aside is { } words)
        {
            var line = new TextBlock
            {
                FontFamily = Fonts.ChromeFamily,
                FontSize = TypeScale.Small,
                FontWeight = FontWeight.Medium,
                LetterSpacing = TypeScale.Small * Fonts.ChromeTracking,
                VerticalAlignment = VerticalAlignment.Bottom,
            };
            Themed(line, TextBlock.ForegroundProperty, ThemeManager.AKey);
            line.Inlines!.Add(new Run(words.Lead.ToUpperInvariant()));

            var reference = new Run(words.Reference.ToUpperInvariant());
            reference.Bind(TextElement.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(ThemeManager.CyanKey));
            line.Inlines.Add(reference);
            left.Children.Add(line);
        }

        if (sorted is not null)
        {
            var order = Chrome($"· {sorted}", TypeScale.Small, ThemeManager.AKey);
            order.Name = SortedName;
            order.VerticalAlignment = VerticalAlignment.Bottom;
            left.Children.Add(order);
        }

        if (asked is not null)
        {
            var when = Chrome(asked, TypeScale.Small, ThemeManager.GreyKey);
            when.Name = AskedName;
            when.VerticalAlignment = VerticalAlignment.Bottom;
            left.Children.Add(when);
        }

        var tiles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tile, HorizontalAlignment = HorizontalAlignment.Right };

        if (_headset && !disabled)
        {
            var edit = new Button { Name = EditName, Content = "EDIT SEARCH", Height = TypeScale.MinimumTarget };
            edit.Click += (_, _) => Edit();
            tiles.Children.Add(edit);
        }

        if (refresh is not null || disabled)
        {
            var again = refresh;
            var button = new Button { Name = RefreshName, Content = "REFRESH", Height = TypeScale.MinimumTarget, IsEnabled = !disabled && _lookupsEnabled() };
            button.Click += (_, _) =>
            {
                if (again is not null)
                {
                    _ = RunAsync(again);
                }
            };
            tiles.Children.Add(button);
        }

        if (save || disabled)
        {
            var posting = saved ?? refresh;
            var button = new Button
            {
                Name = SaveName,
                Content = "SAVE THIS SEARCH",
                Height = TypeScale.MinimumTarget,
                IsEnabled = !disabled && posting is not null && _searches.Saved is not null,
            };
            button.Click += (_, _) =>
            {
                if (posting is not null)
                {
                    Save(posting);
                }
            };
            tiles.Children.Add(button);
        }

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12, Children = { left, tiles } };
        Grid.SetColumn(tiles, 1);

        var rule = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 0, 0, 6), Child = row };
        Themed(rule, Border.BorderBrushProperty, ThemeManager.AKey);

        return rule;
    }

    private Border Footer()
    {
        var phrase = SayPhrase is { } say ? $"Say: “{say}”"
            : QuickSearches.All.FirstOrDefault(search => search.Tool == Tool) is { } quick
                ? $"Say: “{QuickSearches.PhrasesOf(quick).First()}”"
                : string.Empty;

        var said = Prose(phrase, TypeScale.Tip, ThemeManager.GreyKey);
        said.TextTrimming = TextTrimming.CharacterEllipsis;
        said.TextWrapping = TextWrapping.NoWrap;
        said.VerticalAlignment = VerticalAlignment.Center;

        Grid.SetColumn(_stamp, 1);

        var border = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 10, 0, 0),
            Margin = new Thickness(0, 10, 0, 0),
            Child = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { said, _stamp } },
        };
        Themed(border, Border.BorderBrushProperty, ThemeManager.Line2Key);

        return border;
    }

    private void DrawStamp(GalaxySearchPosting? last)
    {
        _stamp.Inlines ??= [];
        _stamp.Inlines.Clear();

        if (last is null)
        {
            return;
        }

        var words = new Run($"{Origin} · ANSWERED ") { FontFamily = Fonts.ChromeFamily, FontSize = TypeScale.Meta, FontWeight = FontWeight.Medium };
        words.Bind(TextElement.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(ThemeManager.GreyKey));

        var time = new Run(last.AskedAt.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture)) { FontFamily = new FontFamily(Fonts.MonoFamily), FontSize = TypeScale.Meta };
        time.Bind(TextElement.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(ThemeManager.CyanKey));

        _stamp.Inlines.Add(words);
        _stamp.Inlines.Add(time);
    }

    private void Save(IReadOnlyDictionary<string, string> arguments)
    {
        if (_searches.Saved is not { } store || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var dialog = new SaveSearchDialog(
            Noun,
            Tool,
            arguments,
            name => store.Add(name, Tool, arguments, DateTimeOffset.UtcNow, _searches.PhrasesTaken()));

        _ = dialog.Over(owner);
    }

    private void Forget(string name)
    {
        if (_searches.Saved is not { } store || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var dialog = new ForgetSearchDialog(Noun, name, () => store.Forget(name));

        _ = dialog.Over(owner);
    }

    /// <summary>A form field's label: Saira 12, upper case, Grey.</summary>
    protected static TextBlock Label(string text, double? width)
    {
        var block = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Caption,
            FontWeight = FontWeight.Medium,
            LetterSpacing = TypeScale.Caption * 0.06,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (width is { } fixedWidth)
        {
            block.Width = fixedWidth;
        }

        Themed(block, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        return block;
    }

    protected static TextBlock Chrome(string text, double size, string key)
    {
        var block = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontFamily = Fonts.ChromeFamily,
            FontSize = size,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = size * Fonts.ChromeTracking,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Themed(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    protected static TextBlock Mono(string text, double size, string key)
    {
        var block = new TextBlock { Text = text, FontFamily = new FontFamily(Fonts.MonoFamily), FontSize = size };
        Themed(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    protected static TextBlock Prose(string text, double size, string key)
    {
        var block = new TextBlock { Text = text, FontFamily = Fonts.ProseFamily, FontSize = size, TextWrapping = TextWrapping.Wrap };
        Themed(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    protected static void Themed(StyledElement target, AvaloniaProperty property, string key) =>
        target.Bind(property, Application.Current!.Resources.GetResourceObservable(key));

    protected void RaiseSummaryChanged() => SummaryChanged?.Invoke(this, EventArgs.Empty);
}

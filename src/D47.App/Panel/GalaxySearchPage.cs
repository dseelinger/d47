using System.Globalization;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Templates;
using Avalonia.Input;
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

    /// <summary>The Power the Commander is pledged to, or null for none.</summary>
    public Func<string?>? PledgedPower { get; init; }

    /// <summary>Asks for one minor faction's report, holding it for an hour unless refreshed; null for no faction page.</summary>
    public Func<string, bool, IProgress<FactionProgress>, CancellationToken, Task<FactionReport>>? Faction { get; init; }

    /// <summary>The minor faction the Commander's squadron backs, or null when it is not known.</summary>
    public Func<string?>? SquadronFaction { get; init; }

    /// <summary>The Commander's game state, for their own readings of a system and their squadron.</summary>
    public Func<D47.Core.Journal.CommanderGameState?>? GameState { get; init; }

    /// <summary>The time of the last BGS tick, or null when none is known.</summary>
    public Func<DateTimeOffset?>? BgsTick { get; init; }
}

/// <summary>What a search page draws where the answer goes.</summary>
public enum GalaxySearchView
{
    /// <summary>No search of this kind has been answered this session.</summary>
    Unasked,

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
    public const double DefaultFormWidth = 360;

    public const double FormMinWidth = 300;

    public const double FormMaxWidth = 720;

    public const double AnswerMinWidth = 400;

    private const double SplitterWidth = 28;

    public const string RunName = "RunSearch";

    public const string ClearName = "ClearSearch";

    public const string RefreshName = "RefreshSearch";

    public const string SaveName = "SaveSearch";

    public const string EditName = "EditSearch";

    public const string RetryName = "RetrySearch";

    public const string RetryPageName = "RetryPage";

    /// <summary>The tile that fetches the next page while the filter hides rows.</summary>
    public const string FetchNextName = "FetchNextPage";

    /// <summary>The line that ends the rows while the filter hides some of them.</summary>
    public const string FilterEndName = "FilterEnd";

    /// <summary>The headset pager's tile that shows the page before.</summary>
    public const string PreviousPageName = "SearchPreviousPage";

    /// <summary>The headset pager's tile that shows the page after.</summary>
    public const string NextPageName = "SearchNextPage";

    /// <summary>The answer's rows, drawn a screen at a time.</summary>
    public const string RowsName = "SearchRows";

    public const string AskedName = "AskedAgo";

    public const string SortedName = "SortedBy";

    /// <summary>The line under the head that says how many rows the filter shows.</summary>
    public const string FilterWordsName = "FilterWords";

    public const string ForgetName = "ForgetSearch";

    /// <summary>What the START FROM button shows while no saved or quick search is picked.</summary>
    public const string NoPickText = "Choose a saved search";

    /// <summary>Marks a row of the answer table.</summary>
    public const string RowClass = "search-row";

    /// <summary>How long a search may run.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(70);

    private readonly GalaxySearchSurface _searches;
    private readonly bool _headset;

    private readonly Button _run = new() { Name = RunName, Content = "RUN SEARCH", Height = TypeScale.MinimumTarget, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Button _clear = new() { Name = ClearName, Content = "CLEAR", Height = TypeScale.MinimumTarget };
    private readonly ContentControl _answer = new();
    private readonly InlinePicker _startFrom = new() { Label = "Start from a saved search" };
    private readonly Button _forget = new() { Name = ForgetName, Content = "FORGET THIS SEARCH", Height = TypeScale.MinimumTarget, HorizontalAlignment = HorizontalAlignment.Left, Classes = { Settings.SettingsView.DestructiveClass } };
    private readonly TextBlock _said = new() { TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap, VerticalAlignment = VerticalAlignment.Center };
    private Control? _savedSection;
    private string? _picked;
    private bool _pickedIsOwn;
    private IReadOnlyDictionary<string, string>? _pickedAt;
    private bool _pickPosting;
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
    private AvaloniaList<object>? _rows;
    private int _shownRows;
    private int _page;
    private IReadOnlyDictionary<string, string>? _pageFor;
    private ScrollViewer? _scroll;
    private TextBlock? _count;
    private GalaxySearchPosting? _drawn;
    private CancellationTokenSource? _nextPage;
    private PageFailed? _pageFailed;
    private IReadOnlyDictionary<string, string>? _filterFor;
    private TextBlock? _filterWords;
    private Control? _filterLine;
    private Border? _columnHead;

    /// <summary>The last item of the rows while the next page loads.</summary>
    private sealed record PageLoading;

    /// <summary>The last item of the rows when the next page failed.</summary>
    private sealed record PageFailed(string Text, DateTimeOffset At, int Page);

    private static readonly PageLoading Loading = new();

    /// <summary>The last item of the rows while the filter hides some of the rows fetched.</summary>
    private sealed record FilterEnd(int Fetched, int Hidden, bool More);

    /// <summary>Copies a system name to the clipboard; null draws no copy glyphs.</summary>
    protected Func<string, Task<bool>>? Copy { get; set; }

    private const string YourSystem = "your system";

    protected GalaxySearchPage(GalaxySearchSurface searches, bool headset)
    {
        _searches = searches;
        _headset = headset;
        _history = new HistoryTile(searches);

        _run.Click += (_, _) => _ = RunAsync(WithKeptSort(FormArguments()));
        _clear.Click += (_, _) =>
        {
            ResetPick();
            ClearForm();
            Draw();
        };
        _startFrom.Picked += (_, name) => StartFrom(name);
        _forget.Click += (_, _) =>
        {
            if (_picked is { } name)
            {
                Forget(name);
            }
        };

        ToolTip.SetShowOnDisabled(_run, true);
    }

    event EventHandler? IPageSummary.SummaryChanged
    {
        add { }
        remove { }
    }

    public Control? BarTool => _history.Button;

    public string? HelpTopic => null;

    /// <summary>The history entry drawn instead of the board's last answer, or null.</summary>
    public string? HistoryId { get; private set; }

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
        ResetPick();
        _failure = null;
        DropNextPage();

        FillForm(posting.Arguments);
        _showingAnswer = true;
        Draw();
    }

    /// <summary>The id the page's table sorts are remembered under.</summary>
    protected abstract string SortTableId { get; }

    private bool _keptRead;
    private (string Key, SortDirection Direction, string? OrderBy)? _kept;
    private IReadOnlyDictionary<string, string>? _localFor;
    private bool _localShown;
    private TextBlock? _orderChip;

    /// <summary>The table's remembered sort; <c>OrderBy</c> is null for a sort of the rows fetched.</summary>
    private (string Key, SortDirection Direction, string? OrderBy)? Kept
    {
        get
        {
            if (!_keptRead)
            {
                _keptRead = true;

                if (ColumnArrangements?.Sorts is { } memory && memory.Remembered(SortTableId) is { } stored)
                {
                    _kept = (stored.Key, stored.Direction, memory.RememberedOrderBy(SortTableId));
                }
            }

            return _kept;
        }
    }

    private void Keep((string Key, SortDirection Direction)? sort, string? orderBy)
    {
        _keptRead = true;
        _kept = sort is { } chosen ? (chosen.Key, chosen.Direction, orderBy) : null;
        ColumnArrangements?.Sorts.Remember(SortTableId, sort, orderBy);
    }

    /// <summary><paramref name="arguments"/> with the remembered service sort added, unless they already name an order.</summary>
    private IReadOnlyDictionary<string, string> WithKeptSort(IReadOnlyDictionary<string, string> arguments)
    {
        if (Kept is not { OrderBy: { } by } kept
            || arguments.ContainsKey("order_by")
            || SearchSorts.For(Kind).All(field => field.Name != by)
            || (by == SearchSorts.Material && !arguments.ContainsKey("material")))
        {
            return arguments;
        }

        return new Dictionary<string, string>(arguments, StringComparer.Ordinal)
        {
            ["order_by"] = by,
            ["order"] = kept.Direction == SortDirection.Ascending ? "asc" : "desc",
        };
    }

    /// <summary>The remembered sort of the rows fetched covers the answer asked with <paramref name="arguments"/>.</summary>
    private bool LocalCovers(IReadOnlyDictionary<string, string> arguments) =>
        Kept is { OrderBy: null } && ReferenceEquals(_localFor, arguments);

    private bool LocalShownFor(GalaxySearchPosting posting) => _localShown && ReferenceEquals(_localFor, posting.Arguments);

    /// <summary>
    /// The rows of <paramref name="posting"/> the filter lets through, in the order of a sort of the rows fetched while one
    /// is shown.
    /// </summary>
    private IReadOnlyList<object> RowsShown(GalaxySearchPosting posting) =>
        _arranged is not { } table ? Rows(posting)
        : LocalShownFor(posting) ? table.Shown(Rows(posting))
        : table.Filter(Rows(posting));

    /// <summary>The name of the sort of the rows fetched while one is shown, else null.</summary>
    private string? LocalSortName(GalaxySearchPosting posting)
    {
        if (!LocalShownFor(posting) || _arranged is not { SortedBy: { } sort } table)
        {
            return null;
        }

        var field = table.ShownFields.FirstOrDefault(shown => shown.Key == sort.Key);

        return field?.Name ?? field?.Label ?? sort.Key;
    }

    /// <summary>"these 25 sorted by Volcanism" while the rows fetched are sorted, else null.</summary>
    private string? LocalWords(GalaxySearchPosting posting) =>
        LocalSortName(posting) is { } name ? $"these {Count(posting).ToString(CultureInfo.InvariantCulture)} sorted by {name}" : null;

    /// <summary>"these 18 of 25, filtered by Economy and Power", with the sort of the rows fetched when one is shown; null while the filter hides nothing.</summary>
    private string? FilterWords(GalaxySearchPosting posting) =>
        _arranged is { } table ? ColumnFilter.Words(table, Rows(posting), LocalSortName(posting)) : null;

    /// <summary>The filter hides some of the rows of <paramref name="posting"/>.</summary>
    private bool Hides(GalaxySearchPosting posting) => _arranged is { } table && table.Hidden(Rows(posting)) > 0;

    private void OnSorted(ArrangedTable<object> table)
    {
        var wasLocal = _localShown;
        _page = 0;

        if (table.SortedBy is { } sort && !SortFields.IsRemote(sort.Key))
        {
            _localFor = Posted.Arguments;
            Keep(sort, null);
            return;
        }

        _localFor = null;
        _localShown = false;
        Keep(table.SortedBy, table.SortedBy is { } remote ? SortFields.OrderBy(remote.Key) : null);

        if (!wasLocal || table.SortedBy is not null)
        {
            _ = RunAsync(SortFields.Sorted(Rerun(Posted), table));
        }
    }

    /// <summary>The search tool this page runs.</summary>
    public abstract string Tool { get; }

    /// <summary>The kind of answer on the board this page draws.</summary>
    public abstract GalaxySearchKind Kind { get; }

    public abstract string Summary { get; }

    /// <summary>Where the form width is remembered; null where the host gives the form a fixed width.</summary>
    public PaneWidthMemory? PaneWidths { get; init; }

    public ColumnWidthMemory? ColumnWidths { get; init; }

    /// <summary>Where the columns each table shows are remembered; null keeps the table's own.</summary>
    public ColumnArrangementMemory? ColumnArrangements { get; init; }

    private SearchTableFields? _sortFields;
    private ArrangedTable<object>? _arranged;
    private string? _arrangedShape;

    private SearchTableFields SortFields => _sortFields ??= new SearchTableFields(Kind);

    /// <summary>What is drawn where the answer goes.</summary>
    public GalaxySearchView View { get; private set; }

    /// <summary>The sentence drawn for the current state, where it has one.</summary>
    public string? Sentence { get; private set; }

    /// <summary>Whether RUN SEARCH can be pressed.</summary>
    public bool CanRun => _run.IsEnabled;

    /// <summary>Whether this page is drawn for the headset.</summary>
    protected bool Headset => _headset;

    /// <summary>The plural noun the answer head counts in, upper case.</summary>
    protected abstract string Noun { get; }

    /// <summary>The singular of <see cref="Noun"/>, upper case.</summary>
    protected abstract string NounOne { get; }

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

    /// <summary>Redraws when the Commander's system changed; true when it did.</summary>
    public bool Tick()
    {
        var tileChanged = _history.Refresh();

        if (EditedSincePick())
        {
            ResetPick();
            Draw();
            return true;
        }

        if (Equals(Stamp(), _seen))
        {
            // The headset raises no ScrollChanged, so the end of the answer is also checked here.
            MoreIfAtEnd();
            return tileChanged;
        }

        Draw();
        return true;
    }

    /// <summary>Fills the form from <paramref name="arguments"/> and runs them.</summary>
    public void Run(IReadOnlyDictionary<string, string> arguments)
    {
        ResetPick();
        FillForm(arguments);
        _ = RunAsync(WithKeptSort(arguments));
    }

    /// <summary>Runs <see cref="Tool"/> with <paramref name="arguments"/> as the Commander; the answer arrives through the board.</summary>
    public async Task RunAsync(IReadOnlyDictionary<string, string> arguments)
    {
        if (_inFlight is not null)
        {
            return;
        }

        DropNextPage();
        arguments = Sent(arguments);

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
        names = new SystemNameSuggestions<SystemNameMatch>(field, lookup, match => match.Name, () => true, match => names!.SetText(match.Name), _searches.SuggestionPause);

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
        names = new SystemNameSuggestions<string>(field, lookup, name => name, () => true, name => names!.SetText(name), _searches.SuggestionPause);

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

    /// <summary>Adds <paramref name="value"/>, trimmed, to <paramref name="arguments"/> unless it is blank.</summary>
    protected static void Put(Dictionary<string, string> arguments, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            arguments[key] = value.Trim();
        }
    }

    /// <summary>Adds <c>near</c> and, when it is not the default, <c>max_distance</c>.</summary>
    protected static void PutNear(Dictionary<string, string> arguments, TextBox near, DistancePicker distance)
    {
        Put(arguments, "near", near.Text);

        if (distance.Argument(sendChosenDefault: false) is { } value)
        {
            arguments["max_distance"] = value;
        }
    }

    /// <summary>Sets <paramref name="distance"/> from <c>max_distance</c> when it is a number.</summary>
    protected static void FillDistance(IReadOnlyDictionary<string, string> arguments, DistancePicker distance)
    {
        if (arguments.TryGetValue("max_distance", out var typed)
            && double.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out var asked))
        {
            distance.Set(asked);
        }
    }

    /// <summary>The “WITHIN n LY OF ” aside, from <c>max_distance</c> or <paramref name="defaultDistance"/>, held to the farthest distance searched.</summary>
    protected static (string Lead, string Reference) WithinAside(IReadOnlyDictionary<string, string> arguments, string? reference, int defaultDistance)
    {
        var within = arguments.TryGetValue("max_distance", out var typed)
                     && double.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out var asked)
            ? Math.Min(asked, D47.Core.Knowledge.GalaxyQuery.FarthestDistance)
            : defaultDistance;

        return ($"WITHIN {within.ToString("0.##", CultureInfo.InvariantCulture)} LY OF ", AsideFrom(arguments, reference));
    }

    /// <summary>The reference an aside measures from: the resolved one, the typed <c>near</c>, or “your system”.</summary>
    protected static string AsideFrom(IReadOnlyDictionary<string, string> arguments, string? reference) =>
        reference
        ?? (arguments.TryGetValue("near", out var near) && near.Trim().Length > 0 ? near.Trim() : null)
        ?? YourSystem;

    /// <summary>Makes a dropdown for <paramref name="name"/> and registers it, set to Any.</summary>
    protected void AddPicker(
        Dictionary<string, (InlinePicker Picker, IReadOnlyList<string> Choices)> pickers,
        Dictionary<string, string> picked,
        string name,
        IReadOnlyList<string> choices,
        bool several)
    {
        var picker = new InlinePicker { Label = name.Replace('_', ' '), ListMaxHeight = 240 };

        picker.Picked += (_, id) => ShowPick(pickers, picked, name, id, several);

        pickers[name] = (picker, choices);
        ShowPick(pickers, picked, name, SystemSearchPage.Any, several);
    }

    /// <summary>
    /// Sets a dropdown to <paramref name="value"/>, or to Any for a value it does not offer. With
    /// <paramref name="several"/>, a list of offered choices is kept as given where the filter takes several.
    /// </summary>
    protected static void ShowPick(
        Dictionary<string, (InlinePicker Picker, IReadOnlyList<string> Choices)> pickers,
        Dictionary<string, string> picked,
        string name,
        string value,
        bool several)
    {
        var (picker, choices) = pickers[name];
        var chosen = choices.FirstOrDefault(choice => string.Equals(choice, value, StringComparison.OrdinalIgnoreCase));

        if (chosen is null
            && several
            && GalaxyFilters.Find(name) is { Several: true }
            && value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) is { Length: > 1 } parts
            && parts.All(part => choices.Contains(part, StringComparer.OrdinalIgnoreCase)))
        {
            chosen = string.Join(", ", parts);
        }

        if (chosen is null)
        {
            picked.Remove(name);
        }
        else
        {
            picked[name] = chosen;
        }

        picker.Show(
            [new InlinePickerOption(SystemSearchPage.Any, SystemSearchPage.Any, ThemeManager.GreyKey), .. choices.Select(choice => new InlinePickerOption(choice, choice, ThemeManager.WhiteKey))],
            chosen ?? SystemSearchPage.Any,
            chosen ?? SystemSearchPage.Any,
            chosen is null ? ThemeManager.GreyKey : ThemeManager.WhiteKey,
            null);
    }

    /// <summary>Resets every dropdown to Any, or to the value <paramref name="arguments"/> holds for it.</summary>
    protected static void ResetPickers(
        Dictionary<string, (InlinePicker Picker, IReadOnlyList<string> Choices)> pickers,
        Dictionary<string, string> picked,
        IReadOnlyDictionary<string, string>? arguments,
        bool several)
    {
        foreach (var name in pickers.Keys)
        {
            ShowPick(pickers, picked, name, arguments?.GetValueOrDefault(name) ?? SystemSearchPage.Any, several);
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

    /// <summary>The words after the count in the answer head, such as “WITHIN 500 LY OF ”, or null for none.</summary>
    protected abstract (string Lead, string Reference)? Aside(IReadOnlyDictionary<string, string> arguments, string? reference);

    /// <summary>The answer's table drawn whole, or null when it matched nothing or the page draws it through <see cref="Paged"/>.</summary>
    protected virtual Control? Table(GalaxySearchPosting posting, bool headset) => null;

    /// <summary>An answer table drawn a row at a time: its column header, and the control for one item of <see cref="Rows"/>.</summary>
    protected sealed record PagedTable(Control Head, Func<object, Control> Row);

    /// <summary>The answer's table as a header and rows, or null where the page draws it through <see cref="Table"/>.</summary>
    protected virtual PagedTable? Paged(GalaxySearchPosting posting, bool headset) => null;

    /// <summary>The answer's rows, in order.</summary>
    protected virtual IReadOnlyList<object> Rows(GalaxySearchPosting posting) => [];

    /// <summary>How many rows the service has for the search in all.</summary>
    protected virtual int Total(GalaxySearchPosting posting) => Count(posting);

    /// <summary>Whether <see cref="Total"/> is the service's count of every row the search matches.</summary>
    protected virtual bool Counted(GalaxySearchPosting posting) => true;

    /// <summary>How many the answer holds.</summary>
    protected abstract int Count(GalaxySearchPosting posting);

    /// <summary>The arguments a search started from this page sends: without <c>limit</c>, so the answer comes a page at a time.</summary>
    protected virtual IReadOnlyDictionary<string, string> Sent(IReadOnlyDictionary<string, string> arguments)
    {
        var sent = new Dictionary<string, string>(arguments, StringComparer.Ordinal);
        sent.Remove("limit");
        return sent;
    }

    /// <summary>
    /// The answer's table, built once and kept across redraws so its COLUMNS bar stays open. A new <paramref name="shape"/>
    /// builds it again from <paramref name="fields"/>, which registers the sorts of <see cref="SortFields"/>. The sort shown
    /// is the one in <paramref name="posting"/>'s arguments.
    /// </summary>
    protected ArrangedTable<object> Arrange(string id, string headName, string shape, GalaxySearchPosting posting, Func<IReadOnlyList<TableField<object>>> fields)
    {
        Posted = posting;

        if (_arranged is null || _arrangedShape != shape)
        {
            SortFields.Clear();

            var table = new ArrangedTable<object>(id, fields(), ColumnArrangements, _headset ? null : ColumnWidths, 12, headName: headName);
            table.Changed += () =>
            {
                _page = 0;
                Draw();
            };
            table.Sorted += () => OnSorted(table);
            _arranged = table;
            _arrangedShape = shape;
        }
        else
        {
            _arranged.Refresh();
        }

        if (!ReferenceEquals(_filterFor, posting.Arguments))
        {
            _arranged.ClearFilters();
            _filterFor = posting.Arguments;
        }

        _arranged.Observe(Rows(posting));

        _localShown = LocalCovers(posting.Arguments) && Kept is { } local && _arranged.ShowSort(local.Key, local.Direction);

        if (!_localShown)
        {
            SortFields.Show(_arranged, posting.Arguments);
        }

        return _arranged;
    }

    /// <summary>The table shows the column of this key.</summary>
    protected bool ColumnShown(string key) => _arranged?.ShownFields.Any(field => field.Key == key) == true;

    /// <summary>The table's column head on its rule; a later page redraws its filtered counts.</summary>
    protected Control ColumnHead(ArrangedTable<object> table) => _columnHead = SearchTableFields.Head(table);

    /// <summary>The posting the table is drawn for.</summary>
    protected GalaxySearchPosting Posted { get; private set; } = null!;

    /// <summary>
    /// A field of this kind of table; pass <c>sort</c> for the <c>order_by</c> its header asks for, else <c>local</c> sorts
    /// the rows fetched. <c>filters</c> are the value lists the filter bar offers.
    /// </summary>
    protected TableField<object> SearchField(
        string key,
        TableColumn column,
        string label,
        Func<object, Control> cell,
        string? sort = null,
        string? second = null,
        bool right = false,
        bool byDefault = true,
        Func<string, SortColumn<object>>? local = null,
        IReadOnlyList<ValueList<object>>? filters = null) =>
        SortFields.Field(key, column, label, cell, sort, second, right, byDefault, local, filters);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _filterFor = null;
        _arranged?.StopArranging();

        if (_arranged is { Filtering: true } filtering)
        {
            filtering.ToggleFiltering();
        }

        _searches.Board.Posted += OnPosted;
        _searches.Board.Extended += OnExtended;
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
        _searches.Board.Extended -= OnExtended;
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

        var pageAsked = _running is not null;

        Dispatcher.UIThread.Post(() =>
        {
            DropNextPage();
            _failure = null;
            _fromHistory = null;
            HistoryId = null;
            _showingAnswer = true;

            if (pageAsked && _searches.Board.Last(Kind) is { } posted)
            {
                _localFor = posted.Arguments;
            }

            if (!_pickPosting)
            {
                ResetPick();
            }

            FillFromLast();

            if (_picked is not null)
            {
                _pickedAt = FormArguments();
            }

            Draw();
        });
    }

    /// <summary>Adds the rows of a later page to the drawn table, leaving the scroll position, the form and the pick alone.</summary>
    private void OnExtended(GalaxySearchKind kind)
    {
        if (kind != Kind)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (_fromHistory is not null || _searches.Board.Last(Kind) is not { } last)
            {
                return;
            }

            if (_filledFrom is { } filled && ReferenceEquals(filled.Arguments, last.Arguments))
            {
                _filledFrom = last;
            }

            if (_rows is null || _count is null || _drawn is null || !ReferenceEquals(_drawn.Arguments, last.Arguments))
            {
                Draw();
                return;
            }

            var table = _arranged;
            table?.Observe(Rows(last));

            if (_headset && RowsShown(last).Count is var arrived && arrived > _shownRows)
            {
                _page = _shownRows / FactionModel.PageSize;
            }

            if (table is { Filtering: true })
            {
                Draw();
                return;
            }

            var all = RowsShown(last);
            var at = _rows.Count > 0 && _rows[^1] is PageLoading or PageFailed or FilterEnd ? _rows.Count - 1 : _rows.Count;
            _drawn = last;

            if (_headset || LocalShownFor(last) || table is { FilteredLabels.Count: > 0 })
            {
                _rows.Clear();
                _rows.AddRange(Slice(all));
                ShowTail();
                ShowHeadWords(last);
            }
            else
            {
                _rows.InsertRange(at, all.Skip(_shownRows));
            }

            _shownRows = all.Count;
            _count.Text = CountText(last).ToUpperInvariant();
            _seen = Stamp();
            CheckSoon();
        });
    }

    private void OnSavedChanged() => Dispatcher.UIThread.Post(Draw);

    /// <summary>Cancels a next-page fetch and forgets a failed one.</summary>
    private void DropNextPage()
    {
        _pageFailed = null;

        if (_nextPage is { } fetch)
        {
            _nextPage = null;
            fetch.Cancel();
        }

        ShowTail();
    }

    /// <summary>Ends the rows with the loading slab or the failure notice, or with neither.</summary>
    private void ShowTail()
    {
        if (_rows is null)
        {
            return;
        }

        while (_rows.Count > 0 && _rows[^1] is PageLoading or PageFailed or FilterEnd)
        {
            _rows.RemoveAt(_rows.Count - 1);
        }

        if (_nextPage is not null)
        {
            _rows.Add(Loading);
        }
        else if (_pageFailed is { } failed)
        {
            _rows.Add(failed);
        }

        if (_drawn is not { } drawn)
        {
            return;
        }

        var hidden = _arranged is { } arranged ? arranged.Hidden(Rows(drawn)) : 0;

        if (_headset)
        {
            var shown = RowsShown(drawn).Count;

            if (hidden > 0 || shown > FactionModel.PageSize || CanFetchNext())
            {
                _rows.Add(new FilterEnd(Count(drawn), hidden, CanFetchNext()));
            }
        }
        else if (_nextPage is null && _pageFailed is null && hidden > 0)
        {
            _rows.Add(new FilterEnd(Count(drawn), hidden, CanFetchNext()));
        }
    }

    /// <summary>The rows of the page the headset shows, within the pages <paramref name="all"/> fill; every row on the desktop.</summary>
    private IReadOnlyList<object> Slice(IReadOnlyList<object> all)
    {
        if (!_headset)
        {
            return all;
        }

        _page = FactionModel.ClampPage(_page, all.Count);

        return [.. all.Skip(_page * FactionModel.PageSize).Take(FactionModel.PageSize)];
    }

    /// <summary>Shows the page <see cref="_page"/> of the rows fetched, fetching nothing.</summary>
    private void ShowPage(int page)
    {
        if (_rows is null || _drawn is not { } drawn)
        {
            return;
        }

        _page = page;
        _rows.Clear();
        _rows.AddRange(Slice(RowsShown(drawn)));
        ShowTail();
        _scroll?.ScrollToHome();
    }

    /// <summary>The next page of the answer drawn can be fetched now: the service has more and no fetch is under way.</summary>
    private bool CanFetchNext() =>
        _nextPage is null
        && _pageFailed is null
        && _running is null
        && _fromHistory is null
        && _searches.Board.Last(Kind) is { More: true } last
        && _drawn is not null
        && ReferenceEquals(last.Arguments, _drawn.Arguments);

    /// <summary>Fetches the next page of the answer drawn, when it can be fetched.</summary>
    private void FetchNext()
    {
        if (CanFetchNext() && _searches.Board.Last(Kind) is { } last)
        {
            _ = NextPageAsync(last.Arguments, last.Pages + 1);
        }
    }

    /// <summary>The words for the order of the rows shown; the filter's line names a sort of the rows fetched instead.</summary>
    private string? OrderWords(GalaxySearchPosting posting, string? filter) =>
        filter is null ? LocalWords(posting) ?? Sorted(posting) : LocalShownFor(posting) ? null : Sorted(posting);

    /// <summary>Brings the head's words, the filter's line and the column head's filtered counts up to date with the rows held.</summary>
    private void ShowHeadWords(GalaxySearchPosting posting)
    {
        var filter = FilterWords(posting);
        var words = OrderWords(posting, filter);

        if (_orderChip is not null)
        {
            _orderChip.Text = words is null ? null : $"· {words}".ToUpperInvariant();
            _orderChip.IsVisible = words is not null;
        }

        if (_filterWords is not null && _filterLine is not null)
        {
            _filterWords.Text = filter?.ToUpperInvariant();
            _filterLine.IsVisible = filter is not null;
        }

        if (_columnHead is { Child: Control old } rule && _arranged is { } table)
        {
            var grid = table.Header();
            grid.Margin = old.Margin;
            rule.Child = grid;
        }
    }

    private void CheckSoon() => Dispatcher.UIThread.Post(MoreIfAtEnd, DispatcherPriority.Background);

    /// <summary>
    /// Fetches the next page when the answer's scroller is within one row of its end and the service has more. While the
    /// filter hides rows, only once the rows fill the scroller and it has been scrolled to their end. Never while the filter
    /// bar is open.
    /// </summary>
    private void MoreIfAtEnd()
    {
        if (_nextPage is not null
            || _pageFailed is not null
            || _running is not null
            || _fromHistory is not null
            || _headset
            || View != GalaxySearchView.Answer
            || _rows is not { Count: > 0 } rows
            || _showingAnswer is not true
            || _scroll is not { } scroll
            || scroll.Viewport.Height <= 0
            || _searches.Board.Last(Kind) is not { More: true } last
            || _drawn is null
            || !ReferenceEquals(last.Arguments, _drawn.Arguments))
        {
            return;
        }

        if (_arranged is { Filtering: true }
            || (Hides(last) && (scroll.Offset.Y <= 0 || scroll.Extent.Height <= scroll.Viewport.Height)))
        {
            return;
        }

        var row = scroll.Extent.Height / rows.Count;

        if (scroll.Offset.Y + scroll.Viewport.Height >= scroll.Extent.Height - row)
        {
            _ = NextPageAsync(last.Arguments, last.Pages + 1);
        }
    }

    /// <summary>Runs <see cref="Tool"/> for <paramref name="page"/> of the search; the rows arrive through <see cref="GalaxySearchBoard.Extended"/>.</summary>
    private async Task NextPageAsync(IReadOnlyDictionary<string, string> search, int page)
    {
        var fetch = _nextPage = new CancellationTokenSource(Budget);
        _pageFailed = null;
        ShowTail();

        var arguments = new Dictionary<string, string>(search, StringComparer.Ordinal)
        {
            ["page"] = page.ToString(CultureInfo.InvariantCulture),
        };

        string? failure = null;

        try
        {
            var result = await _searches.Registry
                .InvokeAsync(Tool, new ToolArguments(arguments), fetch.Token, ToolCaller.Commander)
                .ConfigureAwait(true);

            if (result.IsError)
            {
                failure = result.Content;
            }
        }
        catch (OperationCanceledException)
        {
            failure = "The galaxy search took too long to answer.";
        }
        finally
        {
            fetch.Dispose();
        }

        if (!ReferenceEquals(_nextPage, fetch))
        {
            return;
        }

        _nextPage = null;
        _pageFailed = failure is null ? null : new PageFailed(failure, DateTimeOffset.Now, page);
        ShowTail();
    }

    /// <summary>The answer head's count: "25 OF 1234 SYSTEMS" while the service has more, otherwise the rows drawn.</summary>
    private string CountText(GalaxySearchPosting posting)
    {
        var count = Count(posting);

        return posting.More && Counted(posting) ? $"{count} OF {GalaxyQuery.SayTotal(Total(posting))} {Noun}"
            : count == 1 ? $"1 {NounOne}"
            : $"{count} {Noun}";
    }

    /// <summary>Sets the form to a posting it has not shown yet, so typing survives the page being re-attached.</summary>
    private void FillFromLast()
    {
        if (_fromHistory is not null || _searches.Board.Last(Kind) is not { } last || ReferenceEquals(last.Arguments, _filledFrom?.Arguments))
        {
            return;
        }

        _filledFrom = last;
        FillForm(last.Arguments);
    }

    private GalaxySearchPosting? Latest => _fromHistory ?? _searches.Board.Last(Kind);

    /// <summary>What a redraw follows. A later page added to the answer keeps its arguments, so it is not a change here.</summary>
    private object Stamp() => (Latest?.Arguments, Latest?.AskedAt, _searches.FrontierId(), Followed);

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

        if (EditedSincePick())
        {
            ResetPick();
        }

        _showingAnswer ??= Latest is not null;

        var last = Latest;

        View = _running is not null ? GalaxySearchView.Running
            : _failure is { } failure ? failure.View
            : last is null ? GalaxySearchView.Unasked
            : Count(last) == 0 ? GalaxySearchView.NoResults
            : GalaxySearchView.Answer;

        Sentence = View switch
        {
            GalaxySearchView.Unavailable or GalaxySearchView.Refused => _failure!.Value.Text,
            GalaxySearchView.NoResults => Described(last!),
            _ => null,
        };

        _run.IsEnabled = _running is null;

        DrawStamp(last);
        _answer.Content = AnswerBlock(last);

        if (Content is null)
        {
            Content = _headset ? HeadsetLayout() : Layout();
        }

        DrawSaved();

        if (_formView is not null && _answerView is not null)
        {
            _formView.IsVisible = !_showingAnswer!.Value;
            _answerView.IsVisible = _showingAnswer.Value;
        }

        if (_scroll is not null)
        {
            CheckSoon();
        }
    }

    private Grid HeadsetLayout()
    {
        _form = FormWithSaved();

        var buttons = Buttons();
        var form = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Bottom);
        form.Children.Add(buttons);
        form.Children.Add(LoadoutPages.Scrolling(_form));

        var answer = new DockPanel();
        var footer = Footer();
        DockPanel.SetDock(footer, Dock.Bottom);
        answer.Children.Add(footer);
        answer.Children.Add(_answer);

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
        _form = FormWithSaved();

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

        right.Children.Add(_answer);

        var grid = new Grid { Margin = new Thickness(0, 16, 0, 0) };

        if (PaneWidths is not { } memory)
        {
            grid.ColumnDefinitions = new ColumnDefinitions("360,*");
            grid.ColumnSpacing = 28;
            grid.Children.Add(left);
            grid.Children.Add(right);
            Grid.SetColumn(right, 1);

            return grid;
        }

        grid.ColumnDefinitions.Add(new ColumnDefinition(FormWidth(memory.SearchFormWidth), GridUnitType.Pixel)
        {
            MinWidth = FormMinWidth,
            MaxWidth = FormMaxWidth,
        });
        grid.ColumnDefinitions.Add(new ColumnDefinition(SplitterWidth, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star) { MinWidth = AnswerMinWidth });

        var splitter = new GridSplitter
        {
            ResizeDirection = GridResizeDirection.Columns,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.SizeWestEast),
            Template = new FuncControlTemplate<GridSplitter>(
                (_, _) => new Border { Background = Brushes.Transparent }),
        };
        splitter.DragCompleted += (_, _) => memory.RememberSearchForm(grid.ColumnDefinitions[0].ActualWidth);

        var rule = new Border
        {
            Width = 1,
            HorizontalAlignment = HorizontalAlignment.Center,
            IsHitTestVisible = false,
        };
        rule.Bind(Border.BackgroundProperty, rule.GetResourceObservable("D47.Line2"));

        grid.Children.Add(left);
        grid.Children.Add(splitter);
        grid.Children.Add(rule);
        grid.Children.Add(right);
        Grid.SetColumn(splitter, 1);
        Grid.SetColumn(rule, 1);
        Grid.SetColumn(right, 2);

        return grid;
    }

    /// <summary>The form column's width for a remembered value: clamped to the limits, or the default when unusable.</summary>
    public static double FormWidth(double? remembered) =>
        remembered is { } width && double.IsFinite(width) && width > 0
            ? Math.Clamp(width, FormMinWidth, FormMaxWidth)
            : DefaultFormWidth;

    private StackPanel FormWithSaved()
    {
        _savedSection = SystemSearchPage.Group("Saved search", SystemSearchPage.Labelled("Start from", _startFrom), _forget);

        return new StackPanel { Spacing = 22, Children = { _savedSection, BuildForm() } };
    }

    private List<QuickSearch> QuickHere() => [.. QuickSearches.All.Where(search => search.Tool == Tool)];

    private List<SavedSearch> YoursHere() =>
        _searches.Saved is { } store ? [.. store.All().Where(search => search.Tool == Tool)] : [];

    private void DrawSaved()
    {
        var quick = QuickHere();
        var yours = YoursHere();

        if (_picked is not null && _pickedIsOwn && !yours.Any(search => search.Name == _picked))
        {
            ResetPick();
        }

        var entries = new List<InlinePickerEntry>();

        if (quick.Count > 0)
        {
            entries.Add(new InlinePickerHeading("Quick"));
            entries.AddRange(quick.Select(search => new InlinePickerOption(search.Name, search.Name, ThemeManager.WhiteKey)));
        }

        if (yours.Count > 0)
        {
            entries.Add(new InlinePickerHeading("Yours"));
            entries.AddRange(yours.Select(search => new InlinePickerOption(search.Name, search.Name, ThemeManager.WhiteKey)));
        }

        if (_savedSection is not null)
        {
            _savedSection.IsVisible = entries.Count > 0;
        }

        _startFrom.Show(entries, _picked ?? string.Empty, _picked ?? NoPickText, _picked is null ? ThemeManager.GreyKey : ThemeManager.WhiteKey, null);
        _startFrom.IsEnabled = _running is null;

        _forget.IsVisible = _picked is not null && _pickedIsOwn;
        Avalonia.Automation.AutomationProperties.SetName(_forget, $"Forget the {_picked} search");

        _said.Text = SayText();
    }

    private string SayText()
    {
        if (_picked is { } name)
        {
            var phrase = _pickedIsOwn
                ? SavedSearchPhrases.Of(name).First()
                : QuickSearches.PhrasesOf(QuickHere().First(search => search.Name == name)).First();
            return $"Say: “{phrase}”";
        }

        return SayPhrase is { } say ? $"Say: “{say}”"
            : QuickHere().FirstOrDefault() is { } first
                ? $"Say: “{QuickSearches.PhrasesOf(first).First()}”"
                : string.Empty;
    }

    private void StartFrom(string name)
    {
        IReadOnlyDictionary<string, string> arguments;
        bool own;

        if (QuickHere().FirstOrDefault(search => search.Name == name) is { } quick)
        {
            arguments = quick.Arguments;
            own = false;
        }
        else if (YoursHere().FirstOrDefault(search => search.Name == name) is { } mine)
        {
            arguments = mine.Arguments;
            own = true;
        }
        else
        {
            return;
        }

        _picked = name;
        _pickedIsOwn = own;
        FillForm(arguments);
        Follow();
        _pickedAt = FormArguments();
        _pickPosting = true;

        Draw();
        _ = RunPicked(arguments);
    }

    private async Task RunPicked(IReadOnlyDictionary<string, string> arguments)
    {
        await RunAsync(WithKeptSort(arguments)).ConfigureAwait(true);
        Dispatcher.UIThread.Post(() => _pickPosting = false);
    }

    private void ResetPick()
    {
        _picked = null;
        _pickedIsOwn = false;
        _pickedAt = null;
    }

    private bool EditedSincePick()
    {
        if (_picked is null || _pickedAt is not { } at)
        {
            return false;
        }

        var now = FormArguments();
        return now.Count != at.Count || now.Any(pair => !at.TryGetValue(pair.Key, out var value) || value != pair.Value);
    }

    private Control AnswerBlock(GalaxySearchPosting? last)
    {
        _rows = null;
        _scroll = null;
        _count = null;
        _orderChip = null;
        _filterWords = null;
        _filterLine = null;
        _drawn = null;
        _shownRows = 0;

        if (last is not null && !ReferenceEquals(_pageFor, last.Arguments))
        {
            _page = 0;
            _pageFor = last.Arguments;
        }

        var block = new StackPanel { Spacing = 14 };

        switch (View)
        {
            case GalaxySearchView.Unasked:
                break;

            case GalaxySearchView.Running:
                block.Children.Add(Head("Searching", Aside(_running!, null), refresh: null, save: false, disabled: true));

                block.Children.Add(LoadingMark());
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
                var paged = Paged(last!, _headset);
                var arranged = paged is null ? null : _arranged;

                var filtered = arranged is null ? null : FilterWords(last!);

                block.Children.Add(Head(
                    CountText(last!),
                    Aside(last!.Arguments, last.Reference),
                    Rerun(last),
                    save: true,
                    asked: Asked(last),
                    saved: last.Arguments,
                    sorted: OrderWords(last, filtered),
                    columns: arranged is null ? null : ColumnArranger.Tile(arranged),
                    filter: arranged is null ? null : ColumnFilter.Tile(arranged),
                    clear: arranged is null ? null : ColumnFilter.HeadClear(arranged),
                    filtered: filtered));

                if (arranged is not null && ColumnArranger.Bar(arranged) is { } bar)
                {
                    block.Children.Add(bar);
                }

                if (arranged is not null && ColumnFilter.Bar(arranged, Rows(last), search: true) is { } filterBar)
                {
                    block.Children.Add(filterBar);
                }

                AddNotes(block, last);

                if (paged is not null)
                {
                    return RowsBelow(block, paged, last, _headset ? null : arranged?.Layout);
                }

                if (Table(last, _headset) is { } table)
                {
                    block.Children.Add(table);
                }

                break;
        }

        return LoadoutPages.Scrolling(block);
    }

    /// <summary>
    /// The head above the answer's rows, which the desktop realises only as they scroll into view. Given
    /// <paramref name="sideways"/>, the column head and the rows scroll sideways together when the columns do not fit.
    /// </summary>
    private Grid RowsBelow(StackPanel head, PagedTable paged, GalaxySearchPosting posting, TableLayout? sideways)
    {
        var rows = RowsShown(posting);
        _rows = [.. Slice(rows)];
        _shownRows = rows.Count;
        _drawn = posting;
        ShowTail();

        var list = new ItemsControl
        {
            Name = RowsName,
            // The headset lays out without a layout manager, so a virtualising panel there never learns its viewport.
            ItemsPanel = new FuncTemplate<Avalonia.Controls.Panel?>(() => _headset ? new StackPanel() : new VirtualizingStackPanel()),
            ItemTemplate = new FuncDataTemplate<object>((item, _) => item switch
            {
                PageLoading => Spaced(LoadingMark()),
                PageFailed failed => Spaced(PageNotice(failed)),
                FilterEnd end => Spaced(FilterEndRow(end)),
                null => null,
                _ => Spaced(paged.Row(item)),
            }),
            ItemsSource = _rows,
            IsVisible = _arranged is not { } table || !ColumnFilter.HidesRows(table),
        };

        var grid = LoadoutPages.HeaderAndList(head, paged.Head, list, sideways, out var scroll);
        _scroll = scroll;
        scroll.ScrollChanged += (_, _) => CheckSoon();

        return grid;
    }

    /// <summary>
    /// "Rows 1–25 fetched · 20 hidden by the filter", or in the headset the pager row, with FETCH THE NEXT 25
    /// while the service has more; when every row is hidden, the line naming the filtered fields with CLEAR FILTERS.
    /// </summary>
    private Control FilterEndRow(FilterEnd end)
    {
        Button? fetch = null;

        if (end.More)
        {
            fetch = new Button
            {
                Name = FetchNextName,
                Content = $"FETCH THE NEXT {PageSize(_drawn).ToString(CultureInfo.InvariantCulture)}",
                Height = TypeScale.MinimumTarget,
                VerticalAlignment = VerticalAlignment.Center,
            };
            fetch.Click += (_, _) => FetchNext();
        }

        if (end.Hidden >= end.Fetched && _arranged is { } table && _drawn is { } drawn
            && ColumnFilter.Empty(table, Rows(drawn), search: true, beside: fetch) is { } empty)
        {
            return empty;
        }

        if (_headset)
        {
            return PagerRow(end, fetch);
        }

        var text = $"Rows 1–{end.Fetched.ToString(CultureInfo.InvariantCulture)} fetched · {end.Hidden.ToString(CultureInfo.InvariantCulture)} hidden by the filter";
        var line = Typeset.Mono(text, TypeScale.Small, ThemeManager.GreyKey, vertical: VerticalAlignment.Center);
        line.Name = FilterEndName;

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12, MinHeight = TypeScale.MinimumTarget, Children = { line } };

        if (fetch is not null)
        {
            Grid.SetColumn(fetch, 1);
            grid.Children.Add(fetch);
        }

        return grid;
    }

    /// <summary>The headset's pager: the readout, FETCH THE NEXT 25 while the service has more, then PREVIOUS and NEXT.</summary>
    private Grid PagerRow(FilterEnd end, Button? fetch)
    {
        var shown = end.Fetched - end.Hidden;
        var readout = FactionModel.PageReadout(_page, shown);

        if (end.Hidden > 0)
        {
            readout += $" shown · {end.Fetched.ToString(CultureInfo.InvariantCulture)} fetched";
        }

        var line = Typeset.Mono(readout, TypeScale.Small, ThemeManager.GreyKey, vertical: VerticalAlignment.Center);
        line.Name = FilterEndName;

        var previous = new Button { Name = PreviousPageName, Content = "◄ PREVIOUS", Height = TypeScale.MinimumTarget, IsEnabled = _page > 0 };
        var next = new Button
        {
            Name = NextPageName,
            Content = "NEXT ►",
            Height = TypeScale.MinimumTarget,
            IsEnabled = FactionModel.ClampPage(_page + 1, shown) > _page,
        };
        previous.Click += (_, _) => ShowPage(_page - 1);
        next.Click += (_, _) => ShowPage(_page + 1);

        var tiles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tile };

        if (fetch is not null)
        {
            tiles.Children.Add(fetch);
        }

        tiles.Children.Add(previous);
        tiles.Children.Add(next);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12, MinHeight = TypeScale.MinimumTarget, Children = { line } };
        Grid.SetColumn(tiles, 1);
        grid.Children.Add(tiles);

        return grid;
    }

    /// <summary>How many rows a page of the answer holds: its <c>limit</c>, else the service's default.</summary>
    private static int PageSize(GalaxySearchPosting? posting) =>
        posting?.Arguments.TryGetValue("limit", out var typed) == true
        && int.TryParse(typed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit)
        && limit is >= 1 and <= GalaxyQuery.MaxSize
            ? limit
            : GalaxyQuery.DefaultSize;

    /// <summary>A row with the gap the table keeps between rows.</summary>
    private static Border Spaced(Control row) => new() { Margin = new Thickness(0, 2, 0, Gaps.Tile - 2), Child = row };

    /// <summary>The sentence the wait mark says while a search runs.</summary>
    public const string RetrievingText = "Retrieving results…";

    /// <summary>The wait mark drawn while a search runs.</summary>
    private static WaitingState LoadingMark() => new(WaitForm.Body, RetrievingText);

    /// <summary>The failure of a later page, with RETRY asking for that page again.</summary>
    private Notice PageNotice(PageFailed failed)
    {
        var notice = new Notice(NoticeLevel.Error)
        {
            Label = "GALAXY SEARCH UNAVAILABLE",
            Text = failed.Text,
            Detail = failed.At.ToString("HH:mm", CultureInfo.InvariantCulture),
        };

        var retry = new Button { Name = RetryPageName, Content = "RETRY", Height = TypeScale.MinimumTarget };
        retry.Click += (_, _) =>
        {
            if (ReferenceEquals(_pageFailed, failed)
                && _searches.Board.Last(Kind) is { } last
                && last.Pages == failed.Page - 1
                && ReferenceEquals(last.Arguments, _drawn?.Arguments))
            {
                _ = NextPageAsync(last.Arguments, failed.Page);
            }
        };
        notice.Actions.Add(retry);

        return notice;
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
        string? sorted = null,
        Control? columns = null,
        Control? filter = null,
        Button? clear = null,
        string? filtered = null)
    {
        var name = Chrome(count, TypeScale.Section, ThemeManager.AKey);
        name.VerticalAlignment = VerticalAlignment.Center;
        _count = name;
        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center, Children = { name } };

        if (aside is { } words)
        {
            TextBlock Words(string text, string ink)
            {
                var block = new TextBlock
                {
                    Text = text.ToUpperInvariant(),
                    FontFamily = Fonts.ChromeFamily,
                    FontSize = TypeScale.Small,
                    FontWeight = FontWeight.Medium,
                    LetterSpacing = TypeScale.Small * Fonts.ChromeTracking,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                ThemeInk.Bind(block, TextBlock.ForegroundProperty, ink);
                return block;
            }

            var isSystem = !string.Equals(words.Reference, YourSystem, StringComparison.Ordinal);
            var line = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            line.Children.Add(Words(words.Lead, ThemeManager.AKey));
            line.Children.Add(CopyGlyph.LinkBeside(Words(words.Reference, ThemeManager.CyanKey), isSystem ? words.Reference : null, Copy));
            left.Children.Add(line);
        }

        if (sorted is not null || clear is not null)
        {
            // Kept, hidden, while the answer has a table, so a later page can show the words.
            var order = Chrome($"· {sorted}", TypeScale.Small, ThemeManager.AKey);
            order.Name = SortedName;
            _orderChip = order;
            order.VerticalAlignment = VerticalAlignment.Center;

            if (sorted is null)
            {
                order.Text = null;
                order.IsVisible = false;
            }

            left.Children.Add(order);
        }


        if (asked is not null)
        {
            var when = Chrome(asked, TypeScale.Small, ThemeManager.GreyKey);
            when.Name = AskedName;
            when.VerticalAlignment = VerticalAlignment.Center;
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
            var button = new Button { Name = RefreshName, Content = "REFRESH", Height = TypeScale.MinimumTarget, IsEnabled = !disabled };
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

        if (filter is not null)
        {
            tiles.Children.Add(filter);
        }

        if (columns is not null)
        {
            tiles.Children.Add(columns);
        }

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12, Children = { left, tiles } };
        Grid.SetColumn(tiles, 1);

        Control lines = row;

        if (clear is not null)
        {
            var shown = Typeset.Chrome(
                filtered?.ToUpperInvariant() ?? string.Empty, TypeScale.Small, ThemeManager.AKey,
                weight: FontWeight.Medium, tracking: TypeScale.Small * Fonts.ChromeTracking,
                wrap: true, vertical: VerticalAlignment.Center);
            shown.Name = FilterWordsName;
            clear.VerticalAlignment = VerticalAlignment.Center;

            var line = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                MinHeight = TypeScale.MinimumTarget,
                Margin = new Thickness(0, 6, 0, 0),
                IsVisible = filtered is not null,
                Children = { shown, clear },
            };

            _filterWords = shown;
            _filterLine = line;
            lines = new StackPanel { Children = { row, line } };
        }

        var rule = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 0, 0, 6), Child = lines };
        ThemeInk.Bind(rule, Border.BorderBrushProperty, ThemeManager.AKey);

        return rule;
    }

    private Border Footer()
    {
        ThemeInk.Bind(_said, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        _said.FontSize = TypeScale.Tip;
        _said.FontFamily = Fonts.ProseFamily;

        Grid.SetColumn(_stamp, 1);

        var border = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 10, 0, 0),
            Margin = new Thickness(0, 10, 0, 0),
            Child = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { _said, _stamp } },
        };
        ThemeInk.Bind(border, Border.BorderBrushProperty, ThemeManager.Line2Key);

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

        ThemeInk.Bind(block, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
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
        ThemeInk.Bind(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    protected static TextBlock Mono(string text, double size, string key)
    {
        var block = new TextBlock { Text = text, FontFamily = new FontFamily(Fonts.MonoFamily), FontSize = size };
        ThemeInk.Bind(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    protected static TextBlock Prose(string text, double size, string key)
    {
        var block = new TextBlock { Text = text, FontFamily = Fonts.ProseFamily, FontSize = size, TextWrapping = TextWrapping.Wrap };
        ThemeInk.Bind(block, TextBlock.ForegroundProperty, key);
        return block;
    }
}

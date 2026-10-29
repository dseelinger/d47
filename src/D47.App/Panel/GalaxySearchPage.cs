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

    public const string AskedName = "AskedAgo";

    public const string SortedName = "SortedBy";

    public const string OpenSettingName = "OpenTheSetting";

    public const string ForgetName = "ForgetSearch";

    /// <summary>What the START FROM button shows while no saved or quick search is picked.</summary>
    public const string NoPickText = "Choose a saved search";

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

    protected GalaxySearchPage(GalaxySearchSurface searches, Func<bool> lookupsEnabled, Action? openSettings, bool headset, Func<bool>? namesEnabled = null)
    {
        _searches = searches;
        _lookupsEnabled = lookupsEnabled;
        _namesEnabled = namesEnabled ?? lookupsEnabled;
        _openSettings = openSettings;
        _headset = headset;
        _history = new HistoryTile(searches);

        _run.Click += (_, _) => _ = RunAsync(FormArguments());
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

    public event EventHandler? SummaryChanged;

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

        FillForm(posting.Arguments);
        _showingAnswer = true;
        Draw();
    }

    /// <summary>The search tool this page runs.</summary>
    public abstract string Tool { get; }

    /// <summary>The kind of answer on the board this page draws.</summary>
    public abstract GalaxySearchKind Kind { get; }

    public abstract string Summary { get; }

    /// <summary>Where the form width is remembered; null where the host gives the form a fixed width.</summary>
    public PaneWidthMemory? PaneWidths { get; init; }

    public ColumnWidthMemory? ColumnWidths { get; init; }

    /// <summary>A table layout for an answer; the headset draws it with no drag handles.</summary>
    protected TableLayout NewTable(string id, IReadOnlyList<TableColumn> columns) =>
        new(id, columns, _headset ? null : ColumnWidths, 12);

    /// <summary>What is drawn where the answer goes.</summary>
    public GalaxySearchView View { get; private set; }

    /// <summary>The sentence drawn for the current state, where it has one.</summary>
    public string? Sentence { get; private set; }

    /// <summary>Whether the headset draws the answer view rather than the form view.</summary>
    public bool ShowsAnswer => _showingAnswer ?? false;

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
    protected virtual string OffTip => LookupsOffTip;

    /// <summary>The RUN SEARCH tooltip while “Look things up in the galaxy” is off.</summary>
    public const string LookupsOffTip = "Turn on “Look things up in the galaxy” in Settings to search.";

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

        if (EditedSincePick())
        {
            ResetPick();
            Draw();
            return true;
        }

        if (Equals(Stamp(), _seen))
        {
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
        _ = RunAsync(arguments);
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

    /// <summary>Adds <paramref name="value"/>, trimmed, to <paramref name="arguments"/> unless it is blank.</summary>
    protected static void Put(Dictionary<string, string> arguments, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            arguments[key] = value.Trim();
        }
    }

    /// <summary>Adds <c>near</c> and, when it is not the default, <c>max_distance</c>.</summary>
    protected static void PutNear(Dictionary<string, string> arguments, TextBox near, Amount distance, int defaultDistance)
    {
        Put(arguments, "near", near.Text);

        if (distance.Value is { } value && value != defaultDistance)
        {
            arguments["max_distance"] = value.ToString("0", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Adds <c>limit</c> when it is not the default.</summary>
    private protected static void PutLimit(Dictionary<string, string> arguments, ShowUpTo limit, int defaultLimit)
    {
        if (limit.Value is { } value && value != defaultLimit)
        {
            arguments["limit"] = value.ToString("0", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Sets <paramref name="distance"/> from <c>max_distance</c>, clamped to <paramref name="ceiling"/>, when it is a number.</summary>
    protected static void FillDistance(IReadOnlyDictionary<string, string> arguments, Amount distance, double ceiling = 99999)
    {
        if (arguments.TryGetValue("max_distance", out var typed)
            && double.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out var asked))
        {
            distance.Value = (decimal)Math.Clamp(asked, 1, ceiling);
        }
    }

    /// <summary>Sets <paramref name="limit"/> from <c>limit</c>, clamped, when it is a number.</summary>
    private protected static void FillLimit(IReadOnlyDictionary<string, string> arguments, ShowUpTo limit)
    {
        if (arguments.TryGetValue("limit", out var typed)
            && int.TryParse(typed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var asked))
        {
            limit.Value = Math.Clamp(asked, 1, ShowUpTo.Max);
        }
    }

    /// <summary>The “WITHIN n LY OF ” aside, from <c>max_distance</c> or <paramref name="defaultDistance"/>.</summary>
    protected static (string Lead, string Reference) WithinAside(IReadOnlyDictionary<string, string> arguments, string? reference, int defaultDistance)
    {
        var within = arguments.TryGetValue("max_distance", out var typed)
                     && double.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out var asked)
            ? asked
            : defaultDistance;

        return ($"WITHIN {within.ToString("0.##", CultureInfo.InvariantCulture)} LY OF ", AsideFrom(arguments, reference));
    }

    /// <summary>The reference an aside measures from: the resolved one, the typed <c>near</c>, or “your system”.</summary>
    protected static string AsideFrom(IReadOnlyDictionary<string, string> arguments, string? reference) =>
        reference
        ?? (arguments.TryGetValue("near", out var near) && near.Trim().Length > 0 ? near.Trim() : null)
        ?? "your system";

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
    protected Control SearchHead(GalaxySearchPosting posting, string name, TableLayout layout, IReadOnlyList<SearchColumn> columns)
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

        var grid = layout.Header(head, [.. columns.Select(column => column.Right ? HorizontalAlignment.Right : HorizontalAlignment.Left)]);
        grid.Margin = new Thickness(12, 0, 12, 6);

        var rule = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Child = grid };
        ThemeInk.Bind(rule, Border.BorderBrushProperty, ThemeManager.LineKey);

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

        if (EditedSincePick())
        {
            ResetPick();
        }

        _showingAnswer ??= Latest is not null;

        var lookups = _lookupsEnabled();
        var last = Latest;

        View = !lookups ? GalaxySearchView.LookupsOff
            : _running is not null ? GalaxySearchView.Running
            : _failure is { } failure ? failure.View
            : last is null ? GalaxySearchView.Unasked
            : Count(last) == 0 ? GalaxySearchView.NoResults
            : GalaxySearchView.Answer;

        Sentence = View switch
        {
            GalaxySearchView.LookupsOff => OffSentence,
            GalaxySearchView.Unavailable or GalaxySearchView.Refused => _failure!.Value.Text,
            GalaxySearchView.NoResults => Described(last!),
            _ => null,
        };

        _run.IsEnabled = lookups && _running is null;
        ToolTip.SetTip(_run, lookups ? null : OffTip);

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

        right.Children.Add(LoadoutPages.Scrolling(_answer));

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
        _startFrom.IsEnabled = _lookupsEnabled() && _running is null;

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
        await RunAsync(arguments).ConfigureAwait(true);
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
                ThemeInk.Bind(asking, Border.BackgroundProperty, ThemeManager.SlabKey);
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
            ThemeInk.Bind(line, TextBlock.ForegroundProperty, ThemeManager.AKey);
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

    protected void RaiseSummaryChanged() => SummaryChanged?.Invoke(this, EventArgs.Empty);
}

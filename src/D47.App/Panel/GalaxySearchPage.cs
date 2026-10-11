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

    /// <summary>How long typing has to stop before names are suggested.</summary>
    public TimeSpan SuggestionPause { get; init; } = SystemNameSuggester.DefaultPause;
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
/// the answer and the saved searches only.
/// </summary>
public abstract class GalaxySearchPage : UserControl, IPageSummary, IPageChrome
{
    public const string LookupsOffText =
        "Turn on “Look things up in the galaxy” in Settings and D47 will search Spansh for star systems, stations and bodies.";

    public const string RunName = "RunSearch";

    public const string ClearName = "ClearSearch";

    public const string RefreshName = "RefreshSearch";

    public const string SaveName = "SaveSearch";

    public const string RetryName = "RetrySearch";

    public const string AskedName = "AskedAgo";

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
    private readonly Action? _openSettings;
    private readonly bool _headset;

    private readonly Button _run = new() { Name = RunName, Content = "RUN SEARCH", Height = TypeScale.MinimumTarget, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Button _clear = new() { Name = ClearName, Content = "CLEAR", Height = TypeScale.MinimumTarget };
    private readonly ContentControl _answer = new();
    private readonly ContentControl _saved = new();
    private readonly TextBlock _stamp = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };

    private Control? _form;
    private CancellationTokenSource? _inFlight;
    private IReadOnlyDictionary<string, string>? _running;
    private (GalaxySearchView View, string Text, DateTimeOffset At, IReadOnlyDictionary<string, string> Arguments)? _failure;
    private object? _seen;
    private GalaxySearchPosting? _filledFrom;
    private readonly HistoryTile _history;
    private GalaxySearchPosting? _fromHistory;

    protected GalaxySearchPage(GalaxySearchSurface searches, Func<bool> lookupsEnabled, Action? openSettings, bool headset)
    {
        _searches = searches;
        _lookupsEnabled = lookupsEnabled;
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

        if (!_headset)
        {
            FillForm(posting.Arguments);
        }

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

    /// <summary>The answer drawn, or null while none is.</summary>
    public GalaxySearchPosting? Shown { get; private set; }

    /// <summary>Whether RUN SEARCH can be pressed.</summary>
    public bool CanRun => _run.IsEnabled;

    /// <summary>The plural noun the answer head counts in, upper case.</summary>
    protected abstract string Noun { get; }

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
    protected SystemNameSuggestions? SuggestNames(TextBox field)
    {
        if (_searches.PrefixNames is not { } lookup)
        {
            return null;
        }

        SystemNameSuggestions? names = null;
        names = new SystemNameSuggestions(field, lookup, _lookupsEnabled, match => names!.SetText(match.Name), _searches.SuggestionPause);

        return names;
    }

    /// <summary>The field with its suggestions listed under it.</summary>
    protected static Control Beneath(TextBox field, SystemNameSuggestions? names) =>
        names is null ? field : new StackPanel { Spacing = 2, Children = { field, names.List } };

    /// <summary>Sets the field's text without listing names for it.</summary>
    protected static void Fill(TextBox field, SystemNameSuggestions? names, string text)
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
            FillFromLast();
            Draw();
        });
    }

    private void OnSavedChanged() => Dispatcher.UIThread.Post(Draw);

    /// <summary>Sets the form to a posting it has not shown yet, so typing survives the page being re-attached.</summary>
    private void FillFromLast()
    {
        if (_headset || _fromHistory is not null || _searches.Board.Last(Kind) is not { } last || ReferenceEquals(last, _filledFrom))
        {
            return;
        }

        _filledFrom = last;
        FillForm(last.Arguments);
    }

    private GalaxySearchPosting? Latest => _fromHistory ?? _searches.Board.Last(Kind);

    private object Stamp() => (_lookupsEnabled(), Latest, _searches.FrontierId(), Followed);

    private void Draw()
    {
        _seen = Stamp();
        Follow();

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
            GalaxySearchView.LookupsOff => LookupsOffText,
            GalaxySearchView.Unavailable or GalaxySearchView.Refused => _failure!.Value.Text,
            GalaxySearchView.NoResults => Described(last!),
            _ => null,
        };

        _run.IsEnabled = lookups && _running is null;
        ToolTip.SetTip(_run, lookups ? null : "Turn on “Look things up in the galaxy” in Settings to search.");

        _saved.Content = SavedBlock();
        DrawStamp(last);
        _answer.Content = AnswerBlock(last);

        Content ??= _headset
            ? LoadoutPages.Scrolling(new StackPanel { Spacing = 14, Children = { _saved, _answer } })
            : Layout();
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

        var buttons = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = Gaps.Tile,
            Margin = new Thickness(0, 12, 0, 0),
            Children = { _run, _clear },
        };
        Grid.SetColumn(_clear, 1);

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
        var fid = _searches.FrontierId();
        var yours = _searches.Saved is { } store && fid.Length > 0
            ? store.For(fid).Where(search => search.Tool == Tool).ToList()
            : [];

        var block = new StackPanel { Spacing = 8 };

        if (_headset)
        {
            var row = new WrapPanel { ItemSpacing = Gaps.Tile, LineSpacing = Gaps.Tile };
            row.Children.Add(Label("Saved", width: null));

            if (quick.Count == 0)
            {
                row.Children.Add(NoQuick());
            }

            foreach (var search in quick)
            {
                row.Children.Add(SavedTile(search.Name, search.Arguments));
            }

            foreach (var search in yours)
            {
                row.Children.Add(SavedTile(search.Name, search.Arguments));
            }

            block.Children.Add(row);
            return block;
        }

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
                forget.Click += (_, _) => Forget(fid, search.Name);

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
                block.Children.Add(Prose(LookupsOffText, TypeScale.Secondary, ThemeManager.WhiteKey));

                if (_openSettings is { } open)
                {
                    var setting = new Button { Name = OpenSettingName, Content = "OPEN THE SETTING", Height = TypeScale.MinimumTarget, HorizontalAlignment = HorizontalAlignment.Left };
                    setting.Click += (_, _) => open();
                    block.Children.Add(setting);
                }

                break;

            case GalaxySearchView.Running:
                block.Children.Add(Head("Searching", Aside(_running!, null), refresh: null, save: false, disabled: true));

                var asking = new Border { MinHeight = 50, Padding = new Thickness(14, 0), Child = Label("Asking Spansh", width: null) };
                Themed(asking, Border.BackgroundProperty, ThemeManager.SlabKey);
                block.Children.Add(asking);
                break;

            case GalaxySearchView.Unavailable:
                block.Children.Add(Head(Noun, null, refresh: null, save: false));

                var unavailable = new Notice(NoticeLevel.Error)
                {
                    Label = "SPANSH UNAVAILABLE",
                    Text = Sentence,
                    Detail = $"{D47.Knowledge.SpanshGalaxyService.Host.ToUpperInvariant()} · {_failure!.Value.At.ToString("HH:mm", CultureInfo.InvariantCulture)}",
                };

                var again = _failure.Value.Arguments;
                var retry = new Button { Name = RetryName, Content = "RETRY", Height = TypeScale.MinimumTarget };
                retry.Click += (_, _) => _ = RunAsync(again);
                unavailable.Actions.Add(retry);
                block.Children.Add(unavailable);
                break;

            case GalaxySearchView.Refused:
                block.Children.Add(new Notice(NoticeLevel.Error) { Label = "FILTER REFUSED", Text = Sentence });
                break;

            case GalaxySearchView.NoResults:
                block.Children.Add(Head($"0 {Noun}", Aside(last!.Arguments, last.Reference), Rerun(last), save: true, asked: Asked(last), saved: last.Arguments));
                AddNotes(block, last);
                block.Children.Add(Prose(Sentence!, TypeScale.Secondary, ThemeManager.WhiteKey));
                break;

            case GalaxySearchView.Answer:
                block.Children.Add(Head($"{Count(last!)} {Noun}", Aside(last!.Arguments, last.Reference), Rerun(last), save: true, asked: Asked(last), saved: last.Arguments));
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
        posting.Notes.Length > 0 && posting.Answer.StartsWith(posting.Notes, StringComparison.Ordinal)
            ? posting.Answer[posting.Notes.Length..].Trim()
            : posting.Answer;

    /// <summary>The arguments REFRESH and SAVE use: a history entry's <c>near</c> is its stored reference when left out.</summary>
    private IReadOnlyDictionary<string, string> Rerun(GalaxySearchPosting posting) =>
        ReferenceEquals(posting, _fromHistory)
            ? SearchHistoryPicker.Rerun(posting.Arguments, posting.Reference)
            : posting.Arguments;

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
        IReadOnlyDictionary<string, string>? saved = null)
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

        if (asked is not null)
        {
            var when = Chrome(asked, TypeScale.Small, ThemeManager.GreyKey);
            when.Name = AskedName;
            when.VerticalAlignment = VerticalAlignment.Bottom;
            left.Children.Add(when);
        }

        var tiles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tile, HorizontalAlignment = HorizontalAlignment.Right };

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

        if (!_headset && (save || disabled))
        {
            var fid = _searches.FrontierId();
            var posting = saved ?? refresh;
            var button = new Button
            {
                Name = SaveName,
                Content = "SAVE THIS SEARCH",
                Height = TypeScale.MinimumTarget,
                IsEnabled = !disabled && posting is not null && _searches.Saved is not null && fid.Length > 0,
            };
            button.Click += (_, _) =>
            {
                if (posting is not null)
                {
                    Save(fid, posting);
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
        var phrase = QuickSearches.All.FirstOrDefault(search => search.Tool == Tool) is { } quick
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

        var words = new Run("SPANSH · ANSWERED ") { FontFamily = Fonts.ChromeFamily, FontSize = TypeScale.Meta, FontWeight = FontWeight.Medium };
        words.Bind(TextElement.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(ThemeManager.GreyKey));

        var time = new Run(last.AskedAt.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture)) { FontFamily = new FontFamily(Fonts.MonoFamily), FontSize = TypeScale.Meta };
        time.Bind(TextElement.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(ThemeManager.CyanKey));

        _stamp.Inlines.Add(words);
        _stamp.Inlines.Add(time);
    }

    private void Save(string frontierId, IReadOnlyDictionary<string, string> arguments)
    {
        if (_searches.Saved is not { } store || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var dialog = new SaveSearchDialog(
            Noun,
            Tool,
            arguments,
            name => store.Add(frontierId, name, Tool, arguments, DateTimeOffset.UtcNow, _searches.PhrasesTaken()));

        _ = dialog.Over(owner);
    }

    private void Forget(string frontierId, string name)
    {
        if (_searches.Saved is not { } store || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var dialog = new ForgetSearchDialog(Noun, name, () => store.Forget(frontierId, name));

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

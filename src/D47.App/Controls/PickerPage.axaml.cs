using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.Core.Capabilities;

namespace D47.App.Controls;

/// <summary>What the picker was asked to choose between.</summary>
public sealed record PickerRequest
{
    public required string Prompt { get; init; }

    public string? Help { get; init; }

    public IReadOnlyList<string> Choices { get; init; } = [];

    /// <summary>How a choice is written for a person.</summary>
    public Func<string, string>? Describe { get; init; }

    /// <summary>What is selected now, so cancelling and keeping are the same thing.</summary>
    public string? Current { get; init; }

    /// <summary>Shown when nothing has been chosen.</summary>
    public string? DefaultDisplay { get; init; }

    /// <summary>Whether a value outside <see cref="Choices"/> may be typed.</summary>
    public bool AllowsFreeText { get; init; }

    /// <summary>
    /// Why <see cref="Choices"/> is empty, when the row knows and the generic wording would be wrong.
    /// </summary>
    public string? WhyEmpty { get; init; }

    /// <summary>How to play the highlighted value, when the row offers that.</summary>
    public PickerAudition? Audition { get; init; }

    /// <summary>
    /// A structured filter offered beside the search box, or null where the choices carry no property
    /// worth filtering on (#146).
    /// </summary>
    public SettingFacet? Facet { get; init; }

    /// <summary>Facets that combine; used in place of <see cref="Facet"/> when there is more than one.</summary>
    public IReadOnlyList<SettingFacet>? Facets { get; init; }

    /// <summary>What the count under several facets counts. "voices".</summary>
    public string ChoiceNoun { get; init; } = "choices";

    /// <summary>
    /// Ticks any number of rows and returns them in <see cref="PickerResult.Values"/>.
    /// <see cref="AllowsFreeText"/> and <see cref="Audition"/> are ignored.
    /// </summary>
    public bool Several { get; init; }

    /// <summary>The values ticked when a <see cref="Several"/> picker opens.</summary>
    public IReadOnlyCollection<string>? CurrentSeveral { get; init; }

    /// <summary>The status line shown beside a choice, or null for none.</summary>
    public Func<string, ChoiceStatus?>? Status { get; init; }

    /// <summary>
    /// Clip state, detail line and fetch for a Chatterbox voice list; splits it into what is on this PC and what
    /// is fetched when played or chosen.
    /// </summary>
    public SettingChoiceClips? Clips { get; init; }

    /// <summary>What the accept button says.</summary>
    public string AcceptText { get; init; } = "Use this";
}

/// <summary>Hearing a value before choosing it.</summary>
public sealed record PickerAudition
{
    /// <summary>Plays one value.</summary>
    public required Func<string, CancellationToken, Task> Play { get; init; }

    /// <summary>What a press costs, stated once above the list.</summary>
    public required string Cost { get; init; }

    /// <summary>Why nothing here can be played, or null when it can.</summary>
    public string? Unavailable { get; init; }

    /// <summary>Plays one value's free sample, where it has one (#106).</summary>
    public Func<string, CancellationToken, Task>? Preview { get; init; }

    /// <summary>Whether one value has a sample for <see cref="Preview"/>.</summary>
    public Func<string, bool>? HasPreview { get; init; }

    /// <summary>What one press of <see cref="Play"/> costs, or null where <see cref="Cost"/> says it.</summary>
    public string? LineCost { get; init; }
}

/// <summary>
/// The chosen value, where null means "clear this and use the default". A <see cref="PickerRequest.Several"/>
/// picker answers in <paramref name="Values"/>, in <see cref="PickerRequest.Choices"/> order, empty for the default.
/// </summary>
public sealed record PickerResult(string? Value, IReadOnlyList<string>? Values = null);

/// <summary>
/// One line of the list: what it is called, what it really is, and — where the row offers an audition —
/// the control that plays it (change-requests.md 18).
/// </summary>
public sealed class PickerChoice : INotifyPropertyChanged
{
    /// <summary>A right-pointing triangle, and a square.</summary>
    private static readonly Geometry Play = Geometry.Parse("M 8,5 L 19,12 L 8,19 Z");

    private static readonly Geometry Stop = Geometry.Parse("M 6,6 L 18,6 L 18,18 L 6,18 Z");

    /// <summary>A speech bubble, for the line the voice would actually say.</summary>
    private static readonly Geometry Speak = Geometry.Parse("M 4,5 L 20,5 L 20,15 L 11,15 L 6,19 L 6,15 L 4,15 Z");

    private bool _playing;

    private bool _playingLine;

    private bool _ticked;

    private ChoiceClip? _clip;

    private string? _head;

    private string? _headCount;

    /// <summary>What choosing this row writes to settings — an id, not the words above it.</summary>
    public required string Value { get; init; }

    /// <summary>What the row says, which is the row's own <c>Describe</c> applied to the value.</summary>
    public required string Text { get; init; }

    /// <summary>Whether this list offers auditions at all.</summary>
    public required bool CanPlay { get; init; }

    /// <summary>And whether one can be played right now — false where the provider cannot speak.</summary>
    public required bool Playable { get; init; }

    /// <summary>What a press costs, or why it cannot be pressed — null for a free sample, which costs nothing.</summary>
    public string? Why { get; init; }

    /// <summary>
    /// Whether the play glyph plays a free sample, which puts the paid line on a second glyph beside it
    /// (#106).
    /// </summary>
    public bool HasSample { get; init; }

    /// <summary>What the second glyph's press costs, or why it cannot be pressed.</summary>
    public string? LineWhy { get; init; }

    /// <summary>The status text a row's own status gives, or null.</summary>
    public string? StatusText { get; init; }

    /// <summary>The tone of <see cref="StatusText"/>.</summary>
    public ChoiceTone StatusTone { get; init; }

    /// <summary>The second line under the label, or null.</summary>
    public string? Detail { get; init; }

    /// <summary>The clip line of a Chatterbox voice, or null on a list that has none.</summary>
    public ChoiceClip? Clip
    {
        get => _clip;
        set
        {
            _clip = value;
            Raise(nameof(Clip));
            Raise(nameof(Status));
            Raise(nameof(StatusIsYellow));
            Raise(nameof(StatusIsWhite));
            Raise(nameof(StatusIsRed));
            Raise(nameof(Size));
            Raise(nameof(CanRetry));
        }
    }

    /// <summary>The status text beside the label: the clip's state word in capitals, else <see cref="StatusText"/>.</summary>
    public string? Status => _clip?.State.Text.ToUpperInvariant() ?? StatusText;

    /// <summary>The clip's size, or null.</summary>
    public string? Size => _clip?.Size;

    /// <summary>Whether the row offers Retry.</summary>
    public bool CanRetry => _clip?.CanRetry == true;

    /// <summary>Whether the voice plays without a fetch.</summary>
    public bool OnThisPc => _clip?.OnThisPc == true;

    /// <summary>The tone <see cref="Status"/> is drawn in.</summary>
    public ChoiceTone Tone => _clip?.State.Tone ?? StatusTone;

    public bool StatusIsYellow => Tone == ChoiceTone.Yellow;

    public bool StatusIsWhite => Tone == ChoiceTone.White;

    public bool StatusIsRed => Tone == ChoiceTone.Red;

    /// <summary>The row uses this voice now.</summary>
    public bool InUse { get; init; }

    /// <summary>The role this shipped voice is suggested for, or null.</summary>
    public string? SuggestedFor { get; init; }

    /// <summary>The tag text: <c>Suggested for</c> and the role, or null.</summary>
    public string? Suggestion => SuggestedFor is null ? null : $"SUGGESTED FOR {SuggestedFor.ToUpperInvariant()}";

    /// <summary>The head drawn above this row, when it is the first of its group.</summary>
    public string? Head
    {
        get => _head;
        set
        {
            if (_head == value)
            {
                return;
            }

            _head = value;
            Raise(nameof(Head));
        }
    }

    /// <summary>The group's count, drawn beside <see cref="Head"/>.</summary>
    public string? HeadCount
    {
        get => _headCount;
        set
        {
            if (_headCount == value)
            {
                return;
            }

            _headCount = value;
            Raise(nameof(HeadCount));
        }
    }

    /// <summary>The tooltip: the unavailable reason while shut, the action and its cost while live (#383).</summary>
    public string? Tip => Playable ? Why is { Length: > 0 } cost ? $"{ActionName}. {cost}" : ActionName : Why;

    /// <summary>The second glyph's tooltip, on the same terms as <see cref="Tip"/>.</summary>
    public string? LineTip =>
        Playable ? LineWhy is { Length: > 0 } cost ? $"{LineActionName}. {cost}" : LineActionName : LineWhy;

    /// <summary>Whether a several-at-once picker has this row ticked.</summary>
    public bool Ticked
    {
        get => _ticked;
        set
        {
            if (_ticked == value)
            {
                return;
            }

            _ticked = value;
            Raise(nameof(Ticked));
        }
    }

    public bool PlayingLine
    {
        get => _playingLine;
        set
        {
            if (_playingLine == value)
            {
                return;
            }

            _playingLine = value;
            Raise(nameof(PlayingLine));
            Raise(nameof(LineGlyph));
            Raise(nameof(LineActionName));
            Raise(nameof(LineTip));
        }
    }

    public bool Playing
    {
        get => _playing;
        set
        {
            if (_playing == value)
            {
                return;
            }

            _playing = value;
            Raise(nameof(Playing));
            Raise(nameof(Glyph));
            Raise(nameof(ActionName));
            Raise(nameof(Tip));
        }
    }

    /// <summary>Play, or stop while this row is the one talking.</summary>
    public Geometry Glyph => _playing ? Stop : Play;

    /// <summary>What the glyph is for, in words.</summary>
    public string ActionName => _playing ? $"Stop {Text}" : $"Play {Text}";

    /// <summary>Say the line, or stop while this row's line is the one talking.</summary>
    public Geometry LineGlyph => _playingLine ? Stop : Speak;

    /// <summary>What the second glyph is for, in words.</summary>
    public string LineActionName => _playingLine ? $"Stop {Text}" : $"Say the line in {Text}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// The searchable picker every settings Choice row opens, drawn as a page of the panel so the window and
/// the headset both show it.
/// </summary>
public partial class PickerPage : UserControl
{
    private PickerRequest _request = new() { Prompt = "Choose" };

    private Action<PickerResult> _chosen = _ => { };

    private Action _cancelled = () => { };

    /// <summary>Every choice, built once when the picker is bound.</summary>
    private IReadOnlyList<PickerChoice> _all = [];

    /// <summary>The rows currently listed, in the order they are drawn.</summary>
    private IReadOnlyList<PickerChoice> _visible = [];

    /// <summary>Whether the page has answered, so it answers once.</summary>
    private bool _answered;

    /// <summary>The audition in flight, so the next press can drop it.</summary>
    private CancellationTokenSource? _auditioning;

    /// <summary>Ends the subscription to clip changes.</summary>
    private Action? _unwatch;

    public PickerPage()
    {
        InitializeComponent();

        // Esc anywhere on the page leaves it, as it does from the filter and the list.
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && !e.Handled)
            {
                e.Handled = true;
                Cancel();
            }
        };
    }

    /// <summary>A bound picker that calls back once: with a result when one is taken, or cancelled.</summary>
    /// <param name="onListed">Called once the page is in a tree with its list built.</param>
    public static PickerPage For(
        PickerRequest request,
        Action<PickerResult>? chosen = null,
        Action? cancelled = null,
        Action? onListed = null)
    {
        var picker = new PickerPage
        {
            _request = request,
            _chosen = chosen ?? (_ => { }),
            _cancelled = cancelled ?? (() => { }),
        };

        picker.Bind(onListed);

        return picker;
    }

    private void Bind(Action? onListed)
    {
        AutomationProperties.SetName(this, _request.Prompt);
        AutomationProperties.SetName(Choices, _request.Prompt);

        HelpText.Text = _request.Help ?? string.Empty;
        HelpText.IsVisible = !string.IsNullOrWhiteSpace(_request.Help);

        FilterBox.Text = string.Empty;
        FilterBox.PlaceholderText = _request.AllowsFreeText
            ? "Type to filter, or type a value of your own"
            : "Type to filter";

        BindFacets();

        DefaultButton.IsVisible = _request.DefaultDisplay is not null;
        AcceptButton.Content = _request.AcceptText;

        // Bracketed here because the row hands over the bare phrase (SettingRow.BareDefaultFor).
        var useDefault = _request.Several
            ? _request.DefaultDisplay
            : $"Use the default ({_request.DefaultDisplay})";

        DefaultButtonText.Text = useDefault;
        ToolTip.SetTip(DefaultButton, useDefault);

        // Shut, it says why nothing can be played; live, what a press costs.
        var auditioned = _request.Several ? null : _request.Audition;

        if (auditioned is { } audition)
        {
            AuditionNote.IsVisible = true;
            AuditionNote.Text = audition.Unavailable ?? audition.Cost;
        }

        _all = [.. _request.Choices.Select(value =>
        {
            var sample = auditioned is { Preview: not null, HasPreview: { } has } && has(value);

            return new PickerChoice
            {
                Value = value,
                Text = Label(value),
                StatusText = _request.Status?.Invoke(value)?.Text.ToUpperInvariant(),
                StatusTone = _request.Status?.Invoke(value)?.Tone ?? ChoiceTone.Grey,
                Detail = _request.Clips?.Detail?.Invoke(value),
                Clip = _request.Clips?.Clip(value),
                InUse = !_request.Several && string.Equals(value, _request.Current, StringComparison.OrdinalIgnoreCase),
                SuggestedFor = _request.Clips?.Suggested is { } suggested
                    && string.Equals(suggested.Voice, value, StringComparison.OrdinalIgnoreCase)
                        ? suggested.Role
                        : null,
                CanPlay = auditioned is not null,
                Playable = auditioned is { Unavailable: null },
                HasSample = sample,
                Why = auditioned is { } offered
                    ? offered.Unavailable ?? (sample ? null : offered.LineCost ?? offered.Cost)
                    : null,
                LineWhy = auditioned is { } paid ? paid.Unavailable ?? paid.LineCost ?? paid.Cost : null,
            };
        })];

        if (_request.Several)
        {
            BindSeveral();
        }

        ApplyFilter();

        // The current value is highlighted, so Enter with no typing keeps it.
        Choices.SelectedIndex = _request.Several || _request.Current is null
            ? -1
            : Array.FindIndex(
                [.. _visible],
                choice => string.Equals(choice.Value, _request.Current, StringComparison.OrdinalIgnoreCase));

        var listed = false;

        AttachedToVisualTree += (_, _) =>
        {
            _unwatch?.Invoke();
            _unwatch = _request.Clips?.Watch?.Invoke(OnClipChanged);

            // The headset lays out a window that is never shown, where a virtualizing panel is never told its
            // viewport and realizes no rows.
            if (this.FindAncestorOfType<Panel.PanelView>()?.Classes.Contains("headset") == true)
            {
                Choices.ItemsPanel = new FuncTemplate<Avalonia.Controls.Panel?>(() => new StackPanel());
            }

            // Posted, so focus and scrolling run after the page under it has finished building.
            Dispatcher.UIThread.Post(
                () =>
                {
                    if (Choices.SelectedIndex >= 0)
                    {
                        Choices.ScrollIntoView(Choices.SelectedIndex);
                    }

                    FilterBox.Focus();

                    if (!listed)
                    {
                        listed = true;
                        onListed?.Invoke();
                    }
                },
                DispatcherPriority.Loaded);
        };

        // Leaving the page stops whatever it was playing.
        DetachedFromVisualTree += (_, _) =>
        {
            _auditioning?.Cancel();
            _unwatch?.Invoke();
            _unwatch = null;
        };
    }

    /// <summary>Rows become tick boxes; ticking chooses nothing until USE THESE.</summary>
    private void BindSeveral()
    {
        var current = new HashSet<string>(_request.CurrentSeveral ?? [], StringComparer.OrdinalIgnoreCase);

        foreach (var row in _all)
        {
            row.Ticked = current.Contains(row.Value);
            row.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PickerChoice.Ticked))
                {
                    ShowWhetherAnythingCanBeTaken();
                }
            };
        }

        Choices.SelectionMode = SelectionMode.Single;
        Choices.ItemTemplate = new FuncDataTemplate<PickerChoice>((row, _) =>
        {
            var (box, _) = LabeledCheckBox.Build(row?.Text ?? string.Empty);

            box.MinHeight = 44;
            box.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            box.IsChecked = row?.Ticked;
            box.IsCheckedChanged += (_, _) =>
            {
                if (row is not null)
                {
                    row.Ticked = box.IsChecked == true;
                }
            };

            return box;
        });
    }

    private readonly List<(SettingFacet Facet, Segment Box)> _facets = [];

    private void BindFacets()
    {
        _facets.Clear();
        FacetGrid.Children.Clear();

        var several = _request.Facets is { Count: > 1 };
        IReadOnlyList<SettingFacet> facets = several ? _request.Facets! : _request.Facet is { } one ? [one] : [];

        FacetPanel.IsVisible = !several && facets.Count == 1;
        FacetGrid.IsVisible = several;
        MatchCount.IsVisible = several;

        foreach (var facet in facets)
        {
            var box = several ? new Segment { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center } : FacetBox;

            box.ItemsSource = facet.Options.Select(option => option.Label).ToArray();
            AutomationProperties.SetName(box, facet.Label);

            // The option that hides nothing, so the list does not open pre-narrowed.
            box.SelectedIndex = 0;

            if (several)
            {
                box.SelectionChanged += OnFacetChanged;
                var label = Grey(new TextBlock
                {
                    Text = facet.Label.ToUpperInvariant(),
                    Width = 64,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    FontSize = FacetLabel.FontSize,
                    FontWeight = FontWeight.SemiBold,
                });

                DockPanel.SetDock(label, Dock.Left);
                box.Margin = new Thickness(10, 0, 0, 0);
                FacetGrid.Children.Add(new DockPanel
                {
                    Width = 342,
                    Margin = new Thickness(0, 0, 28, 10),
                    Children = { label, box },
                });
            }
            else
            {
                FacetLabel.Text = facet.Label;
            }

            _facets.Add((facet, box));
        }
    }

    private TextBlock Grey(TextBlock text)
    {
        text.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("D47.Grey"));

        return text;
    }

    private string Label(string choice) => _request.Describe?.Invoke(choice) ?? choice;

    private void ApplyFilter()
    {
        var filter = FilterBox.Text?.Trim() ?? string.Empty;

        // The facet first, then the text search within it.
        var facet = SelectedFacet();
        var narrowed = _facets.Where(pair => ChosenMatches(pair) is not null).ToArray();

        // Matches on the label or the id.
        var matches = _all
            .Where(choice => (facet is null || facet(choice.Value))
                             && (ChoiceMatch.Matches(choice.Value, filter)
                                 || ChoiceMatch.Matches(choice.Text, filter)))
            .ToArray();

        matches = Grouped(matches);
        _visible = matches;

        // The same row objects, filtered — never new ones.
        Choices.ItemsSource = matches;
        Choices.IsVisible = matches.Length > 0;

        EmptyHint.IsVisible = matches.Length == 0;

        MatchCount.Inlines!.Clear();
        var number = new Run(matches.Length.ToString());
        number.Bind(TextElement.ForegroundProperty, this.GetResourceObservable("D47.White"));
        MatchCount.Inlines.Add(number);
        MatchCount.Inlines.Add(new Run($" OF {_all.Count} {_request.ChoiceNoun.ToUpperInvariant()}"));

        EmptyHint.Text = _request.Choices.Count == 0
            ? _request.WhyEmpty
              ?? "There is nothing to offer here — D47 does not know this endpoint's vocabulary. Type the value you want, or keep the current one."
            : _facets.Count > 1 && narrowed.Length > 0
                ? $"Nothing matches. Set {FacetToReset(narrowed, filter).Facet.Label} back to {FacetToReset(narrowed, filter).Facet.Options[0].Label}."
            : facet is not null && filter.Length == 0
                ? $"No {FacetBox.SelectedItem} choices here. Choose {_request.Facet!.Options[0].Label} to see everything."
                : $"Nothing matches \"{filter}\"{(facet is null ? string.Empty : $" under {FacetBox.SelectedItem}")}. {(_request.AllowsFreeText ? "Use it anyway, or clear the box to see everything." : "Clear the box to see everything.")}";

        ShowWhetherAnythingCanBeTaken();
    }

    /// <summary>
    /// Under a Chatterbox list, the rows on this PC first with the suggested voice leading them, then the rest,
    /// each group headed by its first row. Other lists pass through.
    /// </summary>
    private PickerChoice[] Grouped(PickerChoice[] rows)
    {
        foreach (var row in rows)
        {
            row.Head = null;
        }

        if (_request.Clips is null)
        {
            return rows;
        }

        var here = rows.Where(row => row.OnThisPc).OrderBy(row => row.SuggestedFor is null).ToArray();
        var elsewhere = rows.Where(row => !row.OnThisPc).ToArray();

        Head(here, "ON THIS PC");
        Head(elsewhere, "FETCHED WHEN PLAYED OR CHOSEN");

        return [.. here, .. elsewhere];
    }

    private static void Head(PickerChoice[] group, string head)
    {
        if (group.Length == 0)
        {
            return;
        }

        group[0].HeadCount = group.Length.ToString();
        group[0].Head = head;
    }

    /// <summary>A clip changed state: the row follows it, and moves between the heads when it crossed.</summary>
    private void OnClipChanged(string id) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (_all.FirstOrDefault(row => string.Equals(row.Value, id, StringComparison.OrdinalIgnoreCase)) is not { } row
                || _request.Clips is not { } clips)
            {
                return;
            }

            var was = row.OnThisPc;

            row.Clip = clips.Clip(id);

            if (row.OnThisPc == was)
            {
                ShowWhatChoosingDoes();
                return;
            }

            var selected = Choices.SelectedIndex >= 0 && Choices.SelectedIndex < _visible.Count
                ? _visible[Choices.SelectedIndex].Value
                : null;

            ApplyFilter();

            if (selected is not null)
            {
                Choices.SelectedIndex = Array.FindIndex([.. _visible], visible => visible.Value == selected);
            }

            ShowWhatChoosingDoes();
        });

    /// <summary>Under a Chatterbox list, what choosing the highlighted voice would fetch and who speaks until it arrives.</summary>
    private void ShowWhatChoosingDoes()
    {
        var line = _request.Clips is { } clips && !_request.Several
            && Choices.SelectedIndex >= 0 && Choices.SelectedIndex < _visible.Count
                ? clips.Choosing(_visible[Choices.SelectedIndex].Value)
                : null;

        ChoosingLine.Text = line;
        ChoosingLine.IsVisible = line is not null;
    }

    private void OnRetryClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: PickerChoice choice })
        {
            _request.Clips?.Fetch?.Invoke(choice.Value);
        }
    }

    /// <summary>Use this is enabled exactly when <see cref="Accept"/> would take something (#190).</summary>
    private void ShowWhetherAnythingCanBeTaken()
    {
        if (_request.Several)
        {
            AcceptButton.Content = $"Use these · {_all.Count(choice => choice.Ticked)}";
            AcceptButton.IsEnabled = true;
            return;
        }

        AcceptButton.IsEnabled =
            Choices.SelectedIndex >= 0
            || (_request.AllowsFreeText && !string.IsNullOrWhiteSpace(FilterBox.Text));
    }

    /// <summary>The chosen facet option's predicate, or null for no facet or the option that takes everything.</summary>
    private Func<string, bool>? SelectedFacet()
    {
        var chosen = _facets.Select(ChosenMatches).OfType<Func<string, bool>>().ToArray();

        return chosen.Length == 0 ? null : id => chosen.All(matches => matches(id));
    }

    /// <summary>The first chosen facet that, set back to its first option, lists something; the first chosen one when none does.</summary>
    private (SettingFacet Facet, Segment Box) FacetToReset((SettingFacet Facet, Segment Box)[] narrowed, string filter)
    {
        foreach (var pair in narrowed)
        {
            var others = _facets.Where(other => other != pair).Select(ChosenMatches).OfType<Func<string, bool>>().ToArray();

            if (_all.Any(choice => others.All(matches => matches(choice.Value))
                                   && (ChoiceMatch.Matches(choice.Value, filter) || ChoiceMatch.Matches(choice.Text, filter))))
            {
                return pair;
            }
        }

        return narrowed[0];
    }

    private static Func<string, bool>? ChosenMatches((SettingFacet Facet, Segment Box) pair) =>
        pair.Box.SelectedIndex >= 0 && pair.Box.SelectedIndex < pair.Facet.Options.Count
            ? pair.Facet.Options[pair.Box.SelectedIndex].Matches
            : null;

    /// <summary>Typing re-filters and then puts the highlight back on something visible (#190).</summary>
    private void OnFilterChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilter();

        if (!string.IsNullOrEmpty(FilterBox.Text))
        {
            HighlightSomethingVisible();
        }
    }

    private void OnFacetChanged(object? sender, EventArgs e)
    {
        ApplyFilter();
        HighlightSomethingVisible();
    }

    /// <summary>Puts the highlight on the top match when the last one has been filtered away.</summary>
    private void HighlightSomethingVisible()
    {
        if (!_request.Several && Choices.SelectedIndex < 0 && _visible.Count > 0)
        {
            Choices.SelectedIndex = 0;
            Choices.ScrollIntoView(0);
        }
    }

    private void OnChoiceChanged(object? sender, SelectionChangedEventArgs e)
    {
        ShowWhetherAnythingCanBeTaken();
        ShowWhatChoosingDoes();
    }

    private void OnFilterKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                Cancel();
                break;

            case Key.Enter:
                e.Handled = true;
                Accept();
                break;

            case Key.Down when Choices.ItemCount > 0:
                e.Handled = true;
                Move(1);
                break;

            case Key.Up when Choices.ItemCount > 0:
                e.Handled = true;
                Move(-1);
                break;
        }
    }

    /// <summary>Wraps at either end.</summary>
    private void Move(int delta)
    {
        var count = Choices.ItemCount;
        var next = Choices.SelectedIndex < 0 && delta < 0 ? count - 1 : Choices.SelectedIndex + delta;

        Choices.SelectedIndex = ((next % count) + count) % count;
        Choices.ScrollIntoView(Choices.SelectedIndex);
    }

    private void Accept()
    {
        if (_request.Several)
        {
            Answer(new PickerResult(null, [.. _all.Where(choice => choice.Ticked).Select(choice => choice.Value)]));
            return;
        }

        // A selection wins over typed text.
        if (Choices.SelectedIndex >= 0 && Choices.SelectedIndex < _visible.Count)
        {
            Answer(new PickerResult(_visible[Choices.SelectedIndex].Value));
            return;
        }

        var typed = FilterBox.Text?.Trim();

        if (_request.AllowsFreeText && !string.IsNullOrEmpty(typed))
        {
            Answer(new PickerResult(typed));
        }
    }

    private void Answer(PickerResult result)
    {
        if (_answered)
        {
            return;
        }

        _answered = true;
        _auditioning?.Cancel();
        _chosen(result);
    }

    private void Cancel()
    {
        if (_answered)
        {
            return;
        }

        _answered = true;
        _auditioning?.Cancel();
        _cancelled();
    }

    private void OnAcceptClick(object? sender, RoutedEventArgs e) => Accept();

    private void OnChoiceDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (!_request.Several)
        {
            Accept();
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Cancel();

    private void OnUseDefaultClick(object? sender, RoutedEventArgs e) => Answer(new PickerResult(null, _request.Several ? [] : null));

    /// <summary>
    /// Plays the row the glyph is on, not the selection, and commits nothing: the free sample where the row
    /// has one, and the paid line otherwise.
    /// </summary>
    private async void OnPlayClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control
            && control.DataContext is PickerChoice choice
            && _request.Audition is { Unavailable: null } audition)
        {
            await AuditionAsync(
                choice,
                line: false,
                choice.HasSample && audition.Preview is { } preview ? preview : audition.Play);
        }
    }

    /// <summary>Has the row's voice say its own line, from the glyph beside a free sample (#106).</summary>
    private async void OnSayLineClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control
            && control.DataContext is PickerChoice choice
            && _request.Audition is { Unavailable: null } audition)
        {
            await AuditionAsync(choice, line: true, audition.Play);
        }
    }

    /// <summary>One audition from one glyph: a second press on that glyph stops it, and any press stops the rest.</summary>
    private async Task AuditionAsync(PickerChoice choice, bool line, Func<string, CancellationToken, Task> play)
    {
        // Read before stopping, because stopping clears it.
        var stopping = line ? choice.PlayingLine : choice.Playing;

        await StopAsync();

        if (stopping)
        {
            return;
        }

        var mine = new CancellationTokenSource();
        _auditioning = mine;
        SetPlaying(choice, line, playing: true);

        try
        {
            await play(choice.Value, mine.Token);
        }
        catch (OperationCanceledException)
        {
            // A second press, the shut-up key, or the page closing.
        }
        catch (Exception ex)
        {
            // A provider that would not speak (#383).
            AuditionNote.Text = ex.Message;
        }
        finally
        {
            SetPlaying(choice, line, playing: false);

            if (ReferenceEquals(_auditioning, mine))
            {
                _auditioning = null;
            }

            mine.Dispose();
        }
    }

    private static void SetPlaying(PickerChoice choice, bool line, bool playing)
    {
        if (line)
        {
            choice.PlayingLine = playing;
        }
        else
        {
            choice.Playing = playing;
        }
    }

    /// <summary>Silences whatever is talking and puts every glyph back to play.</summary>
    private async Task StopAsync()
    {
        var previous = _auditioning;

        _auditioning = null;

        // Every row, not only the listed ones.
        foreach (var row in _all)
        {
            row.Playing = false;
            row.PlayingLine = false;
        }

        if (previous is not null)
        {
            await previous.CancelAsync();
            previous.Dispose();
        }
    }
}


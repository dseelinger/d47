using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

using D47.App.Windowing;
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
}

/// <summary>The chosen value, where null means "clear this and use the default".</summary>
public sealed record PickerResult(string? Value);

/// <summary>
/// One line of the list: what it is called, what it really is, and — where the row offers an audition —
/// the control that plays it (change-requests.md 18).
/// </summary>
public sealed class PickerChoice : INotifyPropertyChanged
{
    /// <summary>A right-pointing triangle, and a square.</summary>
    private static readonly Geometry Play = Geometry.Parse("M 8,5 L 19,12 L 8,19 Z");

    private static readonly Geometry Stop = Geometry.Parse("M 6,6 L 18,6 L 18,18 L 6,18 Z");

    private bool _playing;

    /// <summary>What choosing this row writes to settings — an id, not the words above it.</summary>
    public required string Value { get; init; }

    /// <summary>What the row says, which is the row's own <c>Describe</c> applied to the value.</summary>
    public required string Text { get; init; }

    /// <summary>Whether this list offers auditions at all.</summary>
    public required bool CanPlay { get; init; }

    /// <summary>And whether one can be played right now — false where the provider cannot speak.</summary>
    public required bool Playable { get; init; }

    /// <summary>The pointer text on the glyph: what a press costs, or why it cannot be pressed.</summary>
    public string? Why { get; init; }

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
        }
    }

    /// <summary>Play, or stop while this row is the one talking.</summary>
    public Geometry Glyph => _playing ? Stop : Play;

    /// <summary>What the glyph is for, in words.</summary>
    public string ActionName => _playing ? $"Stop {Text}" : $"Play {Text}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// One searchable picker, used everywhere a value is chosen — models, themes, log levels, and the
/// voices and devices that arrive in later phases (Phase 4).
/// </summary>
public partial class PickerWindow : Window
{
    private PickerRequest _request = new() { Prompt = "Choose" };

    /// <summary>Every choice, built once when the picker is bound.</summary>
    private IReadOnlyList<PickerChoice> _all = [];

    /// <summary>The rows currently listed, in the order they are drawn.</summary>
    private IReadOnlyList<PickerChoice> _visible = [];

    public PickerWindow()
    {
        InitializeComponent();
    }

    /// <summary><param name="onListed"> Called once the picker is on screen with its list built.</summary>
    /// <param name="onListed">Called once the picker is on screen with its list built.</param>
    public static async Task<PickerResult?> ShowAsync(
        Window owner,
        PickerRequest request,
        Action? onListed = null)
    {
        var picker = For(request);

        if (onListed is not null)
        {
            picker.Opened += (_, _) => onListed();
        }

        return await picker.Over<PickerResult?>(owner);
    }

    /// <summary>A bound picker that has not been shown.</summary>
    public static PickerWindow For(PickerRequest request)
    {
        var picker = new PickerWindow { _request = request };
        picker.Bind();

        return picker;
    }

    private void Bind()
    {
        Title = _request.Prompt;
        PromptText.Text = _request.Prompt;
        HelpText.Text = _request.Help ?? string.Empty;
        HelpText.IsVisible = !string.IsNullOrWhiteSpace(_request.Help);

        // Empty, not the current value.
        FilterBox.Text = string.Empty;
        FilterBox.PlaceholderText = _request.AllowsFreeText
            ? "Type to filter, or type a value of your own"
            : "Type to filter";

        // Absent rather than empty where the choices carry nothing to filter on, which is every picker but
        // the three voice ones (#146).
        FacetPanel.IsVisible = _request.Facet is not null;

        if (_request.Facet is { } facet)
        {
            FacetLabel.Text = facet.Label;
            FacetBox.ItemsSource = facet.Options.Select(option => option.Label).ToArray();

            // The first option is the one that hides nothing, which is where a picker has to open: a list
            // that arrives pre-narrowed looks like a list with things missing.
            FacetBox.SelectedIndex = 0;
        }

        DefaultButton.IsVisible = _request.DefaultDisplay is not null;

        // Bracketed unconditionally, because what arrives here is the bare phrase — see
        // SettingRow.BareDefaultFor, which is why this cannot say "((the provider's default))".
        var useDefault = $"Use the default ({_request.DefaultDisplay})";

        DefaultButtonText.Text = useDefault;
        ToolTip.SetTip(DefaultButton, useDefault);

        // Said once for the whole list, whichever way it goes: shut, it says why nothing here can be played;
        // live, it says what pressing a glyph will do, which on a paid provider is spend money.
        if (_request.Audition is { } audition)
        {
            AuditionNote.IsVisible = true;
            AuditionNote.Text = audition.Unavailable ?? audition.Cost;
        }

        _all = [.. _request.Choices.Select(value => new PickerChoice
        {
            Value = value,
            Text = Label(value),
            CanPlay = _request.Audition is not null,
            Playable = _request.Audition is { Unavailable: null },
            Why = _request.Audition is { } offered ? offered.Unavailable ?? offered.Cost : null,
        })];

        ApplyFilter();

        // Selecting the current value means Enter with no typing keeps what you had, which is the least
        // surprising thing a picker opened by accident can do — and it is the only thing showing what is
        // selected now, since the box above no longer says.
        Choices.SelectedIndex = _request.Current is null
            ? -1
            : Array.FindIndex(
                [.. _visible],
                choice => string.Equals(choice.Value, _request.Current, StringComparison.OrdinalIgnoreCase));

        if (Choices.SelectedIndex >= 0)
        {
            Choices.ScrollIntoView(Choices.SelectedIndex);
        }

        Opened += (_, _) =>
        {
            FilterBox.Focus();
            FilterBox.SelectAll();
        };
    }

    private string Label(string choice) => _request.Describe?.Invoke(choice) ?? choice;

    private void ApplyFilter()
    {
        var filter = FilterBox.Text?.Trim() ?? string.Empty;

        // The facet first, because it is a statement about the list and the text is a search within it.
        var facet = SelectedFacet();

        // Matches on either what it is called or what it is named, so a Commander who types what they can see
        // finds it, and one who types the id does too.
        var matches = _all
            .Where(choice => (facet is null || facet(choice.Value))
                             && (ChoiceMatch.Matches(choice.Value, filter)
                                 || ChoiceMatch.Matches(choice.Text, filter)))
            .ToArray();

        _visible = matches;

        // The same row objects, filtered — never new ones.
        Choices.ItemsSource = matches;
        Choices.IsVisible = matches.Length > 0;

        EmptyHint.IsVisible = matches.Length == 0;

        // Three different empties, and the row gets to answer the first one.
        EmptyHint.Text = _request.Choices.Count == 0
            ? _request.WhyEmpty
              ?? "There is nothing to offer here — D47 does not know this endpoint's vocabulary. Type the value you want, or keep the current one."
            : facet is not null && filter.Length == 0
                ? $"No {FacetBox.SelectionBoxItem} choices here. Choose {_request.Facet!.Options[0].Label} to see everything."
                : $"Nothing matches \"{filter}\"{(facet is null ? string.Empty : $" under {FacetBox.SelectionBoxItem}")}. {(_request.AllowsFreeText ? "Use it anyway, or clear the box to see everything." : "Clear the box to see everything.")}";

        ShowWhetherAnythingCanBeTaken();
    }

    /// <summary>
    /// Whether Use this has anything to take — the same question <see cref="Accept"/> asks, so the
    /// button cannot be pressed into the branch that does nothing (#190).
    /// </summary>
    private void ShowWhetherAnythingCanBeTaken() =>
        AcceptButton.IsEnabled =
            Choices.SelectedIndex >= 0
            || (_request.AllowsFreeText && !string.IsNullOrWhiteSpace(FilterBox.Text));

    /// <summary>
    /// The predicate for the facet option currently chosen, or null when there is no facet or when the
    /// chosen option is the one that takes everything.
    /// </summary>
    private Func<string, bool>? SelectedFacet() =>
        _request.Facet is { } facet
        && FacetBox.SelectedIndex >= 0
        && FacetBox.SelectedIndex < facet.Options.Count
            ? facet.Options[FacetBox.SelectedIndex].Matches
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

    /// <summary>Choosing a facet re-filters and puts the highlight back on something visible.</summary>
    private void OnFacetChanged(object? sender, SelectionChangedEventArgs e)
    {
        ApplyFilter();
        HighlightSomethingVisible();
    }

    /// <summary>Puts the highlight on the top match when the last one has been filtered away.</summary>
    private void HighlightSomethingVisible()
    {
        if (Choices.SelectedIndex < 0 && _visible.Count > 0)
        {
            Choices.SelectedIndex = 0;
            Choices.ScrollIntoView(0);
        }
    }

    /// <summary>The selection is half of what Use this can take, so it says so (#190).</summary>
    private void OnChoiceChanged(object? sender, SelectionChangedEventArgs e) =>
        ShowWhetherAnythingCanBeTaken();

    private void OnFilterKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                Close(null);
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

    /// <summary>Wraps, so a list of three is navigable without looking at where the end is.</summary>
    private void Move(int delta)
    {
        var count = Choices.ItemCount;
        var next = Choices.SelectedIndex < 0 && delta < 0 ? count - 1 : Choices.SelectedIndex + delta;

        Choices.SelectedIndex = ((next % count) + count) % count;
        Choices.ScrollIntoView(Choices.SelectedIndex);
    }

    private void Accept()
    {
        // A selection wins over typed text, because typing is how you got to the selection.
        if (Choices.SelectedIndex >= 0 && Choices.SelectedIndex < _visible.Count)
        {
            Close(new PickerResult(_visible[Choices.SelectedIndex].Value));
            return;
        }

        var typed = FilterBox.Text?.Trim();

        if (_request.AllowsFreeText && !string.IsNullOrEmpty(typed))
        {
            Close(new PickerResult(typed));
        }
    }

    private void OnAcceptClick(object? sender, RoutedEventArgs e) => Accept();

    /// <summary>A click highlights, and the second one takes it (change-requests.md 19).</summary>
    private void OnChoiceDoubleTapped(object? sender, TappedEventArgs e) => Accept();

    /// <summary>The audition in flight, so the next press can drop it.</summary>
    private CancellationTokenSource? _auditioning;

    /// <summary>
    /// Plays the row the glyph is on — not the selection, which is the point of moving it there
    /// (change-requests.md 18): a Commander can listen to one voice while another stays highlighted,
    /// and pressing play commits to nothing whatsoever.
    /// </summary>
    private async void OnPlayClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control
            || control.DataContext is not PickerChoice choice
            || _request.Audition is not { Unavailable: null } audition)
        {
            return;
        }

        // Read before stopping, because stopping is what clears it.
        var stopping = choice.Playing;

        await StopAsync();

        if (stopping)
        {
            return;
        }

        var mine = new CancellationTokenSource();
        _auditioning = mine;
        choice.Playing = true;

        try
        {
            await audition.Play(choice.Value, mine.Token);
        }
        catch (OperationCanceledException)
        {
        // A second press, or the shut-up key.
        }
        catch (Exception ex)
        {
            // A provider that would not speak.
            ToolTip.SetTip(control, ex.Message);
            AuditionNote.Text = ex.Message;
        }
        finally
        {
            choice.Playing = false;

            if (ReferenceEquals(_auditioning, mine))
            {
                _auditioning = null;
            }

            mine.Dispose();
        }
    }

    /// <summary>Silences whatever is talking and puts every glyph back to play.</summary>
    private async Task StopAsync()
    {
        var previous = _auditioning;

        _auditioning = null;

        // Every row, not the listed ones.
        foreach (var row in _all)
        {
            row.Playing = false;
        }

        if (previous is not null)
        {
            await previous.CancelAsync();
            previous.Dispose();
        }
    }

    /// <summary>Whatever is still being auditioned when the dialog goes stops with it.</summary>
    protected override void OnClosed(EventArgs e)
    {
        _auditioning?.Cancel();
        base.OnClosed(e);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void OnUseDefaultClick(object? sender, RoutedEventArgs e) => Close(new PickerResult(null));
}

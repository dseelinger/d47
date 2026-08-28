using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

using D47.App.Windowing;

namespace D47.App.Controls;

/// <summary>What the picker was asked to choose between.</summary>
public sealed record PickerRequest
{
    public required string Prompt { get; init; }

    public string? Help { get; init; }

    public IReadOnlyList<string> Choices { get; init; } = [];

    /// <summary>
    /// How a choice is written for a person. The value chosen is still the underlying one, so
    /// what lands in settings is an id whatever the list looked like.
    /// </summary>
    public Func<string, string>? Describe { get; init; }

    /// <summary>What is selected now, so cancelling and keeping are the same thing.</summary>
    public string? Current { get; init; }

    /// <summary>Shown when nothing has been chosen. Offering it back is how a row is cleared.</summary>
    public string? DefaultDisplay { get; init; }

    /// <summary>Whether a value outside <see cref="Choices"/> may be typed.</summary>
    public bool AllowsFreeText { get; init; }

    /// <summary>
    /// Why <see cref="Choices"/> is empty, when the row knows and the generic wording would be
    /// wrong. Null everywhere else, and then the picker says what it always said.
    /// <para>
    /// The voice row is what this exists for: "D47 does not know this endpoint's vocabulary,
    /// type the value you want" is true of a model name and useless for a voice id, which the
    /// Commander has no way of knowing and four different reasons for not being shown
    /// (Phase 19).
    /// </para>
    /// </summary>
    public string? WhyEmpty { get; init; }

    /// <summary>
    /// How to play the highlighted value, when the row offers that. Null everywhere else, and
    /// the button is then absent rather than present and inert (Phase 19).
    /// </summary>
    public PickerAudition? Audition { get; init; }
}

/// <summary>
/// Hearing a value before choosing it. Everything about it is the row's to decide — the picker
/// owns the button, the cancellation of the previous press, and nothing else.
/// </summary>
public sealed record PickerAudition
{
    /// <summary>
    /// Plays one value. Never commits it, never closes the dialog. Cancelled when a second
    /// press arrives, so starting an audition drops the one before it mid-word rather than
    /// queueing behind it.
    /// </summary>
    public required Func<string, CancellationToken, Task> Play { get; init; }

    /// <summary>
    /// What a press costs, stated once above the list. The disclosure outlived the button it used
    /// to be written on (change-requests.md 18): a glyph has no room for a price, and a price
    /// discovered afterwards on the bill is the thing Phase 11 put it there to prevent. So it is
    /// a sentence about the list, and it is also the pointer text on every glyph in it.
    /// </summary>
    public required string Cost { get; init; }

    /// <summary>
    /// Why nothing here can be played, or null when it can. Shut and explained rather than
    /// silently inert: "no voice provider is selected" is a fact the Commander can act on and an
    /// unresponsive glyph is not.
    /// </summary>
    public string? Unavailable { get; init; }
}

/// <summary>The chosen value, where null means "clear this and use the default".</summary>
public sealed record PickerResult(string? Value);

/// <summary>
/// One line of the list: what it is called, what it really is, and — where the row offers an
/// audition — the control that plays it (change-requests.md 18).
/// <para>
/// The play control lives on the row rather than under the list because that is where a
/// Commander looks for it, and because a glyph beside the thing it plays needs no selection to
/// explain which thing that is. It is a press either way: on a paid provider every audition is a
/// synthesis request billed by the character, so nothing here may fire on hover or on a
/// selection change.
/// </para>
/// <para>
/// Mutable in exactly one respect. <see cref="Playing"/> swaps the glyph between play and stop
/// while a voice is talking, so the control says what it will do next rather than what it did.
/// Everything else is fixed at construction.
/// </para>
/// </summary>
public sealed class PickerChoice : INotifyPropertyChanged
{
    /// <summary>A right-pointing triangle, and a square. Drawn in repo as path data in one 24x24
    /// space, the same rule the reveal and clear glyphs follow.</summary>
    private static readonly Geometry Play = Geometry.Parse("M 8,5 L 19,12 L 8,19 Z");

    private static readonly Geometry Stop = Geometry.Parse("M 6,6 L 18,6 L 18,18 L 6,18 Z");

    private bool _playing;

    /// <summary>What choosing this row writes to settings — an id, not the words above it.</summary>
    public required string Value { get; init; }

    /// <summary>What the row says, which is the row's own <c>Describe</c> applied to the value.</summary>
    public required string Text { get; init; }

    /// <summary>Whether this list offers auditions at all. False leaves the glyph out entirely.</summary>
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

    /// <summary>
    /// What the glyph is for, in words. Dropping the label is what buys the room for a control on
    /// every row, and a bare shape tells a screen reader nothing — so the name says which voice as
    /// well as which action, because on this list "Play" alone names four hundred controls.
    /// </summary>
    public string ActionName => _playing ? $"Stop {Text}" : $"Play {Text}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// One searchable picker, used everywhere a value is chosen — models, themes, log levels, and
/// the voices and devices that arrive in later phases (Phase 4). Command-palette
/// shaped: type to filter, arrows to move, Enter to take it, Escape to keep what you had.
/// <para>
/// Fail-soft by contract. An empty list is a supported state, not an error state: the filter
/// box is also the value box, so a model name only your endpoint has ever heard of is one you
/// can still type. That is why the same control can serve a closed vocabulary like log levels
/// and an open one like models.
/// </para>
/// </summary>
public partial class PickerWindow : Window
{
    private PickerRequest _request = new() { Prompt = "Choose" };

    /// <summary>
    /// Every choice, built once when the picker is bound. Filtering picks from these rather than
    /// making new ones, which is what keeps a selection and a playing glyph alive across a
    /// keystroke: a <see cref="ListBox"/> holds its selection by object, so handing it a fresh
    /// row for the same value silently deselects it. That is not hypothetical — rebuilding per
    /// filter cost the picker the current value the moment it opened, because a text box raises
    /// TextChanged as its template applies.
    /// </summary>
    private IReadOnlyList<PickerChoice> _all = [];

    /// <summary>The rows currently listed, in the order they are drawn.</summary>
    private IReadOnlyList<PickerChoice> _visible = [];

    public PickerWindow()
    {
        InitializeComponent();
    }

    /// <param name="onListed">
    /// Called once the picker is on screen with its list built. The caller is a settings row
    /// that has disabled its own button and put a spinner beside it; this is what tells it to
    /// stop, because "working" ends when the Commander can see the list, not when they have
    /// finished choosing from it.
    /// </param>
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

    /// <summary>
    /// A bound picker that has not been shown. Public for the headless UI tests, which drive it
    /// the same way <see cref="ShowAsync"/> does — a modal dialog cannot be inspected from the
    /// thread that opened it, and what is worth asserting here is what the Commander is looking
    /// at before they touch anything.
    /// </summary>
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

        // Empty, not the current value. Pre-filling it put the stored value in the box, and a
        // stored value is an id: a Commander opening the microphone picker was shown
        // "{0.0.1.00000000}.{a711ffd8-...}" and a list filtered down to the one device that id
        // matched, with every other microphone on the machine hidden behind text they did not
        // type. The current value is still selected below, so Enter with no typing keeps it.
        FilterBox.Text = string.Empty;
        FilterBox.PlaceholderText = _request.AllowsFreeText
            ? "Type to filter, or type a value of your own"
            : "Type to filter";

        DefaultButton.IsVisible = _request.DefaultDisplay is not null;

        // Bracketed unconditionally, because what arrives here is the bare phrase — see
        // SettingRow.BareDefaultFor, which is why this cannot say "((the provider's default))".
        // The label is trimmed to the button and repeated on the tooltip, because a resolved
        // default names a device and device names are long.
        var useDefault = $"Use the default ({_request.DefaultDisplay})";

        DefaultButtonText.Text = useDefault;
        ToolTip.SetTip(DefaultButton, useDefault);

        // Said once for the whole list, whichever way it goes: shut, it says why nothing here can
        // be played; live, it says what pressing a glyph will do, which on a paid provider is
        // spend money.
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

        // Selecting the current value means Enter with no typing keeps what you had, which is
        // the least surprising thing a picker opened by accident can do — and it is the only
        // thing showing what is selected now, since the box above no longer says.
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

        // Matches on either what it is called or what it is named, so a Commander who types
        // what they can see finds it, and one who types the id does too.
        var matches = _all
            .Where(choice => filter.Length == 0
                             || choice.Value.Contains(filter, StringComparison.OrdinalIgnoreCase)
                             || choice.Text.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        _visible = matches;

        // The same row objects, filtered — never new ones. See _all.
        Choices.ItemsSource = matches;
        Choices.IsVisible = matches.Length > 0;

        EmptyHint.IsVisible = matches.Length == 0;

        // Three different empties, and the row gets to answer the first one. A list that is
        // empty because the provider refused a key is not the same as one that is empty because
        // d47 does not know an endpoint's vocabulary, and the advice differs: one is "fix the row
        // above", the other is "type what you want".
        EmptyHint.Text = _request.Choices.Count == 0
            ? _request.WhyEmpty
              ?? "There is nothing to offer here — D47 does not know this endpoint's vocabulary. Type the value you want, or keep the current one."
            : $"Nothing matches \"{filter}\". {(_request.AllowsFreeText ? "Use it anyway, or clear the box to see everything." : "Clear the box to see everything.")}";

        // A closed vocabulary means the typed text is a filter and nothing else, so there has to
        // be something selected for the button to accept.
        AcceptButton.IsEnabled = _request.AllowsFreeText || matches.Length > 0;
    }

    private void OnFilterChanged(object? sender, TextChangedEventArgs e) => ApplyFilter();

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

    /// <summary>
    /// A click highlights, and the second one takes it (change-requests.md 19).
    /// <para>
    /// This overturns the reasoning that stood here — "a list of things to choose from is not a
    /// file manager, and every picker a Commander has met this decade commits on the first
    /// click". That is true of a command palette, whose list is a means of getting at one known
    /// answer. It is false of a list of four hundred voices, which is a list to be examined:
    /// committing on the first click meant there was no way to look at one without taking it, and
    /// it left no row for a play glyph to live on, because a row that dismisses the window when
    /// touched cannot hold a control (change-requests.md 18).
    /// </para>
    /// <para>
    /// The ways out are unchanged, and there are four ways in: double-click, Enter,
    /// <b>Use this</b>, and the arrows that move the highlight before any of them.
    /// </para>
    /// </summary>
    private void OnChoiceDoubleTapped(object? sender, TappedEventArgs e) => Accept();

    /// <summary>
    /// The audition in flight, so the next press can drop it. One at a time by construction:
    /// two voices talking over each other tells you nothing about either.
    /// </summary>
    private CancellationTokenSource? _auditioning;

    /// <summary>
    /// Plays the row the glyph is on — not the selection, which is the point of moving it there
    /// (change-requests.md 18): a Commander can listen to one voice while another stays
    /// highlighted, and pressing play commits to nothing whatsoever.
    /// <para>
    /// A press on the row that is already talking stops it. That is what the glyph says at the
    /// time, and a stop control that restarted the sound instead would be the one state nobody
    /// could get out of without waiting the line out.
    /// </para>
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
            // A second press, or the shut-up key. Both are the Commander saying "not that one".
        }
        catch (Exception ex)
        {
            // A provider that would not speak. Said on the glyph that was pressed and in the line
            // above the list, because an audition that silently does nothing is indistinguishable
            // from a voice that is very quiet.
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

    /// <summary>
    /// Silences whatever is talking and puts every glyph back to play.
    /// <para>
    /// The field is swapped before the old source is cancelled, so two presses landing in the
    /// same instant cannot leave one cancelling its own successor.
    /// </para>
    /// </summary>
    private async Task StopAsync()
    {
        var previous = _auditioning;

        _auditioning = null;

        // Every row, not the listed ones. A voice can still be talking about a row the Commander
        // has since filtered out of sight, and it is the one that has to be put back.
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

    /// <summary>
    /// Whatever is still being auditioned when the dialog goes stops with it. A voice still
    /// talking about a picker that has closed is a voice nobody can now stop from here.
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        _auditioning?.Cancel();
        base.OnClosed(e);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void OnUseDefaultClick(object? sender, RoutedEventArgs e) => Close(new PickerResult(null));
}

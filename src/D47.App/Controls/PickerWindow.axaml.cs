using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

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
    /// (list.md Phase 19).
    /// </para>
    /// </summary>
    public string? WhyEmpty { get; init; }

    /// <summary>
    /// How to play the highlighted value, when the row offers that. Null everywhere else, and
    /// the button is then absent rather than present and inert (list.md Phase 19).
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
    /// What the button says, which is where the price goes: <c>Hear it</c> where the provider
    /// is free and something that says otherwise where it is not. A Commander pressing a button
    /// that costs money should be able to see that it does before they press it.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Why it cannot be pressed, or null when it can. Shut and explained rather than silently
    /// inert: "no voice provider is selected" is a fact the Commander can act on and an
    /// unresponsive button is not.
    /// </summary>
    public string? Unavailable { get; init; }
}

/// <summary>The chosen value, where null means "clear this and use the default".</summary>
public sealed record PickerResult(string? Value);

/// <summary>
/// One searchable picker, used everywhere a value is chosen — models, themes, log levels, and
/// the voices and devices that arrive in later phases (list.md Phase 4). Command-palette
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

    /// <summary>The values behind the labels currently listed, in the same order.</summary>
    private IReadOnlyList<string> _visible = [];

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

        return await picker.ShowDialog<PickerResult?>(owner);
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

        if (_request.Audition is { } audition)
        {
            AuditionButton.IsVisible = true;
            AuditionButton.Content = audition.Label;
            AuditionButton.IsEnabled = audition.Unavailable is null;

            // The reason on the pointer whichever way it goes: shut, it says why; live, it says
            // what pressing it will do, which on a paid provider is spend money.
            ToolTip.SetTip(AuditionButton, audition.Unavailable ?? audition.Label);
        }

        ApplyFilter();

        // Selecting the current value means Enter with no typing keeps what you had, which is
        // the least surprising thing a picker opened by accident can do — and it is the only
        // thing showing what is selected now, since the box above no longer says.
        Choices.SelectedIndex = _request.Current is null
            ? -1
            : Array.FindIndex(
                _visible.ToArray(),
                value => string.Equals(value, _request.Current, StringComparison.OrdinalIgnoreCase));

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
        var matches = _request.Choices
            .Where(choice => filter.Length == 0
                             || choice.Contains(filter, StringComparison.OrdinalIgnoreCase)
                             || Label(choice).Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        _visible = matches;
        Choices.ItemsSource = matches.Select(Label).ToArray();
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
            Close(new PickerResult(_visible[Choices.SelectedIndex]));
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
    /// One click takes it. A list of things to choose from is not a file manager: the second
    /// click is a step nobody is asking for, and every picker a Commander has met this decade —
    /// a command palette, a browser's address bar, a phone's share sheet — commits on the first
    /// one. Cancel and Escape are the way out, and re-opening and picking again is the undo.
    /// <para>
    /// Only when the click landed on a row. A click on the empty space below the last item
    /// leaves the selection alone, and accepting there would take a value the Commander did not
    /// point at. The keyboard path is unchanged: arrows move, Enter or <b>Use this</b> takes it.
    /// </para>
    /// </summary>
    private void OnChoiceTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Visual source
            && source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not null)
        {
            Accept();
        }
    }

    /// <summary>
    /// The audition in flight, so the next press can drop it. One at a time by construction:
    /// two voices talking over each other tells you nothing about either.
    /// </summary>
    private CancellationTokenSource? _auditioning;

    private async void OnAuditionClick(object? sender, RoutedEventArgs e)
    {
        if (_request.Audition is not { Unavailable: null } audition
            || Choices.SelectedIndex < 0
            || Choices.SelectedIndex >= _visible.Count)
        {
            return;
        }

        var value = _visible[Choices.SelectedIndex];

        // Swapped before the old one is cancelled, so the handler below cannot cancel its own
        // successor if two presses land in the same instant.
        var previous = _auditioning;
        var mine = new CancellationTokenSource();
        _auditioning = mine;

        if (previous is not null)
        {
            await previous.CancelAsync();
            previous.Dispose();
        }

        try
        {
            await audition.Play(value, mine.Token);
        }
        catch (OperationCanceledException)
        {
            // A second press, or the shut-up key. Both are the Commander saying "not that one".
        }
        catch (Exception ex)
        {
            // A provider that would not speak. Said on the button, because that is what was
            // pressed and the picker has nowhere else to put a message.
            ToolTip.SetTip(AuditionButton, ex.Message);
        }
        finally
        {
            if (ReferenceEquals(_auditioning, mine))
            {
                _auditioning = null;
            }

            mine.Dispose();
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

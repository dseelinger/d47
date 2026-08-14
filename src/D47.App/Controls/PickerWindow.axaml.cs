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
        EmptyHint.Text = _request.Choices.Count == 0
            ? "There is nothing to offer here — D47 does not know this endpoint's vocabulary. Type the value you want, or keep the current one."
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

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void OnUseDefaultClick(object? sender, RoutedEventArgs e) => Close(new PickerResult(null));
}

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace D47.App.Panel;

/// <summary>
/// How much of itself the panel is showing (list.md Phase 9, "TheApp's panel works in VR").
/// <para>
/// Mini is a mode, not a surface. The checklist is specific about this: not a separate window,
/// not a scaled-down copy, a reduced content set on the same panel. Which is why it is a value
/// on the view model rather than a second view — a second view is exactly the thing that would
/// let the two disagree.
/// </para>
/// </summary>
public enum PanelMode
{
    Full,

    /// <summary>The transcript's tail and the provenance line. No ask box, no banners, no gear.</summary>
    Mini,
}

/// <summary>
/// What the panel shows, independent of where it is being shown.
/// <para>
/// This is the half of "one widget tree renders to both surfaces" that the framework will
/// actually do. A <c>Visual</c> belongs to exactly one visual tree, so the desktop window and
/// the VR overlay each instantiate <see cref="PanelView"/> for themselves — but both bind to
/// one of these, so there is no path where the two surfaces can be showing different things
/// and no second place to fix a bug in what the panel says (architecture.md D1).
/// </para>
/// <para>
/// Deliberately free of Avalonia types beyond the binding contract, and of anything that owns
/// a thread. Every setter here is expected on the UI thread; the callers that hear from a tick
/// loop or a provider post to it first.
/// </para>
/// </summary>
public sealed class PanelViewModel : INotifyPropertyChanged
{
    private readonly StringBuilder _transcript = new();
    private string _turnLine = string.Empty;
    private string? _errorText;
    private string? _updateText;
    private bool _updateBusy;
    private string _askText = string.Empty;
    private bool _canAsk = true;
    private PanelMode _mode = PanelMode.Full;
    private string _transcriptText = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raised when the transcript grew. Each hosting view scrolls itself: a scroll position is
    /// a property of a rendered surface, and the desktop window and the overlay have different
    /// ones even though they are showing the same text.
    /// </summary>
    public event Action? TranscriptAppended;

    /// <summary>Raised when a view's send affordance was used. The host runs the turn.</summary>
    public event Action? AskRequested;

    /// <summary>Raised when a view's gear was used. Only the desktop window answers it.</summary>
    public event Action? SettingsRequested;

    public event Action? UpdateAccepted;

    public event Action? UpdateDismissed;

    public string TranscriptText
    {
        get => _transcriptText;
        private set => Set(ref _transcriptText, value);
    }

    public string TurnLine
    {
        get => _turnLine;
        set => Set(ref _turnLine, value);
    }

    /// <summary>Null when there is nothing wrong. The banner's visibility is derived from it.</summary>
    public string? ErrorText
    {
        get => _errorText;
        set
        {
            if (Set(ref _errorText, value))
            {
                Raise(nameof(HasError));
                Raise(nameof(ShowError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(_errorText);

    public string? UpdateText
    {
        get => _updateText;
        set
        {
            if (Set(ref _updateText, value))
            {
                Raise(nameof(HasUpdate));
                Raise(nameof(ShowUpdate));
            }
        }
    }

    public bool HasUpdate => !string.IsNullOrEmpty(_updateText);

    /// <summary>
    /// An update is being fetched or installed. The toast stays up and says what is happening,
    /// but its buttons go away: "Update now" a second time would start a second download, and
    /// "Later" cannot un-download the one already running.
    /// </summary>
    public bool UpdateBusy
    {
        get => _updateBusy;
        set
        {
            if (Set(ref _updateBusy, value))
            {
                Raise(nameof(UpdateActionable));
            }
        }
    }

    /// <summary>Whether the toast's buttons are shown.</summary>
    public bool UpdateActionable => !_updateBusy;

    public string AskText
    {
        get => _askText;
        set => Set(ref _askText, value);
    }

    /// <summary>False while a turn is in flight. The send affordance follows it.</summary>
    public bool CanAsk
    {
        get => _canAsk;
        set => Set(ref _canAsk, value);
    }

    public PanelMode Mode
    {
        get => _mode;
        set
        {
            if (Set(ref _mode, value))
            {
                Raise(nameof(IsFull));
                Raise(nameof(ShowError));
                Raise(nameof(ShowUpdate));
            }
        }
    }

    /// <summary>
    /// Bound by the parts of the panel that mini does without. A positive property rather than
    /// a converter over the enum, because a binding that has to be read backwards is a binding
    /// that gets inverted by accident.
    /// </summary>
    public bool IsFull => _mode == PanelMode.Full;

    /// <summary>
    /// Whether the error banner is on screen. Two conditions rather than one, and derived here
    /// rather than combined in a binding: a banner has to be both wanted and welcome, and an
    /// expression that says so in XAML is an expression no test can reach.
    /// </summary>
    public bool ShowError => HasError && IsFull;

    public bool ShowUpdate => HasUpdate && IsFull;

    /// <summary>Adds to the transcript. The only way it grows.</summary>
    public void Append(string text)
    {
        _transcript.Append(text);
        TranscriptText = _transcript.ToString();
        TranscriptAppended?.Invoke();
    }

    /// <summary>The last <paramref name="lines"/> lines, for a surface with less room than a window.</summary>
    public string Tail(int lines)
    {
        var all = TranscriptText.Split('\n');
        return all.Length <= lines ? TranscriptText : string.Join('\n', all[^lines..]);
    }

    public void Ask() => AskRequested?.Invoke();

    public void OpenSettings() => SettingsRequested?.Invoke();

    public void AcceptUpdate() => UpdateAccepted?.Invoke();

    public void DismissUpdate() => UpdateDismissed?.Invoke();

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Raise(property);
        return true;
    }

    private void Raise(string? property) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}

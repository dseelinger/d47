using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace D47.App.Panel;

/// <summary>
/// How much of itself the panel is showing (list.md Phase 9, "TheApp's panel works in VR").
/// <para>
/// Mini is a mode, not a surface: not a separate window, not a scaled-down copy, a reduced
/// content set on the same panel.
/// </para>
/// <para>
/// It lives on the <see cref="PanelView"/> rather than on the view model, and the difference
/// matters. The two surfaces share one model, so a mode held there would put the desktop
/// window into mini the moment the headset went into it. What is shared is the content; how
/// much of it a given surface shows is a property of that surface.
/// </para>
/// </summary>
public enum PanelMode
{
    Full,

    /// <summary>The transcript's tail and the provenance line. No ask box, no banners, no gear.</summary>
    Mini,
}

/// <summary>
/// Which page of the transcript a surface is showing.
/// <para>
/// A property of the surface, exactly like <see cref="PanelMode"/> and for the same reason: the
/// two surfaces bind one model, so a page held there would send the headset to the log file the
/// moment the window went to it. The model owns the content; each surface picks its page.
/// </para>
/// </summary>
public enum TranscriptPage
{
    /// <summary>The Commander and the ship's AI, and nothing else.</summary>
    Conversation,

    /// <summary>The same, with the diagnostics left in. What the panel always used to show.</summary>
    Technical,

    /// <summary>Today's log file, read when this page is opened.</summary>
    Log,
}

/// <summary>
/// What kind of line this is, which is the whole basis of the conversation/technical split.
/// <para>
/// Decided by the caller at the moment it writes, because that is the only place that knows.
/// The transcript used to be one string and the distinction was not recoverable from it: a
/// version banner and a reply are both text, and no amount of pattern-matching afterwards tells
/// them apart without guessing at somebody's prose.
/// </para>
/// </summary>
public enum TranscriptKind
{
    /// <summary>The Commander and the ship's AI. The default, so the streaming path is untouched.</summary>
    Conversation,

    /// <summary>Diagnostics, provenance, availability - true, useful, and not the conversation.</summary>
    Technical,
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
    /// <summary>
    /// The transcript in order, split into runs of one kind. A run rather than one buffer per
    /// kind, because order across kinds has to survive: a technical line written between two
    /// replies belongs between them, and two buffers cannot say that.
    /// <para>
    /// Appended to the last run when the kind matches, which is what keeps a streamed reply -
    /// one call per delta - from becoming one run per token.
    /// </para>
    /// </summary>
    private readonly List<(TranscriptKind Kind, StringBuilder Text)> _runs = [];

    private string _conversationText = string.Empty;
    private string _logText = string.Empty;
    private string _turnLine = string.Empty;
    private string? _errorText;
    private string? _updateText;
    private bool _updateBusy;
    private string? _modelText;
    private bool _modelBusy;
    private string _askText = string.Empty;
    private bool _canAsk = true;
    private string _transcriptText = string.Empty;
    private D47.Core.Audio.LoopState _loopState = D47.Core.Audio.LoopState.Idle;

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

    public event Action? HelpRequested;

    public event Action? UpdateAccepted;

    public event Action? UpdateDismissed;

    /// <summary>The Commander agreed to the download, having read what it costs.</summary>
    public event Action? ModelDownloadAccepted;

    public event Action? ModelDownloadDismissed;

    /// <summary>Everything, in order. The transcript as it has always been.</summary>
    public string TranscriptText
    {
        get => _transcriptText;
        private set => Set(ref _transcriptText, value);
    }

    /// <summary>The conversation alone, with the diagnostics taken out.</summary>
    public string ConversationText
    {
        get => _conversationText;
        private set => Set(ref _conversationText, value);
    }

    /// <summary>
    /// Today's log file, as of the last <see cref="RefreshLog"/>. Empty until something asks.
    /// </summary>
    public string LogText
    {
        get => _logText;
        private set => Set(ref _logText, value);
    }

    /// <summary>
    /// Where <see cref="LogText"/> comes from. A function rather than a path, so this stays a
    /// view model that knows nothing about a disk and a test can hand it a string.
    /// </summary>
    public Func<string>? LogSource { get; set; }

    /// <summary>
    /// Re-reads the log. Called when a surface switches to it and when the Commander asks
    /// again - a log nobody is looking at is not worth a file read per tick.
    /// </summary>
    public void RefreshLog()
    {
        if (LogSource is not { } read)
        {
            LogText = "No log file is being written.";
            return;
        }

        try
        {
            LogText = read();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The log is a diagnostic. Failing to show it must not be a second fault to chase.
            LogText = $"The log could not be read: {ex.Message}";
        }
    }

    /// <summary>
    /// Where the conversation loop is (list.md Phase 11, "Ship's AI Avatar"). The panel has
    /// always had these states audibly, one cue each; this is the same states, visible.
    /// <para>
    /// On the view model rather than on the view, like everything else here, because the
    /// desktop window and the headset overlay each instantiate a view against this one model —
    /// a state owned by one of them would make the other a guest (list.md Phase 9).
    /// </para>
    /// </summary>
    public D47.Core.Audio.LoopState LoopState
    {
        get => _loopState;
        set => Set(ref _loopState, value);
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

    /// <summary>
    /// The speech model that is selected but not on disk, stated with its size and where it
    /// comes from - the consent text itself, not a pointer to it.
    /// <para>
    /// A banner rather than only a dialog, because this is a <em>state</em> and not an event.
    /// The offer used to be a modal raised once per launch, parented to the desktop window; a
    /// Commander whose window was on a second display got the question asked where they were
    /// not looking, three sessions running, while the panel said only "no speech model is
    /// loaded" and never that one was a click away.
    /// </para>
    /// </summary>
    public string? ModelText
    {
        get => _modelText;
        set
        {
            if (Set(ref _modelText, value))
            {
                Raise(nameof(HasModelOffer));
            }
        }
    }

    public bool HasModelOffer => !string.IsNullOrEmpty(_modelText);

    /// <summary>Downloading. Same reason as <see cref="UpdateBusy"/>: the buttons go away.</summary>
    public bool ModelBusy
    {
        get => _modelBusy;
        set
        {
            if (Set(ref _modelBusy, value))
            {
                Raise(nameof(ModelActionable));
            }
        }
    }

    public bool ModelActionable => !_modelBusy;

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

    /// <summary>
    /// Adds to the transcript. The only way it grows.
    /// <para>
    /// <paramref name="kind"/> defaults to <see cref="TranscriptKind.Conversation"/> so the
    /// streaming reply path - one call per delta - reads exactly as it did, and only the
    /// callers writing diagnostics have to say so.
    /// </para>
    /// </summary>
    public void Append(string text, TranscriptKind kind = TranscriptKind.Conversation)
    {
        if (_runs.Count == 0 || _runs[^1].Kind != kind)
        {
            _runs.Add((kind, new StringBuilder()));
        }

        _runs[^1].Text.Append(text);

        TranscriptText = string.Concat(_runs.Select(run => run.Text.ToString()));
        ConversationText = string.Concat(
            _runs.Where(run => run.Kind == TranscriptKind.Conversation).Select(run => run.Text.ToString()));

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

    /// <summary>
    /// The Commander asked for the documentation. Raised rather than acted on, for the same
    /// reason opening settings is: this view is instantiated by the headset overlay too, and a
    /// panel that launched a browser would be a panel that knows what a desktop is.
    /// </summary>
    public void OpenHelp() => HelpRequested?.Invoke();

    public void AcceptUpdate() => UpdateAccepted?.Invoke();

    public void AcceptModelDownload() => ModelDownloadAccepted?.Invoke();

    public void DismissModelDownload() => ModelDownloadDismissed?.Invoke();

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

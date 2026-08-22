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
/// Which reading of the transcript a surface is showing (list.md Phase 25, "One transcript,
/// three views").
/// <para>
/// Three <em>modes</em> of one tab rather than three tabs, because they are three readings of
/// one exchange rather than three destinations — which is what freed the bar for Checklist,
/// Loadout, Engineers and Utilities without it growing. Settings used to be a fourth member
/// here, occupying the same slot from the same strip; it is a <see cref="PanelTab"/> now, along
/// with everything else the bar carries.
/// </para>
/// <para>
/// One asymmetry, kept rather than smoothed over: Conversation and Technical are the same
/// exchange at two verbosities and the log is <b>a file read off disk</b>, which is why it
/// carries the busy glyph and why the collapse is three modes rather than a single toggle.
/// </para>
/// <para>
/// <b>One choice across every surface, since list.md Phase 45 — a reversal, recorded rather
/// than smoothed over.</b> This used to be a property of the surface, exactly like
/// <see cref="PanelMode"/> and for the same reason: a page held in common would send the
/// headset to the log file the moment the window went to it. The Commander's ruling is that
/// this is the point — <em>"I want the Windows UI transcript selection to be echoed in VR and
/// vice-versa."</em> The principle that keeps the reversal from being arbitrary: <b>what you
/// are reading is shared; how it is drawn is not.</b> So <see cref="PanelMode"/> and zoom stay
/// per surface, the page is still read from each surface's navigator, and the host's
/// <c>TranscriptMirror</c> keeps those navigators agreeing about it.
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
/// A stretch of transcript drawn one way. <paramref name="Marker"/> is the panel noting
/// something about the conversation — the core changing — rather than a line of it.
/// </summary>
public sealed record TranscriptSegment(string Text, bool Marker);

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
    private readonly List<(TranscriptKind Kind, bool Marker, StringBuilder Text)> _runs = [];

    /// <summary>Guards <see cref="_runs"/> and the strings derived from it. See Append.</summary>
    private readonly Lock _appendLock = new();

    private string _conversationText = string.Empty;
    private string _logText = string.Empty;
    private string _turnLine = string.Empty;
    private string? _errorText;
    private string? _updateText;
    private bool _updateBusy;
    private string _askText = string.Empty;
    private bool _canAsk = true;
    private bool _hasAsked;
    private string _transcriptText = string.Empty;
    private D47.Core.Audio.LoopState _loopState = D47.Core.Audio.LoopState.Idle;
    private D47.Core.Listening.MicrophoneState _microphone = D47.Core.Listening.MicrophoneState.Off;
    private string? _switchesText;
    private string _microphoneDetail = string.Empty;

    // True in every mode, and replaced by the host's own wording within a tick. A default that
    // named a key or claimed to be listening would be a guess about a setting this model does
    // not read, and one of the two guesses is the defect item 12 is about.
    private string _listeningPrompt = PanelPrompts.WaitingFallback;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raised when the transcript grew. Each hosting view scrolls itself: a scroll position is
    /// a property of a rendered surface, and the desktop window and the overlay have different
    /// ones even though they are showing the same text.
    /// </summary>
    public event Action? TranscriptAppended;

    /// <summary>Raised when a view's send affordance was used. The host runs the turn.</summary>
    public event Action? AskRequested;

    public event Action? UpdateAccepted;

    public event Action? UpdateDismissed;

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
    public void RefreshLog() => ShowLog(ReadLog());

    /// <summary>
    /// The read alone, with no property set — the half that is safe off the UI thread.
    /// Setting a bound property raises PropertyChanged on the calling thread, and Avalonia's
    /// binding table answers an off-thread raise by reading the process-global dispatcher
    /// right there, which under the headless harness can hijack the UI thread's identity
    /// mid-reset and fail whichever test runs next (bugs.md, the headless-session entry).
    /// So a surface runs this on a worker and brings the result to <see cref="ShowLog"/>.
    /// </summary>
    public string ReadLog()
    {
        if (LogSource is not { } read)
        {
            return "No log file is being written.";
        }

        try
        {
            return read();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The log is a diagnostic. Failing to show it must not be a second fault to chase.
            return $"The log could not be read: {ex.Message}";
        }
    }

    /// <summary>Puts a read's result on the page. UI thread, like every property set here.</summary>
    public void ShowLog(string text) => LogText = text;

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

    /// <summary>
    /// What the microphone is doing (list.md Phase 13, "Show that the microphone is open").
    /// <para>
    /// On the view model rather than on either surface, and that is the requirement rather than
    /// a convenience: the checklist asks for the state to be visible on the panel <em>and</em>
    /// the VR surface, and the only way to guarantee those two agree is for there to be one
    /// copy of it. A headset that showed the microphone shut while the window showed it open
    /// would be worse than neither showing anything.
    /// </para>
    /// </summary>
    public D47.Core.Listening.MicrophoneState Microphone
    {
        get => _microphone;
        set
        {
            if (Set(ref _microphone, value))
            {
                Raise(nameof(MicrophoneVisible));
            }
        }
    }

    /// <summary>
    /// The rest of the sentence — which key to hold, or which name to say. Set by the host,
    /// because what opens the gate is a settings question and this view model does not read
    /// settings.
    /// </summary>
    public string MicrophoneDetail
    {
        get => _microphoneDetail;
        set => Set(ref _microphoneDetail, value);
    }

    /// <summary>
    /// What an open prompt says while it waits on a spoken value (remediation.md 10, item 12).
    /// <para>
    /// Here rather than on <see cref="PanelPrompts"/> for the reason the line above is here: what
    /// opens the gate is a settings question, and one model serves both surfaces — so the headset
    /// and the window cannot end up telling the Commander two different things about one
    /// microphone.
    /// </para>
    /// </summary>
    public string ListeningPrompt
    {
        get => _listeningPrompt;
        set => Set(ref _listeningPrompt, value);
    }

    /// <summary>
    /// Whether the indicator is drawn at all. Off means no device is open, and a row saying so
    /// permanently would be a row about nothing — every other state is worth a Commander's
    /// glance, including the idle one, because "the microphone is open and nothing is kept" is
    /// a claim they are entitled to see made.
    /// </summary>
    public bool MicrophoneVisible => _microphone != D47.Core.Listening.MicrophoneState.Off;

    /// <summary>
    /// Which assigned switches currently sit against the game's state (list.md Phase 21, "Show
    /// which switches disagree with the game"). Null when none do, which is nearly always.
    /// <para>
    /// On the view model rather than on either surface, for the same reason
    /// <see cref="Microphone"/> is: the checklist asks for it on the panel <em>and</em> the VR
    /// surface, and one copy is the only arrangement that guarantees the two agree.
    /// </para>
    /// <para>
    /// It is an annunciator, not a servo. Showing that a switch is stale costs a line of text;
    /// moving the switch is impossible, and correcting the game behind the Commander's back is
    /// the thing the reconcile rule exists to refuse.
    /// </para>
    /// </summary>
    public string? SwitchesText
    {
        get => _switchesText;
        set
        {
            if (Set(ref _switchesText, value))
            {
                Raise(nameof(SwitchesVisible));
            }
        }
    }

    public bool SwitchesVisible => !string.IsNullOrEmpty(_switchesText);

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
    /// Whether this Commander has ever asked d47 anything. Set from view state at startup and
    /// again the first time they ask; the host is what persists it.
    /// <para>
    /// On the model rather than on the view because both surfaces show the ask box and there is
    /// one Commander behind them. A flag per instantiation would retire the hint on the desktop
    /// window while the headset went on offering it.
    /// </para>
    /// </summary>
    public bool HasAsked
    {
        get => _hasAsked;
        set
        {
            if (Set(ref _hasAsked, value))
            {
                Raise(nameof(AskHint));
            }
        }
    }

    /// <summary>
    /// What the ask box says when it is empty. The worked example is onboarding, so it goes once
    /// the Commander has asked anything by any route — but the box still has to say what it is
    /// for, because a text box with no label is a box.
    /// <para>
    /// The examples are the half that expires. "Where am I" teaches that d47 can be asked about
    /// the game rather than only commanded, which is worth saying once and reads as instruction
    /// for ever afterwards.
    /// </para>
    /// </summary>
    public string AskHint => _hasAsked
        ? "Ask D47 something"
        : "Ask D47 something — try \"where am I\" or \"what's your status\"";

    /// <summary>
    /// Adds to the transcript. The only way it grows.
    /// <para>
    /// <paramref name="kind"/> defaults to <see cref="TranscriptKind.Conversation"/> so the
    /// streaming reply path - one call per delta - reads exactly as it did, and only the
    /// callers writing diagnostics have to say so.
    /// </para>
    /// </summary>
    public void Append(string text, TranscriptKind kind = TranscriptKind.Conversation, bool marker = false)
    {
        string transcript;
        string conversation;

        // Locked, because there is more than one writer now. The turn path appends a streaming
        // reply from its own thread while the speech loop appends stage lines from the audio
        // path and the log bridge appends errors from wherever they were raised — and a
        // StringBuilder mutated while another thread is reading it throws out of
        // ToString(), which is a crash in a diagnostics feature.
        lock (_appendLock)
        {
            if (_runs.Count == 0 || _runs[^1].Kind != kind || _runs[^1].Marker != marker)
            {
                _runs.Add((kind, marker, new StringBuilder()));
            }

            _runs[^1].Text.Append(text);

            transcript = string.Concat(_runs.Select(run => run.Text.ToString()));
            conversation = string.Concat(
                _runs.Where(run => run.Kind == TranscriptKind.Conversation).Select(run => run.Text.ToString()));
        }

        // Outside the lock. These raise PropertyChanged, which reaches bindings and handlers that
        // marshal to the UI thread — holding a lock across that is how a deadlock is built.
        TranscriptText = transcript;
        ConversationText = conversation;

        TranscriptAppended?.Invoke();
    }

    /// <summary>
    /// Empties what the transcript is showing (remediation.md 11, item 14).
    /// <para>
    /// <b>The page, not the record.</b> Nothing here is the conversation: the model's own history
    /// lives in the turn loop and is what a follow-up question is answered against, and the log
    /// file on disk is written by Serilog and is not this. Clearing the page is a reader tidying
    /// what is in front of them, which is why it needs no confirmation and costs nothing to get
    /// wrong.
    /// </para>
    /// <para>
    /// Both readings at once, because they are one set of runs seen two ways — clearing the
    /// conversation and leaving the technical page holding the same lines would be a page that
    /// disagreed with the one beside it about what had happened.
    /// </para>
    /// </summary>
    public void ClearTranscript()
    {
        lock (_appendLock)
        {
            _runs.Clear();
        }

        // Outside the lock, like Append: these raise PropertyChanged, and holding a lock across a
        // handler that marshals to the UI thread is how a deadlock is built.
        TranscriptText = string.Empty;
        ConversationText = string.Empty;

        TranscriptAppended?.Invoke();
    }

    /// <summary>
    /// Notes something that happened to the conversation rather than something said in it — the
    /// core changing under it being the case this exists for.
    /// <para>
    /// It is a conversation line, not a technical one: which core answered is what the lines
    /// around it mean, so a transcript that omits the switch reads as one companion changing
    /// character mid-page. Bracketed and coloured because it is the panel speaking about the
    /// conversation and not a voice in it, and a reader should be able to tell those apart at a
    /// metre in a headset without reading either.
    /// </para>
    /// </summary>
    public void Mark(string text) =>
        Append($"\n[{text}]\n", TranscriptKind.Conversation, marker: true);

    /// <summary>
    /// A page's content, in order, split where its emphasis changes. The view renders these
    /// rather than one string, because a run that is marked has to be drawn differently and a
    /// string cannot carry that.
    /// <para>
    /// The strings above are still the content of record — every test and both capture paths
    /// read them — and these are the same characters, cut where the drawing changes.
    /// </para>
    /// </summary>
    public IReadOnlyList<TranscriptSegment> Segments(TranscriptPage page) => page switch
    {
        TranscriptPage.Log => [new TranscriptSegment(LogText, Marker: false)],
        TranscriptPage.Technical =>
            [.. _runs.Select(run => new TranscriptSegment(run.Text.ToString(), run.Marker))],
        _ =>
        [
            .. _runs
                .Where(run => run.Kind == TranscriptKind.Conversation)
                .Select(run => new TranscriptSegment(run.Text.ToString(), run.Marker))
        ],
    };

    /// <summary>The last <paramref name="lines"/> lines, for a surface with less room than a window.</summary>
    public string Tail(int lines)
    {
        var all = TranscriptText.Split('\n');
        return all.Length <= lines ? TranscriptText : string.Join('\n', all[^lines..]);
    }

    public void Ask() => AskRequested?.Invoke();

    public void AcceptUpdate() => UpdateAccepted?.Invoke();

    public void DismissUpdate() => UpdateDismissed?.Invoke();

    /// <summary>
    /// The Commander has read the message and is done with it.
    /// <para>
    /// Cleared here rather than raised to the host, unlike the update banner: there is nothing to
    /// decide and nobody else to tell. The next fault sets the text again and the banner comes
    /// back with it, which is the property that makes this a dismiss rather than a mute.
    /// </para>
    /// </summary>
    public void DismissError() => ErrorText = null;

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

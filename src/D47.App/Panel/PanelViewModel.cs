using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace D47.App.Panel;

/// <summary>How much of itself the panel is showing (Phase 9, "TheApp's panel works in VR").</summary>
public enum PanelMode
{
    Full,

    /// <summary>The transcript's tail and the provenance line.</summary>
    Mini,
}

/// <summary>
/// Which reading of the transcript a surface is showing (Phase 25, "One transcript, three views").
/// </summary>
public enum TranscriptPage
{
    /// <summary>
    /// The Commander, the ship's AI, and the callouts spoken to the Commander — everything but
    /// invented chatter and an overheard relay.
    /// </summary>
    Conversation,

    /// <summary>
    /// Today's log file, read when this page is opened and followed while it is the one showing (#294).
    /// </summary>
    Log,

    /// <summary>
    /// Elite's journal as sentences, with the fields behind the selected line beside it
    /// (https://github.com/dseelinger/d47/issues/51).
    /// </summary>
    Journal,

    /// <summary>The same events, shown as the JSON Elite wrote.</summary>
    RawJournal,
}

/// <summary>Who said it.</summary>
public enum TranscriptVoice
{
    /// <summary>The ship's AI, and the panel speaking in its register.</summary>
    Ship,

    /// <summary>The Commander, however they said it — typed, spoken, or through a switch.</summary>
    Commander,
}

/// <summary>What a run is, beyond ordinary spoken or typed text (#277).</summary>
public enum TranscriptRunKind
{
    Text,

    /// <summary>A checklist proposal, drawn as a card rather than as a bubble's plain words.</summary>
    Proposal,
}

/// <summary>A stretch of transcript drawn one way.</summary>
/// <param name="Speaker">Who said it, or null on the flat Log and Raw Journal blocks, which carry no chip.</param>
/// <param name="SourceKey">The callout key this run came from, or null for one that did not.</param>
/// <param name="Kind">Whether this is a proposal card rather than plain text (#277).</param>
/// <param name="ProposalId">Which proposal a <see cref="TranscriptRunKind.Proposal"/> run is about.</param>
public sealed record TranscriptSegment(
    string Text,
    bool Marker,
    TranscriptVoice Voice,
    string? Speaker = null,
    string? SourceKey = null,
    DateTimeOffset Time = default,
    TranscriptRunKind Kind = TranscriptRunKind.Text,
    string? ProposalId = null);

/// <summary>What the panel shows, independent of where it is being shown.</summary>
public sealed class PanelViewModel : INotifyPropertyChanged
{
    /// <summary>The transcript in order, split into runs that are drawn the same way.</summary>
    private readonly List<Run> _runs = [];

    /// <summary>One drawn run: who said it, when, and from which callout, alongside its text.</summary>
    private sealed record Run(
        bool Marker,
        TranscriptVoice Voice,
        string Speaker,
        string? SourceKey,
        DateTimeOffset Time,
        StringBuilder Text)
    {
        /// <summary>Whether this is a proposal card rather than plain text (#277).</summary>
        public TranscriptRunKind Kind { get; init; } = TranscriptRunKind.Text;

        /// <summary>Which proposal a <see cref="TranscriptRunKind.Proposal"/> run is about.</summary>
        public string? ProposalId { get; init; }
    }

    /// <summary>Guards <see cref="_runs"/> and the strings derived from it.</summary>
    private readonly Lock _appendLock = new();

    private string _logText = string.Empty;
    private string _turnLine = string.Empty;
    private string? _errorText;
    private string? _updateText;
    private bool _updateBusy;
    private string _askText = string.Empty;

    /// <summary>What has been sent from the box this session, oldest first (#224).</summary>
    private readonly List<string> _sent = [];

    /// <summary>Where the walk is, or -1 for "not walking, the box holds the Commander's own text".</summary>
    private int _walk = -1;

    /// <summary>What was in the box when the walk started, restored by stepping down past the newest.</summary>
    private string? _draft;
    private bool _canAsk = true;
    private bool _hasAsked;
    private string _transcriptText = string.Empty;
    private D47.Core.Audio.LoopState _loopState = D47.Core.Audio.LoopState.Idle;
    private D47.Core.Listening.MicrophoneState _microphone = D47.Core.Listening.MicrophoneState.Off;
    private string? _switchesText;
    private string? _startupText;
    private string _microphoneDetail = string.Empty;
    private bool _modelLoading;

    // True in every mode, and replaced by the host's own wording within a tick.
    private string _listeningPrompt = PanelPrompts.WaitingFallback;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when the transcript grew.</summary>
    public event Action? TranscriptAppended;

    /// <summary>Raised when a view's send affordance was used.</summary>
    public event Action? AskRequested;

    public event Action? UpdateAccepted;

    public event Action? UpdateDismissed;

    /// <summary>Everything said, in order.</summary>
    public string TranscriptText
    {
        get => _transcriptText;
        private set => Set(ref _transcriptText, value);
    }

    /// <summary>Today's log file, as of the last <see cref="RefreshLog"/>.</summary>
    public string LogText
    {
        get => _logText;
        private set => Set(ref _logText, value);
    }

    /// <summary>Where <see cref="LogText"/> comes from.</summary>
    public Func<string>? LogSource { get; set; }

    private IReadOnlyList<D47.Core.Journal.JournalEntry> _journal = [];
    private int _journalSelected = -1;
    private bool _journalDetail = true;
    private bool _journalNoise;

    /// <summary>Elite's journal as the page holds it, newest first (#51).</summary>
    public IReadOnlyList<D47.Core.Journal.JournalEntry> Journal
    {
        get => _journal;
        private set => Set(ref _journal, value);
    }

    /// <summary>
    /// Where <see cref="Journal"/> comes from — the log the tick loop feeds, asked for what it holds.
    /// </summary>
    public Func<bool, IReadOnlyList<D47.Core.Journal.JournalEntry>>? JournalSource { get; set; }

    /// <summary>Which line's fields the detail pane is showing, or -1 for none.</summary>
    public int JournalSelected
    {
        get => _journalSelected;
        set => Set(ref _journalSelected, value);
    }

    /// <summary>Whether the fields are shown beside the list.</summary>
    public bool JournalDetail
    {
        get => _journalDetail;
        set => Set(ref _journalDetail, value);
    }

    /// <summary>Whether the kinds nobody reads are listed.</summary>
    public bool JournalNoise
    {
        get => _journalNoise;
        set => Set(ref _journalNoise, value);
    }

    private string _journalRaw = string.Empty;

    /// <summary>The events as the file holds them, one per line — what Raw Journal shows.</summary>
    public string JournalRawText
    {
        get => _journalRaw;
        private set => Set(ref _journalRaw, value);
    }

    /// <summary>Where <see cref="JournalRawText"/> comes from.</summary>
    public Func<bool, string>? JournalDocumentSource { get; set; }

    /// <summary>The fields behind the selected line, or empty when nothing is selected.</summary>
    public string JournalDetailText =>
        JournalSelected >= 0 && JournalSelected < Journal.Count
            ? Journal[JournalSelected].Raw
            : string.Empty;

    /// <summary>Re-reads the journal from the log.</summary>
    public void RefreshJournal()
    {
        Journal = JournalSource is { } read ? read(JournalNoise) : [];
        JournalRawText = JournalDocumentSource is { } document ? document(JournalNoise) : string.Empty;

        // The selection is an index into a list that has just been rebuilt, so it cannot survive it.
        JournalSelected = Journal.Count > 0 ? 0 : -1;
    }

    /// <summary>Re-reads the log.</summary>
    public void RefreshLog() => ShowLog(ReadLog());

    /// <summary>The read alone, with no property set — the half that is safe off the UI thread.</summary>
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
            // The log is a diagnostic.
            return $"The log could not be read: {ex.Message}";
        }
    }

    /// <summary>Puts a read's result on the page.</summary>
    public void ShowLog(string text) => LogText = text;

    /// <summary>Where the conversation loop is (Phase 11, "Ship's AI Avatar").</summary>
    public D47.Core.Audio.LoopState LoopState
    {
        get => _loopState;
        set => Set(ref _loopState, value);
    }

    /// <summary>What the microphone is doing (Phase 13, "Show that the microphone is open").</summary>
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
    /// Whether the speech model is still loading, which the indicator says rather than reporting that
    /// the microphone is ready (#147).
    /// </summary>
    public bool ModelLoading
    {
        get => _modelLoading;
        set => Set(ref _modelLoading, value);
    }

    /// <summary>The rest of the sentence — which key to hold, or which name to say.</summary>
    public string MicrophoneDetail
    {
        get => _microphoneDetail;
        set => Set(ref _microphoneDetail, value);
    }

    /// <summary>What an open prompt says while it waits on a spoken value (remediation.md 10, item 12).</summary>
    public string ListeningPrompt
    {
        get => _listeningPrompt;
        set => Set(ref _listeningPrompt, value);
    }

    /// <summary>Whether the indicator is drawn at all.</summary>
    public bool MicrophoneVisible => _microphone != D47.Core.Listening.MicrophoneState.Off;

    /// <summary>
    /// Which assigned switches currently sit against the game's state (Phase 21, "Show which switches
    /// disagree with the game").
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

    /// <summary>What startup is still doing, or null once it is doing nothing (#148).</summary>
    public string? StartupText
    {
        get => _startupText;
        set
        {
            if (Set(ref _startupText, value))
            {
                Raise(nameof(StartupVisible));
            }
        }
    }

    /// <summary>Whether the startup row is drawn at all.</summary>
    public bool StartupVisible => !string.IsNullOrEmpty(_startupText);

    public string TurnLine
    {
        get => _turnLine;
        set => Set(ref _turnLine, value);
    }

    /// <summary>Null when there is nothing wrong.</summary>
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

    /// <summary>An update is being fetched or installed.</summary>
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

    /// <summary>False while a turn is in flight.</summary>
    public bool CanAsk
    {
        get => _canAsk;
        set => Set(ref _canAsk, value);
    }

    /// <summary>Whether this Commander has ever asked d47 anything.</summary>
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

    /// <summary>What the ask box says when it is empty.</summary>
    public string AskHint => _hasAsked
        ? "What can you do?"
        : "What can you do? — try \"where am I\" or \"what's your status\"";

    /// <summary>
    /// Adds to the transcript. <paramref name="speaker"/> falls back to CMDR or D47 by
    /// <paramref name="voice"/> — everything that does not name its own speaker is one of those two.
    /// </summary>
    public void Append(
        string text,
        bool marker = false,
        TranscriptVoice voice = TranscriptVoice.Ship,
        string? speaker = null,
        string? sourceKey = null,
        DateTimeOffset? time = null)
    {
        var named = speaker ?? (voice == TranscriptVoice.Commander ? "CMDR" : "D47");
        var at = time ?? DateTimeOffset.Now;
        string transcript;

        // Locked, because there is more than one writer.
        lock (_appendLock)
        {
            // A run merges only into one from the same speaker and the same source: two callouts spoken
            // back to back stay two bubbles even when both are the ship's own voice. A proposal card never
            // merges with plain text either way (#277).
            if (_runs.Count == 0
                || _runs[^1].Marker != marker
                || _runs[^1].Voice != voice
                || _runs[^1].Speaker != named
                || _runs[^1].SourceKey != sourceKey
                || _runs[^1].Kind != TranscriptRunKind.Text)
            {
                _runs.Add(new Run(marker, voice, named, sourceKey, at, new StringBuilder()));
            }

            _runs[^1].Text.Append(text);

            transcript = string.Concat(_runs.Select(Flatten));
        }

        // Outside the lock.
        TranscriptText = transcript;

        TranscriptAppended?.Invoke();
    }

    /// <summary>
    /// Puts a proposal's card in the conversation: the summary, with Accept and Decline while it waits
    /// (#277). Always a run of its own — see <see cref="Append"/>'s merge guard.
    /// </summary>
    public void AppendProposal(string proposalId, string summary)
    {
        string transcript;

        lock (_appendLock)
        {
            _runs.Add(new Run(Marker: false, TranscriptVoice.Ship, "D47", "proposal", DateTimeOffset.Now, new StringBuilder(summary))
            {
                Kind = TranscriptRunKind.Proposal,
                ProposalId = proposalId,
            });

            transcript = string.Concat(_runs.Select(Flatten));
        }

        TranscriptText = transcript;

        TranscriptAppended?.Invoke();
    }

    /// <summary>
    /// Settles a proposal's card, from whichever surface or voice command answered it: the buttons go,
    /// the tag says what happened, and the text becomes the outcome (#277).
    /// </summary>
    public void SettleProposal(string proposalId, bool accepted, string outcome)
    {
        string transcript;

        lock (_appendLock)
        {
            var index = _runs.FindLastIndex(run =>
                run.Kind == TranscriptRunKind.Proposal
                && string.Equals(run.ProposalId, proposalId, StringComparison.Ordinal));

            if (index < 0)
            {
                return;
            }

            var run = _runs[index];
            run.Text.Clear();
            run.Text.Append(outcome);

            _runs[index] = run with { SourceKey = accepted ? "proposal · accepted" : "proposal · declined" };

            transcript = string.Concat(_runs.Select(Flatten));
        }

        TranscriptText = transcript;

        TranscriptAppended?.Invoke();
    }

    /// <summary>Empties what the transcript is showing (remediation.md 11, item 14).</summary>
    public void ClearTranscript()
    {
        lock (_appendLock)
        {
            _runs.Clear();
        }

        // Outside the lock, like Append: this raises PropertyChanged, and holding a lock across a handler
        // that marshals to the UI thread is how a deadlock is built.
        TranscriptText = string.Empty;

        TranscriptAppended?.Invoke();
    }

    /// <summary>
    /// Notes something that happened to the conversation rather than something said in it — the core
    /// changing under it being the case this exists for.
    /// </summary>
    public void Mark(string text) => Append($"\n[{text}]\n", marker: true);

    /// <summary>A page's content, in order, split where its emphasis changes.</summary>
    public IReadOnlyList<TranscriptSegment> Segments(TranscriptPage page, bool framed = true)
    {
        string Text(Run run) => framed ? Flatten(run) : run.Text.ToString();

        return page switch
        {
            TranscriptPage.Log => [new TranscriptSegment(LogText, Marker: false, TranscriptVoice.Ship)],

            // A file, drawn as one, exactly as the log above is.
            TranscriptPage.RawJournal =>
                [new TranscriptSegment(JournalRawText, Marker: false, TranscriptVoice.Ship)],
            _ =>
            [
                .. _runs.Select(run =>
                    new TranscriptSegment(
                        Text(run), run.Marker, run.Voice, run.Speaker, run.SourceKey, run.Time,
                        run.Kind, run.ProposalId))
            ],
        };
    }

    /// <summary>One run as a flat page draws it.</summary>
    private static string Flatten(Run run) =>
        run.Voice == TranscriptVoice.Commander
            ? $"\n\n> {run.Text}\n"
            : run.Text.ToString();

    /// <summary>The last <paramref name="lines"/> lines, for a surface with less room than a window.</summary>
    public string Tail(int lines)
    {
        var all = TranscriptText.Split('\n');
        return all.Length <= lines ? TranscriptText : string.Join('\n', all[^lines..]);
    }

    /// <summary>How many sent lines are kept.</summary>
    private const int MostRemembered = 200;

    /// <summary>Send what is in the box, and remember it (#224).</summary>
    public void Ask()
    {
        var line = _askText?.Trim();

        if (!string.IsNullOrEmpty(line)
            && (_sent.Count == 0 || !string.Equals(_sent[^1], line, StringComparison.Ordinal)))
        {
            // Consecutive duplicates collapse: asking the same thing twice should not need two presses to get
            // past it.
            _sent.Add(line);

            if (_sent.Count > MostRemembered)
            {
                _sent.RemoveAt(0);
            }
        }

        // Sending ends the walk and drops the held draft, so the next Up starts from the newest.
        _walk = -1;
        _draft = null;

        AskRequested?.Invoke();
    }

    /// <summary>The previous sent line, on Up.</summary>
    public bool WalkBack()
    {
        if (_sent.Count == 0)
        {
            return false;
        }

        if (_walk < 0)
        {
            _draft = _askText;
            _walk = _sent.Count;
        }

        if (_walk == 0)
        {
            return true;
        }

        _walk--;
        AskText = _sent[_walk];

        return true;
    }

    /// <summary>Forward again, on Down — and past the newest, back to the draft the walk interrupted.</summary>
    public bool WalkForward()
    {
        if (_walk < 0)
        {
            return false;
        }

        _walk++;

        if (_walk >= _sent.Count)
        {
            AskText = _draft ?? string.Empty;
            _walk = -1;
            _draft = null;

            return true;
        }

        AskText = _sent[_walk];

        return true;
    }

    public void AcceptUpdate() => UpdateAccepted?.Invoke();

    public void DismissUpdate() => UpdateDismissed?.Invoke();

    /// <summary>The Commander has read the message and is done with it.</summary>
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

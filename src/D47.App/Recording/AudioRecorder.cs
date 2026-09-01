using System.Collections.Concurrent;
using D47.Core;
using D47.Core.Audio;
using D47.Core.Diagnostics.Recording;
using D47.Core.Listening;
using Microsoft.Extensions.Logging;

namespace D47.App.Recording;

/// <summary>
/// What actually crossed the audio boundary, in both directions, retained in a capped ring
/// (<a href="https://github.com/dseelinger/d47/issues/164">#164</a>).
/// <para>
/// <b>Both capture points already existed as seams, so this is retention plus a surface rather
/// than new plumbing.</b> The spoken side is the arbiter's render reference tap, which is the
/// right point on purpose: it catches what was <em>played</em> rather than what was synthesised,
/// so it also catches the family of faults that were never synthesis at all — the wrong voice
/// heard, a silent default device, a bed mixed over the top. The heard side is the exact buffer
/// handed to the transcriber, which is where the truth lives: the capture path invented 99%
/// silence for weeks, and a recorder at this point would have caught it the first day.
/// </para>
/// <para>
/// <b>Off unless asked for, and available in Release.</b> Debug-only is the first instinct and it
/// is wrong here for a reason this repository has already recorded: the Commander flies the
/// installed Release build, and the headset is where the evidence happens. So it takes the
/// coverage recorder's shape — an environment variable rather than a setting, nothing on the
/// surface at all when it is unset, and no file written. Flip it on for a session, review, flip
/// it off.
/// </para>
/// <para>
/// <b>Only audio that was already being processed.</b> The heard side records the gated
/// utterance the transcriber was given and nothing else, so a microphone sitting open between
/// utterances is never written down — push-to-talk at rest runs into a half-second ring that
/// this never sees. It is not telemetry and nothing is transmitted; the audio never joins an
/// incident donation either, because voice is biometric and is the one payload that
/// "show it before it leaves" cannot make safe enough to be worth it.
/// </para>
/// </summary>
public sealed class AudioRecorder : IDisposable
{
    /// <summary>Set this to <c>1</c> to turn recording, the review pane and the wipe row on.</summary>
    public const string EnvironmentVariable = "D47_RECORD_AUDIO";

    /// <summary>
    /// The same switch with no shell in it
    /// (<a href="https://github.com/dseelinger/d47/issues/180">#180</a>).
    /// <para>
    /// The variable is the right gate and was the wrong road. Reaching it meant knowing
    /// PowerShell's prefix syntax, knowing the install path, and knowing that a variable only
    /// reaches a d47 launched from that same shell — so a Commander who started d47 the way they
    /// always do never got there, which is how the first attempt to use the recorder failed. A
    /// switch is something a desktop shortcut can carry, and "the recording d47" becomes a thing
    /// launched on purpose rather than an incantation typed correctly.
    /// </para>
    /// <para>
    /// <b>It changes nothing about the gating stance.</b> Both roads are per-run and neither is a
    /// setting: unasked-for, there is no row, no pane and no file, which is the whole of what
    /// "absent from the surface unless enabled" means and the reason a permanent toggle was
    /// refused.
    /// </para>
    /// </summary>
    public const string Flag = "--record-audio";

    /// <summary>
    /// What the switch and the variable were called before
    /// (<a href="https://github.com/dseelinger/d47/issues/214">#214</a>), still accepted.
    /// <para>
    /// <b>Because the only things in the field that carry them are shortcuts a Commander made by
    /// hand</b>, and the failure of dropping them is the quiet kind: d47 starts normally and
    /// simply does not record, which is noticed later, while looking for a pane that is not
    /// there. Neither name is a setting and neither is remembered anywhere, so keeping them costs
    /// two comparisons.
    /// </para>
    /// <para>
    /// They are not silent, though. Arriving by the old name says so once in the log, with the
    /// new name in it — a compatibility shim that never mentions itself is one nobody ever stops
    /// depending on.
    /// </para>
    /// </summary>
    public const string RetiredFlag = "--flight-recorder";

    /// <summary>The variable's own old name. Same reasoning as <see cref="RetiredFlag"/>.</summary>
    public const string RetiredEnvironmentVariable = "D47_FLIGHT_RECORDER";

    /// <summary>
    /// Whether the command line carried <see cref="Flag"/>. Set once from <c>Program.Main</c>,
    /// before the host is built and therefore before anything asks <see cref="Enabled"/> — the
    /// composition root reads it while wiring the audio, and a value arriving after that would
    /// leave a recorder that records with no row to review it from.
    /// </summary>
    internal static bool Switched { get; private set; }

    /// <summary>Whether whatever turned this on used a name that has been retired (#214).</summary>
    internal static bool ByRetiredName { get; private set; }

    /// <summary>
    /// Reads the command line for <see cref="Flag"/>. Called once from <c>Program.Main</c>, above
    /// everything the host builds, because the composition root asks <see cref="Enabled"/> while
    /// it wires the audio — and it is a method rather than a settable flag so that the one place
    /// deciding what the switch is spelled like is the one place a test can drive.
    /// </summary>
    public static void ReadCommandLine(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        Switched = args.Contains(Flag, StringComparer.Ordinal);

        var old = args.Contains(RetiredFlag, StringComparer.Ordinal);

        ByRetiredName = old && !Switched;
        Switched = Switched || old;
    }

    /// <summary>
    /// The most one utterance may hold — about two minutes of the mix. A ceiling rather than a
    /// policy: it exists so that a clip which never closes, because something stopped reporting
    /// that it had finished, cannot grow until the machine notices.
    /// </summary>
    private const int MaxUtteranceBytes = 48_000 * 2 * 2 * 120;

    /// <summary>How many synthesis notes are held waiting for their playback to start.</summary>
    private const int NoteMemory = 64;

    private readonly RecordingLog _log;
    private readonly Func<DateTimeOffset> _now;
    private readonly ILogger _logger;
    private readonly Lock _gate = new();
    private readonly BlockingCollection<RecordingCapture> _pending = [];
    private readonly Thread _writer;
    private readonly Queue<SynthesisNote> _notes = new();

    private IRenderReferenceTap? _tap;
    private AudioArbiter? _arbiter;
    private Open? _open;
    private volatile bool _closed;

    private AudioRecorder(RecordingLog log, Func<DateTimeOffset> now, ILogger logger)
    {
        _log = log;
        _now = now;
        _logger = logger;

        // A thread of its own rather than a pool task, and that is not a preference. Shutdown
        // has to wait for the queue to drain, and a pool task waited on from a caller that is
        // itself on the pool is the starvation this repository has already recorded once: it
        // passes alone and fails in a loaded suite, which is the worst way for it to fail.
        _writer = new Thread(Write)
        {
            IsBackground = true,
            Name = "d47 audio recorder",
        };

        _writer.Start();
    }

    /// <summary>An utterance being played, filling up from the render thread.</summary>
    private sealed class Open
    {
        public required long Id { get; init; }

        public required DateTimeOffset When { get; init; }

        public required MemoryStream Pcm { get; init; }

        public string? Caption { get; set; }

        public SynthesisNote? Note { get; set; }

        public AudioFormat? Format { get; set; }
    }

    /// <summary>
    /// A recorder if this process asked for one, otherwise null — so every caller's check is
    /// "is there one", and there is no disabled object quietly doing nothing on the audio thread.
    /// </summary>
    public static AudioRecorder? Create(AppPaths paths, Func<DateTimeOffset> now, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(logger);

        if (!Enabled)
        {
            return null;
        }

        // **The folder on disk keeps the old word** (#214). Everything else in this family was
        // renamed because "flight" competes with Elite's own vocabulary on every read — but this
        // string is where a Commander's clips already are, and renaming it would either orphan
        // them or need a migration, which is a different act from a rename. Same reasoning as the
        // settings key beside it, which is frozen for the same kind of reason.
        var folder = Path.Combine(paths.Data, "flight");

        logger.LogInformation(
            "The audio recorder is on; up to {Megabytes} MB is retained in {Folder}",
            RecordingLog.CapBytes / (1024 * 1024),
            folder);

        if (AskedByItsOldName)
        {
            // Said once, with the new name in it. A shim that never mentions itself is one
            // nobody ever stops depending on (#214).
            logger.LogInformation(
                "That was asked for by its old name. {Flag} and {Variable} are what they are "
                + "called now; {RetiredFlag} and {RetiredVariable} still work.",
                Flag,
                EnvironmentVariable,
                RetiredFlag,
                RetiredEnvironmentVariable);
        }

        return Regardless(new RecordingLog(folder, logger), now, logger);
    }

    /// <summary>
    /// A recorder without consulting the environment, so a test can exercise the stitching
    /// without setting a process-wide variable that every other test in the run would also see.
    /// </summary>
    internal static AudioRecorder Regardless(
        RecordingLog log,
        Func<DateTimeOffset> now,
        ILogger logger) =>
        new(log, now, logger);

    /// <summary>
    /// Whether recording is switched on for this process, by either road. Both are per-run and
    /// neither is remembered, so it is off again the next time d47 starts on its own.
    /// </summary>
    public static bool Enabled =>
        Switched
        || Environment.GetEnvironmentVariable(EnvironmentVariable) == "1"
        || Environment.GetEnvironmentVariable(RetiredEnvironmentVariable) == "1";

    /// <summary>
    /// Whether recording was turned on by a name that has been retired, so the log can say what
    /// the name is now (#214). Asked once, where the recorder is built.
    /// </summary>
    private static bool AskedByItsOldName =>
        ByRetiredName || Environment.GetEnvironmentVariable(RetiredEnvironmentVariable) == "1";

    /// <summary>What has been recorded, for the review pane and the settings row.</summary>
    public RecordingLog Log => _log;

    /// <summary>
    /// Starts listening to the two seams. Separate from construction for the reason the arbiter's
    /// own <c>Start</c> is: the composition root builds these in an order, and a recorder that
    /// subscribed from its constructor would be taking frames before anything had decided it
    /// should.
    /// </summary>
    public void Watch(AudioArbiter arbiter, IRenderReferenceTap tap)
    {
        ArgumentNullException.ThrowIfNull(arbiter);
        ArgumentNullException.ThrowIfNull(tap);

        _arbiter = arbiter;
        _tap = tap;

        arbiter.ActivityChanged += OnActivity;
        tap.Rendered += OnRendered;
    }

    /// <summary>
    /// The exact buffer handed to the transcriber, beside what it came back with.
    /// <para>
    /// Called after the transcription rather than before it, so the row carries both halves — a
    /// clip on its own says a sound happened, and it is the pair that says a mishear did.
    /// </para>
    /// </summary>
    public void Heard(Utterance utterance, Transcription transcription)
    {
        ArgumentNullException.ThrowIfNull(utterance);
        ArgumentNullException.ThrowIfNull(transcription);

        var wav = WavWriter.ToBytes(utterance.Samples, utterance.SampleRate);

        Queue(new RecordingCapture(
            RecordingDirection.Heard,
            _now(),
            wav,
            utterance.Duration)
        {
            Text = transcription.Text,
            Model = transcription.Model,
            Elapsed = transcription.Elapsed,
        });
    }

    /// <summary>
    /// One sentence, rendered. Held until its playback starts, which is what puts the phonemes
    /// and the provider on the row the tap produces.
    /// </summary>
    public void Noted(SynthesisNote note)
    {
        ArgumentNullException.ThrowIfNull(note);

        lock (_gate)
        {
            _notes.Enqueue(note);

            while (_notes.Count > NoteMemory)
            {
                _notes.Dequeue();
            }
        }
    }

    /// <summary>
    /// The arbiter says what is audible; this decides which stretch of the render stream is one
    /// utterance. A change of clip id closes the open row and opens the next.
    /// </summary>
    private void OnActivity(AudioActivity activity)
    {
        RecordingCapture? finished = null;

        lock (_gate)
        {
            var speaking = activity.Channel is AudioChannel.Speech or AudioChannel.Alert
                && activity.Utterance is not null;

            if (_open is { } open && (!speaking || open.Id != activity.Utterance))
            {
                finished = Close(open);
                _open = null;
            }

            if (speaking && _open is null)
            {
                _open = new Open
                {
                    Id = activity.Utterance!.Value,
                    When = _now(),
                    Pcm = new MemoryStream(),
                    Caption = activity.Caption,
                    Note = Claim(activity.Caption),
                };
            }
            else if (speaking && _open is { } current && current.Caption is null)
            {
                // A later snapshot of the same clip can carry the caption the first one did not.
                current.Caption = activity.Caption;
            }
        }

        if (finished is not null)
        {
            Queue(finished);
        }
    }

    /// <summary>
    /// One buffer of what actually went to the speakers. On the render thread, so it copies and
    /// returns — everything else about a row happens elsewhere.
    /// </summary>
    private void OnRendered(RenderReferenceFrame frame)
    {
        lock (_gate)
        {
            if (_open is not { } open || open.Pcm.Length >= MaxUtteranceBytes)
            {
                return;
            }

            open.Format ??= frame.Format;
            open.Pcm.Write(frame.Pcm.Span);
        }
    }

    /// <summary>
    /// The note this playback is speaking, if one is waiting.
    /// <para>
    /// By caption where there is one, and otherwise the oldest note still unclaimed — synthesis
    /// runs strictly ahead of playback and the queue keeps a reply in order, so the head is the
    /// right guess for an uncaptioned line. A guess rather than a guarantee, because an alert
    /// cuts in front of whatever is playing: this is a workbench aid, and a row that named the
    /// wrong provider would be visible as such beside its own audio.
    /// </para>
    /// </summary>
    private SynthesisNote? Claim(string? caption)
    {
        if (_notes.Count == 0)
        {
            return null;
        }

        if (caption is { Length: > 0 })
        {
            var match = _notes.FirstOrDefault(note =>
                string.Equals(note.Text, caption, StringComparison.Ordinal));

            if (match is not null)
            {
                var kept = _notes.Where(note => !ReferenceEquals(note, match)).ToList();
                _notes.Clear();
                kept.ForEach(_notes.Enqueue);

                return match;
            }
        }

        return _notes.Dequeue();
    }

    private RecordingCapture? Close(Open open)
    {
        var pcm = open.Pcm.ToArray();
        open.Pcm.Dispose();

        if (pcm.Length == 0)
        {
            // Enqueued and superseded before a single frame of it rendered. Nothing was heard,
            // so there is nothing to keep.
            return null;
        }

        var format = open.Format ?? AudioFormat.Standard;

        return new RecordingCapture(
            RecordingDirection.Spoken,
            open.When,
            WavWriter.ToBytes(pcm, format),
            format.DurationOf(pcm.Length))
        {
            Text = open.Caption ?? open.Note?.Text ?? string.Empty,
            Phonemes = open.Note?.Phonemes,
            Provider = open.Note?.Provider,
            Voice = open.Note?.Voice,
            Elapsed = open.Note?.Elapsed ?? TimeSpan.Zero,
        };
    }

    /// <summary>
    /// One writer, off both the audio thread and the transcription path. Writing a clip is file
    /// IO and the render thread has 60 ms of latency to keep fed, so nothing that touches disk
    /// runs where a frame is produced.
    /// </summary>
    private void Write()
    {
        foreach (var capture in _pending.GetConsumingEnumerable())
        {
            try
            {
                _log.Add(capture);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A full disk, or a folder the Commander has open. Recording is a workbench aid
                // and stopping the app over one would be the wrong trade.
                _logger.LogWarning(ex, "Could not write a audio recorder clip");
            }
        }
    }

    /// <summary>
    /// Unhooks both seams, closes whatever was mid-utterance, and waits for the queue to drain.
    /// <para>
    /// Bounded rather than open-ended. The last clip of a session is worth waiting a moment for
    /// — it is often the one being investigated — but a recorder is a workbench aid and must not
    /// be the reason d47 will not shut down.
    /// </para>
    /// </summary>
    public void Dispose()
    {
        if (_arbiter is { } arbiter)
        {
            arbiter.ActivityChanged -= OnActivity;
        }

        if (_tap is { } tap)
        {
            tap.Rendered -= OnRendered;
        }

        RecordingCapture? last = null;

        lock (_gate)
        {
            if (_open is { } open)
            {
                last = Close(open);
                _open = null;
            }
        }

        if (last is not null)
        {
            Queue(last);
        }

        _closed = true;
        _pending.CompleteAdding();

        if (!_writer.Join(TimeSpan.FromSeconds(5)))
        {
            _logger.LogWarning("The audio recorder was still writing at shutdown; some clips were dropped");
        }

        _pending.Dispose();
    }

    /// <summary>
    /// Hands one clip to the writer, and drops it once the recorder is closing. Dropping is the
    /// right answer there: what arrives after shutdown has begun is a clip nobody will review,
    /// and throwing out of the audio thread's callback would be a worse trade than losing it.
    /// </summary>
    private void Queue(RecordingCapture capture)
    {
        if (_closed)
        {
            return;
        }

        try
        {
            _pending.Add(capture);
        }
        catch (InvalidOperationException)
        {
            // Closed between the check above and this line. Nothing to do about it and nothing
            // worth saying: the queue is shutting down and this clip was the last one.
        }
    }
}

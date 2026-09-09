using System.Collections.Concurrent;
using D47.Core;
using D47.Core.Audio;
using D47.Core.Diagnostics.Recording;
using D47.Core.Listening;
using Microsoft.Extensions.Logging;

namespace D47.App.Recording;

/// <summary>
/// What actually crossed the audio boundary, in both directions, retained in a capped ring (#164).
/// </summary>
public sealed class AudioRecorder : IDisposable
{
    /// <summary>Set this to <c>1</c> to turn recording, the review pane and the wipe row on.</summary>
    public const string EnvironmentVariable = "D47_RECORD_AUDIO";

    /// <summary>The same switch with no shell in it (#180).</summary>
    public const string Flag = "--record-audio";

    /// <summary>What the switch and the variable were called before (#214), still accepted.</summary>
    public const string RetiredFlag = "--flight-recorder";

    /// <summary>The variable's own old name.</summary>
    public const string RetiredEnvironmentVariable = "D47_FLIGHT_RECORDER";

    /// <summary>Whether the command line carried <see cref="Flag"/>.</summary>
    internal static bool Switched { get; private set; }

    /// <summary>Whether whatever turned this on used a name that has been retired (#214).</summary>
    internal static bool ByRetiredName { get; private set; }

    /// <summary>Reads the command line for <see cref="Flag"/>.</summary>
    public static void ReadCommandLine(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        Switched = args.Contains(Flag, StringComparer.Ordinal);

        var old = args.Contains(RetiredFlag, StringComparer.Ordinal);

        ByRetiredName = old && !Switched;
        Switched = Switched || old;
    }

    /// <summary>The most one utterance may hold — about two minutes of the mix.</summary>
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

        // A thread of its own rather than a pool task, and that is not a preference.
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
    /// A recorder if this process asked for one, otherwise null — so every caller's check is "is there
    /// one", and there is no disabled object silently doing nothing on the audio thread.
    /// </summary>
    public static AudioRecorder? Create(AppPaths paths, Func<DateTimeOffset> now, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(logger);

        if (!Enabled)
        {
            return null;
        }

        // **The folder on disk keeps the old word** (#214).
        var folder = Path.Combine(paths.Data, "flight");

        logger.LogInformation(
            "The audio recorder is on; up to {Megabytes} MB is retained in {Folder}",
            RecordingLog.CapBytes / (1024 * 1024),
            folder);

        if (AskedByItsOldName)
        {
            // Said once, with the new name in it.
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
    /// A recorder without consulting the environment, so a test can exercise the stitching without
    /// setting a process-wide variable that every other test in the run would also see.
    /// </summary>
    internal static AudioRecorder Regardless(
        RecordingLog log,
        Func<DateTimeOffset> now,
        ILogger logger) =>
        new(log, now, logger);

    /// <summary>Whether recording is switched on for this process, by either road.</summary>
    public static bool Enabled =>
        Switched
        || Environment.GetEnvironmentVariable(EnvironmentVariable) == "1"
        || Environment.GetEnvironmentVariable(RetiredEnvironmentVariable) == "1";

    /// <summary>
    /// Whether recording was turned on by a name that has been retired, so the log can say what the
    /// name is now (#214).
    /// </summary>
    private static bool AskedByItsOldName =>
        ByRetiredName || Environment.GetEnvironmentVariable(RetiredEnvironmentVariable) == "1";

    /// <summary>What has been recorded, for the review pane and the settings row.</summary>
    public RecordingLog Log => _log;

    /// <summary>Starts listening to the two seams.</summary>
    public void Watch(AudioArbiter arbiter, IRenderReferenceTap tap)
    {
        ArgumentNullException.ThrowIfNull(arbiter);
        ArgumentNullException.ThrowIfNull(tap);

        _arbiter = arbiter;
        _tap = tap;

        arbiter.ActivityChanged += OnActivity;
        tap.Rendered += OnRendered;
    }

    /// <summary>The exact buffer handed to the transcriber, beside what it came back with.</summary>
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

    /// <summary>One sentence, rendered.</summary>
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
    /// utterance.
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

    /// <summary>One buffer of what actually went to the speakers.</summary>
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

    /// <summary>The note this playback is speaking, if one is waiting.</summary>
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
            // Enqueued and superseded before a single frame of it rendered.
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

    /// <summary>One writer, off both the audio thread and the transcription path.</summary>
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
                // A full disk, or a folder the Commander has open.
                _logger.LogWarning(ex, "Could not write a audio recorder clip");
            }
        }
    }

    /// <summary>Unhooks both seams, closes whatever was mid-utterance, and waits for the queue to drain.</summary>
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

    /// <summary>Hands one clip to the writer, and drops it once the recorder is closing.</summary>
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
        // Closed between the check above and this line.
        }
    }
}

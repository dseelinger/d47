using System.Collections.Concurrent;
using D47.Core;
using D47.Core.Audio;
using D47.Core.Diagnostics.Flight;
using D47.Core.Listening;
using Microsoft.Extensions.Logging;

namespace D47.App.Flight;

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
/// surface at all when it is unset, and no file written. Flip it on for a flight, review, flip
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
public sealed class AudioFlightRecorder : IDisposable
{
    /// <summary>Set this to <c>1</c> to turn recording, the review pane and the wipe row on.</summary>
    public const string EnvironmentVariable = "D47_FLIGHT_RECORDER";

    /// <summary>
    /// The most one utterance may hold — about two minutes of the mix. A ceiling rather than a
    /// policy: it exists so that a clip which never closes, because something stopped reporting
    /// that it had finished, cannot grow until the machine notices.
    /// </summary>
    private const int MaxUtteranceBytes = 48_000 * 2 * 2 * 120;

    /// <summary>How many synthesis notes are held waiting for their playback to start.</summary>
    private const int NoteMemory = 64;

    private readonly FlightLog _log;
    private readonly Func<DateTimeOffset> _now;
    private readonly ILogger _logger;
    private readonly Lock _gate = new();
    private readonly BlockingCollection<FlightCapture> _pending = [];
    private readonly Thread _writer;
    private readonly Queue<SynthesisNote> _notes = new();

    private IRenderReferenceTap? _tap;
    private AudioArbiter? _arbiter;
    private Open? _open;
    private volatile bool _closed;

    private AudioFlightRecorder(FlightLog log, Func<DateTimeOffset> now, ILogger logger)
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
            Name = "d47 flight recorder",
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
    public static AudioFlightRecorder? Create(AppPaths paths, Func<DateTimeOffset> now, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(logger);

        if (!Enabled)
        {
            return null;
        }

        var folder = Path.Combine(paths.Data, "flight");

        logger.LogInformation(
            "The audio flight recorder is on; up to {Megabytes} MB is retained in {Folder}",
            FlightLog.CapBytes / (1024 * 1024),
            folder);

        return Regardless(new FlightLog(folder, logger), now, logger);
    }

    /// <summary>
    /// A recorder without consulting the environment, so a test can exercise the stitching
    /// without setting a process-wide variable that every other test in the run would also see.
    /// </summary>
    internal static AudioFlightRecorder Regardless(
        FlightLog log,
        Func<DateTimeOffset> now,
        ILogger logger) =>
        new(log, now, logger);

    /// <summary>Whether recording is switched on for this process.</summary>
    public static bool Enabled =>
        Environment.GetEnvironmentVariable(EnvironmentVariable) == "1";

    /// <summary>What has been recorded, for the review pane and the settings row.</summary>
    public FlightLog Log => _log;

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

        Queue(new FlightCapture(
            FlightDirection.Heard,
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
        FlightCapture? finished = null;

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

    private FlightCapture? Close(Open open)
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

        return new FlightCapture(
            FlightDirection.Spoken,
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
                _logger.LogWarning(ex, "Could not write a flight recorder clip");
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

        FlightCapture? last = null;

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
            _logger.LogWarning("The flight recorder was still writing at shutdown; some clips were dropped");
        }

        _pending.Dispose();
    }

    /// <summary>
    /// Hands one clip to the writer, and drops it once the recorder is closing. Dropping is the
    /// right answer there: what arrives after shutdown has begun is a clip nobody will review,
    /// and throwing out of the audio thread's callback would be a worse trade than losing it.
    /// </summary>
    private void Queue(FlightCapture capture)
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

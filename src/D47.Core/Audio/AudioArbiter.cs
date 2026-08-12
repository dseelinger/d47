using Microsoft.Extensions.Logging;

namespace D47.Core.Audio;

/// <summary>
/// What a sound is for, which is also what it outranks. Higher wins.
/// </summary>
public enum AudioChannel
{
    /// <summary>
    /// The looping bed under a working turn (list.md Phase 5, #18). Not in the serial queue —
    /// it plays underneath whatever is, and ducks.
    /// </summary>
    Bed = 0,

    /// <summary>A short non-speech marker. Loop-state cues (#20) are these.</summary>
    Cue = 1,

    /// <summary>An answer, spoken.</summary>
    Speech = 2,

    /// <summary>
    /// A journal-triggered danger callout (list.md Phase 8). Outranks speech because an alert
    /// that waits for the current sentence to finish is not an alert.
    /// </summary>
    Alert = 3,
}

/// <summary>One thing to make audible.</summary>
public sealed record AudioRequest
{
    public required AudioChannel Channel { get; init; }

    public required AudioClip Clip { get; init; }

    /// <summary>
    /// The scope a supersede applies to — the turn id, for speech. Sentences of one reply
    /// share a group, so starting a new turn can drop the tail of the previous one without
    /// touching the cues or the alerts queued around them.
    /// </summary>
    public string? Group { get; init; }

    public bool Loop { get; init; }

    /// <summary>
    /// The text this is speaking, if any. Carried on the request rather than tracked
    /// separately because captions are timed from the end of speech (list.md Phase 9), and the
    /// only component that knows when speech ends is the one that owns the queue.
    /// </summary>
    public string? Caption { get; init; }
}

/// <summary>What the arbiter is doing right now.</summary>
public sealed record AudioActivity(AudioChannel? Channel, string? Caption, bool BedPlaying);

/// <summary>
/// The one queue in front of every audible thing (architecture.md D7). Speech, cues, the
/// thinking bed and Phase 8's alerts all enter here, which is what makes ducking,
/// interruption, supersede and caption timing properties of one component rather than four
/// mechanisms that have to agree.
/// <para>
/// Owns no thread and reads no clock. It advances when something happens to it: an enqueue, a
/// completion reported by the sink, or a silence. That is what lets the whole of it be tested
/// against a null sink with no audio device present (architecture.md §8).
/// </para>
/// <para>
/// The bed is the one thing not in the serial queue. It is a background layer with its own
/// slot, because a loop that took its turn at the head of a queue would never give it back.
/// It still enters through here and is still stopped by <see cref="Silence"/>, so "everything
/// audible goes through one arbiter" holds.
/// </para>
/// </summary>
public sealed class AudioArbiter(IAudioSink sink, ILogger<AudioArbiter> logger) : IDisposable
{
    /// <summary>
    /// How far the bed drops under speech. Enough to stay present as evidence the turn is
    /// still running, quiet enough not to compete with the words.
    /// </summary>
    private const float DuckedBedGain = 0.35f;

    private readonly Lock _gate = new();
    private readonly List<Pending> _queue = [];

    private long _nextId = 1;
    private Playing? _current;
    private Playing? _bed;
    private bool _subscribed;

    private sealed record Pending(long Id, AudioRequest Request);

    private sealed record Playing(long Id, AudioRequest Request);

    /// <summary>
    /// Raised when everything was cut off — the Commander said stop. Anything with work in
    /// flight that would otherwise arrive after the silence listens to this: in-flight speech
    /// synthesis, most of all, since a sentence that finishes synthesising a moment later
    /// would otherwise start speaking into the silence it was supposed to end.
    /// </summary>
    public event Action? Silenced;

    /// <summary>Raised whenever what is audible changes. The caption layer and the panel read this.</summary>
    public event Action<AudioActivity>? ActivityChanged;

    public AudioActivity Activity
    {
        get
        {
            lock (_gate)
            {
                return Snapshot();
            }
        }
    }

    /// <summary>Whether anything is queued or playing, the bed aside.</summary>
    public bool IsSpeaking
    {
        get
        {
            lock (_gate)
            {
                return _current is not null;
            }
        }
    }

    /// <summary>
    /// Subscribes to the sink. Separate from the constructor so a test can construct the
    /// arbiter, wire expectations, and only then let completions start arriving.
    /// </summary>
    public AudioArbiter Start()
    {
        lock (_gate)
        {
            if (!_subscribed)
            {
                sink.Finished += OnSinkFinished;
                _subscribed = true;
            }
        }

        return this;
    }

    public void Enqueue(AudioRequest request)
    {
        AudioActivity activity;

        lock (_gate)
        {
            if (request.Channel == AudioChannel.Bed)
            {
                StartBed(request);
                activity = Snapshot();
            }
            else
            {
                var id = _nextId++;
                _queue.Add(new Pending(id, request));

                // Highest channel first; within a channel, arrival order. Speech has to stay
                // in order — sentences of one reply are only an answer in sequence — and a
                // stable sort is what guarantees that without tracking sequence numbers.
                _queue.Sort(static (left, right) =>
                {
                    var byChannel = right.Request.Channel.CompareTo(left.Request.Channel);
                    return byChannel != 0 ? byChannel : left.Id.CompareTo(right.Id);
                });

                // Only an alert cuts in mid-playback, and the interrupted item is dropped
                // rather than resumed: half a sentence spoken before an interdiction warning
                // does not become worth finishing afterwards.
                //
                // Note that this is narrower than "a higher channel wins". Ranking decides who
                // goes *next*; it does not decide who gets cut off. Letting speech supersede
                // on rank alone truncates every loop-state cue, because a cue is a fifth of a
                // second that plays immediately before the speech it announces — the arriving
                // sentence would cut off the tick that introduced it, every single turn.
                if (_current is { } playing
                    && request.Channel == AudioChannel.Alert
                    && playing.Request.Channel < AudioChannel.Alert)
                {
                    logger.LogDebug(
                        "{Channel} supersedes {Interrupted} mid-playback",
                        request.Channel,
                        playing.Request.Channel);

                    sink.Stop(playing.Id);
                    _current = null;
                }

                Pump();
                activity = Snapshot();
            }
        }

        ActivityChanged?.Invoke(activity);
    }

    /// <summary>
    /// Drops everything belonging to one group, playing or pending. This is how a new turn
    /// clears the tail of the previous one without touching alerts queued alongside it.
    /// </summary>
    public void DropGroup(string group)
    {
        AudioActivity activity;

        lock (_gate)
        {
            _queue.RemoveAll(pending => pending.Request.Group == group);

            if (_current is { } playing && playing.Request.Group == group)
            {
                sink.Stop(playing.Id);
                _current = null;
            }

            Pump();
            activity = Snapshot();
        }

        ActivityChanged?.Invoke(activity);
    }

    /// <summary>
    /// Shut up (list.md Phase 5). Flush the queue, stop mid-sentence, drop the bed.
    /// <para>
    /// It is a queue operation rather than a feature layered on top, which is precisely why it
    /// can be instant and why nothing can gate it behind a turn completing: there is no work
    /// to await here, only state to clear.
    /// </para>
    /// </summary>
    public void Silence()
    {
        AudioActivity activity;
        var hadSomething = false;

        lock (_gate)
        {
            hadSomething = _current is not null || _bed is not null || _queue.Count > 0;

            _queue.Clear();
            _current = null;
            _bed = null;
            sink.StopAll();
            activity = Snapshot();
        }

        if (hadSomething)
        {
            logger.LogInformation("Silenced");
        }

        Silenced?.Invoke();
        ActivityChanged?.Invoke(activity);
    }

    /// <summary>
    /// The audible consequence of the loop moving: the state's own cue, and the bed running
    /// for exactly as long as the turn does.
    /// <para>
    /// Both of #20 and #18 live here rather than in the turn loop, because the alternative is
    /// the turn loop knowing about beds. The bed is started on the way into
    /// <see cref="LoopState.Thinking"/> and dropped on the way out of it, so it cannot outlive
    /// its turn even if the turn ends by failing.
    /// </para>
    /// </summary>
    public void EnterState(
        LoopState state,
        CueLibrary cues,
        string? bedName = null,
        bool cueEnabled = true,
        bool bedEnabled = true)
    {
        if (cueEnabled)
        {
            Enqueue(new AudioRequest { Channel = AudioChannel.Cue, Clip = cues.For(state) });
        }

        if (state == LoopState.Thinking && bedEnabled)
        {
            Enqueue(new AudioRequest
            {
                Channel = AudioChannel.Bed,
                Clip = cues.Bed(bedName),
                Loop = true,
            });
        }
        else
        {
            StopBed();
        }
    }

    public void StopBed()
    {
        AudioActivity activity;

        lock (_gate)
        {
            if (_bed is null)
            {
                return;
            }

            sink.Stop(_bed.Id);
            _bed = null;
            activity = Snapshot();
        }

        ActivityChanged?.Invoke(activity);
    }

    private void StartBed(AudioRequest request)
    {
        if (_bed is not null)
        {
            sink.Stop(_bed.Id);
        }

        var id = _nextId++;
        _bed = new Playing(id, request);
        sink.Play(new PlaybackRequest(id, request.Clip, Loop: true, Gain: BedGain()));
    }

    /// <summary>Start the head of the queue if nothing is playing, and re-level the bed either way.</summary>
    private void Pump()
    {
        if (_current is null && _queue.Count > 0)
        {
            var next = _queue[0];
            _queue.RemoveAt(0);
            _current = new Playing(next.Id, next.Request);
            sink.Play(new PlaybackRequest(next.Id, next.Request.Clip, next.Request.Loop, Gain: 1f));
        }

        if (_bed is { } bed)
        {
            sink.SetGain(bed.Id, BedGain());
        }
    }

    /// <summary>
    /// Ducking is a consequence of what is playing, recomputed rather than tracked. A duck
    /// held as its own boolean is a duck that eventually gets stuck on.
    /// </summary>
    private float BedGain() =>
        _current?.Request.Channel is AudioChannel.Speech or AudioChannel.Alert ? DuckedBedGain : 1f;

    private void OnSinkFinished(long playbackId)
    {
        AudioActivity activity;

        lock (_gate)
        {
            // A completion for something already stopped is normal: Silence and supersede both
            // race the sink by design, and the loser is this callback.
            if (_current?.Id != playbackId)
            {
                return;
            }

            _current = null;
            Pump();
            activity = Snapshot();
        }

        ActivityChanged?.Invoke(activity);
    }

    private AudioActivity Snapshot() =>
        new(_current?.Request.Channel, _current?.Request.Caption, _bed is not null);

    public void Dispose()
    {
        lock (_gate)
        {
            if (_subscribed)
            {
                sink.Finished -= OnSinkFinished;
                _subscribed = false;
            }
        }
    }
}

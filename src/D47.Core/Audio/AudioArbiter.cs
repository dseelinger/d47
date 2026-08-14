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

    /// <summary>
    /// Situational ambience (list.md Phase 12, "Ambient music"). The second background layer,
    /// beside the bed and not in the serial queue either — a loop that took its turn at the head
    /// of a queue would never give it back, and two of them would never give it back twice.
    /// <para>
    /// Ranked at the bottom with the bed. Neither ever enters the queue, so the number decides
    /// nothing today; it says what it would mean if one ever did, and "music outranks an alert"
    /// is not a sentence this enum should be able to make.
    /// </para>
    /// </summary>
    Music = 1,

    /// <summary>A short non-speech marker. Loop-state cues (#20) are these.</summary>
    Cue = 2,

    /// <summary>An answer, spoken.</summary>
    Speech = 3,

    /// <summary>
    /// A journal-triggered danger callout (list.md Phase 8). Outranks speech because an alert
    /// that waits for the current sentence to finish is not an alert.
    /// </summary>
    Alert = 4,
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
/// <param name="MusicPlaying">
/// Defaulted, so a caller that predates the ambience layer still constructs one of these. The
/// panel and the caption layer read the first three; nothing yet reads this one, and it is here
/// because a snapshot that omits half the background is a snapshot that will be wrong later.
/// </param>
public sealed record AudioActivity(
    AudioChannel? Channel,
    string? Caption,
    bool BedPlaying,
    bool MusicPlaying = false);

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
/// The bed and the music are the two things not in the serial queue. Each is a background layer
/// with its own slot, because a loop that took its turn at the head of a queue would never give
/// it back — and two of them sharing one slot would be one of them cutting the other off every
/// time a turn started. They still enter through here and are still stopped by
/// <see cref="Silence"/>, so "everything audible goes through one arbiter" holds.
/// </para>
/// </summary>
public sealed class AudioArbiter(IAudioSink sink, ILogger<AudioArbiter> logger) : IDisposable
{
    private readonly Lock _gate = new();
    private readonly List<Pending> _queue = [];

    private long _nextId = 1;
    private Playing? _current;
    private Playing? _bed;
    private Playing? _music;
    private bool _subscribed;
    private AudioMix _mix = AudioMix.Default;

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

    /// <summary>
    /// Raised when an ambience track reached its end on its own. The host answers with the next
    /// one — track selection is not the arbiter's business, and a queue that picked its own music
    /// would be a queue that knows what a situation is.
    /// </summary>
    public event Action? MusicFinished;

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

    /// <summary>
    /// Per-category level, mute and ducking (list.md Phase 12, "#96 Ambient audio mixer").
    /// <para>
    /// Set rather than injected, because it follows a settings row and the arbiter outlives
    /// every value of it. Assigning re-levels whatever is playing rather than waiting for the
    /// next clip: a category turned down while it is audible has to go quiet now, or the control
    /// reads as having done nothing.
    /// </para>
    /// </summary>
    public AudioMix Mix
    {
        get
        {
            lock (_gate)
            {
                return _mix;
            }
        }

        set
        {
            lock (_gate)
            {
                _mix = value;
                Relevel();
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
            else if (request.Channel == AudioChannel.Music)
            {
                StartMusic(request);
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
            hadSomething = _current is not null || _bed is not null || _music is not null || _queue.Count > 0;

            _queue.Clear();
            _current = null;
            _bed = null;

            // Silence is silence. Music is a background layer rather than something d47 is
            // saying, but a Commander who asked for quiet asked for quiet.
            _music = null;
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

    /// <summary>
    /// Stops the ambience without touching anything else. Called when the situation changes, and
    /// when there is nothing to play for the one the Commander has moved into.
    /// </summary>
    public void StopMusic()
    {
        AudioActivity activity;

        lock (_gate)
        {
            if (_music is null)
            {
                return;
            }

            sink.Stop(_music.Id);
            _music = null;
            activity = Snapshot();
        }

        ActivityChanged?.Invoke(activity);
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
        sink.Play(new PlaybackRequest(id, request.Clip, Loop: true, Gain: GainFor(AudioChannel.Bed)));
    }

    /// <summary>
    /// One track, in the music slot. Not looped, whatever the request says: a track that looped
    /// would never finish, and finishing is how the next one is asked for.
    /// </summary>
    private void StartMusic(AudioRequest request)
    {
        if (_music is not null)
        {
            sink.Stop(_music.Id);
        }

        var id = _nextId++;
        _music = new Playing(id, request);
        sink.Play(new PlaybackRequest(id, request.Clip, Loop: false, Gain: GainFor(AudioChannel.Music)));
    }

    /// <summary>Start the head of the queue if nothing is playing, and re-level the rest either way.</summary>
    private void Pump()
    {
        if (_current is null && _queue.Count > 0)
        {
            var next = _queue[0];
            _queue.RemoveAt(0);
            _current = new Playing(next.Id, next.Request);
            sink.Play(new PlaybackRequest(
                next.Id,
                next.Request.Clip,
                next.Request.Loop,
                GainFor(next.Request.Channel)));
        }

        Relevel();
    }

    /// <summary>
    /// Re-states the gain of everything already playing. Called whenever the answer can have
    /// changed: something started, something finished, or the Commander moved a level.
    /// </summary>
    private void Relevel()
    {
        if (_bed is { } bed)
        {
            sink.SetGain(bed.Id, GainFor(AudioChannel.Bed));
        }

        if (_music is { } music)
        {
            sink.SetGain(music.Id, GainFor(AudioChannel.Music));
        }

        if (_current is { } playing)
        {
            sink.SetGain(playing.Id, GainFor(playing.Request.Channel));
        }
    }

    /// <summary>
    /// What a category plays at right now: its level, muted or not, times its duck factor when
    /// something is being said over it.
    /// <para>
    /// Ducking is a consequence of what is playing, recomputed rather than tracked. A duck held
    /// as its own boolean is a duck that eventually gets stuck on — which is why this generalises
    /// the bed's old <c>BedGain</c> rather than adding a second mechanism beside it.
    /// </para>
    /// </summary>
    private float GainFor(AudioChannel channel)
    {
        var speaking = _current?.Request.Channel is AudioChannel.Speech or AudioChannel.Alert;

        // Nothing ducks under itself. Speech and alerts are what everything else yields to, and
        // an alert that dropped its own level while it was the thing playing would be an alert
        // quietening itself.
        return _mix.For(channel).GainWhile(speaking && AudioMix.Ducks(channel));
    }

    private void OnSinkFinished(long playbackId)
    {
        AudioActivity activity;
        var trackEnded = false;

        lock (_gate)
        {
            if (_music?.Id == playbackId)
            {
                _music = null;
                trackEnded = true;
                activity = Snapshot();
            }
            else if (_current?.Id == playbackId)
            {
                _current = null;
                Pump();
                activity = Snapshot();
            }
            else
            {
                // A completion for something already stopped is normal: Silence and supersede
                // both race the sink by design, and the loser is this callback.
                return;
            }
        }

        ActivityChanged?.Invoke(activity);

        // Outside the lock, because the answer to it is another Enqueue.
        if (trackEnded)
        {
            MusicFinished?.Invoke();
        }
    }

    private AudioActivity Snapshot() =>
        new(_current?.Request.Channel, _current?.Request.Caption, _bed is not null, _music is not null);

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

using Microsoft.Extensions.Logging;

namespace D47.Core.Audio;

/// <summary>What a sound is for, which is also what it outranks.</summary>
public enum AudioChannel
{
    /// <summary>The looping bed under a working turn (Phase 5, #18).</summary>
    Bed = 0,

    /// <summary>Situational ambience (Phase 12, "Ambient music").</summary>
    Music = 1,

    /// <summary>A short non-speech marker.</summary>
    Cue = 2,

    /// <summary>An answer, spoken.</summary>
    Speech = 3,

    /// <summary>A journal-triggered danger callout (Phase 8).</summary>
    Alert = 4,
}

/// <summary>One thing to make audible.</summary>
public sealed record AudioRequest
{
    public required AudioChannel Channel { get; init; }

    public required AudioClip Clip { get; init; }

    /// <summary>The scope a supersede applies to — the turn id, for speech.</summary>
    public string? Group { get; init; }

    public bool Loop { get; init; }

    /// <summary>The text this is speaking, if any.</summary>
    public string? Caption { get; init; }
}

/// <summary>What the arbiter is doing right now.</summary>
/// <param name="MusicPlaying">
/// Defaulted, so a caller that predates the ambience layer still constructs one of these.
/// </param>
/// <param name="Utterance">
/// Which clip <paramref name="Caption"/> belongs to, or null when nothing is playing.
/// </param>
public sealed record AudioActivity(
    AudioChannel? Channel,
    string? Caption,
    bool BedPlaying,
    bool MusicPlaying = false,
    long? Utterance = null);

/// <summary>The one queue in front of every audible thing.</summary>
public sealed class AudioArbiter(IAudioSink sink, ILogger<AudioArbiter> logger) : IDisposable
{
    private readonly Lock _gate = new();
    private readonly List<Pending> _queue = [];
    private readonly HashSet<string> _closed = new(StringComparer.Ordinal);

    private long _nextId = 1;
    private Playing? _current;
    private Playing? _bed;
    private Playing? _music;
    private bool _subscribed;
    private AudioMix _mix = AudioMix.Default;

    private sealed record Pending(long Id, AudioRequest Request);

    private sealed record Playing(long Id, AudioRequest Request);

    /// <summary>Raised when everything was cut off — the Commander said stop.</summary>
    public event Action? Silenced;

    /// <summary>Raised whenever what is audible changes.</summary>
    public event Action<AudioActivity>? ActivityChanged;

    /// <summary>Raised when an ambience track reached its end on its own.</summary>
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

    /// <summary>Per-category level, mute and ducking (Phase 12, "#96 Ambient audio mixer").</summary>
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

    /// <summary>Subscribes to the sink.</summary>
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
            if (request.Group is { } closed && _closed.Contains(closed))
            {
                logger.LogDebug("{Group} is closed; {Clip} is not queued", closed, request.Clip.Name);
                return;
            }

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

                // Highest channel first; within a channel, arrival order.
                _queue.Sort(static (left, right) =>
                {
                    var byChannel = right.Request.Channel.CompareTo(left.Request.Channel);
                    return byChannel != 0 ? byChannel : left.Id.CompareTo(right.Id);
                });

                // Only an alert cuts in mid-playback, and the interrupted item is dropped rather than
                // resumed: half a sentence spoken before an interdiction warning does not become worth
                // finishing afterwards.
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

    /// <summary>Drops everything belonging to one group, playing or pending.</summary>
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
    /// Drops a group and refuses anything more in it until <see cref="OpenGroup"/>. A caller that
    /// synthesises ahead of playback has work in flight when it decides to stop, and that work arrives
    /// after the drop; this is what turns it away (#61).
    /// </summary>
    public void CloseGroup(string group)
    {
        lock (_gate)
        {
            _closed.Add(group);
        }

        DropGroup(group);
    }

    /// <summary>Lets a closed group back in.</summary>
    public void OpenGroup(string group)
    {
        lock (_gate)
        {
            _closed.Remove(group);
        }
    }

    /// <summary>Shut up (Phase 5).</summary>
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

            // Silence is silence.
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
    /// The audible consequence of the loop moving: the state's own cue, and the bed running for exactly
    /// as long as the turn does.
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

    /// <summary>Stops the ambience without touching anything else.</summary>
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

    /// <summary>One track, in the music slot.</summary>
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

    /// <summary>Re-states the gain of everything already playing.</summary>
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
    /// </summary>
    private float GainFor(AudioChannel channel)
    {
        var speaking = _current?.Request.Channel is AudioChannel.Speech or AudioChannel.Alert;

        // Nothing ducks under itself.
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
                // A completion for something already stopped is normal: Silence and supersede both race the
                // sink by design, and the loser is this callback.
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
        new(
            _current?.Request.Channel,
            _current?.Request.Caption,
            _bed is not null,
            _music is not null,
            _current?.Id);

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

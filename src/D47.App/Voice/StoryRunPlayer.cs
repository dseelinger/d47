using D47.Core.Audio;
using D47.Core.Interface;
using D47.Core.Messages;
using D47.Core.Stories;

namespace D47.App.Voice;

/// <summary>Why a story run's playback is paused.</summary>
public enum StoryRunPause
{
    None,

    /// <summary>The Commander pressed Pause.</summary>
    Commander,

    /// <summary>Push-to-talk opened the microphone.</summary>
    PushToTalk,

    /// <summary>An alert or a silence cut in.</summary>
    Interrupted,
}

/// <summary>A story run being played: its lines in page order, the one playing or paused at, and why it is paused.</summary>
public sealed record StoryRunPlayback(Story Run, IReadOnlyList<StoryRunLine> Lines, int Index, StoryRunPause Pause)
{
    public StoryRunLine Line => Lines[Index];

    public bool IsPaused => Pause != StoryRunPause.None;

    /// <summary>Whether this playback is of <paramref name="run"/>.</summary>
    public bool Of(Story run) =>
        string.Equals(Run.Id, run.Id, StringComparison.OrdinalIgnoreCase) && Run.PickedAt == run.PickedAt;
}

/// <summary>
/// Plays a story run's kept clips in order, holding the speech channels while it plays. Clips are read off
/// the calling thread through <c>background</c>; a line with no clip, or one that cannot be read, is skipped.
/// </summary>
public sealed class StoryRunPlayer
{
    /// <summary>The group every played line is queued in.</summary>
    public const string Group = "story-run-playback";

    private readonly AudioArbiter _audio;
    private readonly Func<Story, StoryRunLine, AudioClip?> _clip;
    private readonly Action<Action> _background;
    private readonly Lock _gate = new();

    private StoryRunPlayback? _now;
    private AudioRequest? _queued;
    private long _generation;

    /// <param name="clip">Reads a line's kept clip in the standard format, or null; called off the UI thread and the tick.</param>
    /// <param name="background">Runs work off the calling thread; the thread pool when null.</param>
    public StoryRunPlayer(AudioArbiter audio, Func<Story, StoryRunLine, AudioClip?> clip, Action<Action>? background = null)
    {
        _audio = audio;
        _clip = clip;
        _background = background ?? (work => _ = Task.Run(work));
        _audio.Played += OnPlayed;
        _audio.PlaybackPaused += () => Pause(StoryRunPause.Interrupted);
    }

    /// <summary>Raised when what is playing, or whether it is paused, changed; on any thread.</summary>
    public event Action? Changed;

    /// <summary>What is playing or paused, or null.</summary>
    public StoryRunPlayback? Now
    {
        get
        {
            lock (_gate)
            {
                return _now;
            }
        }
    }

    /// <summary>Plays <paramref name="lines"/> in order from <paramref name="from"/>.</summary>
    public void Play(Story run, IReadOnlyList<StoryRunLine> lines, int from)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(lines);

        if (from < 0 || from >= lines.Count)
        {
            return;
        }

        long generation;

        lock (_gate)
        {
            _now = new StoryRunPlayback(run, lines, from, StoryRunPause.None);
            _queued = null;
            generation = ++_generation;
            _audio.DropGroup(Group);
            _audio.HoldSpeechFor(Group);
        }

        Changed?.Invoke();
        Load(generation);
    }

    /// <summary>Pauses at the line playing; <see cref="Resume"/> plays it again from its start.</summary>
    public void Pause(StoryRunPause reason)
    {
        lock (_gate)
        {
            if (_now is not { IsPaused: false } now || reason == StoryRunPause.None)
            {
                return;
            }

            // _queued stays: a line that finished just before this pause still moves the player past it.
            _now = now with { Pause = reason };
            _generation++;
            _audio.PausePlayback();
        }

        Changed?.Invoke();
    }

    public void Resume()
    {
        long generation;

        lock (_gate)
        {
            if (_now is not { IsPaused: true } now)
            {
                return;
            }

            _now = now with { Pause = StoryRunPause.None };
            generation = ++_generation;
            _audio.HoldSpeechFor(Group);
        }

        Changed?.Invoke();
        Load(generation);
    }

    /// <summary>Moves to the next line: plays it, or stays paused at it.</summary>
    public void Skip()
    {
        long generation;
        bool playing;

        lock (_gate)
        {
            if (_now is not { } now)
            {
                return;
            }

            if (now.Index + 1 >= now.Lines.Count)
            {
                End();
                generation = 0;
                playing = false;
            }
            else
            {
                _now = now with { Index = now.Index + 1 };
                _queued = null;
                generation = ++_generation;
                playing = !now.IsPaused;

                if (playing)
                {
                    _audio.DropGroup(Group);
                }
            }
        }

        Changed?.Invoke();

        if (playing)
        {
            Load(generation);
        }
    }

    /// <summary>Ends playback and clears the playing line.</summary>
    public void Stop()
    {
        lock (_gate)
        {
            if (_now is null)
            {
                return;
            }

            End();
        }

        Changed?.Invoke();
    }

    private void End()
    {
        _now = null;
        _queued = null;
        _generation++;
        _audio.EndPlayback();
    }

    private void OnPlayed(AudioRequest request)
    {
        // Raised on the audio thread, possibly under the arbiter's lock: nothing here takes this player's lock.
        if (request.Group == Group)
        {
            _background(() => Advance(request));
        }
    }

    private void Advance(AudioRequest request)
    {
        long generation;

        lock (_gate)
        {
            if (!ReferenceEquals(request, _queued) || _now is not { } now)
            {
                return;
            }

            _queued = null;

            if (now.Index + 1 >= now.Lines.Count)
            {
                End();
                generation = 0;
            }
            else
            {
                _now = now with { Index = now.Index + 1 };
                generation = now.IsPaused ? 0 : ++_generation;
            }
        }

        Changed?.Invoke();

        if (generation != 0)
        {
            Load(generation);
        }
    }

    private void Load(long generation) => _background(() => LoadNow(generation));

    private void LoadNow(long generation)
    {
        StoryRunPlayback? from;

        lock (_gate)
        {
            from = generation == _generation ? _now : null;
        }

        if (from is null)
        {
            return;
        }

        for (var index = from.Index; index < from.Lines.Count; index++)
        {
            var line = from.Lines[index];

            if (line.Clip is null || _clip(from.Run, line) is not { } clip)
            {
                continue;
            }

            lock (_gate)
            {
                if (generation != _generation || _now is not { IsPaused: false } now)
                {
                    return;
                }

                _now = now with { Index = index };
                _queued = new AudioRequest
                {
                    Channel = AudioChannel.Speech,
                    Clip = clip,
                    Group = Group,
                    Caption = line.Body,
                    Picture = PictureOf(line),
                };
                _audio.Enqueue(_queued);
            }

            Changed?.Invoke();
            return;
        }

        lock (_gate)
        {
            if (generation != _generation)
            {
                return;
            }

            End();
        }

        Changed?.Invoke();
    }

    /// <summary>The picture a line is shown and spoken with.</summary>
    public static string? PictureOf(StoryRunLine line) =>
        line.Picture ?? (line.From == MessageStore.Narrator ? SpeakerPictures.Narrator : null);
}

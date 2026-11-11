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

/// <summary>What the player is doing with the line it is at.</summary>
public enum StoryRunLineState
{
    /// <summary>Its clip is queued or playing.</summary>
    Playing,

    /// <summary>It kept no clip, and one is being made in the speaker's current voice.</summary>
    SpeakingAgain,

    /// <summary>It cannot be spoken, and is shown for reading until <see cref="StoryRunPlayback.HeldUntil"/>.</summary>
    Held,
}

/// <summary>A story run being played: its lines in page order, the one playing or paused at, and why it is paused.</summary>
public sealed record StoryRunPlayback(Story Run, IReadOnlyList<StoryRunLine> Lines, int Index, StoryRunPause Pause)
{
    public StoryRunLine Line => Lines[Index];

    public bool IsPaused => Pause != StoryRunPause.None;

    public StoryRunLineState State { get; init; }

    /// <summary>The share of a new clip made so far, while <see cref="State"/> is <see cref="StoryRunLineState.SpeakingAgain"/>.</summary>
    public double Progress { get; init; }

    /// <summary>When a held line's reading time is up, while <see cref="State"/> is <see cref="StoryRunLineState.Held"/>.</summary>
    public DateTimeOffset? HeldUntil { get; init; }

    public StoryLineHold Hold { get; init; }

    /// <summary>The provider's error for a held line, where it gave one.</summary>
    public string? HoldDetail { get; init; }

    /// <summary>Whether this playback is of <paramref name="run"/>.</summary>
    public bool Of(Story run) =>
        string.Equals(Run.Id, run.Id, StringComparison.OrdinalIgnoreCase) && Run.PickedAt == run.PickedAt;

    /// <summary>At line <paramref name="index"/>, playing it.</summary>
    public StoryRunPlayback At(int index) =>
        this with { Index = index, State = StoryRunLineState.Playing, Progress = 0, HeldUntil = null, Hold = StoryLineHold.None, HoldDetail = null };
}

/// <summary>
/// Plays a story run's kept clips in order, holding the speech channels while it plays. Clips are read off the calling
/// thread through <c>background</c>. A line with no clip, or one that cannot be read, is spoken again through
/// <c>speakAgain</c>, and the next line with no clip is made while the line before it plays. A line that cannot be spoken
/// is held for its <see cref="ReadingTime"/>. Without <c>speakAgain</c> such a line is skipped.
/// </summary>
public sealed class StoryRunPlayer
{
    /// <summary>The group every played line is queued in.</summary>
    public const string Group = "story-run-playback";

    private readonly AudioArbiter _audio;
    private readonly Func<Story, StoryRunLine, AudioClip?> _clip;
    private readonly Action<Action> _background;
    private readonly Func<Story, StoryRunLine, Action<double>, Task<SpokenAgain>>? _speakAgain;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();

    private StoryRunPlayback? _now;
    private AudioRequest? _queued;
    private long _generation;
    private ITimer? _hold;

    /// <param name="clip">Reads a line's kept clip in the standard format, or null; called off the UI thread and the tick.</param>
    /// <param name="background">Runs work off the calling thread; the thread pool when null.</param>
    /// <param name="speakAgain">Makes the clip of a line that has none, reporting progress; called off the UI thread and the tick.</param>
    public StoryRunPlayer(
        AudioArbiter audio,
        Func<Story, StoryRunLine, AudioClip?> clip,
        Action<Action>? background = null,
        Func<Story, StoryRunLine, Action<double>, Task<SpokenAgain>>? speakAgain = null,
        TimeProvider? time = null)
    {
        _audio = audio;
        _clip = clip;
        _background = background ?? (work => _ = Task.Run(work));
        _speakAgain = speakAgain;
        _time = time ?? TimeProvider.System;
        _audio.Played += OnPlayed;
        _audio.PlaybackPaused += () => Pause(StoryRunPause.Interrupted);
    }

    /// <summary>Raised when what is playing, or whether it is paused, changed; on any thread.</summary>
    public event Action? Changed;

    /// <summary>How long a line that cannot be spoken is shown: half a second a word, and at least four seconds.</summary>
    public static TimeSpan ReadingTime(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        return TimeSpan.FromSeconds(Math.Max(4, 0.5 * words));
    }

    /// <summary>The clock a held line's countdown is read against.</summary>
    public TimeProvider Time => _time;

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
            CancelHold();
            _audio.DropGroup(Group);
            _audio.HoldSpeechFor(Group);
        }

        Changed?.Invoke();
        Load(generation);
    }

    /// <summary>Pauses at the line playing; <see cref="Resume"/> plays it again from its start. A Commander pause replaces any other reason.</summary>
    public void Pause(StoryRunPause reason)
    {
        lock (_gate)
        {
            if (_now is { IsPaused: true } held && reason == StoryRunPause.Commander && held.Pause != StoryRunPause.Commander)
            {
                _now = held with { Pause = reason };
            }
            else if (_now is not { IsPaused: false } now || reason == StoryRunPause.None)
            {
                return;
            }
            else
            {
                // _queued stays: a line that finished just before this pause still moves the player past it.
                _now = now with { Pause = reason };
                _generation++;
                CancelHold();
                _audio.PausePlayback();
            }
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

            _now = now.At(now.Index) with { Pause = StoryRunPause.None };
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
                _now = now.At(now.Index + 1);
                _queued = null;
                generation = ++_generation;
                playing = !now.IsPaused;
                CancelHold();

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
        CancelHold();
        _audio.EndPlayback();
    }

    /// <summary>Stops a held line's countdown. Called holding <see cref="_gate"/>.</summary>
    private void CancelHold()
    {
        _hold?.Dispose();
        _hold = null;
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
            generation = Next(now);
        }

        Changed?.Invoke();

        if (generation != 0)
        {
            Load(generation);
        }
    }

    /// <summary>A held line's reading time is up: moves to the next line.</summary>
    private void HeldOut(long generation)
    {
        lock (_gate)
        {
            if (generation != _generation || _now is not { State: StoryRunLineState.Held, IsPaused: false } now)
            {
                return;
            }

            _hold = null;
            generation = Next(now);
        }

        Changed?.Invoke();

        if (generation != 0)
        {
            Load(generation);
        }
    }

    /// <summary>Moves past the current line; the generation to load, or 0 when it ended or is paused. Called holding <see cref="_gate"/>.</summary>
    private long Next(StoryRunPlayback now)
    {
        if (now.Index + 1 >= now.Lines.Count)
        {
            End();
            return 0;
        }

        _now = now.At(now.Index + 1);
        return now.IsPaused ? 0 : ++_generation;
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
            var clip = line.Clip is null ? null : _clip(from.Run, line);

            if (clip is not null)
            {
                if (Queue(generation, index, clip))
                {
                    Changed?.Invoke();
                    Ahead(from.Run, from.Lines, index);
                }

                return;
            }

            if (_speakAgain is null)
            {
                continue;
            }

            lock (_gate)
            {
                if (generation != _generation || _now is not { IsPaused: false } now)
                {
                    return;
                }

                _now = now.At(index) with { State = StoryRunLineState.SpeakingAgain };
            }

            Changed?.Invoke();
            _ = SpeakAgainAsync(generation, from.Run, from.Lines, index);
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

    /// <summary>Queues <paramref name="clip"/> as line <paramref name="index"/>, unless playback moved on or paused.</summary>
    private bool Queue(long generation, int index, AudioClip clip)
    {
        var levelled = SpeechLeveller.Apply(clip);

        lock (_gate)
        {
            if (generation != _generation || _now is not { IsPaused: false } now)
            {
                return false;
            }

            var line = now.Lines[index];
            _now = now.At(index);
            _queued = new AudioRequest
            {
                Channel = AudioChannel.Speech,
                Clip = levelled,
                Group = Group,
                Caption = line.Body,
                Picture = PictureOf(line),
            };
            _audio.Enqueue(_queued);
            return true;
        }
    }

    /// <summary>Makes line <paramref name="index"/>'s clip and plays it, or holds the line for reading when it cannot be spoken.</summary>
    private async Task SpeakAgainAsync(long generation, Story run, IReadOnlyList<StoryRunLine> lines, int index)
    {
        var line = lines[index];
        SpokenAgain spoken;

        try
        {
            spoken = await _speakAgain!(run, line, share => Progressed(line, share)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            spoken = SpokenAgain.Held(StoryLineHold.Failed, ex.Message);
        }

        if (spoken.Clip is { } clip)
        {
            if (Queue(generation, index, clip))
            {
                Changed?.Invoke();
                Ahead(run, lines, index);
            }

            return;
        }

        lock (_gate)
        {
            if (generation != _generation || _now is not { IsPaused: false } now || now.Index != index)
            {
                return;
            }

            var reading = ReadingTime(line.Body);
            _now = now.At(index) with
            {
                State = StoryRunLineState.Held,
                HeldUntil = _time.GetUtcNow() + reading,
                Hold = spoken.Hold == StoryLineHold.None ? StoryLineHold.Failed : spoken.Hold,
                HoldDetail = spoken.Detail,
            };
            CancelHold();
            _hold = _time.CreateTimer(_ => _background(() => HeldOut(generation)), null, reading, Timeout.InfiniteTimeSpan);
        }

        Changed?.Invoke();
        Ahead(run, lines, index);
    }

    /// <summary>Starts making the clip of the next line after <paramref name="index"/> that kept none, while this one plays or is read.</summary>
    private void Ahead(Story run, IReadOnlyList<StoryRunLine> lines, int index)
    {
        if (_speakAgain is null)
        {
            return;
        }

        for (var next = index + 1; next < lines.Count; next++)
        {
            if (lines[next].Clip is null)
            {
                var line = lines[next];
                _ = _speakAgain(run, line, share => Progressed(line, share));
                return;
            }
        }
    }

    private void Progressed(StoryRunLine line, double share)
    {
        lock (_gate)
        {
            if (_now is not { State: StoryRunLineState.SpeakingAgain } now || !ReferenceEquals(now.Line, line))
            {
                return;
            }

            _now = now with { Progress = share };
        }

        Changed?.Invoke();
    }

    /// <summary>The picture a line is shown and spoken with.</summary>
    public static string? PictureOf(StoryRunLine line) =>
        line.Picture ?? (line.From == MessageStore.Narrator ? SpeakerPictures.Narrator : null);
}

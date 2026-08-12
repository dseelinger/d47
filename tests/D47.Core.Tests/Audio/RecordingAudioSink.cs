using D47.Core.Audio;

namespace D47.Core.Tests.Audio;

/// <summary>
/// The null sink from architecture.md §8: it renders nothing and records every queue
/// operation instead. Ducking, supersede and interruption ordering are all assertable
/// against this with no audio device present, which is the only reason those behaviours can
/// be covered at all — the alternative is listening, and listening is not a test.
/// </summary>
public sealed class RecordingAudioSink : IAudioSink
{
    private readonly List<string> _log = [];

    public List<PlaybackRequest> Started { get; } = [];

    public List<long> Stopped { get; } = [];

    public List<(long Id, float Gain)> GainChanges { get; } = [];

    public int StopAllCount { get; private set; }

    /// <summary>Ids started and not since stopped or finished. "What would be audible now."</summary>
    public IReadOnlyCollection<long> Live =>
        [.. Started.Select(request => request.Id).Where(id => !Stopped.Contains(id) && !_finished.Contains(id))];

    private readonly List<long> _finished = [];

    /// <summary>Every call in order, for asserting that one thing happened before another.</summary>
    public IReadOnlyList<string> Log => _log;

    public event Action<long>? Finished;

    public IRenderReferenceTap ReferenceTap { get; } = new NullTap();

    public void Play(PlaybackRequest request)
    {
        Started.Add(request);
        _log.Add($"play {request.Id} {request.Clip.Name}{(request.Loop ? " loop" : "")} gain={request.Gain:0.##}");
    }

    public void Stop(long playbackId)
    {
        Stopped.Add(playbackId);
        _log.Add($"stop {playbackId}");
    }

    public void StopAll()
    {
        StopAllCount++;
        _log.Add("stopAll");
    }

    public void SetGain(long playbackId, float gain)
    {
        GainChanges.Add((playbackId, gain));
        _log.Add($"gain {playbackId} {gain:0.##}");
    }

    /// <summary>Stands in for a clip reaching its end. The arbiter only advances when this happens.</summary>
    public void CompletePlayback(long playbackId)
    {
        _finished.Add(playbackId);
        _log.Add($"finished {playbackId}");
        Finished?.Invoke(playbackId);
    }

    /// <summary>Completes whatever is currently live and not looping — the usual "let it play out".</summary>
    public void CompleteCurrent()
    {
        var current = Started
            .Where(request => !request.Loop)
            .Select(request => request.Id)
            .LastOrDefault(id => !Stopped.Contains(id) && !_finished.Contains(id));

        if (current != 0)
        {
            CompletePlayback(current);
        }
    }

    /// <summary>
    /// Lets everything queued play out, one after another. Needed because only the head of
    /// the queue is ever started — asserting the *order* of a reply means running the queue
    /// down, not reading what happens to be playing.
    /// </summary>
    public void PlayOutAll()
    {
        for (var guard = 0; guard < 1000; guard++)
        {
            var before = Started.Count;
            CompleteCurrent();

            if (Started.Count == before)
            {
                return;
            }
        }

        throw new InvalidOperationException("the queue never drained");
    }

    public float? GainOf(long id) =>
        GainChanges.Count == 0
            ? Started.FirstOrDefault(request => request.Id == id)?.Gain
            : GainChanges.Where(change => change.Id == id)
                .Select(change => (float?)change.Gain)
                .LastOrDefault()
              ?? Started.FirstOrDefault(request => request.Id == id)?.Gain;

    private sealed class NullTap : IRenderReferenceTap
    {
        public event Action<RenderReferenceFrame>? Rendered;

        /// <summary>Nothing raises this on the null sink; it exists so the seam is exercised.</summary>
        public void Ignore() => Rendered?.Invoke(default);
    }
}

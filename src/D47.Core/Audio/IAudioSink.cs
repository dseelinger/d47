namespace D47.Core.Audio;

/// <summary>
/// One buffer of what actually went to the speakers, and where it sat in the render stream.
/// <para>
/// <see cref="SamplePosition"/> is the count of frames the sink has rendered since it opened
/// — a monotonic counter, not a wall clock. That is what acoustic echo cancellation needs to
/// line the far-end signal up against capture, and it is also the only kind of timestamp Core
/// is allowed to carry, since no Core component reads the clock (architecture.md §8).
/// </para>
/// </summary>
public readonly record struct RenderReferenceFrame(
    long SamplePosition,
    ReadOnlyMemory<byte> Pcm,
    AudioFormat Format);

/// <summary>
/// A copy of the mixed output, for AEC3 to subtract from capture in Phase 13.
/// <para>
/// This exists now, with nothing consuming it, on purpose. Retrofitting a tap means opening
/// the one component every voice path depends on, at the point where echo cancellation is
/// already hard enough (architecture.md D7). Exposed from day one, it is a subscription.
/// </para>
/// </summary>
public interface IRenderReferenceTap
{
    event Action<RenderReferenceFrame>? Rendered;
}

public sealed record PlaybackRequest(long Id, AudioClip Clip, bool Loop, float Gain);

/// <summary>
/// The hardware end of the arbiter. Everything above this line is queue policy and is
/// testable with no audio device; everything below it is WASAPI (architecture.md §8).
/// </summary>
public interface IAudioSink
{
    /// <summary>Starts rendering and returns immediately. Completion arrives on <see cref="Finished"/>.</summary>
    void Play(PlaybackRequest request);

    /// <summary>
    /// Stops one playback. Does not raise <see cref="Finished"/> — a stop is the arbiter's own
    /// decision and it already knows. Only a clip ending by itself is news.
    /// </summary>
    void Stop(long playbackId);

    void StopAll();

    /// <summary>Live gain change, used to duck the bed under speech rather than restart it.</summary>
    void SetGain(long playbackId, float gain);

    /// <summary>Raised when a clip reaches its end on its own. Never raised for a looping clip.</summary>
    event Action<long>? Finished;

    IRenderReferenceTap ReferenceTap { get; }
}

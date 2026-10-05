namespace D47.Core.Audio;

/// <summary>One buffer of what actually went to the speakers, and where it sat in the render stream.</summary>
public readonly record struct RenderReferenceFrame(
    long SamplePosition,
    ReadOnlyMemory<byte> Pcm,
    AudioFormat Format);

/// <summary>A copy of the mixed output, for AEC3 to subtract from capture in Phase 13.</summary>
public interface IRenderReferenceTap
{
    event Action<RenderReferenceFrame>? Rendered;
}

/// <summary>
/// One thing for the sink to render: a clip held in memory, a track streamed from disk, or a clip still
/// arriving.
/// </summary>
public sealed record PlaybackRequest(long Id, AudioClip? Clip, bool Loop, float Gain)
{
    /// <summary>Set in place of <see cref="Clip"/> for a streamed track.</summary>
    public MusicTrack? Track { get; init; }

    /// <summary>Set in place of <see cref="Clip"/> for a clip still arriving.</summary>
    public ArrivingClip? Arriving { get; init; }

    public string Name => Clip?.Name ?? Track?.Name ?? Arriving?.Name ?? string.Empty;

    public static PlaybackRequest Stream(long id, MusicTrack track, float gain) =>
        new(id, Clip: null, Loop: false, gain) { Track = track };

    public static PlaybackRequest Streaming(long id, ArrivingClip arriving, float gain) =>
        new(id, Clip: null, Loop: false, gain) { Arriving = arriving };
}

/// <summary>The hardware end of the arbiter.</summary>
public interface IAudioSink
{
    /// <summary>Starts rendering and returns immediately.</summary>
    void Play(PlaybackRequest request);

    /// <summary>Stops one playback.</summary>
    void Stop(long playbackId);

    void StopAll();

    /// <summary>Holds one playback at its position, rendering silence without reading from it.</summary>
    void Pause(long playbackId);

    /// <summary>Continues a paused playback from where it was held.</summary>
    void Resume(long playbackId);

    /// <summary>Live gain change, used to duck the bed under speech rather than restart it.</summary>
    void SetGain(long playbackId, float gain);

    /// <summary>
    /// Raised when a clip, track or arriving clip reaches its end on its own, including a track that failed
    /// to open or to read.
    /// </summary>
    event Action<long>? Finished;

    IRenderReferenceTap ReferenceTap { get; }
}

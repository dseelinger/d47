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

/// <summary>One thing for the sink to render: a clip held in memory, or a track streamed from disk.</summary>
public sealed record PlaybackRequest(long Id, AudioClip? Clip, bool Loop, float Gain)
{
    /// <summary>Set in place of <see cref="Clip"/> for a streamed track.</summary>
    public MusicTrack? Track { get; init; }

    public string Name => Clip?.Name ?? Track?.Name ?? string.Empty;

    public static PlaybackRequest Stream(long id, MusicTrack track, float gain) =>
        new(id, Clip: null, Loop: false, gain) { Track = track };
}

/// <summary>The hardware end of the arbiter.</summary>
public interface IAudioSink
{
    /// <summary>Starts rendering and returns immediately.</summary>
    void Play(PlaybackRequest request);

    /// <summary>Stops one playback.</summary>
    void Stop(long playbackId);

    void StopAll();

    /// <summary>Live gain change, used to duck the bed under speech rather than restart it.</summary>
    void SetGain(long playbackId, float gain);

    /// <summary>
    /// Raised when a clip or track reaches its end on its own, including a track that failed to open or
    /// to read.
    /// </summary>
    event Action<long>? Finished;

    IRenderReferenceTap ReferenceTap { get; }
}

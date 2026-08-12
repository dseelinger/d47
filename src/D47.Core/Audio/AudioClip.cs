namespace D47.Core.Audio;

/// <summary>
/// The format every clip in the app is in by the time it reaches the arbiter. Fixed rather
/// than negotiated: resampling is the sink's problem exactly once, at the device boundary,
/// and a queue holding buffers of assorted rates is a mixer waiting to be written twice.
/// </summary>
public sealed record AudioFormat(int SampleRate, int Channels)
{
    /// <summary>What the shipped cues are, and what TTS output is converted to.</summary>
    public static readonly AudioFormat Standard = new(48_000, 1);

    public int BytesPerFrame => Channels * 2;

    public TimeSpan DurationOf(int byteCount) =>
        TimeSpan.FromSeconds((double)byteCount / (SampleRate * BytesPerFrame));
}

/// <summary>
/// Signed 16-bit little-endian PCM, ready to render. Core holds these because they are just
/// bytes — parsing a RIFF header and slicing samples needs no audio device, which is what
/// keeps the cue library testable with no hardware (architecture.md §8).
/// </summary>
public sealed record AudioClip(string Name, ReadOnlyMemory<byte> Pcm, AudioFormat Format)
{
    public TimeSpan Duration => Format.DurationOf(Pcm.Length);
}

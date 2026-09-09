namespace D47.Core.Audio;

/// <summary>The format every clip in the app is in by the time it reaches the arbiter.</summary>
public sealed record AudioFormat(int SampleRate, int Channels)
{
    /// <summary>What the shipped cues are, and what TTS output is converted to.</summary>
    public static readonly AudioFormat Standard = new(48_000, 1);

    public int BytesPerFrame => Channels * 2;

    public TimeSpan DurationOf(int byteCount) =>
        TimeSpan.FromSeconds((double)byteCount / (SampleRate * BytesPerFrame));
}

/// <summary>Signed 16-bit little-endian PCM, ready to render.</summary>
public sealed record AudioClip(string Name, ReadOnlyMemory<byte> Pcm, AudioFormat Format)
{
    public TimeSpan Duration => Format.DurationOf(Pcm.Length);
}

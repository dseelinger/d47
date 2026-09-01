namespace D47.Core.Audio;

/// <summary>
/// The other half of <see cref="WavReader"/>: 16-bit PCM out, as a file anything can open.
/// <para>
/// It exists for the audio recorder (<a href="https://github.com/dseelinger/d47/issues/164">#164</a>),
/// which retains what crossed the audio boundary in both directions. A retained clip has to be
/// playable in whatever the Commander already has — the whole point of keeping it is that a
/// person can listen to it and hear what d47 heard — so it is a plain WAV rather than a raw
/// buffer only d47 can interpret.
/// </para>
/// <para>
/// Deliberately as narrow as the reader: one canonical 44-byte header, no LIST chunk, no
/// metadata. What a recorder needs to say about a clip is in the index beside it, where it can
/// be read without parsing audio.
/// </para>
/// </summary>
public static class WavWriter
{
    /// <summary>The canonical PCM header. RIFF, fmt (16 bytes) and data, in that order.</summary>
    public const int HeaderBytes = 44;

    /// <summary>One clip's bytes, header and all.</summary>
    public static byte[] ToBytes(ReadOnlySpan<byte> pcm, AudioFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);

        var file = new byte[HeaderBytes + pcm.Length];
        var header = file.AsSpan(0, HeaderBytes);

        "RIFF"u8.CopyTo(header);
        Write32(header[4..], (uint)(HeaderBytes - 8 + pcm.Length));
        "WAVE"u8.CopyTo(header[8..]);

        "fmt "u8.CopyTo(header[12..]);
        Write32(header[16..], 16);
        Write16(header[20..], 1); // Uncompressed PCM, the one encoding the reader accepts.
        Write16(header[22..], (ushort)format.Channels);
        Write32(header[24..], (uint)format.SampleRate);
        Write32(header[28..], (uint)(format.SampleRate * format.BytesPerFrame));
        Write16(header[32..], (ushort)format.BytesPerFrame);
        Write16(header[34..], 16);

        "data"u8.CopyTo(header[36..]);
        Write32(header[40..], (uint)pcm.Length);

        pcm.CopyTo(file.AsSpan(HeaderBytes));

        return file;
    }

    /// <summary>
    /// The same, from the float samples the capture path carries.
    /// <para>
    /// Clamped rather than scaled to fit: a sample outside ±1 is a fault upstream, and quietly
    /// rescaling the whole clip to hide it would make the recorder lie about what was heard —
    /// which is the one thing it exists not to do.
    /// </para>
    /// </summary>
    public static byte[] ToBytes(ReadOnlySpan<float> samples, int sampleRate, int channels = 1)
    {
        var pcm = new byte[samples.Length * 2];

        for (var i = 0; i < samples.Length; i++)
        {
            var sample = (short)Math.Clamp(samples[i] * 32767f, short.MinValue, short.MaxValue);
            pcm[i * 2] = (byte)sample;
            pcm[(i * 2) + 1] = (byte)(sample >> 8);
        }

        return ToBytes(pcm, new AudioFormat(sampleRate, channels));
    }

    private static void Write16(Span<byte> into, ushort value)
    {
        into[0] = (byte)value;
        into[1] = (byte)(value >> 8);
    }

    private static void Write32(Span<byte> into, uint value)
    {
        into[0] = (byte)value;
        into[1] = (byte)(value >> 8);
        into[2] = (byte)(value >> 16);
        into[3] = (byte)(value >> 24);
    }
}

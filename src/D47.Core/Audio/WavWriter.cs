namespace D47.Core.Audio;

/// <summary>The other half of <see cref="WavReader"/>: 16-bit PCM out, as a file anything can open.</summary>
public static class WavWriter
{
    /// <summary>The canonical PCM header.</summary>
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

    /// <summary>The same, from the float samples the capture path carries.</summary>
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

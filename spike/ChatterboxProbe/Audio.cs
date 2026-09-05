using System.Text;
using NAudio.Wave;

namespace ChatterboxProbe;

/// <summary>
/// The librosa-shaped half of the pipeline, which is the part the Python wrappers get for free and
/// d47 would not: a reference clip has to arrive as mono float at 24 kHz before the speech encoder
/// will look at it. It is less than it sounds — read the RIFF, downmix, resample.
/// </summary>
internal static class Audio
{
    public static (float[] Samples, int Rate) ReadWav(string path)
    {
        var bytes = File.ReadAllBytes(path);

        // Anything that is not a RIFF goes to Media Foundation, which is how a clip recorded by
        // Windows Sound Recorder arrives: it writes .m4a and offers no WAV in its own interface.
        // Asking for the file to be converted first is friction the probe can absorb, and the
        // decoder is a Windows component rather than a new dependency to ship.
        if (bytes.Length < 12 ||
            Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF" ||
            Encoding.ASCII.GetString(bytes, 8, 4) != "WAVE")
        {
            return ReadThroughMediaFoundation(path);
        }

        var at = 12;
        int format = 0, channels = 0, rate = 0, bits = 0;

        while (at + 8 <= bytes.Length)
        {
            var chunk = Encoding.ASCII.GetString(bytes, at, 4);
            var size = BitConverter.ToInt32(bytes, at + 4);
            var body = at + 8;

            if (chunk == "fmt ")
            {
                format = BitConverter.ToUInt16(bytes, body);
                channels = BitConverter.ToUInt16(bytes, body + 2);
                rate = BitConverter.ToInt32(bytes, body + 4);
                bits = BitConverter.ToUInt16(bytes, body + 14);
            }
            else if (chunk == "data")
            {
                return (Decode(bytes, body, size, format, channels, bits), rate);
            }

            at = body + size + (size & 1);
        }

        throw new InvalidDataException($"{path} has no data chunk.");
    }

    /// <summary>
    /// Whatever Windows can decode — .m4a, .mp3, .wma, .flac — downmixed to mono, at whatever rate
    /// the file carries. The caller resamples, exactly as it does for a WAV.
    /// </summary>
    private static (float[] Samples, int Rate) ReadThroughMediaFoundation(string path)
    {
        using var reader = new MediaFoundationReader(path);

        var channels = reader.WaveFormat.Channels;
        var samples = reader.ToSampleProvider();
        var buffer = new float[reader.WaveFormat.SampleRate * channels];
        var mono = new List<float>();
        int read;

        while ((read = samples.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (var i = 0; i + channels <= read; i += channels)
            {
                var sum = 0f;

                for (var c = 0; c < channels; c++)
                {
                    sum += buffer[i + c];
                }

                mono.Add(sum / channels);
            }
        }

        return ([.. mono], reader.WaveFormat.SampleRate);
    }

    private static float[] Decode(byte[] bytes, int at, int size, int format, int channels, int bits)
    {
        // WAVE_FORMAT_EXTENSIBLE (0xFFFE) carries the real tag in a sub-format GUID; the bit depth
        // is enough to tell the two cases apart for anything a microphone or a TTS writes.
        var isFloat = format == 3 || (format == 0xFFFE && bits == 32);
        var stride = bits / 8;
        var frames = size / stride / channels;
        var mono = new float[frames];

        for (var frame = 0; frame < frames; frame++)
        {
            var sum = 0f;

            for (var channel = 0; channel < channels; channel++)
            {
                var offset = at + ((frame * channels) + channel) * stride;

                sum += (bits, isFloat) switch
                {
                    (32, true) => BitConverter.ToSingle(bytes, offset),
                    (16, _) => BitConverter.ToInt16(bytes, offset) / 32768f,
                    (32, false) => BitConverter.ToInt32(bytes, offset) / 2147483648f,
                    (24, _) => ((bytes[offset] << 8 | bytes[offset + 1] << 16 | bytes[offset + 2] << 24) >> 8) / 8388608f,
                    (8, _) => (bytes[offset] - 128) / 128f,
                    _ => throw new InvalidDataException($"{bits}-bit {(isFloat ? "float" : "integer")} PCM is not handled."),
                };
            }

            mono[frame] = sum / channels;
        }

        return mono;
    }

    /// <summary>
    /// Linear interpolation, with a box pre-filter when going down. Not soxr, and the difference
    /// would matter if the reference clip were the thing being judged — it is not; the model's
    /// output is. Feed it 24 kHz and this does nothing at all.
    /// </summary>
    public static float[] Resample(float[] samples, int from, int to)
    {
        if (from == to || samples.Length == 0)
        {
            return samples;
        }

        var source = from > to ? Smooth(samples, (float)from / to) : samples;
        var length = (int)((long)samples.Length * to / from);
        var resampled = new float[length];
        var step = (double)from / to;

        for (var i = 0; i < length; i++)
        {
            var position = i * step;
            var left = (int)position;
            var right = Math.Min(left + 1, source.Length - 1);
            var fraction = (float)(position - left);

            resampled[i] = source[left] * (1 - fraction) + source[right] * fraction;
        }

        return resampled;
    }

    private static float[] Smooth(float[] samples, float ratio)
    {
        var width = Math.Max(1, (int)Math.Round(ratio));

        if (width == 1)
        {
            return samples;
        }

        var smoothed = new float[samples.Length];

        for (var i = 0; i < samples.Length; i++)
        {
            var sum = 0f;
            var count = 0;

            for (var j = Math.Max(0, i - width / 2); j <= Math.Min(samples.Length - 1, i + width / 2); j++)
            {
                sum += samples[j];
                count++;
            }

            smoothed[i] = sum / count;
        }

        return smoothed;
    }

    /// <summary>16-bit mono PCM, so the result can be listened to rather than only measured.</summary>
    public static void WriteWav(string path, float[] samples, int rate)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.UTF8);

        var bytes = samples.Length * 2;

        writer.Write("RIFF"u8);
        writer.Write(36 + bytes);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(rate);
        writer.Write(rate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(bytes);

        foreach (var sample in samples)
        {
            writer.Write((short)(Math.Clamp(sample, -1f, 1f) * short.MaxValue));
        }
    }
}

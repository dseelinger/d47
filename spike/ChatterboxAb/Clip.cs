using NAudio.Vorbis;
using NAudio.Wave;

namespace ChatterboxAb;

/// <summary>
/// Turns whatever a fan site serves into the one thing the cloner wants: five to seven seconds of
/// one voice, mono, 24 kHz, trimmed and level. Soundboard clips are one to three seconds each, so a
/// voice is usually several files joined with a short gap.
/// </summary>
internal static class Clip
{
    public const int Rate = 24000;

    private const double MinSeconds = 5.0;
    private const double MaxSeconds = 7.0;
    private const double GapSeconds = 0.15;

    /// <param name="where">
    /// For a long recording, the stretch that holds the good speech, as "m:ss-m:ss" or "s-s";
    /// null takes the file from the top.
    /// </param>
    public static float[] Prepare(IReadOnlyList<string> files, string? where)
    {
        var joined = new List<float>();
        var gap = new float[(int)(GapSeconds * Rate)];
        var first = true;

        foreach (var file in files)
        {
            var (samples, rate) = Decode(file);
            var mono = Resample(samples, rate, Rate);

            if (first && where is not null)
            {
                mono = Window(mono, where);
            }

            mono = Trim(mono);

            if (mono.Length == 0)
            {
                continue;
            }

            if (!first)
            {
                joined.AddRange(gap);
            }

            first = false;

            var room = (int)(MaxSeconds * Rate) - joined.Count;

            if (mono.Length > room)
            {
                joined.AddRange(mono.AsSpan(0, Math.Max(0, room)));
                break;
            }

            joined.AddRange(mono);

            if (joined.Count >= MinSeconds * Rate)
            {
                break;
            }
        }

        return Normalise([.. joined]);
    }

    public static double Seconds(float[] samples) => samples.Length / (double)Rate;

    public static (float[] Samples, int Rate) Decode(string path)
    {
        using WaveStream reader = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".ogg" => new VorbisWaveReader(path),
            ".wav" => new WaveFileReader(path),
            _ => new MediaFoundationReader(path),
        };

        var provider = reader.ToSampleProvider();
        var channels = reader.WaveFormat.Channels;
        var buffer = new float[reader.WaveFormat.SampleRate * channels];
        var mono = new List<float>();
        int read;

        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
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

    private static float[] Window(float[] samples, string where)
    {
        var parts = where.Split('-');

        if (parts.Length != 2)
        {
            return samples;
        }

        var from = (int)(Parse(parts[0]) * Rate);
        var to = (int)(Parse(parts[1]) * Rate);

        from = Math.Clamp(from, 0, samples.Length);
        to = Math.Clamp(to, from, samples.Length);

        return samples[from..to];

        static double Parse(string clock)
        {
            var bits = clock.Trim().Split(':');
            return bits.Length == 2
                ? double.Parse(bits[0]) * 60 + double.Parse(bits[1])
                : double.Parse(bits[0]);
        }
    }

    /// <summary>Leading and trailing quiet below -45 dBFS goes, keeping 40 ms either side.</summary>
    private static float[] Trim(float[] samples)
    {
        const float Floor = 0.0056f;
        var pad = (int)(0.04 * Rate);
        var start = 0;
        var end = samples.Length;

        while (start < end && Math.Abs(samples[start]) < Floor)
        {
            start++;
        }

        while (end > start && Math.Abs(samples[end - 1]) < Floor)
        {
            end--;
        }

        start = Math.Max(0, start - pad);
        end = Math.Min(samples.Length, end + pad);

        return samples[start..end];
    }

    private static float[] Normalise(float[] samples)
    {
        var peak = 0f;

        foreach (var s in samples)
        {
            peak = Math.Max(peak, Math.Abs(s));
        }

        if (peak < 1e-4f)
        {
            return samples;
        }

        var gain = 0.7f / peak;

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] *= gain;
        }

        return samples;
    }

    private static float[] Resample(float[] samples, int from, int to)
    {
        if (from == to)
        {
            return samples;
        }

        var count = (int)((long)samples.Length * to / from);
        var output = new float[count];

        for (var i = 0; i < count; i++)
        {
            var position = i * (double)from / to;
            var index = (int)position;
            var fraction = (float)(position - index);
            var next = Math.Min(index + 1, samples.Length - 1);
            output[i] = samples[index] * (1 - fraction) + samples[next] * fraction;
        }

        return output;
    }

    public static void WriteWav(string path, float[] samples)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var writer = new WaveFileWriter(path, new WaveFormat(Rate, 16, 1));
        var bytes = new byte[samples.Length * 2];

        for (var i = 0; i < samples.Length; i++)
        {
            var value = (short)Math.Clamp(samples[i] * 32767, -32768, 32767);
            bytes[i * 2] = (byte)value;
            bytes[i * 2 + 1] = (byte)(value >> 8);
        }

        writer.Write(bytes, 0, bytes.Length);
    }
}

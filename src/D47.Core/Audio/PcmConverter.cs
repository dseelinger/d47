using System.Buffers.Binary;

namespace D47.Core.Audio;

/// <summary>
/// Converts 16-bit PCM of any rate and channel count to <see cref="AudioFormat.Standard"/>: channels
/// averaged to mono, then resampled by linear interpolation.
/// </summary>
public static class PcmConverter
{
    public static AudioClip ToStandard(AudioClip clip)
    {
        if (clip.Format == AudioFormat.Standard)
        {
            return clip;
        }

        var source = new WavPcmStream(new MemoryStream(clip.Pcm.ToArray()), clip.Format, clip.Pcm.Length);
        using var converted = new ConvertingPcmStream(source);
        using var output = new MemoryStream();
        var chunk = new byte[16_384];
        int read;

        while ((read = converted.Read(chunk)) > 0)
        {
            output.Write(chunk, 0, read);
        }

        return new AudioClip(clip.Name, output.ToArray(), AudioFormat.Standard);
    }

    /// <summary>Float samples in -1..1 as 16-bit little-endian PCM, truncating toward zero.</summary>
    public static byte[] FloatsToPcm16(ReadOnlySpan<float> samples)
    {
        var pcm = new byte[samples.Length * 2];

        for (var i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(
                pcm.AsSpan(i * 2),
                (short)(Math.Clamp(samples[i], -1f, 1f) * short.MaxValue));
        }

        return pcm;
    }

    /// <summary>16-bit little-endian PCM as floats in -1..1.</summary>
    public static float[] Pcm16ToFloats(ReadOnlySpan<byte> pcm)
    {
        var samples = new float[pcm.Length / 2];

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = BinaryPrimitives.ReadInt16LittleEndian(pcm[(i * 2)..]) / 32768f;
        }

        return samples;
    }

    /// <summary><paramref name="pcm"/> itself when nothing is held, otherwise the held bytes followed by it.</summary>
    public static ReadOnlySpan<byte> Join(byte[] held, ReadOnlySpan<byte> pcm)
    {
        if (held.Length == 0)
        {
            return pcm;
        }

        var joined = new byte[held.Length + pcm.Length];

        held.CopyTo(joined, 0);
        pcm.CopyTo(joined.AsSpan(held.Length));
        return joined;
    }

    /// <summary>The stream itself when it is already standard. Takes ownership of <paramref name="source"/>.</summary>
    public static IPcmStream ToStandard(WavPcmStream source) =>
        source.Format == AudioFormat.Standard ? source : new ConvertingPcmStream(source);
}

internal sealed class ConvertingPcmStream : IPcmStream
{
    private const int Capacity = 4096;

    private readonly WavPcmStream _source;
    private readonly int _channels;

    /// <summary>Source samples per output sample.</summary>
    private readonly double _step;

    private readonly byte[] _raw;
    private readonly float[] _mono = new float[Capacity];
    private int _count;

    /// <summary>Where the next output sample falls, in <see cref="_mono"/> indices.</summary>
    private double _position;

    private bool _ended;

    public ConvertingPcmStream(WavPcmStream source)
    {
        _source = source;
        _channels = source.Format.Channels;
        _step = (double)source.Format.SampleRate / AudioFormat.Standard.SampleRate;
        _raw = new byte[Capacity * source.Format.BytesPerFrame];
    }

    public int Read(Span<byte> buffer)
    {
        var written = 0;

        while (written + 2 <= buffer.Length)
        {
            var index = (int)_position;

            if (index + 1 >= _count && !_ended)
            {
                Refill();
                continue;
            }

            if (index >= _count)
            {
                break;
            }

            var here = _mono[index];
            var next = index + 1 < _count ? _mono[index + 1] : here;
            var sample = here + ((next - here) * (_position - index));

            BinaryPrimitives.WriteInt16LittleEndian(
                buffer[written..],
                (short)Math.Round(Math.Clamp(sample, short.MinValue, short.MaxValue)));

            written += 2;
            _position += _step;
        }

        return written;
    }

    public void Dispose() => _source.Dispose();

    /// <summary>Drops the samples already passed and reads more frames behind the rest.</summary>
    private void Refill()
    {
        var spent = Math.Min((int)_position, _count);
        Array.Copy(_mono, spent, _mono, 0, _count - spent);
        _count -= spent;
        _position -= spent;

        var frameBytes = _source.Format.BytesPerFrame;
        var read = _source.Read(_raw.AsSpan(0, (Capacity - _count) * frameBytes));

        if (read == 0)
        {
            _ended = true;
            return;
        }

        for (var offset = 0; offset < read; offset += frameBytes)
        {
            var sum = 0;

            for (var channel = 0; channel < _channels; channel++)
            {
                sum += BinaryPrimitives.ReadInt16LittleEndian(_raw.AsSpan(offset + (channel * 2)));
            }

            _mono[_count++] = (float)sum / _channels;
        }
    }
}

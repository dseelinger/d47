using D47.Core.Audio;
using NAudio.Wave;

namespace D47.Audio;

/// <summary>One <see cref="AudioClip"/> as a sample source, optionally looping.</summary>
internal sealed class ClipSampleProvider(AudioClip clip, bool loop) : ISampleProvider
{
    private int _position;

    public WaveFormat WaveFormat { get; } =
        WaveFormat.CreateIeeeFloatWaveFormat(clip.Format.SampleRate, clip.Format.Channels);

    public int Read(float[] buffer, int offset, int count)
    {
        var pcm = clip.Pcm.Span;
        var written = 0;

        while (written < count)
        {
            if (_position >= pcm.Length)
            {
                if (!loop)
                {
                    break;
                }

                _position = 0;

                // A zero-length clip would spin here forever rather than ending.
                if (pcm.Length == 0)
                {
                    break;
                }
            }

            var sample = (short)(pcm[_position] | (pcm[_position + 1] << 8));
            buffer[offset + written] = sample / 32768f;
            _position += 2;
            written++;
        }

        return written;
    }
}

/// <summary>
/// A pass-through that carries the arbiter's playback id, so <c>MixerInputEnded</c> can be attributed
/// back to the request that started it.
/// </summary>
internal sealed class TrackedSampleProvider(long id, ISampleProvider source) : ISampleProvider
{
    public long Id { get; } = id;

    public WaveFormat WaveFormat => source.WaveFormat;

    public int Read(float[] buffer, int offset, int count) => source.Read(buffer, offset, count);
}

/// <summary>
/// The render reference tap: every buffer that goes to the device, copied out with
/// the running sample position that locates it in the stream.
/// </summary>
internal sealed class RenderTap(ISampleProvider source) : ISampleProvider, IRenderReferenceTap
{
    private long _position;

    public WaveFormat WaveFormat => source.WaveFormat;

    public event Action<RenderReferenceFrame>? Rendered;

    public int Read(float[] buffer, int offset, int count)
    {
        var read = source.Read(buffer, offset, count);

        if (Rendered is { } subscribers && read > 0)
        {
            var frames = read / WaveFormat.Channels;
            var pcm = new byte[read * 2];

            for (var i = 0; i < read; i++)
            {
                var sample = (short)Math.Clamp(buffer[offset + i] * 32767f, short.MinValue, short.MaxValue);
                pcm[i * 2] = (byte)sample;
                pcm[(i * 2) + 1] = (byte)(sample >> 8);
            }

            subscribers(new RenderReferenceFrame(
                _position,
                pcm,
                new AudioFormat(WaveFormat.SampleRate, WaveFormat.Channels)));

            _position += frames;
        }
        else
        {
            _position += read / WaveFormat.Channels;
        }

        return read;
    }
}

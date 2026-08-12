using D47.Core.Audio;
using NAudio.Wave;

namespace D47.Audio;

/// <summary>
/// One <see cref="AudioClip"/> as a sample source, optionally looping.
/// <para>
/// Reads straight out of the clip's PCM rather than through a <c>RawSourceWaveStream</c>, so
/// a looping bed costs no allocation per cycle and the loop point is a index reset rather
/// than a stream seek — which is what keeps a three-second bed from ticking once every three
/// seconds on a busy machine.
/// </para>
/// </summary>
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

                // A zero-length clip would spin here forever rather than ending. It cannot
                // happen with the shipped set — CueLibrary rejects an empty clip — but a
                // custom cue dropped in for Phase 12 is not under our control.
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
/// A pass-through that carries the arbiter's playback id, so <c>MixerInputEnded</c> can be
/// attributed back to the request that started it. NAudio identifies a mixer input by object
/// reference and nothing else, so the id has to ride along on the object.
/// </summary>
internal sealed class TrackedSampleProvider(long id, ISampleProvider source) : ISampleProvider
{
    public long Id { get; } = id;

    public WaveFormat WaveFormat => source.WaveFormat;

    public int Read(float[] buffer, int offset, int count) => source.Read(buffer, offset, count);
}

/// <summary>
/// The render reference tap (architecture.md D7): every buffer that goes to the device, copied
/// out with the running sample position that locates it in the stream.
/// <para>
/// It is a pass-through in the graph rather than a fork off the side, which is what makes it
/// correct by construction — there is no arrangement of the mixer in which the tap sees
/// something other than exactly what was rendered.
/// </para>
/// <para>
/// Nothing consumes it until AEC3 in Phase 13. That is the point: retrofitting it later means
/// opening the one component every voice path depends on.
/// </para>
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

using D47.Core.Audio;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace D47.Audio;

/// <summary>
/// An <see cref="ArrivingClip"/> as a sample source, read on the render thread without waiting. Silence until
/// <see cref="PreRoll"/> has arrived or the clip is complete; after that, silence wherever playback catches up
/// with what has arrived (an underrun). Ends once the clip is complete and every byte has been read.
/// </summary>
internal sealed class ArrivingClipSampleProvider(ArrivingClip clip, ILogger logger) : ISampleProvider, IDisposable
{
    internal static readonly TimeSpan PreRoll = TimeSpan.FromMilliseconds(100);

    private static readonly long PreRollBytes =
        (long)(AudioFormat.Standard.SampleRate * PreRoll.TotalSeconds) * AudioFormat.Standard.BytesPerFrame;

    private byte[] _pcm = [];
    private long _position;
    private bool _started;
    private bool _starved;
    private int _underruns;
    private int _disposed;

    public WaveFormat WaveFormat { get; } =
        WaveFormat.CreateIeeeFloatWaveFormat(clip.Format.SampleRate, clip.Format.Channels);

    /// <summary>How many times playback caught up with what had arrived.</summary>
    internal int Underruns => Volatile.Read(ref _underruns);

    public int Read(float[] buffer, int offset, int count)
    {
        // Complete before length: once complete, the length read after it is final.
        var complete = clip.IsComplete;

        if (!_started)
        {
            if (!complete && clip.Length < PreRollBytes)
            {
                Array.Clear(buffer, offset, count);
                return count;
            }

            _started = true;
        }

        var bytes = count * 2;

        if (_pcm.Length < bytes)
        {
            _pcm = new byte[bytes];
        }

        var read = clip.Read(_position, _pcm.AsSpan(0, bytes)) / 2;
        _position += read * 2;

        for (var i = 0; i < read; i++)
        {
            buffer[offset + i] = (short)(_pcm[i * 2] | (_pcm[(i * 2) + 1] << 8)) / 32768f;
        }

        if (read == count)
        {
            _starved = false;
            return count;
        }

        if (complete)
        {
            return read;
        }

        if (!_starved)
        {
            _starved = true;
            Interlocked.Increment(ref _underruns);
        }

        Array.Clear(buffer, offset + read, count - read);
        return count;
    }

    /// <summary>Logs the underrun count. Safe to call more than once.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            logger.LogDebug("{Clip} ended after {Underruns} underruns", clip.Name, Underruns);
        }
    }
}

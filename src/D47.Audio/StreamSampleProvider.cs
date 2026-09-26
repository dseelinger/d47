using D47.Core.Audio;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace D47.Audio;

/// <summary>
/// A <see cref="MusicTrack"/> as a sample source. A feeder thread opens and reads the track, keeping a
/// few seconds decoded ahead; the render side only copies out of that buffer. An underrun renders
/// silence, and the source ends once the track is read and the buffer is empty, or when the track
/// fails.
/// </summary>
internal sealed class StreamSampleProvider : ISampleProvider, IDisposable
{
    internal static readonly TimeSpan Ahead = TimeSpan.FromSeconds(3);

    private static readonly TimeSpan Chunk = TimeSpan.FromMilliseconds(100);

    private readonly MusicTrack _track;
    private readonly ILogger _logger;
    private readonly BufferedWaveProvider _buffer;
    private readonly CancellationTokenSource _stop = new();
    private readonly Thread _feeder;

    private byte[] _pcm = [];
    private volatile bool _drained;

    public StreamSampleProvider(MusicTrack track, ILogger logger)
    {
        _track = track;
        _logger = logger;

        var format = AudioFormat.Standard;

        _buffer = new BufferedWaveProvider(new WaveFormat(format.SampleRate, 16, format.Channels))
        {
            BufferDuration = Ahead + Ahead,
            ReadFully = false,
            DiscardOnBufferOverflow = false,
        };

        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(format.SampleRate, format.Channels);

        _feeder = new Thread(Feed) { IsBackground = true, Name = $"d47 music: {track.Name}" };
        _feeder.Start();
    }

    public WaveFormat WaveFormat { get; }

    /// <summary>Whether the feeder has finished, by reaching the end or by failing.</summary>
    internal bool Drained => _drained;

    public int Read(float[] buffer, int offset, int count)
    {
        // Read before the buffer: once set, everything the feeder will ever add is already in it.
        var drained = _drained;
        var bytes = count * 2;

        if (_pcm.Length < bytes)
        {
            _pcm = new byte[bytes];
        }

        var read = _buffer.Read(_pcm, 0, bytes) / 2;

        for (var i = 0; i < read; i++)
        {
            buffer[offset + i] = (short)(_pcm[i * 2] | (_pcm[(i * 2) + 1] << 8)) / 32768f;
        }

        if (drained)
        {
            return read;
        }

        Array.Clear(buffer, offset + read, count - read);
        return count;
    }

    /// <summary>Stops the feeder, which closes the track. Safe to call more than once.</summary>
    public void Dispose() => _stop.Cancel();

    private void Feed()
    {
        var chunk = new byte[(int)(AudioFormat.Standard.SampleRate * Chunk.TotalSeconds) * AudioFormat.Standard.BytesPerFrame];
        var ahead = (int)(AudioFormat.Standard.SampleRate * Ahead.TotalSeconds) * AudioFormat.Standard.BytesPerFrame;

        try
        {
            using var stream = _track.Open();

            while (!_stop.IsCancellationRequested)
            {
                if (_buffer.BufferedBytes >= ahead)
                {
                    _stop.Token.WaitHandle.WaitOne(Chunk);
                    continue;
                }

                var read = stream.Read(chunk);

                if (read == 0)
                {
                    break;
                }

                _buffer.AddSamples(chunk, 0, read);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Music track {Track} could not be read; moving on", _track.Name);
        }
        finally
        {
            _drained = true;
        }
    }
}

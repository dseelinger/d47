using D47.Core.Audio;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace D47.Audio;

/// <summary>
/// A <see cref="MusicTrack"/> as a sample source. A feeder thread opens and reads the track, keeping a
/// few seconds decoded ahead; the render side only copies out of that buffer. An underrun renders
/// silence. The source ends once the track is read, no seek is waiting and the buffer is empty, or when
/// the track fails. Position counts the frames copied out, so it stops while the playback is paused or held.
/// </summary>
internal sealed class StreamSampleProvider : ISampleProvider, IDisposable
{
    internal static readonly TimeSpan Ahead = TimeSpan.FromSeconds(3);

    private static readonly TimeSpan Chunk = TimeSpan.FromMilliseconds(100);

    private const long NoSeek = -1;
    private const long NoLength = -1;

    private readonly MusicTrack _track;
    private readonly ILogger _logger;
    private readonly BufferedWaveProvider _buffer;
    private readonly CancellationTokenSource _stop = new();
    private readonly AutoResetEvent _wake = new(initialState: false);
    private readonly Thread _feeder;

    /// <summary>Taken by <see cref="Read"/>, by a seek being posted or applied, and by the feeder adding to the buffer.</summary>
    private readonly Lock _gate = new();

    private byte[] _pcm = [];
    private long _elapsedFrames;
    private long _lengthTicks = NoLength;
    private long _seekTicks = NoSeek;

    /// <summary>The feeder has read to the end of the track; everything left is in the buffer.</summary>
    private bool _atEnd;

    /// <summary>The feeder has stopped, failed or never opened the track, and closed it.</summary>
    private bool _exited;

    /// <summary><see cref="Read"/> has returned short, so the mixer has removed this source.</summary>
    private bool _ended;

    public StreamSampleProvider(MusicTrack track, ILogger logger)
    {
        _track = track;
        _logger = logger;

        var format = AudioFormat.Music;

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

    /// <summary>Whether the feeder has read to the end, failed or stopped.</summary>
    internal bool Drained
    {
        get
        {
            lock (_gate)
            {
                return _atEnd || _exited;
            }
        }
    }

    /// <summary>The frames copied out so far, clamped to the length once it is known. Takes no lock.</summary>
    public TrackProgress Progress
    {
        get
        {
            var elapsed = FromFrames(Interlocked.Read(ref _elapsedFrames));
            var lengthTicks = Interlocked.Read(ref _lengthTicks);

            if (lengthTicks == NoLength)
            {
                return new TrackProgress(elapsed, null);
            }

            var length = TimeSpan.FromTicks(lengthTicks);
            return new TrackProgress(elapsed < length ? elapsed : length, length);
        }
    }

    /// <summary>Posts a seek for the feeder to carry out. False once the track has ended or its feeder has stopped.</summary>
    public bool Seek(TimeSpan to)
    {
        lock (_gate)
        {
            if (_ended || _exited)
            {
                return false;
            }

            Interlocked.Exchange(ref _seekTicks, Math.Max(0, to.Ticks));
        }

        _wake.Set();
        return true;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var bytes = count * 2;

        if (_pcm.Length < bytes)
        {
            _pcm = new byte[bytes];
        }

        int read;
        bool ended;

        lock (_gate)
        {
            read = _buffer.Read(_pcm, 0, bytes) / 2;
            Interlocked.Add(ref _elapsedFrames, read / WaveFormat.Channels);

            if (read < count && (_exited || (_atEnd && Interlocked.Read(ref _seekTicks) == NoSeek)))
            {
                _ended = true;
            }

            ended = _ended;
        }

        for (var i = 0; i < read; i++)
        {
            buffer[offset + i] = (short)(_pcm[i * 2] | (_pcm[(i * 2) + 1] << 8)) / 32768f;
        }

        if (ended)
        {
            return read;
        }

        Array.Clear(buffer, offset + read, count - read);
        return count;
    }

    /// <summary>Stops the feeder, which closes the track. Safe to call more than once.</summary>
    public void Dispose() => _stop.Cancel();

    private static TimeSpan FromFrames(long frames) =>
        TimeSpan.FromTicks(frames * TimeSpan.TicksPerSecond / AudioFormat.Music.SampleRate);

    private static long ToFrames(TimeSpan time) => time.Ticks * AudioFormat.Music.SampleRate / TimeSpan.TicksPerSecond;

    private void Feed()
    {
        var chunk = new byte[(int)(AudioFormat.Music.SampleRate * Chunk.TotalSeconds) * AudioFormat.Music.BytesPerFrame];
        var ahead = (int)(AudioFormat.Music.SampleRate * Ahead.TotalSeconds) * AudioFormat.Music.BytesPerFrame;
        WaitHandle[] waits = [_stop.Token.WaitHandle, _wake];

        try
        {
            using var stream = _track.Open();
            Interlocked.Exchange(ref _lengthTicks, stream.Length is { } length ? length.Ticks : NoLength);

            while (!_stop.IsCancellationRequested)
            {
                var seekTicks = Interlocked.Read(ref _seekTicks);

                if (seekTicks != NoSeek)
                {
                    ApplySeek(stream, seekTicks);
                    continue;
                }

                bool atEnd;

                lock (_gate)
                {
                    atEnd = _atEnd;
                }

                if (atEnd || _buffer.BufferedBytes >= ahead)
                {
                    WaitHandle.WaitAny(waits, Chunk);
                    continue;
                }

                var read = stream.Read(chunk);

                lock (_gate)
                {
                    if (read == 0)
                    {
                        _atEnd = true;
                    }
                    else
                    {
                        _buffer.AddSamples(chunk, 0, read);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Music track {Track} could not be read; moving on", _track.Name);
        }
        finally
        {
            lock (_gate)
            {
                _exited = true;
            }
        }
    }

    /// <summary>
    /// Seeks outside the lock, since a seek can decode forward for some time, then drops the buffered audio and
    /// sets the position together. The target stays posted until then, so <see cref="Read"/> does not end the
    /// track meanwhile; a newer target posted during the seek stays posted.
    /// </summary>
    private void ApplySeek(IPcmStream stream, long seekTicks)
    {
        var lengthTicks = Interlocked.Read(ref _lengthTicks);
        var to = TimeSpan.FromTicks(lengthTicks != NoLength && seekTicks > lengthTicks ? lengthTicks : seekTicks);

        stream.Seek(to);

        lock (_gate)
        {
            _buffer.ClearBuffer();
            Interlocked.Exchange(ref _elapsedFrames, ToFrames(to));
            _atEnd = false;
            Interlocked.CompareExchange(ref _seekTicks, NoSeek, seekTicks);
        }
    }
}

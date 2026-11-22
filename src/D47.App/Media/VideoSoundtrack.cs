using D47.Core.Audio;

namespace D47.App.Media;

/// <summary>
/// The soundtrack a <see cref="VideoDecoder"/> writes and the sink reads: 48 kHz stereo 16-bit PCM placed by its time
/// in the video. <see cref="Read"/> blocks until there is sound, the end, or the stream is closed. <see cref="Seek"/>
/// hands the target to the decoder and waits until it has moved there.
/// </summary>
internal sealed class VideoSoundtrack(Action wake, TimeSpan? length) : IPcmStream
{
    private const int BytesPerFrame = 4;
    private const int SampleRate = 48_000;

    /// <summary>Sound written further than this from its last write starts after silence.</summary>
    private static readonly long Gap = TimeSpan.FromMilliseconds(20).Ticks;

    /// <summary>What <see cref="Read"/> gives while a seek is due and nothing is written, so the reader reaches the seek.</summary>
    private const int SeekFiller = SampleRate / 100 * BytesPerFrame;

    private readonly object _gate = new();
    private byte[] _ring = new byte[1 << 17];
    private int _head;
    private int _count;

    /// <summary>The video time of the next byte <see cref="Read"/> returns.</summary>
    private long _readAt;

    /// <summary>The video time just past the last byte written.</summary>
    private long _writtenTo;

    private long _posted;
    private long _applied;
    private long _target;
    private bool _seekDue;
    private bool _ended;
    private bool _closed;

    public TimeSpan? Length { get; } = length;

    /// <summary>How many bytes are written and not yet read.</summary>
    internal int Buffered
    {
        get
        {
            lock (_gate)
            {
                return _count;
            }
        }
    }

    /// <summary>The seek the reader has asked for that the decoder has not applied, if any.</summary>
    internal bool TakeSeek(out TimeSpan to, out long generation)
    {
        lock (_gate)
        {
            to = TimeSpan.FromTicks(_target);
            generation = _posted;
            return _posted != _applied;
        }
    }

    /// <summary>The decoder is at <paramref name="to"/> for seek <paramref name="generation"/>: what is written from now is from there.</summary>
    internal void Positioned(TimeSpan to, long generation)
    {
        lock (_gate)
        {
            if (generation != _posted)
            {
                return;
            }

            _applied = generation;
            _head = 0;
            _count = 0;
            _readAt = to.Ticks;
            _writtenTo = to.Ticks;
            _ended = false;
            Monitor.PulseAll(_gate);
        }
    }

    /// <summary>A seek is about to reach <see cref="Seek"/>, so <see cref="Read"/> stops waiting for sound.</summary>
    internal void SeekComing(bool coming)
    {
        lock (_gate)
        {
            _seekDue = coming;
            Monitor.PulseAll(_gate);
        }
    }

    /// <summary>
    /// Places <paramref name="pcm"/> at <paramref name="at"/>: what lies before the last write is cut, and a gap after
    /// it is filled with silence.
    /// </summary>
    internal void Write(ReadOnlySpan<byte> pcm, TimeSpan at, long generation)
    {
        lock (_gate)
        {
            if (generation != _posted || generation != _applied || _closed)
            {
                return;
            }

            var start = at.Ticks;

            if (start > _writtenTo + Gap)
            {
                Silence(start - _writtenTo);
            }
            else if (start < _writtenTo)
            {
                var cut = Math.Min(pcm.Length, ToBytes(_writtenTo - start));
                pcm = pcm[cut..];
            }

            pcm = pcm[..(pcm.Length - (pcm.Length % BytesPerFrame))];
            Append(pcm);
            _writtenTo += ToTicks(pcm.Length);
            Monitor.PulseAll(_gate);
        }
    }

    /// <summary>Writes silence until <paramref name="to"/>.</summary>
    internal void SilenceTo(TimeSpan to, long generation)
    {
        lock (_gate)
        {
            if (generation == _posted && generation == _applied && !_closed && to.Ticks > _writtenTo)
            {
                Silence(to.Ticks - _writtenTo);
                Monitor.PulseAll(_gate);
            }
        }
    }

    /// <summary>Nothing more is written until the next seek; <see cref="Read"/> returns 0 once the rest is read.</summary>
    internal void End()
    {
        lock (_gate)
        {
            _ended = true;
            Monitor.PulseAll(_gate);
        }
    }

    public int Read(Span<byte> buffer)
    {
        var wanted = buffer.Length - (buffer.Length % BytesPerFrame);
        int read;

        lock (_gate)
        {
            while (_count == 0)
            {
                if (_closed || (_ended && _posted == _applied))
                {
                    return 0;
                }

                if (_seekDue)
                {
                    var filler = Math.Min(wanted, SeekFiller);
                    buffer[..filler].Clear();
                    return filler;
                }

                Monitor.Wait(_gate, 50);
            }

            read = Math.Min(wanted, _count);
            var first = Math.Min(read, _ring.Length - _head);
            _ring.AsSpan(_head, first).CopyTo(buffer);
            _ring.AsSpan(0, read - first).CopyTo(buffer[first..]);
            _head = (_head + read) % _ring.Length;
            _count -= read;
            _readAt += ToTicks(read);
        }

        wake();
        return read;
    }

    /// <summary>Called by the sink's reader; waits until the decoder has moved to <paramref name="to"/>, or the stream closes.</summary>
    public void Seek(TimeSpan to)
    {
        if (to < TimeSpan.Zero)
        {
            to = TimeSpan.Zero;
        }

        long generation;

        lock (_gate)
        {
            _seekDue = false;

            if (to.Ticks == _readAt && _posted == _applied && !_ended)
            {
                return;
            }

            generation = ++_posted;
            _target = to.Ticks;
            _head = 0;
            _count = 0;
        }

        wake();

        lock (_gate)
        {
            while (_applied < generation && _posted == generation && !_closed)
            {
                Monitor.Wait(_gate, 100);
            }
        }
    }

    /// <summary>Closes the stream: <see cref="Read"/> returns 0 and writes are dropped.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _closed = true;
            Monitor.PulseAll(_gate);
        }

        wake();
    }

    private static int ToBytes(long ticks) =>
        (int)Math.Min(int.MaxValue / 2, ticks * SampleRate / TimeSpan.TicksPerSecond) * BytesPerFrame;

    private static long ToTicks(int bytes) => (long)bytes / BytesPerFrame * TimeSpan.TicksPerSecond / SampleRate;

    private void Silence(long ticks)
    {
        var bytes = ToBytes(ticks);
        Grow(bytes);

        for (var left = bytes; left > 0;)
        {
            var at = (_head + _count) % _ring.Length;
            var run = Math.Min(left, _ring.Length - at);
            _ring.AsSpan(at, run).Clear();
            _count += run;
            left -= run;
        }

        _writtenTo += ToTicks(bytes);
    }

    private void Append(ReadOnlySpan<byte> pcm)
    {
        Grow(pcm.Length);

        while (pcm.Length > 0)
        {
            var at = (_head + _count) % _ring.Length;
            var run = Math.Min(pcm.Length, _ring.Length - at);
            pcm[..run].CopyTo(_ring.AsSpan(at, run));
            _count += run;
            pcm = pcm[run..];
        }
    }

    private void Grow(int more)
    {
        if (_count + more <= _ring.Length)
        {
            return;
        }

        var size = _ring.Length;

        while (size < _count + more)
        {
            size *= 2;
        }

        var grown = new byte[size];
        var first = Math.Min(_count, _ring.Length - _head);
        _ring.AsSpan(_head, first).CopyTo(grown);
        _ring.AsSpan(0, _count - first).CopyTo(grown.AsSpan(first));
        _ring = grown;
        _head = 0;
    }
}

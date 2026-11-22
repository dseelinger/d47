using System.Runtime.InteropServices;
using D47.Core.Audio;
using Microsoft.Extensions.Logging;
using static D47.App.Media.SourceReader;

namespace D47.App.Media;

/// <summary>
/// One viewing of a file or URL: a single source reader with picture and soundtrack selected, read on a thread of its
/// own. Sound goes to <see cref="Soundtrack"/>; pictures are kept until the clock reaches them, and the newest one at or
/// before the clock is published as <see cref="Frame"/>. Reading waits while <see cref="SoundtrackAhead"/> of sound is
/// written and not read. A video with no soundtrack, or one whose sound ends first, is given silence to the end of its
/// picture.
/// </summary>
internal sealed class VideoDecoder : IDisposable
{
    internal const int LargestWidth = 1280;
    internal const int LargestHeight = 720;

    internal static readonly TimeSpan StallAfter = TimeSpan.FromSeconds(2);

    /// <summary>How much sound is kept decoded ahead here, and by the sink.</summary>
    internal static readonly TimeSpan SoundtrackAhead = TimeSpan.FromMilliseconds(250);

    private static readonly TimeSpan Cadence = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan Landed = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan LandedWithin = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan GiveUpLanding = TimeSpan.FromSeconds(3);

    /// <summary>How long a seek the reader already stood at waits before its landing is believed.</summary>
    private static readonly TimeSpan AlreadyThere = TimeSpan.FromMilliseconds(500);

    private const long None = -1;

    private static readonly int AheadBytes =
        (int)(SoundtrackAhead.TotalSeconds * AudioFormat.Music.SampleRate) * AudioFormat.Music.BytesPerFrame;

    private readonly string _source;
    private readonly TimeSpan _from;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly AutoResetEvent _wake = new(initialState: false);
    private readonly Thread _thread;

    private readonly Queue<VideoFrame> _queued = new();
    private readonly Stack<byte[]> _free = new();

    private IMFSourceReader? _reader;
    private VideoSoundtrack? _soundtrack;
    private uint _pictureStream = uint.MaxValue;
    private uint _soundStream = uint.MaxValue;
    private bool _hasSound;
    private int _width;
    private int _height;
    private int _mostQueued = 30;
    private byte[] _copy = [];

    private bool _pictureEnded;
    private bool _soundEnded;
    private long _pictureTo;
    private long _dropBefore;
    private long _generation;

    private VideoFrame? _shown;
    private VideoFrame? _before;
    private VideoFrame? _frame;

    private volatile bool _stopping;
    private Func<TrackProgress?> _clock = static () => null;
    private long _expect = None;
    private long _expectSince;
    private int _expectPositioned;
    private long _waitingSince;
    private int _failure = -1;

    internal VideoDecoder(string source, TimeSpan from, TimeProvider time, ILogger logger)
    {
        _source = source;
        _from = from < TimeSpan.Zero ? TimeSpan.Zero : from;
        _time = time;
        _logger = logger;
        _expect = _from > TimeSpan.Zero ? _from.Ticks : None;
        _expectSince = time.GetTimestamp();
        _thread = new Thread(Run) { IsBackground = true, Name = "d47-video" };
    }

    /// <summary>Raised on the decoding thread once the reader is open and <see cref="Soundtrack"/> can be played.</summary>
    internal event Action? Opened;

    /// <summary>Raised on the decoding thread when it fails, and when a stall ends.</summary>
    internal event Action? Changed;

    internal TimeSpan From => _from;

    /// <summary>The soundtrack to stream; null until <see cref="Opened"/>.</summary>
    internal VideoSoundtrack? Soundtrack => Volatile.Read(ref _soundtrack);

    /// <summary>The newest picture at or before the clock, or the first one while there is no clock.</summary>
    internal VideoFrame? Frame => Volatile.Read(ref _frame);

    internal VideoFailure? Failure => Volatile.Read(ref _failure) is var failure and >= 0 ? (VideoFailure)failure : null;

    internal TimeSpan? Length => Soundtrack?.Length;

    /// <summary>Whether a sample has been waited for longer than <see cref="StallAfter"/>.</summary>
    internal bool Stalled =>
        Volatile.Read(ref _waitingSince) is var since and not 0 && _time.GetElapsedTime(since) > StallAfter;

    /// <summary>The soundtrack's position, which pictures follow.</summary>
    internal Func<TrackProgress?> Clock
    {
        set => Volatile.Write(ref _clock, value);
    }

    /// <summary>The position a seek is on its way to, until the soundtrack reports it.</summary>
    internal TimeSpan? Expected => Interlocked.Read(ref _expect) is var expect and not None ? TimeSpan.FromTicks(expect) : null;

    internal void Start() => _thread.Start();

    /// <summary>Shows <paramref name="to"/> until the soundtrack's seek lands; call before seeking the soundtrack.</summary>
    internal void ExpectSeek(TimeSpan to)
    {
        Interlocked.Exchange(ref _expectPositioned, 0);
        Interlocked.Exchange(ref _expectSince, _time.GetTimestamp());
        Interlocked.Exchange(ref _expect, Math.Max(0, to.Ticks));
        Soundtrack?.SeekComing(true);
    }

    /// <summary>The seek <see cref="ExpectSeek"/> announced is not happening.</summary>
    internal void CancelSeek()
    {
        Interlocked.Exchange(ref _expect, None);
        Soundtrack?.SeekComing(false);
    }

    public void Dispose()
    {
        _stopping = true;
        Soundtrack?.Dispose();
        _wake.Set();
    }

    private void Run()
    {
        try
        {
            if (!Open())
            {
                return;
            }

            Opened?.Invoke();
            Loop();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Video {Source} could not be read", _source);
            Fail(VideoFailure.Unreachable);
        }
        finally
        {
            Soundtrack?.End();
            Release(Interlocked.Exchange(ref _reader, null));
        }
    }

    private bool Open()
    {
        if (!SourceReader.Start())
        {
            return Fail(VideoFailure.Unreachable, "Media Foundation could not start");
        }

        if (MFCreateAttributes(out var attributes, 1) != 0)
        {
            return Fail(VideoFailure.Unreachable, "no attributes");
        }

        var processing = AdvancedVideoProcessing;
        attributes.SetUINT32(ref processing, 1);
        var opened = MFCreateSourceReaderFromURL(_source, attributes, out var reader);
        Release(attributes);

        if (opened != 0)
        {
            return Fail(VideoFailure.Unreachable, $"0x{opened:X8} opening it");
        }

        _reader = reader;
        reader.SetStreamSelection(AnyStream, false);

        if (reader.SetStreamSelection(FirstVideoStream, true) != 0)
        {
            return Fail(VideoFailure.Picture, "it has no picture");
        }

        if (ChoosePicture(reader) is var picture and not 0)
        {
            return Fail(VideoFailure.Picture, $"0x{picture:X8} decoding the picture");
        }

        _hasSound = reader.SetStreamSelection(FirstAudioStream, true) == 0;

        if (_hasSound && ChooseSound(reader) is var sound and not 0)
        {
            return Fail(VideoFailure.Soundtrack, $"0x{sound:X8} decoding the soundtrack");
        }

        FindStreams(reader);

        var key = Duration;
        TimeSpan? length = reader.GetPresentationAttribute(MediaSource, ref key, out var duration) == 0 && duration.Value > 0
            ? TimeSpan.FromTicks(duration.Value)
            : null;

        Volatile.Write(ref _soundtrack, new VideoSoundtrack(() => _wake.Set(), length));
        Reposition(_from, 0);
        return true;
    }

    /// <summary>Asks for RGB32 at the native size, scaled down to fit 1280 by 720.</summary>
    private int ChoosePicture(IMFSourceReader reader)
    {
        var frameSize = FrameSize;
        var width = LargestWidth;
        var height = LargestHeight;

        if (reader.GetNativeMediaType(FirstVideoStream, 0, out var native) == 0)
        {
            if (native.GetUINT64(ref frameSize, out var packed) == 0 && packed != 0)
            {
                (width, height) = Fit((int)(packed >> 32), (int)(packed & 0xFFFFFFFF));
            }

            Release(native);
        }

        if (MFCreateMediaType(out var wanted) != 0)
        {
            return -1;
        }

        try
        {
            var major = MajorType;
            var video = VideoMajorType;
            var sub = Subtype;
            var rgb = Rgb32;

            wanted.SetGUID(ref major, ref video);
            wanted.SetGUID(ref sub, ref rgb);
            wanted.SetUINT64(ref frameSize, ((ulong)width << 32) | (uint)height);

            var chosen = reader.SetCurrentMediaType(FirstVideoStream, IntPtr.Zero, wanted);
            return chosen != 0 ? chosen : ReadPictureType(reader, FirstVideoStream);
        }
        finally
        {
            Release(wanted);
        }
    }

    /// <summary>The largest even size that fits 1280 by 720 at the native shape, never larger than native.</summary>
    internal static (int Width, int Height) Fit(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return (LargestWidth, LargestHeight);
        }

        var scale = Math.Min(1.0, Math.Min((double)LargestWidth / width, (double)LargestHeight / height));
        return (Math.Max(2, (int)(width * scale) & ~1), Math.Max(2, (int)(height * scale) & ~1));
    }

    private int ReadPictureType(IMFSourceReader reader, uint stream)
    {
        var got = reader.GetCurrentMediaType(stream, out var actual);

        if (got != 0)
        {
            return got;
        }

        try
        {
            var frameSize = FrameSize;

            if (actual.GetUINT64(ref frameSize, out var packed) is var sized and not 0)
            {
                return sized;
            }

            var width = (int)(packed >> 32);
            var height = (int)(packed & 0xFFFFFFFF);

            if (width <= 0 || height <= 0)
            {
                return -1;
            }

            if (width != _width || height != _height)
            {
                _free.Clear();
            }

            _width = width;
            _height = height;

            // A positive stride asks for top-down rows.
            var stride = DefaultStride;
            actual.SetUINT32(ref stride, (uint)(width * 4));
            reader.SetCurrentMediaType(stream, IntPtr.Zero, actual);

            var rate = FrameRate;
            var perSecond = 30.0;

            if (actual.GetUINT64(ref rate, out var timing) == 0 && (timing >> 32) > 0 && (timing & 0xFFFFFFFF) > 0)
            {
                perSecond = (double)(timing >> 32) / (timing & 0xFFFFFFFF);
            }

            // About a second of pictures.
            _mostQueued = Math.Clamp((int)Math.Ceiling(perSecond) + 1, 4, 64);
            return 0;
        }
        finally
        {
            Release(actual);
        }
    }

    /// <summary>Asks for 48 kHz stereo 16-bit PCM, which the reader converts to from any rate and layout it decodes.</summary>
    private static int ChooseSound(IMFSourceReader reader)
    {
        if (MFCreateMediaType(out var wanted) != 0)
        {
            return -1;
        }

        try
        {
            var major = MajorType;
            var audio = AudioMajorType;
            var sub = Subtype;
            var pcm = Pcm;
            var bits = BitsPerSample;
            var channels = Channels;
            var rate = SampleRate;
            var align = BlockAlignment;
            var perSecond = BytesPerSecond;

            wanted.SetGUID(ref major, ref audio);
            wanted.SetGUID(ref sub, ref pcm);
            wanted.SetUINT32(ref bits, 16);
            wanted.SetUINT32(ref channels, (uint)AudioFormat.Music.Channels);
            wanted.SetUINT32(ref rate, (uint)AudioFormat.Music.SampleRate);
            wanted.SetUINT32(ref align, (uint)AudioFormat.Music.BytesPerFrame);
            wanted.SetUINT32(ref perSecond, (uint)(AudioFormat.Music.BytesPerFrame * AudioFormat.Music.SampleRate));

            return reader.SetCurrentMediaType(FirstAudioStream, IntPtr.Zero, wanted);
        }
        finally
        {
            Release(wanted);
        }
    }

    /// <summary>Which real stream numbers the first video and audio streams are, as a read of any stream reports them.</summary>
    private void FindStreams(IMFSourceReader reader)
    {
        var majorKey = MajorType;

        for (var index = 0u; index < 64; index++)
        {
            if (reader.GetStreamSelection(index, out var selected) is var asked and not 0)
            {
                if (asked == InvalidStreamNumber)
                {
                    return;
                }

                continue;
            }

            if (!selected || reader.GetCurrentMediaType(index, out var type) != 0)
            {
                continue;
            }

            if (type.GetGUID(ref majorKey, out var major) == 0)
            {
                if (major == VideoMajorType && _pictureStream == uint.MaxValue)
                {
                    _pictureStream = index;
                }
                else if (major == AudioMajorType && _soundStream == uint.MaxValue)
                {
                    _soundStream = index;
                }
            }

            Release(type);
        }
    }

    private void Loop()
    {
        var soundtrack = Soundtrack!;

        while (!_stopping)
        {
            if (soundtrack.TakeSeek(out var to, out var generation))
            {
                Reposition(to, generation);
            }

            Present();

            if (_soundEnded)
            {
                soundtrack.SilenceTo(TimeSpan.FromTicks(_pictureTo), _generation);
            }

            if ((_pictureEnded && _soundEnded) || soundtrack.Buffered >= AheadBytes)
            {
                if (_pictureEnded && _soundEnded)
                {
                    soundtrack.End();
                }

                _wake.WaitOne(Cadence);
                continue;
            }

            if (!ReadOne(soundtrack))
            {
                return;
            }
        }
    }

    /// <summary>Moves the reader to <paramref name="to"/> and drops what was decoded before.</summary>
    private void Reposition(TimeSpan to, long generation)
    {
        if (to > TimeSpan.Zero || generation > 0)
        {
            var position = new Variant { Type = LongVariant, Value = to.Ticks };
            var format = Guid.Empty;

            if (_reader!.SetCurrentPosition(ref format, ref position) is var moved and not 0)
            {
                _logger.LogDebug("Video {Source} could not seek to {To}: 0x{Result:X8}", _source, to, moved);
            }
        }

        while (_queued.TryDequeue(out var dropped))
        {
            Recycle(dropped);
        }

        _generation = generation;
        _dropBefore = to.Ticks;
        _pictureTo = to.Ticks;
        _pictureEnded = false;
        _soundEnded = !_hasSound;
        Soundtrack!.Positioned(to, generation);

        if (Interlocked.Read(ref _expect) == to.Ticks)
        {
            Interlocked.Exchange(ref _expectSince, _time.GetTimestamp());
            Interlocked.Exchange(ref _expectPositioned, 1);
        }
    }

    /// <summary>Reads one sample from either stream; false once reading has failed.</summary>
    private bool ReadOne(VideoSoundtrack soundtrack)
    {
        var started = _time.GetTimestamp();
        Volatile.Write(ref _waitingSince, started);

        var result = _reader!.ReadSample(AnyStream, 0, out var index, out var flags, out var timestamp, out var sample);

        Volatile.Write(ref _waitingSince, 0);

        try
        {
            if (_stopping)
            {
                return false;
            }

            if (result != 0 || (flags & ErrorFlag) != 0)
            {
                _logger.LogWarning("Video {Source} stopped reading: 0x{Result:X8}", _source, result);
                return Fail(VideoFailure.Unreachable);
            }

            if (_time.GetElapsedTime(started) > StallAfter)
            {
                Changed?.Invoke();
            }

            if (index == _pictureStream)
            {
                if ((flags & CurrentTypeChanged) != 0 && ReadPictureType(_reader, index) is var changed and not 0)
                {
                    _logger.LogWarning("Video {Source} changed its picture to one it cannot show: 0x{Result:X8}", _source, changed);
                    return Fail(VideoFailure.Picture);
                }

                if (sample is not null)
                {
                    TakePicture(sample, timestamp);
                }

                _pictureEnded |= (flags & EndOfStream) != 0;
            }
            else if (index == _soundStream)
            {
                if (sample is not null)
                {
                    TakeSound(sample, timestamp, soundtrack);
                }

                _soundEnded |= (flags & EndOfStream) != 0;
            }
            else if ((flags & EndOfStream) != 0)
            {
                _pictureEnded = true;
                _soundEnded = true;
            }

            return true;
        }
        finally
        {
            Release(sample);
        }
    }

    private void TakePicture(IMFSample sample, long timestamp)
    {
        var length = sample.GetSampleDuration(out var duration) == 0 && duration > 0 ? duration : TimeSpan.TicksPerSecond / 30;
        _pictureTo = Math.Max(_pictureTo, timestamp + length);

        if (timestamp + length <= _dropBefore || sample.ConvertToContiguousBuffer(out var buffer) != 0)
        {
            return;
        }

        try
        {
            if (buffer.Lock(out var data, out _, out var size) != 0)
            {
                return;
            }

            try
            {
                var bytes = _width * _height * 4;

                if (size < bytes)
                {
                    return;
                }

                var pixels = _free.TryPop(out var reused) ? reused : new byte[bytes];
                Marshal.Copy(data, pixels, 0, bytes);
                _queued.Enqueue(new VideoFrame(pixels, _width, _height, TimeSpan.FromTicks(timestamp)));
            }
            finally
            {
                buffer.Unlock();
            }
        }
        finally
        {
            Release(buffer);
        }

        // The oldest goes first: it is the one nearest to being passed by the clock anyway.
        while (_queued.Count > _mostQueued)
        {
            Recycle(_queued.Dequeue());
        }
    }

    private void TakeSound(IMFSample sample, long timestamp, VideoSoundtrack soundtrack)
    {
        if (sample.ConvertToContiguousBuffer(out var buffer) != 0)
        {
            return;
        }

        try
        {
            if (buffer.Lock(out var data, out _, out var size) != 0)
            {
                return;
            }

            try
            {
                if (_copy.Length < size)
                {
                    _copy = new byte[size];
                }

                Marshal.Copy(data, _copy, 0, size);
                soundtrack.Write(_copy.AsSpan(0, size), TimeSpan.FromTicks(timestamp), _generation);
            }
            finally
            {
                buffer.Unlock();
            }
        }
        finally
        {
            Release(buffer);
        }
    }

    /// <summary>Publishes the newest queued picture at or before the clock.</summary>
    private void Present()
    {
        var progress = Volatile.Read(ref _clock)();
        var clock = progress?.Elapsed;

        if (Interlocked.Read(ref _expect) is var expect and not None)
        {
            // A seek to where the reader already stands is not carried out, so it is never positioned.
            var age = _time.GetElapsedTime(Interlocked.Read(ref _expectSince));
            var reported = progress is { } heard
                && heard.Elapsed.Ticks >= expect - Landed.Ticks
                && heard.Elapsed.Ticks <= expect + LandedWithin.Ticks;
            var landed = (reported && (Volatile.Read(ref _expectPositioned) == 1 || age > AlreadyThere)) || age > GiveUpLanding;

            if (landed)
            {
                Interlocked.CompareExchange(ref _expect, None, expect);
            }
            else
            {
                clock = TimeSpan.FromTicks(expect);
            }
        }

        VideoFrame? next = null;

        if (clock is null)
        {
            if (_shown is null && _queued.TryDequeue(out var first))
            {
                next = first;
            }
        }
        else
        {
            while (_queued.TryPeek(out var queued) && queued.Time <= clock)
            {
                if (next is not null)
                {
                    Recycle(next);
                }

                next = _queued.Dequeue();
            }
        }

        if (next is null)
        {
            return;
        }

        if (_before is not null)
        {
            Recycle(_before);
        }

        _before = _shown;
        _shown = next;
        Volatile.Write(ref _frame, next);
    }

    private void Recycle(VideoFrame frame)
    {
        if (frame.Width == _width && frame.Height == _height && _free.Count < _mostQueued + 2)
        {
            _free.Push(frame.Pixels);
        }
    }

    private bool Fail(VideoFailure failure, string? detail = null)
    {
        if (detail is not null)
        {
            _logger.LogWarning("Video {Source} cannot play: {Detail}", _source, detail);
        }

        Interlocked.CompareExchange(ref _failure, (int)failure, -1);
        Changed?.Invoke();
        return false;
    }
}

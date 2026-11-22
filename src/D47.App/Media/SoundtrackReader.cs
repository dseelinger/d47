using System.Runtime.InteropServices;
using D47.Core.Viewing;
using static D47.App.Media.SourceReader;

namespace D47.App.Media;

/// <summary>One stretch of a soundtrack: mono samples at <see cref="ISoundtrackCaptioner.SampleRate"/>, the time of the first, and whether the soundtrack ends with it.</summary>
internal sealed record SoundStretch(float[] Samples, TimeSpan From, bool Ended)
{
    public TimeSpan Length => TimeSpan.FromSeconds((double)Samples.Length / ISoundtrackCaptioner.SampleRate);
}

/// <summary>
/// A video's soundtrack read by a Media Foundation reader of its own with no picture, as mono samples at
/// <see cref="ISoundtrackCaptioner.SampleRate"/>. A read that starts inside the stretch read last takes it from there
/// rather than seeking. One thread uses it.
/// </summary>
internal sealed class SoundtrackReader : IDisposable
{
    private const int Rate = ISoundtrackCaptioner.SampleRate;

    /// <summary>The rate asked for when the reader will not convert to <see cref="Rate"/>: three times it, in stereo.</summary>
    private const int FallbackRate = Rate * 3;

    private IMFSourceReader? _reader;
    private int _channels = 1;
    private int _every = 1;

    /// <summary>The samples the last read took from the reader, from <see cref="_lastFrom"/> to where the reader stands.</summary>
    private float[] _last = [];
    private TimeSpan _lastFrom;
    private bool _standing;
    private bool _ended;

    private SoundtrackReader(IMFSourceReader reader) => _reader = reader;

    /// <summary>A reader on <paramref name="source"/>, a file path or URL, or null with the reason it could not be opened.</summary>
    public static SoundtrackReader? Open(string source, out string? problem)
    {
        problem = null;

        if (!SourceReader.Start())
        {
            problem = "Media Foundation could not start.";
            return null;
        }

        var opened = MFCreateSourceReaderFromURL(source, null, out var reader);

        if (opened != 0)
        {
            problem = $"0x{opened:X8} opening it.";
            return null;
        }

        reader.SetStreamSelection(AnyStream, false);

        if (reader.SetStreamSelection(FirstAudioStream, true) != 0)
        {
            Release(reader);
            problem = "It has no soundtrack.";
            return null;
        }

        var soundtrack = new SoundtrackReader(reader);

        if (soundtrack.ChooseSound(Rate, 1) != 0 && soundtrack.ChooseSound(FallbackRate, 2) is var chosen and not 0)
        {
            soundtrack.Dispose();
            problem = $"0x{chosen:X8} decoding the soundtrack.";
            return null;
        }

        return soundtrack;
    }

    /// <summary>Up to <paramref name="length"/> of sound from <paramref name="from"/>; null once the reader has failed.</summary>
    public SoundStretch? Read(TimeSpan from, TimeSpan length)
    {
        if (_reader is not { } reader)
        {
            return null;
        }

        var wanted = (int)(length.TotalSeconds * Rate);
        var samples = new List<float>(wanted);
        var start = from;

        if (_standing && from >= _lastFrom && Offset(_lastFrom, from) <= _last.Length)
        {
            samples.AddRange(_last.AsSpan(Offset(_lastFrom, from)));
        }
        else
        {
            var position = new Variant { Type = LongVariant, Value = Math.Max(0, from.Ticks) };
            var format = Guid.Empty;

            if (reader.SetCurrentPosition(ref format, ref position) != 0)
            {
                return Fail();
            }

            _ended = false;
            start = TimeSpan.MinValue;
        }

        var held = new List<float>();

        while (samples.Count < wanted && !_ended)
        {
            var result = reader.ReadSample(FirstAudioStream, 0, out _, out var flags, out var timestamp, out var sample);

            try
            {
                if (result != 0 || (flags & ErrorFlag) != 0)
                {
                    return Fail();
                }

                if (sample is not null)
                {
                    held.Clear();
                    Take(sample, held);

                    // A seek lands at or before where it was asked for; what comes before is dropped.
                    var skip = start == TimeSpan.MinValue ? Math.Clamp(Offset(TimeSpan.FromTicks(timestamp), from), 0, held.Count) : 0;

                    if (start == TimeSpan.MinValue && skip < held.Count)
                    {
                        start = TimeSpan.FromTicks(timestamp) + TimeSpan.FromSeconds((double)skip / Rate);
                    }

                    samples.AddRange(held.Skip(skip));
                }

                _ended |= (flags & EndOfStream) != 0;
            }
            finally
            {
                Release(sample);
            }
        }

        if (start == TimeSpan.MinValue)
        {
            start = from;
        }

        _last = [.. samples];
        _lastFrom = start;
        _standing = true;

        var given = Math.Min(wanted, samples.Count);
        return new SoundStretch(_last[..given], start, _ended && given == samples.Count);
    }

    public void Dispose()
    {
        Release(Interlocked.Exchange(ref _reader, null));
    }

    private static int Offset(TimeSpan from, TimeSpan to) => (int)Math.Round((to - from).TotalSeconds * Rate);

    private SoundStretch? Fail()
    {
        Dispose();
        return null;
    }

    /// <summary>Asks for 16-bit PCM at <paramref name="rate"/> in <paramref name="channels"/>, converting from whatever is decoded.</summary>
    private int ChooseSound(int rate, int channels)
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
            var channelCount = Channels;
            var sampleRate = SampleRate;
            var align = BlockAlignment;
            var perSecond = BytesPerSecond;

            wanted.SetGUID(ref major, ref audio);
            wanted.SetGUID(ref sub, ref pcm);
            wanted.SetUINT32(ref bits, 16);
            wanted.SetUINT32(ref channelCount, (uint)channels);
            wanted.SetUINT32(ref sampleRate, (uint)rate);
            wanted.SetUINT32(ref align, (uint)(channels * 2));
            wanted.SetUINT32(ref perSecond, (uint)(channels * 2 * rate));

            var chosen = _reader!.SetCurrentMediaType(FirstAudioStream, IntPtr.Zero, wanted);

            if (chosen == 0)
            {
                _channels = channels;
                _every = rate / Rate;
            }

            return chosen;
        }
        finally
        {
            Release(wanted);
        }
    }

    /// <summary>Appends a sample's sound as mono at <see cref="Rate"/>, averaging channels and, at the fallback rate, every three frames.</summary>
    private void Take(IMFSample sample, List<float> into)
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
                var pcm = new short[size / 2];
                Marshal.Copy(data, pcm, 0, pcm.Length);

                var group = _channels * _every;

                for (var i = 0; i + group <= pcm.Length; i += group)
                {
                    var sum = 0f;

                    for (var j = 0; j < group; j++)
                    {
                        sum += pcm[i + j];
                    }

                    into.Add(sum / group / 32768f);
                }
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
}

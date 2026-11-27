namespace D47.Core.Audio;

/// <summary>
/// Raises a stereo stream whose voiced level is under <see cref="SpeechLeveller.Target"/> to it, as
/// <see cref="SpeechLeveller"/> does for speech, with one gain for both channels. It never cuts, and a peak limiter keeps a
/// raised sample under <see cref="SpeechLeveller.Ceiling"/>.
/// </summary>
/// <remarks>
/// Each frame's gain depends only on the frames before it, so the output does not depend on the sizes the reads are made
/// in and is as long as the input. A seek keeps the gain.
/// </remarks>
public sealed class StreamLeveller(IPcmStream inner) : IPcmStream
{
    /// <summary>How many voiced frames the level is averaged over once it has that many: ten seconds.</summary>
    internal const int SettledFrames = 200;

    /// <summary>How many stereo frames one measured frame is: 50 ms.</summary>
    private const int FrameLength = 2_400;

    private const int BytesPerFrame = 4;

    private static readonly double GainStep = 1 - Math.Exp(-1 / (0.02 * AudioFormat.Music.SampleRate));

    private static readonly double LimiterRelease = 1 - Math.Exp(-1 / (0.08 * AudioFormat.Music.SampleRate));

    private byte[] _scratch = [];
    private double _power;
    private int _frames;
    private double _wanted = 1;
    private double _gain = 1;
    private double _limit = 1;
    private double _sum;
    private int _inFrame;
    private byte[] _held = [];
    private byte[] _ready = [];
    private int _readyAt;
    private bool _ended;

    public TimeSpan? Length => inner.Length;

    public bool CanSeek => inner.CanSeek;

    public void Seek(TimeSpan to)
    {
        if (!inner.CanSeek)
        {
            return;
        }

        inner.Seek(to);
        _held = [];
        _ready = [];
        _readyAt = 0;
        _ended = false;
    }

    public int Read(Span<byte> buffer)
    {
        if (buffer.Length == 0)
        {
            return 0;
        }

        while (_readyAt == _ready.Length && !_ended)
        {
            Fill(buffer.Length);
        }

        var count = Math.Min(buffer.Length, _ready.Length - _readyAt);

        _ready.AsSpan(_readyAt, count).CopyTo(buffer);
        _readyAt += count;
        return count;
    }

    public void Dispose() => inner.Dispose();

    /// <summary>Reads more of the inner stream and levels its whole frames; at the end, passes the rest of a partial frame as it is.</summary>
    private void Fill(int want)
    {
        if (_scratch.Length < want)
        {
            _scratch = new byte[want];
        }

        var read = inner.Read(_scratch.AsSpan(0, want));

        if (read <= 0)
        {
            _ended = true;
            _ready = _held;
            _readyAt = 0;
            _held = [];
            return;
        }

        var input = PcmConverter.Join(_held, _scratch.AsSpan(0, read));
        var whole = input.Length - (input.Length % BytesPerFrame);
        var output = new byte[whole];

        for (var at = 0; at < whole; at += BytesPerFrame)
        {
            var left = (short)(input[at] | (input[at + 1] << 8)) / 32768.0;
            var right = (short)(input[at + 2] | (input[at + 3] << 8)) / 32768.0;
            var gain = Next(left, right);

            Write(output, at, left * gain);
            Write(output, at + 2, right * gain);
        }

        _held = input[whole..].ToArray();
        _ready = output;
        _readyAt = 0;
    }

    private static void Write(byte[] output, int at, double sample)
    {
        var value = (short)Math.Clamp(Math.Round(sample * 32768.0), short.MinValue, short.MaxValue);

        output[at] = (byte)(value & 0xFF);
        output[at + 1] = (byte)((value >> 8) & 0xFF);
    }

    /// <summary>The gain for one stereo frame.</summary>
    private double Next(double left, double right)
    {
        _sum += ((left * left) + (right * right)) / 2;

        if (++_inFrame == FrameLength)
        {
            Measure();
        }

        _gain += GainStep * (_wanted - _gain);
        _limit += LimiterRelease * (1 - _limit);

        var raised = Math.Max(Math.Abs(left), Math.Abs(right)) * _gain;

        if (raised * _limit > SpeechLeveller.Ceiling)
        {
            _limit = SpeechLeveller.Ceiling / raised;
        }

        return Math.Max(1, _gain * _limit);
    }

    /// <summary>Ends a frame: a voiced one moves the level and the gain it wants; a silent one changes nothing.</summary>
    private void Measure()
    {
        var power = _sum / FrameLength;

        _sum = 0;
        _inFrame = 0;

        if (power < SpeechLeveller.Gate * SpeechLeveller.Gate)
        {
            return;
        }

        _power = _frames == 0 ? power : _power + ((power - _power) / Math.Min(_frames + 1, SettledFrames));
        _frames = Math.Min(_frames + 1, SettledFrames);
        _wanted = Math.Clamp(SpeechLeveller.Target / Math.Sqrt(_power), 1, SpeechLeveller.MostGain);
    }
}

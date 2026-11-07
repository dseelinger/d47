using System.Collections.Concurrent;

namespace D47.Core.Audio;

/// <summary>
/// Raises speech whose voiced level is under <see cref="Target"/> to it, as a running filter on
/// <see cref="AudioFormat.Standard"/> PCM. It never cuts, and a peak limiter keeps a raised sample under −1 dBFS.
/// </summary>
/// <remarks>
/// Each sample's gain depends only on the samples before it, so the output does not depend on where the chunks are
/// cut and is as long as the input.
/// </remarks>
public sealed class SpeechLeveller : IPcmFilter
{
    /// <summary>The voiced level speech is raised to, as an RMS of full scale: −18 dBFS.</summary>
    public const double Target = 0.125;

    /// <summary>A frame whose RMS is under this, −45 dBFS, is silence: it is not measured and the gain holds through it.</summary>
    internal const double Gate = 0.005623413251903491;

    /// <summary>The most the gain raises: +18 dB.</summary>
    internal const double MostGain = 7.943282347242815;

    /// <summary>What the limiter holds a raised sample to: a little under −1 dBFS, so a rounded sample stays under it.</summary>
    internal const double Ceiling = 0.89;

    /// <summary>How many samples one measured frame is: 50 ms.</summary>
    private const int FrameSamples = 2_400;

    /// <summary>How many voiced frames the level is averaged over once it has that many: two seconds.</summary>
    private const int SettledFrames = 40;

    /// <summary>How fast the gain moves toward the measured one: a 20 ms time constant.</summary>
    private static readonly double GainStep = 1 - Math.Exp(-1 / (0.02 * AudioFormat.Standard.SampleRate));

    /// <summary>How fast the limiter's gain returns to 1 after a peak: an 80 ms time constant.</summary>
    private static readonly double LimiterRelease = 1 - Math.Exp(-1 / (0.08 * AudioFormat.Standard.SampleRate));

    private readonly Action<double>? _ended;

    /// <summary>The voiced frames' mean square, followed.</summary>
    private double _power;

    /// <summary>How many voiced frames <see cref="_power"/> averages, up to <see cref="SettledFrames"/>.</summary>
    private int _frames;

    private double _wanted = 1;
    private double _gain = 1;
    private double _limit = 1;
    private double _sum;
    private int _inFrame;
    private byte[] _held = [];
    private bool _finished;

    /// <summary>A leveller starting from <paramref name="level"/>, a voiced RMS another clip in the voice ended at, or fresh.</summary>
    public SpeechLeveller(double? level = null)
        : this(level, ended: null)
    {
    }

    /// <param name="ended">Told the voiced level once finished, when one was measured.</param>
    internal SpeechLeveller(double? level, Action<double>? ended)
    {
        _ended = ended;

        if (level is > 0 and var start)
        {
            _power = start * start;
            _frames = SettledFrames;
            _wanted = GainFor(_power);
            _gain = _wanted;
        }
    }

    /// <summary>The voiced RMS measured so far, or null before any voiced frame.</summary>
    public double? Level => _frames > 0 ? Math.Sqrt(_power) : null;

    /// <summary>A clip through a fresh leveller, in the standard format.</summary>
    public static AudioClip Apply(AudioClip clip) => new SpeechLeveller().Run(clip);

    /// <summary>The whole clip through this leveller: the same bytes as Push then Finish.</summary>
    public AudioClip Run(AudioClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);

        var standard = PcmConverter.ToStandard(clip);
        var body = Push(standard.Pcm.Span);
        var tail = Finish();

        return standard with { Pcm = tail.Length == 0 ? body : [.. body, .. tail] };
    }

    public byte[] Push(ReadOnlySpan<byte> pcm)
    {
        if (_finished)
        {
            throw new InvalidOperationException("The leveller has already been finished.");
        }

        var input = PcmConverter.Join(_held, pcm);
        var whole = input.Length & ~1;
        var output = new byte[whole];

        for (var at = 0; at < whole; at += 2)
        {
            var sample = (short)(input[at] | (input[at + 1] << 8));
            var value = (short)Math.Clamp(Math.Round(Next(sample / 32768.0) * 32768.0), short.MinValue, short.MaxValue);

            output[at] = (byte)(value & 0xFF);
            output[at + 1] = (byte)((value >> 8) & 0xFF);
        }

        _held = input[whole..].ToArray();
        return output;
    }

    /// <summary>A trailing odd byte is not a sample and is dropped.</summary>
    public byte[] Finish()
    {
        if (_finished)
        {
            return [];
        }

        _finished = true;
        _held = [];

        if (Level is { } level)
        {
            _ended?.Invoke(level);
        }

        return [];
    }

    private double Next(double sample)
    {
        _sum += sample * sample;

        if (++_inFrame == FrameSamples)
        {
            Measure();
        }

        _gain += GainStep * (_wanted - _gain);
        _limit += LimiterRelease * (1 - _limit);

        var raised = Math.Abs(sample * _gain);

        if (raised * _limit > Ceiling)
        {
            _limit = Ceiling / raised;
        }

        return sample * Math.Max(1, _gain * _limit);
    }

    /// <summary>Ends a frame: a voiced one moves the level and the gain it wants; a silent one changes nothing.</summary>
    private void Measure()
    {
        var power = _sum / FrameSamples;

        _sum = 0;
        _inFrame = 0;

        if (power < Gate * Gate)
        {
            return;
        }

        _power = _frames == 0 ? power : _power + ((power - _power) / Math.Min(_frames + 1, SettledFrames));
        _frames = Math.Min(_frames + 1, SettledFrames);
        _wanted = GainFor(_power);
    }

    private static double GainFor(double power) => Math.Clamp(Target / Math.Sqrt(power), 1, MostGain);
}

/// <summary>Where each voice's leveller ended this session, so its next clip starts there. Held in memory only.</summary>
public sealed class SpeechLevels
{
    private readonly ConcurrentDictionary<(string Provider, string Voice), double> _levels = new();

    /// <summary>A leveller for one clip in a voice, starting where its last clip ended and recording where this one does.</summary>
    public SpeechLeveller For(string provider, string? voice)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var key = (provider, voice ?? string.Empty);

        return new SpeechLeveller(
            _levels.TryGetValue(key, out var level) ? level : null,
            ended => _levels[key] = ended);
    }
}

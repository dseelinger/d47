namespace D47.Core.Audio;

/// <summary>
/// Speech put through a comms link, so a voice arriving from outside the ship sounds like it arrived
/// from outside the ship.
/// </summary>
public static class RadioVoice
{
    /// <summary>Which roles arrive over a link rather than from the next seat.</summary>
    public static bool IsOverTheAir(VoiceRole role) => role is not (VoiceRole.ShipAi or VoiceRole.Crew or VoiceRole.Narrator);

    /// <summary>The treatment for a role, or null where there is none.</summary>
    public static Func<AudioClip, AudioClip>? Colours(VoiceRole role) => Colours(role, 1);

    /// <summary>The treatment for a role over a link of <paramref name="strength"/>, 0 to 1, or null where there is none.</summary>
    public static Func<AudioClip, AudioClip>? Colours(VoiceRole role, double strength) =>
        Colours(role, strength, overheard: false);

    /// <summary>The treatment for a role, thinner and hissier when the line is <paramref name="overheard"/>.</summary>
    public static Func<AudioClip, AudioClip>? Colours(VoiceRole role, double strength, bool overheard) =>
        IsOverTheAir(role) ? clip => Apply(clip, strength, overheard) : null;

    /// <summary><see cref="Colours(VoiceRole, double, bool)"/> as a running filter factory, null in the same cases.</summary>
    public static Func<IPcmFilter>? RunningColours(VoiceRole role, double strength, bool overheard) =>
        IsOverTheAir(role) ? () => Filter(strength, overheard) : null;

    /// <summary>The link as a running filter on <see cref="AudioFormat.Standard"/> PCM.</summary>
    public static IPcmFilter Filter(double strength, bool overheard) => new Link(AudioFormat.Standard, strength, overheard);

    /// <summary>The bottom of the passband.</summary>
    private const double LowEdgeHz = 400;

    /// <summary>The top.</summary>
    private const double HighEdgeHz = 2_700;

    /// <summary>The bottom of the passband for an overheard line.</summary>
    private const double OverheardLowEdgeHz = 550;

    /// <summary>The top.</summary>
    private const double OverheardHighEdgeHz = 2_200;

    /// <summary>How loud the static is under an overheard line.</summary>
    private const double OverheardHissUnderVoice = 0.05;

    /// <summary>The bottom of the passband on a link with no signal left.</summary>
    private const double WeakLowEdgeHz = 600;

    /// <summary>And its top.</summary>
    private const double WeakHighEdgeHz = 2_000;

    /// <summary>The loudness the receiver's automatic gain control brings the voice to, as an RMS of full scale.</summary>
    internal const double Target = 0.10;

    /// <summary>
    /// The level the gain control holds its follower at. Above <see cref="Target"/> because the follower tracks the
    /// louder syllables; set so speech lands on <see cref="Target"/> overall.
    /// </summary>
    private const double Reference = 0.165;

    /// <summary>How fast the gain control follows a voice getting louder.</summary>
    private static readonly TimeSpan Attack = TimeSpan.FromMilliseconds(10);

    /// <summary>And a voice getting quieter.</summary>
    private static readonly TimeSpan Release = TimeSpan.FromMilliseconds(300);

    /// <summary>The least gain the gain control applies.</summary>
    private const double LeastGain = 0.1;

    /// <summary>The most.</summary>
    private const double MostGain = 4.0;

    /// <summary>Butterworth.</summary>
    private const double Q = 0.70710678118654752;

    /// <summary>How hard the link is driven before it saturates.</summary>
    private const double Drive = 2.6;

    /// <summary>How loud the static is under the words.</summary>
    private const double HissUnderVoice = 0.034;

    /// <summary>And how loud it is once the words stop, on the bare carrier.</summary>
    private const double HissOnTheOpenCarrier = 0.148;

    /// <summary>How loud the bare carrier is on a link with no signal left.</summary>
    private const double HissOnAWeakCarrier = 0.3;

    /// <summary>How long one stretch of voice lost to a weak link lasts.</summary>
    private static readonly TimeSpan Dropout = TimeSpan.FromMilliseconds(90);

    /// <summary>How long the voice takes to go and come back around a dropout.</summary>
    private static readonly TimeSpan DropoutEdge = TimeSpan.FromMilliseconds(6);

    /// <summary>The chance each stretch is lost on a link with no signal left.</summary>
    private const double DropoutChance = 0.35;

    /// <summary>How long the floor takes to come up between the two.</summary>
    private static readonly TimeSpan Swell = TimeSpan.FromMilliseconds(40);

    /// <summary>How long the link stays open after the last word, before it cuts.</summary>
    private static readonly TimeSpan Tail = TimeSpan.FromMilliseconds(200);

    /// <summary>How long the very end is faded over.</summary>
    private static readonly TimeSpan Cut = TimeSpan.FromMilliseconds(3);

    /// <summary>The same speech, over a link.</summary>
    public static AudioClip Apply(AudioClip clip) => Apply(clip, 1);

    /// <summary>
    /// The same speech, over a link of <paramref name="strength"/>, 0 to 1. Below 1 the static rises, the
    /// passband narrows and stretches of the voice drop out to the carrier.
    /// </summary>
    public static AudioClip Apply(AudioClip clip, double strength) => Apply(clip, strength, overheard: false);

    /// <summary>The same speech, heard as traffic further off: a narrower passband and more static.</summary>
    public static AudioClip Apply(AudioClip clip, double strength, bool overheard)
    {
        if (clip.Pcm.Length / 2 == 0)
        {
            return clip;
        }

        var filter = new Link(clip.Format, strength, overheard);
        var body = filter.Push(clip.Pcm.Span);
        var tail = filter.Finish();
        var pcm = new byte[body.Length + tail.Length];

        body.CopyTo(pcm, 0);
        tail.CopyTo(pcm, body.Length);

        return clip with { Name = $"{clip.Name} (radio)", Pcm = pcm };
    }

    /// <summary>Interleaved 16-bit PCM through the link, one sample at a time.</summary>
    private sealed class Link : IPcmFilter
    {
        private readonly int _channels;
        private readonly int _rate;
        private readonly double _hissUnderVoice;
        private readonly double _hissOnTheCarrier;
        private readonly (Biquad[] High, Biquad[] Low) _voiceBand;
        private readonly (Biquad[] High, Biquad[] Low) _staticBand;
        private readonly Dropouts? _dropouts;
        private readonly double _attack;
        private readonly double _release;

        /// <summary>The shaped voice's mean square over the attack time.</summary>
        private double _power = Reference * Reference;

        /// <summary>The gain control's level: <see cref="_power"/>, followed up at the attack and down at the release.</summary>
        private double _level = Reference * Reference;

        private double _voiced = 1;
        private uint _noise = 0x9E3779B9u;
        private long _index;
        private byte[] _held = [];
        private bool _finished;

        public Link(AudioFormat format, double strength, bool overheard)
        {
            var weakness = double.IsNaN(strength) ? 0 : 1 - Math.Clamp(strength, 0, 1);

            _channels = Math.Max(1, format.Channels);
            _rate = format.SampleRate > 0 ? format.SampleRate : AudioFormat.Standard.SampleRate;

            var nearLow = overheard ? OverheardLowEdgeHz : LowEdgeHz;
            var nearHigh = overheard ? OverheardHighEdgeHz : HighEdgeHz;
            var nearHiss = overheard ? OverheardHissUnderVoice : HissUnderVoice;

            var lowEdge = nearLow + (weakness * (Math.Max(nearLow, WeakLowEdgeHz) - nearLow));
            var highEdge = nearHigh + (weakness * (Math.Min(nearHigh, WeakHighEdgeHz) - nearHigh));

            _hissUnderVoice = nearHiss + (weakness * (HissOnTheOpenCarrier - nearHiss));
            _hissOnTheCarrier = HissOnTheOpenCarrier + (weakness * (HissOnAWeakCarrier - HissOnTheOpenCarrier));

            // One filter pair per channel, and a second pair for the static.
            _voiceBand = Band(_channels, _rate, lowEdge, highEdge);
            _staticBand = Band(_channels, _rate, lowEdge, highEdge);
            _dropouts = Dropouts.For(_rate, weakness);

            // The gain control is linked across channels, so it steps once a sample.
            _attack = 1 - Math.Exp(-1 / (Attack.TotalSeconds * _rate * _channels));
            _release = 1 - Math.Exp(-1 / (Release.TotalSeconds * _rate * _channels));
        }

        public byte[] Push(ReadOnlySpan<byte> pcm)
        {
            if (_finished)
            {
                throw new InvalidOperationException("The link has already been finished.");
            }

            var input = pcm;

            if (_held.Length > 0)
            {
                var joined = new byte[_held.Length + pcm.Length];

                _held.CopyTo(joined, 0);
                pcm.CopyTo(joined.AsSpan(_held.Length));
                input = joined;
            }

            var whole = input.Length - (input.Length % (2 * _channels));
            var output = new byte[whole];

            for (var at = 0; at < whole; at += 2)
            {
                Write(output, at, Voice((short)(input[at] | (input[at + 1] << 8)) / 32768.0));
            }

            _held = input[whole..].ToArray();
            return output;
        }

        public byte[] Finish()
        {
            if (_finished)
            {
                return [];
            }

            _finished = true;

            // What is left of a part-frame, then the carrier still open, then the cut.
            var ragged = _held.Length / 2;
            var tail = (int)(Tail.TotalSeconds * _rate) / _channels * _channels;
            var fade = Math.Min(tail, (int)(Cut.TotalSeconds * _rate));
            var swell = Math.Max(1, (int)(Swell.TotalSeconds * _rate));
            var output = new byte[(ragged + tail) * 2];

            for (var at = 0; at < ragged * 2; at += 2)
            {
                Write(output, at, Voice((short)(_held[at] | (_held[at + 1] << 8)) / 32768.0));
            }

            for (var index = 0; index < tail; index++)
            {
                var hiss = _hissUnderVoice + ((_hissOnTheCarrier - _hissUnderVoice) * Math.Min(1, (double)index / swell));
                var sample = Static(hiss);

                if (index >= tail - fade)
                {
                    sample *= (double)(tail - index) / fade;
                }

                Write(output, (ragged + index) * 2, sample);
            }

            return output;
        }

        /// <summary>One voice sample through the band, the drive, the gain control and any dropout, over the static.</summary>
        private double Voice(double sample)
        {
            var channel = (int)(_index % _channels);

            if (channel == 0 && _dropouts is not null)
            {
                _voiced = _dropouts.Next();
            }

            var shaped = Math.Tanh(_voiceBand.Low[channel].Next(_voiceBand.High[channel].Next(sample)) * Drive);
            _power += _attack * ((shaped * shaped) - _power);
            _level += (_power > _level ? _attack : _release) * (_power - _level);

            var gain = _level > 0 ? Math.Clamp(Reference / Math.Sqrt(_level), LeastGain, MostGain) : MostGain;
            var voice = Math.Clamp(shaped * gain, -1, 1);
            var hiss = _hissUnderVoice + ((_hissOnTheCarrier - _hissUnderVoice) * (1 - _voiced));

            return (voice * _voiced) + Static(hiss);
        }

        /// <summary>The next static sample at <paramref name="hiss"/>, through a band-pass of its own so it is the voice's colour.</summary>
        private double Static(double hiss)
        {
            var channel = (int)(_index % _channels);

            _noise = (_noise * 1664525u) + 1013904223u;
            _index++;

            var white = ((_noise >> 8) / 16777215.0 * 2) - 1;

            return _staticBand.Low[channel].Next(_staticBand.High[channel].Next(white)) * hiss;
        }

        private static void Write(byte[] pcm, int at, double sample)
        {
            var value = (short)Math.Clamp(Math.Round(sample * 32767.0), short.MinValue, short.MaxValue);

            pcm[at] = (byte)(value & 0xFF);
            pcm[at + 1] = (byte)((value >> 8) & 0xFF);
        }
    }

    /// <summary>A band-pass per channel, between the two edges.</summary>
    private static (Biquad[] High, Biquad[] Low) Band(int channels, int rate, double lowEdge, double highEdge)
    {
        var high = new Biquad[channels];
        var low = new Biquad[channels];

        for (var channel = 0; channel < channels; channel++)
        {
            high[channel] = Biquad.HighPass(lowEdge, rate);
            low[channel] = Biquad.LowPass(highEdge, rate);
        }

        return (high, low);
    }

    /// <summary>
    /// The voice's gain frame by frame, 0 where a weak link has lost it, ramped at each edge. Seeded by a constant, so
    /// the same clip loses the same stretches.
    /// </summary>
    private sealed class Dropouts
    {
        private readonly double _chance;
        private readonly int _stretch;
        private readonly double _step;
        private uint _state = 0x2545F491u;
        private double _gain = 1;
        private bool _lost;
        private long _frame;

        private Dropouts(int rate, double chance)
        {
            _chance = chance;
            _stretch = Math.Max(1, (int)(Dropout.TotalSeconds * rate));
            _step = 1.0 / Math.Max(1, (int)(DropoutEdge.TotalSeconds * rate));
        }

        /// <summary>Null when nothing is lost.</summary>
        public static Dropouts? For(int rate, double weakness)
        {
            var chance = weakness * DropoutChance;

            return chance > 0 ? new Dropouts(rate, chance) : null;
        }

        public double Next()
        {
            if (_frame++ % _stretch == 0)
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                _lost = _state / (double)uint.MaxValue < _chance;
            }

            _gain = _lost ? Math.Max(0, _gain - _step) : Math.Min(1, _gain + _step);
            return _gain;
        }
    }

    /// <summary>One second-order section, direct form 1, with the usual cookbook coefficients.</summary>
    private struct Biquad
    {
        private double _b0, _b1, _b2, _a1, _a2;
        private double _x1, _x2, _y1, _y2;

        public static Biquad LowPass(double frequency, int sampleRate)
        {
            var (cos, alpha) = Shape(frequency, sampleRate);
            var a0 = 1 + alpha;

            return new Biquad
            {
                _b0 = (1 - cos) / 2 / a0,
                _b1 = (1 - cos) / a0,
                _b2 = (1 - cos) / 2 / a0,
                _a1 = -2 * cos / a0,
                _a2 = (1 - alpha) / a0,
            };
        }

        public static Biquad HighPass(double frequency, int sampleRate)
        {
            var (cos, alpha) = Shape(frequency, sampleRate);
            var a0 = 1 + alpha;

            return new Biquad
            {
                _b0 = (1 + cos) / 2 / a0,
                _b1 = -(1 + cos) / a0,
                _b2 = (1 + cos) / 2 / a0,
                _a1 = -2 * cos / a0,
                _a2 = (1 - alpha) / a0,
            };
        }

        /// <summary>The two quantities both responses are built from.</summary>
        private static (double Cos, double Alpha) Shape(double frequency, int sampleRate)
        {
            var rate = sampleRate > 0 ? sampleRate : AudioFormat.Standard.SampleRate;
            var w0 = 2 * Math.PI * Math.Clamp(frequency, 1, rate * 0.45) / rate;

            return (Math.Cos(w0), Math.Sin(w0) / (2 * Q));
        }

        public double Next(double x)
        {
            var y = (_b0 * x) + (_b1 * _x1) + (_b2 * _x2) - (_a1 * _y1) - (_a2 * _y2);

            _x2 = _x1;
            _x1 = x;
            _y2 = _y1;
            _y1 = y;

            return y;
        }
    }
}

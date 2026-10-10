using System.Runtime.InteropServices;

namespace D47.Core.Audio;

public static partial class GuardianVoice
{
    /// <summary>An effect that needs the whole clip: buffers every sample and treats them all on finish.</summary>
    private static WholeClipStage WholeClip(Func<double[], double[]> run) => new(run);

    private sealed class WholeClipStage(Func<double[], double[]> run) : IGuardianStage
    {
        private readonly List<double> _clip = [];

        public void Push(ReadOnlySpan<double> input, List<double> output) => _clip.AddRange(input);

        public void Finish(List<double> output) => output.AddRange(run([.. _clip]));
    }

    /// <summary>Stages in series, each one's output pushed into the next.</summary>
    private sealed class ChainStage(IGuardianStage[] stages) : IGuardianStage
    {
        public int Delay => stages.Sum(stage => stage.Delay);

        public void Push(ReadOnlySpan<double> input, List<double> output) => Run(0, input, output);

        public void Finish(List<double> output)
        {
            for (var index = 0; index < stages.Length; index++)
            {
                var rest = new List<double>();
                stages[index].Finish(rest);
                Run(index + 1, CollectionsMarshal.AsSpan(rest), output);
            }
        }

        private void Run(int from, ReadOnlySpan<double> input, List<double> output)
        {
            for (var index = from; index < stages.Length; index++)
            {
                var next = new List<double>();
                stages[index].Push(input, next);
                input = CollectionsMarshal.AsSpan(next);
            }

            output.AddRange(input);
        }
    }

    /// <summary>
    /// The last <c>capacity</c> samples added, read by their index from the first; zero before the first and
    /// from the next one on. A caller must not read further back than the capacity.
    /// </summary>
    private sealed class History(int capacity)
    {
        private readonly double[] _ring = new double[Math.Max(1, capacity)];

        public int Count { get; private set; }

        public double this[int index] => index >= 0 && index < Count ? _ring[index % _ring.Length] : 0;

        public void Add(double sample)
        {
            _ring[Count % _ring.Length] = sample;
            Count++;
        }

        /// <summary>A linearly interpolated read, zero before the first sample.</summary>
        public double Read(double position)
        {
            if (position < 0)
            {
                return 0;
            }

            var below = (int)position;
            var fraction = position - below;

            return (this[below] * (1 - fraction)) + (this[below + 1] * fraction);
        }

        /// <summary>A capacity that covers reads up to <paramref name="milliseconds"/> back.</summary>
        public static int Reach(double milliseconds, int rate) => (int)Math.Ceiling(milliseconds / 1000 * rate) + 2;
    }

    /// <summary><c>y[n] = x[n] + g·y[n−D]</c>, one sample at a time.</summary>
    internal sealed class FeedbackCombLine(int delay, double feedback)
    {
        private readonly int _delay = delay;
        private readonly double _feedback = feedback;
        private readonly History _output = new(delay);

        public double Next(double input)
        {
            var index = _output.Count;
            var sample = input + (index >= _delay ? _feedback * _output[index - _delay] : 0);

            _output.Add(sample);
            return sample;
        }
    }

    /// <summary><c>y[n] = −g·x[n] + x[n−D] + g·y[n−D]</c>, one sample at a time.</summary>
    internal sealed class AllpassLine(int delay, double gain)
    {
        private readonly int _delay = delay;
        private readonly double _gain = gain;
        private readonly History _input = new(delay);
        private readonly History _output = new(delay);

        public double Next(double input)
        {
            var index = _input.Count;
            var delayed = index >= _delay ? _input[index - _delay] + (_gain * _output[index - _delay]) : 0;
            var sample = (-_gain * input) + delayed;

            _input.Add(input);
            _output.Add(sample);
            return sample;
        }
    }

    /// <summary>The low-passed wet signal of four parallel combs averaged and two allpasses in series.</summary>
    private sealed class ReverbTank(int rate)
    {
        private readonly FeedbackCombLine[] _combs =
            [.. ReverbCombs.Select(comb => new FeedbackCombLine(Samples(comb.DelayMs, rate), comb.Feedback))];

        private readonly AllpassLine[] _allpasses =
            [.. ReverbAllpassMs.Select(delayMs => new AllpassLine(Samples(delayMs, rate), ReverbAllpassGain))];

        private readonly double _smoothing = Math.Exp(-2 * Math.PI * ReverbLowPassHz / rate);
        private double _low;

        public double Next(double input)
        {
            var wet = 0.0;

            foreach (var comb in _combs)
            {
                wet += comb.Next(input) / ReverbCombs.Length;
            }

            foreach (var allpass in _allpasses)
            {
                wet = allpass.Next(wet);
            }

            _low = ((1 - _smoothing) * wet) + (_smoothing * _low);
            return _low;
        }
    }

    private sealed class ChorusStage(double depth, int rate) : IGuardianStage
    {
        private readonly double _depth = depth;
        private readonly int _rate = rate;
        private readonly History _input = new(History.Reach(ChorusCopies.Max(copy => copy.Base + copy.Depth), rate));

        public void Push(ReadOnlySpan<double> input, List<double> output)
        {
            foreach (var sample in input)
            {
                var index = _input.Count;
                var seconds = (double)index / _rate;
                var copies = 0.0;

                _input.Add(sample);

                foreach (var (baseMs, depthMs, hertz, offset) in ChorusCopies)
                {
                    var delayMs = baseMs + (depthMs * Math.Sin((2 * Math.PI * hertz * seconds) + offset));
                    copies += _input.Read(index - (delayMs / 1000 * _rate));
                }

                output.Add(sample + (_depth * copies));
            }
        }

        public void Finish(List<double> output)
        {
        }
    }

    /// <summary>One delayed copy read from a feedback line whose delay is swept sinusoidally.</summary>
    private sealed class FlangerStage(double rateHz, int rate) : IGuardianStage
    {
        private const double Center = (FlangerMinMs + FlangerMaxMs) / 2;
        private const double Depth = (FlangerMaxMs - FlangerMinMs) / 2;

        private readonly double _rateHz = rateHz;
        private readonly int _rate = rate;
        private readonly History _buffer = new(History.Reach(FlangerMaxMs, rate));

        public void Push(ReadOnlySpan<double> input, List<double> output)
        {
            foreach (var sample in input)
            {
                var index = _buffer.Count;
                var seconds = (double)index / _rate;
                var delayMs = Center + (Depth * Math.Sin(2 * Math.PI * _rateHz * seconds));
                var delayed = _buffer.Read(index - (delayMs / 1000 * _rate));

                _buffer.Add(sample + (FlangerFeedback * delayed));
                output.Add((FlangerDry * sample) + (FlangerWet * delayed));
            }
        }

        public void Finish(List<double> output)
        {
        }
    }

    /// <summary><see cref="PhaserStages"/> first-order allpass stages in series, their corner swept together.</summary>
    private sealed class PhaserStage(double rateHz, int rate) : IGuardianStage
    {
        private readonly double[] _x1 = new double[PhaserStages];
        private readonly double[] _y1 = new double[PhaserStages];
        private double _feedback;
        private int _index;

        public void Push(ReadOnlySpan<double> input, List<double> output)
        {
            foreach (var sample in input)
            {
                var seconds = (double)_index++ / rate;
                var sweep = (Math.Sin(2 * Math.PI * rateHz * seconds) + 1) / 2;
                var corner = PhaserMinHz + ((PhaserMaxHz - PhaserMinHz) * sweep);
                var tan = Math.Tan(Math.PI * corner / rate);
                var a = (tan - 1) / (tan + 1);

                var stage = sample + (PhaserFeedback * _feedback);

                for (var s = 0; s < PhaserStages; s++)
                {
                    var y = (a * stage) + _x1[s] - (a * _y1[s]);
                    _x1[s] = stage;
                    _y1[s] = y;
                    stage = y;
                }

                _feedback = stage;
                output.Add((PhaserDry * sample) + (PhaserWet * stage));
            }
        }

        public void Finish(List<double> output)
        {
        }
    }

    /// <summary>A band-pass biquad whose centre tracks an envelope follower on the input's loudness.</summary>
    private sealed class WahStage : IGuardianStage
    {
        private readonly double _wet;
        private readonly double _dry;
        private readonly int _rate;
        private readonly double _attack;
        private readonly double _release;
        private readonly int _controlSamples;
        private Biquad _filter;
        private double _envelope;
        private int _index;

        public WahStage(double wet, int rate)
        {
            _wet = wet;
            _dry = 1 - wet;
            _rate = rate;
            _attack = Math.Exp(-1.0 / (WahAttackSeconds * rate));
            _release = Math.Exp(-1.0 / (WahReleaseSeconds * rate));
            _controlSamples = Math.Max(1, (int)Math.Round(WahControlSeconds * rate));
            _filter.SetBandPass(WahMinHz, WahQ, rate);
        }

        public void Push(ReadOnlySpan<double> input, List<double> output)
        {
            foreach (var x in input)
            {
                var level = Math.Abs(x);
                _envelope = level > _envelope
                    ? (_attack * _envelope) + ((1 - _attack) * level)
                    : (_release * _envelope) + ((1 - _release) * level);

                if (_index++ % _controlSamples == 0)
                {
                    var frequency = WahMinHz + ((WahMaxHz - WahMinHz) * Math.Clamp(_envelope, 0, 1));
                    _filter.SetBandPass(frequency, WahQ, _rate);
                }

                output.Add((_dry * x) + (_wet * _filter.Next(x)));
            }
        }

        public void Finish(List<double> output)
        {
        }
    }

    private sealed class CombStage(double wet, int rate) : IGuardianStage
    {
        private readonly FeedbackCombLine _line = new(Samples(CombDelayMs, rate), CombFeedback);
        private readonly double _wet = wet;
        private readonly double _dry = 1 - wet;

        public void Push(ReadOnlySpan<double> input, List<double> output)
        {
            foreach (var sample in input)
            {
                output.Add((_dry * sample) + (_wet * _line.Next(sample)));
            }
        }

        public void Finish(List<double> output)
        {
        }
    }

    private sealed class RingModStage(double wet, double hertz, int rate) : IGuardianStage
    {
        private readonly double _wet = wet;
        private readonly double _dry = 1 - wet;
        private int _index;

        public void Push(ReadOnlySpan<double> input, List<double> output)
        {
            foreach (var sample in input)
            {
                var carrier = Math.Sin(2 * Math.PI * hertz * _index++ / rate);
                output.Add((_dry * sample) + (_wet * sample * carrier));
            }
        }

        public void Finish(List<double> output)
        {
        }
    }

    private sealed class TremoloStage(double hertz, int rate) : IGuardianStage
    {
        private int _index;

        public void Push(ReadOnlySpan<double> input, List<double> output)
        {
            foreach (var sample in input)
            {
                var gain = 1 - (TremoloDepth * 0.5 * (1 - Math.Cos(2 * Math.PI * hertz * _index++ / rate)));
                output.Add(sample * gain);
            }
        }

        public void Finish(List<double> output)
        {
        }
    }

    private sealed class OverdriveStage(double drive) : IGuardianStage
    {
        public void Push(ReadOnlySpan<double> input, List<double> output)
        {
            foreach (var sample in input)
            {
                output.Add((OverdriveDry * sample) + (OverdriveWet * Math.Tanh(drive * sample)));
            }
        }

        public void Finish(List<double> output)
        {
        }
    }

    /// <summary>
    /// Damages one short stretch at a time, rotating through sample-and-hold, bit reduction and stutter. The
    /// schedule comes from a fixed seed, so the same clip is damaged the same way every time. The gaps between
    /// stretches are divided by the parameter.
    /// </summary>
    private sealed class GlitchStage : IGuardianStage
    {
        private const double LongestSeconds = 0.18;

        private readonly int _rate;
        private readonly double _spacing;
        private readonly History _input;
        private readonly List<Stretch> _stretches = [];
        private Noise _random = new(0xBB67AE85u);
        private double _at;
        private int _kind;

        public GlitchStage(double often, int rate)
        {
            _rate = rate;
            _spacing = 1 / often;
            _input = new History((int)(LongestSeconds * rate) + 2);
            _at = _random.Next(0.4, 0.9) * rate * _spacing;
        }

        public void Push(ReadOnlySpan<double> input, List<double> output)
        {
            foreach (var sample in input)
            {
                var index = _input.Count;
                _input.Add(sample);

                while ((int)_at <= index)
                {
                    _stretches.Add(Draw());
                }

                _stretches.RemoveAll(stretch => stretch.End <= index);
                output.Add(_stretches.Count > 0 ? Damaged(_stretches[^1], index, sample) : sample);
            }
        }

        public void Finish(List<double> output)
        {
        }

        /// <summary>The next stretch in the schedule, starting at the current gap's end.</summary>
        private Stretch Draw()
        {
            var start = (int)_at;
            var end = start + (int)(_random.Next(0.06, LongestSeconds) * _rate);
            var kind = _kind % 3;
            var stretch = kind switch
            {
                0 => new Stretch(start, end, kind, Math.Max(1, (int)Math.Round(_random.Next(0.25, 0.55) / 1000 * _rate)), 0),
                1 => new Stretch(start, end, kind, 0, 2.0 / Math.Round(_random.Next(8, 32))),
                _ => new Stretch(start, end, kind, Math.Max(1, (int)(_random.Next(0.025, 0.05) * _rate)), 0),
            };

            _kind++;
            _at += _random.Next(0.7, 1.6) * _rate * _spacing;

            return stretch;
        }

        private double Damaged(Stretch stretch, int index, double sample) => stretch.Kind switch
        {
            0 => _input[stretch.Start + ((index - stretch.Start) / stretch.Size * stretch.Size)],
            1 => Math.Round(sample / stretch.Step) * stretch.Step,
            _ => _input[stretch.Start + ((index - stretch.Start) % stretch.Size)],
        };

        /// <summary>Samples from <c>Start</c> up to <c>End</c>; <c>Size</c> is the hold or the grain, <c>Step</c> the quantiser's.</summary>
        private readonly record struct Stretch(int Start, int End, int Kind, int Size, double Step);
    }

    /// <summary>Four parallel combs averaged, two allpasses in series, a low-pass on the wet, and a faded tail.</summary>
    private sealed class ReverbStage(double mix, int rate) : IGuardianStage
    {
        private readonly ReverbTank _tank = new(rate);
        private readonly int _tail = (int)Math.Round(ReverbTailSeconds * rate);
        private int _count;

        public void Push(ReadOnlySpan<double> input, List<double> output)
        {
            foreach (var sample in input)
            {
                output.Add((ReverbDry * sample) + (mix * _tank.Next(sample)));
            }

            _count += input.Length;
        }

        public void Finish(List<double> output)
        {
            var total = _count + _tail;

            for (var index = _count; index < total; index++)
            {
                var sample = (ReverbDry * 0.0) + (mix * _tank.Next(0));
                output.Add(sample * ((double)(total - 1 - index) / _tail));
            }
        }
    }

    /// <summary>The line through <see cref="RadioVoice"/> at <c>strength</c>, quantised to 16-bit PCM as the link reads it, with the link's tail on finish.</summary>
    private sealed class RadioStage(double strength, int rate) : IGuardianStage
    {
        private readonly IPcmFilter _link = RadioVoice.Filter(new AudioFormat(rate, 1), strength, overheard: false);

        public void Push(ReadOnlySpan<double> input, List<double> output) =>
            Append(_link.Push(Encode([input.ToArray()], 1)), output);

        public void Finish(List<double> output) => Append(_link.Finish(), output);

        private static void Append(byte[] pcm, List<double> output) =>
            output.AddRange(Decode(pcm, 1, pcm.Length / 2)[0]);
    }

    /// <summary>The radio link blended with the dry line by <c>mix</c>, with a click before the first sample and after the last.</summary>
    private sealed class HelmetStage(double mix, int rate) : IGuardianStage
    {
        private readonly RadioStage _radio = new(1, rate);
        private readonly List<double> _dry = [];
        private int _emitted;
        private bool _opened;

        public void Push(ReadOnlySpan<double> input, List<double> output)
        {
            Open(output);
            _dry.AddRange(input);

            var treated = new List<double>();
            _radio.Push(input, treated);
            Blend(treated, output);
        }

        public void Finish(List<double> output)
        {
            Open(output);

            var treated = new List<double>();
            _radio.Finish(treated);
            Blend(treated, output);
            output.AddRange(Click(rate, rising: true));
        }

        private void Open(List<double> output)
        {
            if (!_opened)
            {
                _opened = true;
                output.AddRange(Click(rate, rising: false));
            }
        }

        private void Blend(List<double> treated, List<double> output)
        {
            foreach (var sample in treated)
            {
                var dry = _emitted < _dry.Count ? _dry[_emitted] : 0;
                output.Add((mix * sample) + ((1 - mix) * dry));
                _emitted++;
            }
        }
    }

    /// <summary>The line with a synthesised breath, the parameter's milliseconds long, added after it.</summary>
    private sealed class RespiratorStage(double breathMs, int rate) : IGuardianStage
    {
        public void Push(ReadOnlySpan<double> input, List<double> output) => output.AddRange(input);

        public void Finish(List<double> output) => output.AddRange(Breath(breathMs, rate));
    }
}

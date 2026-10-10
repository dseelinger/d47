namespace D47.Core.Audio;

public static partial class GuardianVoice
{
    /// <summary>Samples from an absolute index on; earlier ones can be dropped, and a read before the first held is a caller error.</summary>
    private sealed class Backlog
    {
        private readonly List<double> _items = [];

        public int Start { get; private set; }

        public int End => Start + _items.Count;

        public double this[int index]
        {
            get => _items[index - Start];
            set => _items[index - Start] = value;
        }

        public void Add(double sample) => _items.Add(sample);

        public void AddRange(ReadOnlySpan<double> samples) => _items.AddRange(samples);

        public void Extend(int end)
        {
            while (End < end)
            {
                _items.Add(0);
            }
        }

        public void DropBefore(int index)
        {
            var count = Math.Clamp(index - Start, 0, _items.Count);

            if (count > 0)
            {
                _items.RemoveRange(0, count);
                Start += count;
            }
        }
    }

    /// <summary>
    /// Hann frames of <see cref="FrameLength"/> read every <c>analysisHop</c> samples and overlap-added every
    /// <c>synthesisHop</c>. A frame runs once the samples it reads have arrived; the rest run over zeros on finish.
    /// </summary>
    private abstract class FramedStage : IGuardianStage
    {
        protected readonly int Frame;
        protected readonly int AnalysisHop;
        protected readonly int SynthesisHop;
        protected readonly double[] Window;
        protected readonly double[] Re;
        protected readonly double[] Im;
        protected readonly Backlog Input = new();
        protected readonly Backlog Accumulated = new();

        protected FramedStage(int rate, int analysisHop, int synthesisHop)
        {
            Frame = FrameLength(rate);
            AnalysisHop = analysisHop;
            SynthesisHop = synthesisHop;
            Window = Hann(Frame);
            Re = new double[Frame];
            Im = new double[Frame];
        }

        /// <summary>Samples pushed so far.</summary>
        protected int Count { get; private set; }

        /// <summary>The next frame to run.</summary>
        protected int NextFrame { get; private set; }

        /// <summary>Accumulated positions below this are complete until the stage finishes.</summary>
        protected int Complete => NextFrame * SynthesisHop;

        public void Push(ReadOnlySpan<double> input, List<double> output)
        {
            Input.AddRange(input);
            Count += input.Length;
            Run(finishing: false);
            Emit(output, finishing: false);
        }

        public void Finish(List<double> output)
        {
            Run(finishing: true);
            Emit(output, finishing: true);
        }

        /// <summary>Turns the windowed frame <paramref name="k"/> in <c>Re</c> into the time-domain frame, in <c>Re</c>.</summary>
        protected abstract void Process(int k);

        protected abstract void Emit(List<double> output, bool finishing);

        private void Run(bool finishing)
        {
            var limit = finishing ? Count + Frame : Count;

            while ((long)NextFrame * AnalysisHop <= limit)
            {
                var k = NextFrame;
                var start = (k * AnalysisHop) - Frame;

                for (var index = 0; index < Frame; index++)
                {
                    var source = start + index;
                    Re[index] = (source >= 0 && source < Count ? Input[source] : 0) * Window[index];
                    Im[index] = 0;
                }

                Process(k);

                var at = k * SynthesisHop;
                Accumulated.Extend(at + Frame);

                for (var index = 0; index < Frame; index++)
                {
                    Accumulated[at + index] += Re[index] * Window[index] / OverlapGain;
                }

                Input.DropBefore(((k + 1) * AnalysisHop) - Frame);
                NextFrame++;
            }
        }
    }

    /// <summary>The speech's smoothed spectral envelope imposed on a flattened sawtooth-plus-noise carrier.</summary>
    private sealed class CylonStage : FramedStage
    {
        private readonly int _bins;
        private readonly int _halfWidth;
        private readonly double _step;
        private readonly double[] _carrierRe;
        private readonly double[] _carrierIm;
        private readonly double[] _speechMagnitude;
        private readonly double[] _carrierMagnitude;
        private readonly double[] _speechEnvelope;
        private readonly double[] _carrierEnvelope;
        private readonly Backlog _carrier = new();
        private Noise _noise = new(0x6A09E667u);
        private double _phase;
        private int _next;

        public CylonStage(double carrierHz, int rate)
            : base(rate, FrameLength(rate) / 4, FrameLength(rate) / 4)
        {
            _bins = Frame / 2;
            _halfWidth = Math.Max(1, (int)Math.Round(EnvelopeHalfWidthHz / ((double)rate / Frame)));
            _step = Math.Clamp(carrierHz, 1, rate * 0.45) / rate;
            _carrierRe = new double[Frame];
            _carrierIm = new double[Frame];
            _speechMagnitude = new double[_bins + 1];
            _carrierMagnitude = new double[_bins + 1];
            _speechEnvelope = new double[_bins + 1];
            _carrierEnvelope = new double[_bins + 1];
            _next = Frame;
        }

        protected override void Process(int k)
        {
            var start = k * AnalysisHop;

            while (_carrier.End < start + Frame)
            {
                _carrier.Add((2 * _phase) - 1 - PolyBlep(_phase, _step) + (CarrierNoise * _noise.Next(-1, 1)));
                _phase += _step;

                if (_phase >= 1)
                {
                    _phase -= 1;
                }
            }

            for (var index = 0; index < Frame; index++)
            {
                _carrierRe[index] = _carrier[start + index] * Window[index];
                _carrierIm[index] = 0;
            }

            _carrier.DropBefore(start + AnalysisHop);

            Fourier(Re, Im, inverse: false);
            Fourier(_carrierRe, _carrierIm, inverse: false);

            for (var bin = 0; bin <= _bins; bin++)
            {
                _speechMagnitude[bin] = Math.Sqrt((Re[bin] * Re[bin]) + (Im[bin] * Im[bin]));
                _carrierMagnitude[bin] = Math.Sqrt((_carrierRe[bin] * _carrierRe[bin]) + (_carrierIm[bin] * _carrierIm[bin]));
            }

            Smooth(_speechMagnitude, _speechEnvelope, _halfWidth);
            Smooth(_carrierMagnitude, _carrierEnvelope, _halfWidth);

            for (var bin = 0; bin <= _bins; bin++)
            {
                var gain = _carrierEnvelope[bin] > 1e-12 ? _speechEnvelope[bin] / _carrierEnvelope[bin] : 0;
                _carrierRe[bin] *= gain;
                _carrierIm[bin] *= gain;
            }

            Mirror(_carrierRe, _carrierIm);
            Fourier(_carrierRe, _carrierIm, inverse: true);
            Array.Copy(_carrierRe, Re, Frame);
        }

        protected override void Emit(List<double> output, bool finishing) =>
            EmitPadded(Accumulated, Frame, Count, Complete, ref _next, output, finishing);
    }

    /// <summary>Each frame's magnitudes kept and its phases drawn from a fixed seed.</summary>
    private sealed class WhisperStage : FramedStage
    {
        private readonly int _bins;
        private Noise _random = new(WhisperSeed);
        private int _next;

        public WhisperStage(int rate)
            : base(rate, FrameLength(rate) / 4, FrameLength(rate) / 4)
        {
            _bins = Frame / 2;
            _next = Frame;
        }

        protected override void Process(int k)
        {
            Fourier(Re, Im, inverse: false);

            for (var bin = 0; bin <= _bins; bin++)
            {
                var magnitude = Math.Sqrt((Re[bin] * Re[bin]) + (Im[bin] * Im[bin]));
                var phase = _random.Next(-Math.PI, Math.PI);
                Re[bin] = magnitude * Math.Cos(phase);
                Im[bin] = magnitude * Math.Sin(phase);
            }

            Mirror(Re, Im);
            Fourier(Re, Im, inverse: true);
        }

        protected override void Emit(List<double> output, bool finishing) =>
            EmitPadded(Accumulated, Frame, Count, Complete, ref _next, output, finishing);
    }

    /// <summary>Emits the accumulated positions from <paramref name="next"/> that are complete, skipping the leading <paramref name="frame"/> padding.</summary>
    private static void EmitPadded(
        Backlog accumulated,
        int frame,
        int count,
        int complete,
        ref int next,
        List<double> output,
        bool finishing)
    {
        var end = finishing ? frame + count : Math.Min(complete, frame + count);

        for (; next < end; next++)
        {
            output.Add(accumulated[next]);
        }

        accumulated.DropBefore(Math.Min(next, complete));
    }

    /// <summary>
    /// A pitch shift by the factor that keeps the duration: a phase vocoder with identity phase locking
    /// compresses time by the factor, and resampling restores the length.
    /// </summary>
    private sealed class ShiftStage : FramedStage
    {
        private readonly int _bins;
        private readonly double _ratio;
        private readonly double[] _magnitude;
        private readonly double[] _phase;
        private readonly double[] _previous;
        private readonly double[] _synthesis;
        private readonly List<int> _peaks = [];
        private int _emitted;

        public ShiftStage(double factor, int rate)
            : this(factor, rate, FrameLength(rate) / 4)
        {
        }

        private ShiftStage(double factor, int rate, int synthesisHop)
            : base(rate, Math.Max(1, (int)Math.Round(synthesisHop / factor)), synthesisHop)
        {
            _bins = Frame / 2;
            _ratio = (double)synthesisHop / AnalysisHop;
            _magnitude = new double[_bins + 1];
            _phase = new double[_bins + 1];
            _previous = new double[_bins + 1];
            _synthesis = new double[_bins + 1];
        }

        protected override void Process(int k)
        {
            Fourier(Re, Im, inverse: false);

            for (var bin = 0; bin <= _bins; bin++)
            {
                _magnitude[bin] = Math.Sqrt((Re[bin] * Re[bin]) + (Im[bin] * Im[bin]));
                _phase[bin] = Math.Atan2(Im[bin], Re[bin]);
            }

            if (k == 0)
            {
                Array.Copy(_phase, _synthesis, _bins + 1);
            }
            else
            {
                Lock(_magnitude, _phase, _previous, _synthesis, _peaks, Frame, AnalysisHop, SynthesisHop);
            }

            Array.Copy(_phase, _previous, _bins + 1);

            for (var bin = 0; bin <= _bins; bin++)
            {
                Re[bin] = _magnitude[bin] * Math.Cos(_synthesis[bin]);
                Im[bin] = _magnitude[bin] * Math.Sin(_synthesis[bin]);
            }

            Mirror(Re, Im);
            Fourier(Re, Im, inverse: true);
        }

        protected override void Emit(List<double> output, bool finishing)
        {
            while (_emitted < Count)
            {
                var position = ((_emitted + (Frame / 2.0)) * _ratio) + (Frame / 2.0);

                if (!finishing && (int)position + 1 >= Complete)
                {
                    break;
                }

                output.Add(Read(position));
                _emitted++;
            }

            var needed = (int)(((_emitted + (Frame / 2.0)) * _ratio) + (Frame / 2.0));
            Accumulated.DropBefore(Math.Min(needed, Complete));
        }

        private double Read(double position)
        {
            var end = Accumulated.End;

            if (position < 0 || position > end - 1)
            {
                return 0;
            }

            var below = (int)position;

            if (below >= end - 1)
            {
                return Accumulated[end - 1];
            }

            var fraction = position - below;

            return (Accumulated[below] * (1 - fraction)) + (Accumulated[below + 1] * fraction);
        }
    }

    /// <summary>The dry line weighted by <c>dryGain</c> plus the wet stage's output weighted by <c>wetGain</c>, the dry held to line up.</summary>
    private sealed class DryWetStage(IGuardianStage wet, double dryGain, double wetGain) : IGuardianStage
    {
        private readonly Backlog _dry = new();
        private int _emitted;

        public void Push(ReadOnlySpan<double> input, List<double> output)
        {
            _dry.AddRange(input);
            var made = new List<double>();
            wet.Push(input, made);
            Blend(made, output);
        }

        public void Finish(List<double> output)
        {
            var made = new List<double>();
            wet.Finish(made);
            Blend(made, output);
        }

        private void Blend(List<double> made, List<double> output)
        {
            foreach (var sample in made)
            {
                output.Add((dryGain * _dry[_emitted]) + (wetGain * sample));
                _emitted++;
            }

            _dry.DropBefore(_emitted);
        }
    }

    /// <summary>The dry voice with a pitch-shifted, delayed copy per <see cref="HiveCopies"/> row under it, each at <c>gain</c>.</summary>
    private sealed class HiveStage : IGuardianStage
    {
        private readonly double _gain;
        private readonly ShiftStage[] _shifts;
        private readonly int[] _delays;
        private readonly Backlog[] _wet;
        private readonly Backlog _dry = new();
        private int _count;
        private int _emitted;

        public HiveStage(double gain, int rate)
        {
            _gain = gain;
            _shifts = [.. HiveCopies.Select(copy => new ShiftStage(Math.Pow(2, copy.Semitones / 12), rate))];
            _delays = [.. HiveCopies.Select(copy => Samples(copy.DelayMs, rate))];
            _wet = [.. HiveCopies.Select(_ => new Backlog())];
        }

        public void Push(ReadOnlySpan<double> input, List<double> output)
        {
            _dry.AddRange(input);
            _count += input.Length;

            for (var copy = 0; copy < _shifts.Length; copy++)
            {
                var made = new List<double>();
                _shifts[copy].Push(input, made);
                _wet[copy].AddRange(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(made));
            }

            Emit(output, finishing: false);
        }

        public void Finish(List<double> output)
        {
            for (var copy = 0; copy < _shifts.Length; copy++)
            {
                var made = new List<double>();
                _shifts[copy].Finish(made);
                _wet[copy].AddRange(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(made));
            }

            Emit(output, finishing: true);
        }

        private void Emit(List<double> output, bool finishing)
        {
            var limit = _count;

            if (!finishing)
            {
                for (var copy = 0; copy < _wet.Length; copy++)
                {
                    limit = Math.Min(limit, _wet[copy].End + _delays[copy]);
                }
            }

            for (; _emitted < limit; _emitted++)
            {
                var sample = _dry[_emitted];

                for (var copy = 0; copy < _wet.Length; copy++)
                {
                    if (_emitted >= _delays[copy])
                    {
                        sample += _gain * _wet[copy][_emitted - _delays[copy]];
                    }
                }

                output.Add(sample);
            }

            _dry.DropBefore(_emitted);

            for (var copy = 0; copy < _wet.Length; copy++)
            {
                _wet[copy].DropBefore(_emitted - _delays[copy]);
            }
        }
    }

    /// <summary>Reverb whose wet signal has a copy an octave up mixed in at <c>layer</c>, with Reverb's faded tail.</summary>
    private sealed class ShimmerStage(double layer, int rate) : IGuardianStage
    {
        private readonly ReverbTank _tank = new(rate);
        private readonly ShiftStage _octave = new(2, rate);
        private readonly int _tail = (int)Math.Round(ReverbTailSeconds * rate);
        private readonly Backlog _dry = new();
        private readonly Backlog _low = new();
        private int _count;
        private int _emitted;

        public void Push(ReadOnlySpan<double> input, List<double> output)
        {
            var low = new List<double>(input.Length);

            foreach (var sample in input)
            {
                low.Add(_tank.Next(sample));
            }

            _dry.AddRange(input);
            _count += input.Length;
            Octave(low, output, finishing: false);
        }

        public void Finish(List<double> output)
        {
            var low = new List<double>(_tail);

            for (var index = 0; index < _tail; index++)
            {
                low.Add(_tank.Next(0));
            }

            Octave(low, output, finishing: true);
        }

        private void Octave(List<double> low, List<double> output, bool finishing)
        {
            foreach (var sample in low)
            {
                _low.Add(sample);
            }

            var octave = new List<double>();
            _octave.Push(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(low), octave);

            if (finishing)
            {
                _octave.Finish(octave);
            }

            var total = _count + _tail;

            foreach (var up in octave)
            {
                var index = _emitted;
                var sample = (ReverbDry * (index < _count ? _dry[index] : 0)) + (ShimmerWet * (_low[index] + (layer * up)));

                if (index >= _count)
                {
                    sample *= (double)(total - 1 - index) / _tail;
                }

                output.Add(sample);
                _emitted++;
            }

            _dry.DropBefore(_emitted);
            _low.DropBefore(_emitted);
        }
    }
}

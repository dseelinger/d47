namespace D47.Core.Audio;

public static partial class GuardianVoice
{
    /// <summary>
    /// Sample-and-hold to <see cref="BitcrusherSampleHz"/>, then a mid-tread quantiser at the given bits either
    /// side of zero, scaled to the reference peak so a quiet clip keeps its levels and silence stays silent.
    /// Holds back until the reference peak's window has arrived.
    /// </summary>
    private sealed class BitcrusherStage(double bits, int rate) : IGuardianStage
    {
        private readonly ReferencePeak _peak = new(ReferencePeak.WindowAt(rate));
        private readonly double _levels = Math.Pow(2, Math.Round(bits) - 1) - 1;
        private readonly int _step = Math.Max(1, (int)Math.Round(rate / BitcrusherSampleHz));
        private readonly List<double> _pending = [];
        private int _emitted;
        private double _held;

        public void Push(ReadOnlySpan<double> input, List<double> output)
        {
            foreach (var sample in input)
            {
                _peak.Add(sample);
                _pending.Add(sample);
            }

            if (_peak.Primed)
            {
                Drain(output);
            }
        }

        public void Finish(List<double> output) => Drain(output);

        private void Drain(List<double> output)
        {
            foreach (var sample in _pending)
            {
                var peak = _peak.At(_emitted + 1);

                if (_emitted % _step == 0)
                {
                    _held = sample;
                }

                if (peak == 0)
                {
                    output.Add(0);
                }
                else
                {
                    var half = _levels / peak;
                    output.Add(Math.Clamp(Math.Round(_held * half) / half, -1, 1));
                }

                _emitted++;
            }

            _pending.Clear();
        }
    }

    /// <summary>Each voiced frame's pitch moved <c>amount</c> of the way to the base pitch, on a log scale.</summary>
    private sealed class MonotoneStage(double amount, double basePitchHz, int rate) : PitchStage(rate)
    {
        protected override double Target(int index, double pitch) =>
            pitch > 0 ? pitch * Math.Pow(basePitchHz / pitch, amount) : 0;
    }

    /// <summary>
    /// Each voiced frame's pitch moved to the nearest equal-tempered semitone, a new semitone taken only once the
    /// current one has been held for the given milliseconds.
    /// </summary>
    private sealed class SteppedPitchStage(double holdMs, int rate) : PitchStage(rate)
    {
        private readonly List<double> _notes = [];
        private readonly int _holdFrames = (int)Math.Ceiling(holdMs / PitchHopMs);
        private int? _current;
        private int _held;

        protected override void OnFrame(double hertz)
        {
            var note = 0.0;

            if (hertz > 0)
            {
                var nearest = (int)Math.Round(12 * Math.Log2(hertz / SemitoneReferenceHz));

                if (_current is null || (nearest != _current && _held >= _holdFrames))
                {
                    _current = nearest;
                    _held = 0;
                }

                note = SemitoneReferenceHz * Math.Pow(2, _current.Value / 12.0);
            }

            _notes.Add(note);
            _held++;
        }

        protected override double Target(int index, double pitch)
        {
            if (pitch <= 0)
            {
                return 0;
            }

            var below = Math.Min(Frames - 1, index / Hop);
            var above = Math.Min(Frames - 1, below + 1);
            var (near, far) = index - (below * Hop) < Hop / 2 ? (below, above) : (above, below);

            return _notes[near] > 0 ? _notes[near] : _notes[far];
        }
    }

    /// <summary>
    /// Pitch tracking by YIN on the clip averaged down to about 12 kHz, then TD-PSOLA: two-period Hann grains cut
    /// one analysis period apart at the tracked pitch and laid one synthesis period apart at
    /// <see cref="Target"/>, keeping the length. The result is blended with the dry signal by how voiced the
    /// neighbouring frames are, so unvoiced stretches pass unchanged. Frames are measured against the reference
    /// peak, so nothing is analysed until its window has arrived; after that each stage step waits only for the
    /// samples it reads.
    /// </summary>
    private abstract class PitchStage : IGuardianStage
    {
        private readonly int _rate;
        private readonly int _factor;
        private readonly double _analysisRate;
        private readonly int _frame;
        private readonly int _minLag;
        private readonly int _maxLag;
        private readonly int _width;
        private readonly int _maxHalf;
        private readonly double[] _normalised;
        private readonly ReferencePeak _peak;
        private readonly List<double> _signal = [];
        private readonly List<double> _decimated = [];
        private readonly List<double> _track = [];
        private readonly List<int> _marks = [];
        private readonly List<double> _sum = [];
        private readonly List<double> _weight = [];
        private double _total;
        private int _totalCount;
        private double _position;
        private bool _marksDone;
        private int _mark;
        private double _time;
        private int _emitted;
        private bool _finishing;

        protected PitchStage(int rate)
        {
            _rate = rate;
            Hop = Samples(PitchHopMs, rate);
            _factor = Math.Max(1, rate / PitchAnalysisHz);
            _analysisRate = (double)rate / _factor;
            _frame = (int)Math.Round(PitchFrameMs / 1000 * _analysisRate);
            _minLag = Math.Max(2, (int)(_analysisRate / PitchHighestHz));
            _maxLag = (int)Math.Ceiling(_analysisRate / PitchLowestHz);
            _width = Math.Max(1, _frame - _maxLag - 1);
            _maxHalf = (int)(rate / (PitchLowestHz * 0.9)) + 2;
            _normalised = new double[_maxLag + 2];
            _peak = new ReferencePeak(ReferencePeak.WindowAt(rate) / _factor);
        }

        /// <summary>Samples between pitch frames.</summary>
        protected int Hop { get; }

        /// <summary>Pitch frames analysed so far.</summary>
        protected int Frames => _track.Count;

        public void Push(ReadOnlySpan<double> input, List<double> output)
        {
            foreach (var sample in input)
            {
                _signal.Add(sample);
                _total += sample;

                if (++_totalCount == _factor)
                {
                    var mean = _total / _factor;

                    _decimated.Add(mean);
                    _peak.Add(mean);
                    _total = 0;
                    _totalCount = 0;
                }
            }

            Advance(output);
        }

        public void Finish(List<double> output)
        {
            _finishing = true;
            Advance(output);
        }

        /// <summary>The pitch to lay grains at where the tracked pitch is <paramref name="pitch"/>, or 0 to keep it.</summary>
        protected abstract double Target(int index, double pitch);

        /// <summary>Called with each frame's pitch, 0 where unvoiced, in order.</summary>
        protected virtual void OnFrame(double hertz)
        {
        }

        private void Advance(List<double> output)
        {
            AnalyseFrames();
            PlaceGrains();
            Emit(output);
        }

        private int FrameStart(int k) => (int)Math.Round(k * PitchHopMs / 1000 * _analysisRate) - (_frame / 2);

        private double Decimated(int index) => index >= 0 && index < _decimated.Count ? _decimated[index] : 0;

        private void AnalyseFrames()
        {
            if (!_finishing && !_peak.Primed)
            {
                return;
            }

            var total = (_signal.Count / Hop) + 1;

            while (_finishing ? _track.Count < total : FrameStart(_track.Count) + _frame <= _decimated.Count)
            {
                var hertz = Analyse(_track.Count);

                _track.Add(hertz);
                OnFrame(hertz);
            }
        }

        private double Analyse(int k)
        {
            var start = FrameStart(k);
            var peak = _peak.At(start + _frame);
            var squared = 0.0;

            for (var index = 0; index < _frame; index++)
            {
                var sample = Decimated(start + index);
                squared += sample * sample;
            }

            if (peak == 0 || Math.Sqrt(squared / _frame) < peak * PitchSilence)
            {
                return 0;
            }

            var running = 0.0;
            _normalised[0] = 1;

            for (var lag = 1; lag <= _maxLag + 1; lag++)
            {
                var difference = 0.0;

                for (var index = 0; index < _width; index++)
                {
                    var delta = Decimated(start + index) - Decimated(start + index + lag);
                    difference += delta * delta;
                }

                running += difference;
                _normalised[lag] = running > 0 ? difference * lag / running : 1;
            }

            var found = 0;

            for (var lag = _minLag; lag <= _maxLag; lag++)
            {
                if (_normalised[lag] < PitchVoicing)
                {
                    while (lag < _maxLag && _normalised[lag + 1] < _normalised[lag])
                    {
                        lag++;
                    }

                    found = lag;
                    break;
                }
            }

            if (found == 0)
            {
                return 0;
            }

            var before = _normalised[found - 1];
            var after = _normalised[found + 1];
            var curve = before - (2 * _normalised[found]) + after;

            return _analysisRate / (found + (curve > 0 ? (before - after) / (2 * curve) : 0));
        }

        /// <summary>Whether the frames around <paramref name="index"/> are known, so its pitch and voicing are final.</summary>
        private bool Known(int index) => _finishing || (index / Hop) + 2 <= _track.Count;

        /// <summary>
        /// The frame track as a pitch at <paramref name="index"/>: linear between two voiced frames, the voiced
        /// one's pitch beside an unvoiced frame, and 0 between two unvoiced ones.
        /// </summary>
        private double PitchAt(int index)
        {
            var k = Math.Min(_track.Count - 1, index / Hop);
            var left = _track[k];
            var right = _track[Math.Min(_track.Count - 1, k + 1)];
            var fraction = (double)(index - (k * Hop)) / Hop;

            return (left > 0, right > 0) switch
            {
                (true, true) => left + ((right - left) * fraction),
                (true, false) => left,
                (false, true) => right,
                _ => 0,
            };
        }

        /// <summary>Generates analysis marks until there are <paramref name="count"/>, or none are left; false if it must wait.</summary>
        private bool EnsureMarks(int count)
        {
            while (_marks.Count < count && !_marksDone)
            {
                if (_position >= _signal.Count)
                {
                    if (!_finishing)
                    {
                        return false;
                    }

                    _marksDone = true;
                    break;
                }

                var at = (int)_position;

                if (!Known(at))
                {
                    return false;
                }

                var pitch = PitchAt(at);

                _marks.Add(at);
                _position += pitch > 0 ? _rate / pitch : Hop;
            }

            return true;
        }

        private void PlaceGrains()
        {
            while (_time < _signal.Count)
            {
                while (true)
                {
                    if (!EnsureMarks(_mark + 2))
                    {
                        return;
                    }

                    if (_mark + 1 < _marks.Count
                        && Math.Abs(_marks[_mark + 1] - _time) <= Math.Abs(_marks[_mark] - _time))
                    {
                        _mark++;
                        continue;
                    }

                    break;
                }

                var at = (int)Math.Round(_time);
                var centre = _marks[_mark];
                var centrePitch = PitchAt(centre);
                var half = Math.Max(1, (int)Math.Round(centrePitch > 0 ? _rate / centrePitch : Hop));
                var place = _finishing ? Math.Min(_signal.Count - 1, at) : at;

                if ((!_finishing && _signal.Count < centre + half) || !Known(place))
                {
                    return;
                }

                while (_sum.Count < at + half)
                {
                    _sum.Add(0);
                    _weight.Add(0);
                }

                for (var offset = -half; offset < half; offset++)
                {
                    var source = centre + offset;
                    var destination = at + offset;

                    if (source >= 0 && source < _signal.Count && destination >= 0)
                    {
                        var window = 0.5 + (0.5 * Math.Cos(Math.PI * offset / half));
                        _sum[destination] += window * _signal[source];
                        _weight[destination] += window;
                    }
                }

                var pitch = PitchAt(place);
                var target = Target(place, pitch);

                _time += target > 0 ? _rate / target : pitch > 0 ? _rate / pitch : Hop;
            }
        }

        private void Emit(List<double> output)
        {
            var limit = _finishing ? _signal.Count : (int)Math.Round(_time) - _maxHalf;

            while (_emitted < limit && Known(_emitted))
            {
                var index = _emitted;
                var k = Math.Min(_track.Count - 1, index / Hop);
                var fraction = (double)(index - (k * Hop)) / Hop;
                var voiced = ((_track[k] > 0 ? 1 : 0) * (1 - fraction))
                    + ((_track[Math.Min(_track.Count - 1, k + 1)] > 0 ? 1 : 0) * fraction);
                var weight = index < _weight.Count ? _weight[index] : 0;
                var moved = weight > 1e-9 ? _sum[index] / weight : 0;

                output.Add((voiced * moved) + ((1 - voiced) * _signal[index]));
                _emitted++;
            }
        }
    }
}

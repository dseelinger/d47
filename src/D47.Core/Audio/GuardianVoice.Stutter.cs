namespace D47.Core.Audio;

public static partial class GuardianVoice
{
    /// <summary>
    /// Repeats each word's start with a seeded chance, and shifts whole phrases with another. Onsets split the clip
    /// into phrases; the schedule comes from a fixed seed, so the same clip stutters the same way every time.
    /// Onsets are measured against the reference peak, so nothing is classified until its window has arrived.
    /// A shifted phrase runs through its own <see cref="ShiftStage"/>. Lengthens the clip by the repeats it adds.
    /// </summary>
    private sealed class StutterStage : IGuardianStage
    {
        private readonly double _chance;
        private readonly int _rate;
        private readonly int _window;
        private readonly int _frame;
        private readonly ReferencePeak _peak;
        private readonly Joiner _joiner;
        private readonly Backlog _arrived = new();
        private readonly List<double> _scratch = [];
        private Noise _random;
        private int _classified;
        private double _quietMs = double.PositiveInfinity;
        private bool _above;
        private bool _inPhrase;
        private Phrase _phrase = new();

        public StutterStage(double chance, int rate)
        {
            _chance = chance;
            _rate = rate;
            _window = Samples(StutterOnsetWindowMs, rate);
            _frame = FrameLength(rate);
            _peak = new ReferencePeak(ReferencePeak.WindowAt(rate));
            _joiner = new Joiner(Samples(StutterCrossfadeMs, rate));
            _random = new Noise(StutterSeed ^ (uint)Math.Round(chance * 1_000_000));
        }

        public void Push(ReadOnlySpan<double> input, List<double> output)
        {
            foreach (var sample in input)
            {
                _peak.Add(sample);
            }

            _arrived.AddRange(input);

            if (_peak.Primed)
            {
                Classify(finishing: false, output);
            }
        }

        public void Finish(List<double> output)
        {
            Classify(finishing: true, output);

            if (_inPhrase)
            {
                EndPhrase(output);
            }

            _joiner.Finish(output);
        }

        private void Classify(bool finishing, List<double> output)
        {
            while (_arrived.End - _classified >= _window || (finishing && _arrived.End > _classified))
            {
                var length = Math.Min(_window, _arrived.End - _classified);
                var samples = new double[length];

                for (var index = 0; index < length; index++)
                {
                    samples[index] = _arrived[_classified + index];
                }

                _classified += length;
                _arrived.DropBefore(_classified);

                var loud = Rms(samples) >= _peak.At(_classified) * StutterOnsetThreshold;

                if (loud)
                {
                    if (!_above && _quietMs >= StutterOnsetMinQuietMs)
                    {
                        StartPhrase(output);
                    }

                    _above = true;
                    _quietMs = 0;
                }
                else
                {
                    _above = false;
                    _quietMs += 1_000.0 * length / _rate;
                }

                Feed(samples, output);
            }
        }

        private void StartPhrase(List<double> output)
        {
            if (_inPhrase)
            {
                EndPhrase(output);
            }
            else
            {
                _joiner.End(output);
            }

            _inPhrase = true;
            _phrase = new Phrase();
        }

        private void Feed(double[] samples, List<double> output)
        {
            if (!_inPhrase)
            {
                foreach (var sample in samples)
                {
                    _joiner.Add(sample, output);
                }

                return;
            }

            if (_phrase.Decided)
            {
                Route(samples, output);
                return;
            }

            _phrase.Undecided.AddRange(samples);

            if (_phrase.Undecided.Count >= _frame)
            {
                Decide(longEnough: true, output);
            }
        }

        private void EndPhrase(List<double> output)
        {
            if (!_phrase.Decided)
            {
                Decide(longEnough: false, output);
            }

            if (_phrase.Shift is not null)
            {
                _scratch.Clear();
                _phrase.Shift.Finish(_scratch);
                Emit(_scratch, output);
            }

            if (_phrase.Burst is not null && !_phrase.BurstDone)
            {
                PlayBurst(output);
            }

            _joiner.End(output);
        }

        private void Decide(bool longEnough, List<double> output)
        {
            _joiner.Begin(output);
            _phrase.Decided = true;

            if (longEnough && _random.Next(0, 1) < StutterShiftChance)
            {
                var semitones = _random.Next(StutterShiftMinSemitones, StutterShiftMaxSemitones);

                if (_random.Next(0, 1) < 0.5)
                {
                    semitones = -semitones;
                }

                _phrase.Shift = new ShiftStage(Math.Pow(2, semitones / 12), _rate);
            }

            if (_random.Next(0, 1) < _chance)
            {
                var burstMs = _random.Next(StutterRepeatMinMs, StutterRepeatMaxMs);

                _phrase.BurstLength = (int)Math.Round(burstMs / 1000 * _rate);
                _phrase.Repeats = _random.Next(0, 1) < 0.5 ? 2 : 3;
                _phrase.Burst = [];
            }

            var held = _phrase.Undecided.ToArray();

            _phrase.Undecided.Clear();
            Route(held, output);
        }

        private void Route(double[] samples, List<double> output)
        {
            if (_phrase.Shift is null)
            {
                Emit(samples, output);
                return;
            }

            _scratch.Clear();
            _phrase.Shift.Push(samples, _scratch);
            Emit(_scratch, output);
        }

        private void Emit(IReadOnlyList<double> samples, List<double> output)
        {
            foreach (var sample in samples)
            {
                if (_phrase.Burst is not null && !_phrase.BurstDone)
                {
                    _phrase.Burst.Add(sample);

                    if (_phrase.Burst.Count == _phrase.BurstLength)
                    {
                        PlayBurst(output);
                    }
                }
                else
                {
                    _joiner.Add(sample, output);
                }
            }
        }

        private void PlayBurst(List<double> output)
        {
            var burst = _phrase.Burst!;

            _phrase.BurstDone = true;

            for (var copy = 0; copy < _phrase.Repeats; copy++)
            {
                _joiner.Begin(output);

                foreach (var sample in burst)
                {
                    _joiner.Add(sample, output);
                }
            }

            _joiner.Begin(output);
        }

        /// <summary>The state of one phrase between its onset and the next.</summary>
        private sealed class Phrase
        {
            public List<double> Undecided { get; } = [];

            public bool Decided { get; set; }

            public ShiftStage? Shift { get; set; }

            public List<double>? Burst { get; set; }

            public int BurstLength { get; set; }

            public int Repeats { get; set; }

            public bool BurstDone { get; set; }
        }
    }

    /// <summary>
    /// Concatenates segments, each join a linear crossfade of up to <c>crossfade</c> samples, holding back the
    /// last <c>crossfade</c> samples of the output so far. A segment's first samples wait until the crossfade
    /// length has arrived or the segment ends.
    /// </summary>
    private sealed class Joiner(int crossfade)
    {
        private readonly List<double> _tail = [];
        private readonly List<double> _head = [];
        private int _emitted;
        private bool _blending;

        public void Begin(List<double> output)
        {
            End(output);
            _head.Clear();
            _blending = true;
        }

        public void End(List<double> output)
        {
            if (_blending)
            {
                Blend(output);
            }
        }

        public void Add(double sample, List<double> output)
        {
            if (!_blending)
            {
                Append(sample, output);
                return;
            }

            _head.Add(sample);

            if (_head.Count == crossfade)
            {
                Blend(output);
            }
        }

        public void Finish(List<double> output)
        {
            End(output);
            output.AddRange(_tail);
            _tail.Clear();
        }

        private void Blend(List<double> output)
        {
            _blending = false;

            var fade = Math.Min(crossfade, Math.Min(_emitted + _tail.Count, _head.Count));
            var from = _tail.Count - fade;

            for (var index = 0; index < fade; index++)
            {
                var t = (index + 1.0) / (fade + 1);
                _tail[from + index] = (_tail[from + index] * (1 - t)) + (_head[index] * t);
            }

            for (var index = fade; index < _head.Count; index++)
            {
                Append(_head[index], output);
            }

            _head.Clear();
        }

        private void Append(double sample, List<double> output)
        {
            _tail.Add(sample);

            if (_tail.Count > crossfade)
            {
                output.Add(_tail[0]);
                _tail.RemoveAt(0);
                _emitted++;
            }
        }
    }
}

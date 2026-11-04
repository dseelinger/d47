namespace D47.Core.Audio;

public static partial class GuardianVoice
{
    /// <summary>
    /// Each word that follows a pause fades in from silence on a raised-cosine curve over <c>attackMs</c> and then
    /// holds at full gain until the next onset. Onsets are found as <see cref="StutterStage"/> finds them, so nothing is
    /// classified until the reference peak's window has arrived. The clip's length does not change.
    /// </summary>
    private sealed class VolumeSwellStage : IGuardianStage
    {
        private readonly int _rate;
        private readonly int _window;
        private readonly int _attack;
        private readonly ReferencePeak _peak;
        private readonly Backlog _arrived = new();
        private int _classified;
        private int _onsetAt = -1;
        private double _quietMs = double.PositiveInfinity;
        private bool _above;

        public VolumeSwellStage(double attackMs, int rate)
        {
            _rate = rate;
            _window = Samples(StutterOnsetWindowMs, rate);
            _attack = Math.Max(1, Samples(attackMs, rate));
            _peak = new ReferencePeak(ReferencePeak.WindowAt(rate));
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

        public void Finish(List<double> output) => Classify(finishing: true, output);

        private void Classify(bool finishing, List<double> output)
        {
            while (_arrived.End - _classified >= _window || (finishing && _arrived.End > _classified))
            {
                var start = _classified;
                var length = Math.Min(_window, _arrived.End - start);
                var samples = new double[length];

                for (var index = 0; index < length; index++)
                {
                    samples[index] = _arrived[start + index];
                }

                _classified += length;
                _arrived.DropBefore(_classified);

                var rms = Rms(samples);

                if (rms > 0 && rms >= _peak.At(_classified) * StutterOnsetThreshold)
                {
                    if (!_above && _quietMs >= StutterOnsetMinQuietMs)
                    {
                        _onsetAt = start;
                    }

                    _above = true;
                    _quietMs = 0;
                }
                else
                {
                    _above = false;
                    _quietMs += 1_000.0 * length / _rate;
                }

                for (var index = 0; index < length; index++)
                {
                    output.Add(samples[index] * GainAt(start + index));
                }
            }
        }

        private double GainAt(int index)
        {
            if (_onsetAt < 0 || index - _onsetAt >= _attack)
            {
                return 1;
            }

            return 0.5 * (1 - Math.Cos(Math.PI * (index - _onsetAt) / _attack));
        }
    }
}

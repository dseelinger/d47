namespace D47.Core.Audio;

public static partial class GuardianVoice
{
    /// <summary>
    /// Repeating echoes: the line plus <c>mix</c> times a <see cref="DelayMs"/> feedback delay, each repeat
    /// low-passed at <see cref="ReverbLowPassHz"/> and scaled by <see cref="DelayFeedback"/>. Appends
    /// <see cref="DelayTailSeconds"/> after the line, faded linearly to zero at its end.
    /// </summary>
    private sealed class DelayStage : IGuardianStage
    {
        private readonly double _mix;
        private readonly int _tail;
        private readonly double[] _line;
        private readonly double _smoothing;
        private double _low;
        private int _position;
        private int _count;

        public DelayStage(double mix, int rate)
        {
            _mix = mix;
            _tail = (int)Math.Round(DelayTailSeconds * rate);
            _line = new double[Math.Max(1, Samples(DelayMs, rate))];
            _smoothing = Math.Exp(-2 * Math.PI * ReverbLowPassHz / rate);
        }

        public void Push(ReadOnlySpan<double> input, List<double> output)
        {
            foreach (var sample in input)
            {
                output.Add(Next(sample));
            }

            _count += input.Length;
        }

        public void Finish(List<double> output)
        {
            if (_count == 0)
            {
                return;
            }

            for (var index = 0; index < _tail; index++)
            {
                output.Add(Next(0) * ((double)(_tail - 1 - index) / _tail));
            }
        }

        private double Next(double sample)
        {
            _low = ((1 - _smoothing) * _line[_position]) + (_smoothing * _low);

            var echo = _low;

            _line[_position] = sample + (DelayFeedback * echo);
            _position = (_position + 1) % _line.Length;
            return sample + (_mix * echo);
        }
    }
}

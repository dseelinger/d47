namespace D47.Core.Audio;

public static partial class GuardianVoice
{
    private const int ReverseReverbBlock = 2_048;

    /// <summary>
    /// The reverb run backwards, as a causal convolution: each sample is preceded by the reverb's tail reversed,
    /// cut to <see cref="ReverseTailSeconds"/> and faded linearly to zero at its far end. FFT blocks start at fixed
    /// positions from the clip's start. The output is the dry length plus the tail; the first sample is 0.
    /// </summary>
    private sealed class ReverseReverbStage : IGuardianStage
    {
        private readonly double _mix;
        private readonly int _tail;
        private readonly int _size;
        private readonly double[] _kernelRe;
        private readonly double[] _kernelIm;
        private readonly double[] _re;
        private readonly double[] _im;
        private readonly double[] _accumulated;
        private readonly double[] _block = new double[ReverseReverbBlock];
        private readonly Backlog _dry = new();
        private int _filled;
        private int _position;
        private int _pushed;

        public ReverseReverbStage(double mix, int rate)
        {
            _mix = mix;
            _tail = (int)Math.Round(ReverseTailSeconds * rate);

            var length = _tail + 1;
            _size = 1;

            while (_size < ReverseReverbBlock + length - 1)
            {
                _size <<= 1;
            }

            var response = ReverbWet([1], _tail, rate);

            // Output i reads input i - d with weight response[tail - d] * d / tail, for d = 1..tail.
            _kernelRe = new double[_size];
            _kernelIm = new double[_size];

            for (var distance = 1; distance <= _tail; distance++)
            {
                _kernelRe[distance] = response[_tail - distance] * distance / _tail;
            }

            Fourier(_kernelRe, _kernelIm, inverse: false);
            _re = new double[_size];
            _im = new double[_size];
            _accumulated = new double[_size];
        }

        public int Delay => _tail;

        public void Push(ReadOnlySpan<double> input, List<double> output)
        {
            foreach (var sample in input)
            {
                _block[_filled++] = sample;
                _dry.Add(sample);
                _pushed++;

                if (_filled == ReverseReverbBlock)
                {
                    Convolve();
                    Emit(ReverseReverbBlock, output);
                    Advance();
                    _filled = 0;
                }
            }
        }

        public void Finish(List<double> output)
        {
            var total = _pushed + _tail;

            if (_filled > 0)
            {
                Array.Clear(_block, _filled, ReverseReverbBlock - _filled);
                Convolve();
                _filled = 0;
            }
            else if (_pushed == 0)
            {
                return;
            }

            while (_position < total)
            {
                Emit(Math.Min(ReverseReverbBlock, total - _position), output);
                Advance();
            }
        }

        private void Convolve()
        {
            Array.Copy(_block, _re, ReverseReverbBlock);
            Array.Clear(_re, ReverseReverbBlock, _size - ReverseReverbBlock);
            Array.Clear(_im);
            Fourier(_re, _im, inverse: false);

            for (var bin = 0; bin < _size; bin++)
            {
                var re = (_re[bin] * _kernelRe[bin]) - (_im[bin] * _kernelIm[bin]);

                _im[bin] = (_re[bin] * _kernelIm[bin]) + (_im[bin] * _kernelRe[bin]);
                _re[bin] = re;
            }

            Fourier(_re, _im, inverse: true);

            for (var index = 0; index < _size; index++)
            {
                _accumulated[index] += _re[index];
            }
        }

        private void Emit(int count, List<double> output)
        {
            for (var offset = 0; offset < count; offset++)
            {
                var index = _position + offset;
                var dry = index >= _tail ? _dry[index - _tail] : 0;

                output.Add((ReverbDry * dry) + (_mix * _accumulated[offset]));
            }
        }

        private void Advance()
        {
            Array.Copy(_accumulated, ReverseReverbBlock, _accumulated, 0, _size - ReverseReverbBlock);
            Array.Clear(_accumulated, _size - ReverseReverbBlock, ReverseReverbBlock);
            _position += ReverseReverbBlock;
            _dry.DropBefore(_position - _tail);
        }
    }
}

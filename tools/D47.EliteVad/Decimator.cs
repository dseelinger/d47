namespace D47.EliteVad;

/// <summary>Turns 48 kHz stereo 16-bit frames into 16 kHz mono floats through a windowed-sinc low-pass and 3:1 decimation.</summary>
internal sealed class Decimator
{
    private const int Factor = 3;
    private const int Taps = 95;
    private const double CutoffHz = 7200;

    private readonly float[] _kernel = new float[Taps];
    private readonly float[] _history = new float[Taps];
    private int _head;
    private int _phase;

    public Decimator()
    {
        var fc = CutoffHz / ProcessLoopback.SampleRate;
        var sum = 0.0;
        for (var i = 0; i < Taps; i++)
        {
            var m = i - (Taps - 1) / 2.0;
            var sinc = m == 0 ? 2 * fc : Math.Sin(2 * Math.PI * fc * m) / (Math.PI * m);
            var window = 0.42 - 0.5 * Math.Cos(2 * Math.PI * i / (Taps - 1)) + 0.08 * Math.Cos(4 * Math.PI * i / (Taps - 1));
            _kernel[i] = (float)(sinc * window);
            sum += _kernel[i];
        }

        for (var i = 0; i < Taps; i++)
        {
            _kernel[i] = (float)(_kernel[i] / sum);
        }
    }

    /// <summary>Appends each 16 kHz sample produced from <paramref name="frames"/> to <paramref name="output"/>.</summary>
    public void Process(ReadOnlySpan<byte> frames, List<float> output)
    {
        for (var offset = 0; offset + ProcessLoopback.BytesPerFrame <= frames.Length; offset += ProcessLoopback.BytesPerFrame)
        {
            var left = (short)(frames[offset] | frames[offset + 1] << 8);
            var right = (short)(frames[offset + 2] | frames[offset + 3] << 8);
            _history[_head] = (left + right) / 65536f;
            _head = (_head + 1) % Taps;

            if (++_phase < Factor)
            {
                continue;
            }

            _phase = 0;
            var acc = 0f;
            var index = _head;
            for (var i = 0; i < Taps; i++)
            {
                acc += _history[index] * _kernel[i];
                index = index + 1 == Taps ? 0 : index + 1;
            }

            output.Add(acc);
        }
    }
}

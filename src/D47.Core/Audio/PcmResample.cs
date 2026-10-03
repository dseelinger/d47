namespace D47.Core.Audio;

/// <summary>Band-limited resampling of mono float samples, by a Blackman-windowed sinc.</summary>
public static class PcmResample
{
    /// <summary>Zero crossings of the sinc on each side of the sample being computed.</summary>
    private const int HalfTaps = 16;

    public static float[] To(ReadOnlySpan<float> input, int fromRate, int toRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fromRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(toRate);

        if (fromRate == toRate)
        {
            return input.ToArray();
        }

        var step = (double)fromRate / toRate;

        // Below the lower Nyquist, with a little room for the window's transition band.
        var cutoff = Math.Min(1.0, (double)toRate / fromRate) * 0.95;
        var reach = HalfTaps / cutoff;
        var output = new float[(int)((long)input.Length * toRate / fromRate)];

        for (var i = 0; i < output.Length; i++)
        {
            var centre = i * step;
            var first = Math.Max(0, (int)Math.Ceiling(centre - reach));
            var last = Math.Min(input.Length - 1, (int)Math.Floor(centre + reach));
            var sum = 0.0;

            for (var j = first; j <= last; j++)
            {
                var x = j - centre;
                sum += input[j] * cutoff * Sinc(cutoff * x) * Blackman(x / reach);
            }

            output[i] = (float)sum;
        }

        return output;
    }

    private static double Sinc(double x) => x == 0 ? 1 : Math.Sin(Math.PI * x) / (Math.PI * x);

    /// <summary>The window over <paramref name="t"/> in [-1, 1].</summary>
    private static double Blackman(double t) =>
        0.42 + (0.5 * Math.Cos(Math.PI * t)) + (0.08 * Math.Cos(2 * Math.PI * t));
}

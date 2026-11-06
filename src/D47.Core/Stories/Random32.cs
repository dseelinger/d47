namespace D47.Core.Stories;

/// <summary>A generator whose sequence is fixed by its seed.</summary>
internal sealed class Random32(uint seed)
{
    private uint state = seed;

    public int Next(int bound) => (int)(NextUInt() % (uint)bound);

    public double NextDouble() => NextUInt() / 4294967296.0;

    /// <summary>An index drawn in proportion to <paramref name="values"/>, or uniformly when they sum to zero or less.</summary>
    public int Weighted(IReadOnlyList<double> values)
    {
        var total = values.Sum();

        if (total <= 0)
        {
            return Next(values.Count);
        }

        var point = NextDouble() * total;

        for (var index = 0; index < values.Count; index++)
        {
            point -= values[index];

            if (point < 0)
            {
                return index;
            }
        }

        return values.Count - 1;
    }

    private uint NextUInt()
    {
        state += 0x9E3779B9u;

        var value = state;

        value = (value ^ (value >> 16)) * 0x85EBCA6Bu;
        value = (value ^ (value >> 13)) * 0xC2B2AE35u;

        return value ^ (value >> 16);
    }
}

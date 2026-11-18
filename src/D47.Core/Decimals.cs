using System.Globalization;

namespace D47.Core;

/// <summary>Converts a double to decimal at 15 significant digits, so 0.05 becomes 0.05m and not 0.0500000000000000027m.</summary>
public static class Decimals
{
    public static decimal FromDouble(double value) =>
        decimal.Parse(
            value.ToString("G15", CultureInfo.InvariantCulture),
            NumberStyles.Float,
            CultureInfo.InvariantCulture);
}

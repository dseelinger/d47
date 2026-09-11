using System.Globalization;
using System.Text;

namespace D47.Core.Speech;

/// <summary>A number inside a name written out as words, for the provider only (#91).</summary>
public static class SpokenDesignations
{
    /// <summary>The decimal point, which marks a measured quantity rather than a designation.</summary>
    private const char Point = '.';

    /// <summary>The grouping comma, which marks a measured quantity rather than a designation.</summary>
    private const char Grouping = ',';

    /// <summary>
    /// One line with every designation number in it written out. Run this before
    /// <see cref="SpokenUnits.Rewrite"/>, which needs the abbreviation still beside its digits.
    /// </summary>
    public static string Rewrite(string? line)
    {
        if (line is not { Length: > 0 } || !line.Any(char.IsAsciiDigit))
        {
            return line ?? string.Empty;
        }

        var built = new StringBuilder(line.Length + 16);
        var i = 0;

        while (i < line.Length)
        {
            if (!char.IsAsciiDigit(line[i]))
            {
                built.Append(line[i]);
                i++;
                continue;
            }

            var start = i;

            // The whole numeral, grouping commas and a decimal part included, so a measured quantity is
            // recognised as one rather than as the runs of digits inside it.
            while (i < line.Length
                   && (char.IsAsciiDigit(line[i])
                       || (line[i] is Point or Grouping
                           && i + 1 < line.Length
                           && char.IsAsciiDigit(line[i + 1]))))
            {
                i++;
            }

            var numeral = line[start..i];

            if (!IsDesignation(numeral) || UnitFollows(line, i))
            {
                built.Append(numeral);
                continue;
            }

            // A space on either side where the digits ran into letters: "c1-12" is "c one-twelve", and
            // without the space it is "cone-twelve".
            if (start > 0 && char.IsLetter(line[start - 1]))
            {
                built.Append(' ');
            }

            built.Append(Say(numeral));

            if (i < line.Length && char.IsLetter(line[i]))
            {
                built.Append(' ');
            }
        }

        return built.ToString();
    }

    /// <summary>
    /// Whether this numeral is a designation: a number in shape, with neither a decimal point nor a
    /// grouping comma to say it is a measured quantity.
    /// </summary>
    private static bool IsDesignation(string numeral) =>
        SpokenNumber.Looks(numeral)
        && !numeral.Contains(Point, StringComparison.Ordinal)
        && !numeral.Contains(Grouping, StringComparison.Ordinal);

    /// <summary>
    /// Whether a unit abbreviation follows the numeral, in which case <see cref="SpokenUnits"/> owns the
    /// number and needs its digits.
    /// </summary>
    private static bool UnitFollows(string line, int after)
    {
        var i = after;

        while (i < line.Length && line[i] is ' ' or '\t')
        {
            i++;
        }

        // A gap is required, because SpokenUnits requires one.
        if (i == after)
        {
            return false;
        }

        var start = i;

        while (i < line.Length && char.IsLetter(line[i]))
        {
            i++;
        }

        return i > start
               && (i == line.Length || !char.IsAsciiDigit(line[i]))
               && SpokenUnits.Knows(line[start..i]);
    }

    /// <summary>
    /// Digit by digit past three digits, and where a leading zero is part of the name; the casual reading
    /// below that. <c>3269</c> is three two six nine and <c>385</c> is three eighty-five.
    /// </summary>
    private static string Say(string digits) =>
        digits.Length > 3 || (digits.Length > 1 && digits[0] == '0')
            ? SpokenNumber.Digits(digits)
            : SpokenNumber.UpTo999(int.Parse(digits, CultureInfo.InvariantCulture));
}

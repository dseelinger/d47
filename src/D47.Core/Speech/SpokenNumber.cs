namespace D47.Core.Speech;

/// <summary>A run of digits as a person would say it inside a name.</summary>
public static class SpokenNumber
{
    private static readonly string[] Ones =
    [
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
        "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen",
        "seventeen", "eighteen", "nineteen",
    ];

    private static readonly string[] Tens =
    [
        "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety",
    ];

    /// <summary>What each number word sounds like.</summary>
    public static readonly IReadOnlyDictionary<string, string> Sounds =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["zero"] = "zˈiəɹoʊ", ["one"] = "wˌʌn", ["two"] = "tˈuː", ["three"] = "θɹˈiː",
            ["four"] = "fˈoːɹ", ["five"] = "fˈaɪv", ["six"] = "sˈɪks", ["seven"] = "sˈɛvən",
            ["eight"] = "ˈeɪt", ["nine"] = "nˈaɪn", ["ten"] = "tˈɛn", ["eleven"] = "ᵻlˈɛvən",
            ["twelve"] = "twˈɛlv", ["thirteen"] = "θˈɜːtiːn", ["fourteen"] = "fˈoːɹtiːn",
            ["fifteen"] = "fˈɪftiːn", ["sixteen"] = "sˈɪkstiːn", ["seventeen"] = "sˈɛvəntˌiːn",
            ["eighteen"] = "ˈeɪtiːn", ["nineteen"] = "nˈaɪntiːn",
            ["twenty"] = "twˈɛnti", ["thirty"] = "θˈɜːɾi", ["forty"] = "fˈɔːɹɾi",
            ["fifty"] = "fˈɪfti", ["sixty"] = "sˈɪksti", ["seventy"] = "sˈɛvənti",
            ["eighty"] = "ˈeɪɾi", ["ninety"] = "nˈaɪnti",
            ["hundred"] = "hˈʌndɹɪd", ["oh"] = "ˈoʊ",

            // The decimal point, which is a number word now that this rung says one (#177).
            ["point"] = "pˈɔɪnt",

            // The scale words, which are number words now that a measured quantity takes the full reading
            // (#184).
            ["thousand"] = "θˈaʊzənd", ["million"] = "mˈɪliən",
            ["billion"] = "bˈɪliən", ["trillion"] = "tɹˈɪliən",
        };

    /// <summary>The decimal point as it is written.</summary>
    private const char Point = '.';

    /// <summary>The grouping comma, which since #183 is a number's punctuation rather than a word's.</summary>
    private const char Grouping = ',';

    /// <summary>
    /// Whether this token is a number's shape: digits, with one decimal point among them (#177) and
    /// with grouping commas between them (#183).
    /// </summary>
    public static bool Looks(string? token)
    {
        if (token is not { Length: > 0 } || !token.Any(char.IsAsciiDigit))
        {
            return false;
        }

        var point = token.IndexOf(Point, StringComparison.Ordinal);

        // One point at most.
        if (point != token.LastIndexOf(Point))
        {
            return false;
        }

        var whole = point < 0 ? token : token[..point];
        var fraction = point < 0 ? string.Empty : token[(point + 1)..];

        // A grouping comma groups the whole part.
        return fraction.All(char.IsAsciiDigit) && IsGrouped(whole);
    }

    /// <summary>Whether the whole part is digits, grouped legally where it is grouped at all (#183).</summary>
    private static bool IsGrouped(string whole)
    {
        if (!whole.Contains(Grouping, StringComparison.Ordinal))
        {
            return whole.All(char.IsAsciiDigit);
        }

        var groups = whole.Split(Grouping);

        return groups[0].Length is >= 1 and <= 3
               && groups.All(group => group.All(char.IsAsciiDigit))
               && groups.Skip(1).All(group => group.Length == 3);
    }

    /// <summary>The digits as words, with the decimal point spoken where there is one.</summary>
    public static string Say(string digits)
    {
        if (!Looks(digits))
        {
            return digits;
        }

        var point = digits.IndexOf(Point, StringComparison.Ordinal);
        var whole = Ungrouped(point < 0 ? digits : digits[..point]);
        var fraction = point < 0 ? string.Empty : digits[(point + 1)..];
        var measured = IsMeasured(digits, fraction);

        if (fraction.Length == 0)
        {
            return Whole(whole, measured);
        }

        var said = string.Join(" ", fraction.Select(digit => Ones[digit - '0']));

        return whole.Length == 0 ? "point " + said : Whole(whole, measured) + " point " + said;
    }

    /// <summary>
    /// Whether this token is a measured quantity rather than a designation — the ruling #184 asked for,
    /// taken on 2026-08-29.
    /// </summary>
    private static bool IsMeasured(string digits, string fraction) =>
        fraction.Length > 0 || digits.Contains(Grouping, StringComparison.Ordinal);

    /// <summary>
    /// A whole part with its grouping commas taken out, which is the only thing they were ever doing
    /// (#183).
    /// </summary>
    private static string Ungrouped(string whole) =>
        whole.Contains(Grouping, StringComparison.Ordinal)
            ? whole.Replace(Grouping.ToString(), string.Empty, StringComparison.Ordinal)
            : whole;

    /// <summary>
    /// The whole part: the casual reading this rung has always given a run of digits, or the full one
    /// where the token said it was a measured quantity (#184 — see <see cref="IsMeasured"/>).
    /// </summary>
    private static string Whole(string digits, bool measured)
    {
        // A leading zero is part of a name whichever reading is asked for, so this comes first. "007"
        // is "zero zero seven" and dropping the zeros says a different name; a quantity is not written with
        // them.
        if (digits.Length > 1 && digits[0] == '0')
        {
            return Digits(digits);
        }

        if (measured)
        {
            return Measured(digits);
        }

        // Longer than four digits is an identifier rather than a quantity, and nobody says a fourteen-digit
        // market id as a number.
        if (digits.Length > 4)
        {
            return Digits(digits);
        }

        var value = int.Parse(digits, System.Globalization.CultureInfo.InvariantCulture);

        return digits.Length == 4 ? FourDigits(value) : UpTo999(value);
    }

    /// <summary>A run of digits read out one at a time, which is never wrong and never a reading.</summary>
    private static string Digits(string digits) =>
        string.Join(" ", digits.Select(digit => Ones[digit - '0']));

    /// <summary>The scale words, largest first, which is the order they are said in.</summary>
    private static readonly (long Scale, string Word)[] Scales =
    [
        (1_000_000_000_000L, "trillion"),
        (1_000_000_000L, "billion"),
        (1_000_000L, "million"),
        (1_000L, "thousand"),
    ];

    /// <summary>
    /// The full reading a measured quantity takes (#184): <c>1234</c> is one thousand two hundred
    /// thirty-four.
    /// </summary>
    private static string Measured(string digits)
    {
        if (digits.Length > 15)
        {
            return Digits(digits);
        }

        var value = long.Parse(digits, System.Globalization.CultureInfo.InvariantCulture);

        if (value == 0)
        {
            return Ones[0];
        }

        var said = new List<string>();

        foreach (var (scale, word) in Scales)
        {
            if (value < scale)
            {
                continue;
            }

            said.Add(UpTo999Full((int)(value / scale)) + " " + word);
            value %= scale;
        }

        if (value > 0)
        {
            said.Add(UpTo999Full((int)value));
        }

        return string.Join(" ", said);
    }

    /// <summary>
    /// Up to 999, said in full — one hundred twenty-eight rather than the casual one twenty-eight <see
    /// cref="UpTo999"/> gives.
    /// </summary>
    private static string UpTo999Full(int value)
    {
        if (value < 100)
        {
            return UpTo999(value);
        }

        var hundreds = Ones[value / 100] + " hundred";
        var rest = value % 100;

        return rest == 0 ? hundreds : hundreds + " " + UpTo999(rest);
    }

    /// <summary>
    /// <c>1985</c> as nineteen eighty-five, which is how a four-digit designation is read aloud — and
    /// how a year is, which is what most four-digit runs in a name look like.
    /// </summary>
    private static string FourDigits(int value)
    {
        var high = value / 100;
        var low = value % 100;

        if (low == 0)
        {
            return UpTo999(high) + " hundred";
        }

        return low < 10
            ? UpTo999(high) + " oh " + Ones[low]
            : UpTo999(high) + " " + UpTo999(low);
    }

    private static string UpTo999(int value)
    {
        if (value < 20)
        {
            return Ones[value];
        }

        if (value < 100)
        {
            var tens = Tens[value / 10];
            var ones = value % 10;

            return ones == 0 ? tens : tens + "-" + Ones[ones];
        }

        var hundreds = Ones[value / 100];
        var rest = value % 100;

        // "three eighty-five", not "three hundred and eighty-five" - the Commander's reading, and the one
        // anybody uses for a designation rather than a quantity.
        return rest == 0 ? hundreds + " hundred" : hundreds + " " + UpTo999(rest);
    }
}

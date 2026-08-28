namespace D47.Core.Speech;

/// <summary>
/// A run of digits as a person would say it inside a name.
/// <para>
/// <b>Casually, not formally</b> — the Commander's example was <c>385</c> as <em>three
/// eighty-five</em> rather than <em>three hundred and eighty-five</em>. That is how anybody reads a
/// designation aloud, and it is shorter, which matters when a system name carries three of them.
/// </para>
/// <para>
/// Everything here answers in words rather than in IPA, because the words then go through the
/// dictionary like any others: <em>eighty</em> is a word d47 already knows how to say, and spelling
/// it phonetically here would be a second place for it to be wrong.
/// </para>
/// </summary>
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

    /// <summary>
    /// What each number word sounds like.
    /// <para>
    /// <b>Held here rather than left to the letter-to-sound rules, because these are irregular and
    /// they are a closed set.</b> The rules read <c>eighty</c> as <c>eɪɡtaɪ</c> — the <c>gh</c> is
    /// silent and no rule can know that from the spelling. Thirty words written down once is the
    /// honest answer; a rule that guesses at them would be wrong on the commonest words d47 says.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Sounds =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["zero"] = "ˈzɪɹoʊ", ["one"] = "wʌn", ["two"] = "tuː", ["three"] = "θɹiː",
            ["four"] = "fɔːɹ", ["five"] = "faɪv", ["six"] = "sɪks", ["seven"] = "ˈsɛvən",
            ["eight"] = "eɪt", ["nine"] = "naɪn", ["ten"] = "tɛn", ["eleven"] = "əˈlɛvən",
            ["twelve"] = "twɛlv", ["thirteen"] = "θəˈɹtiːn", ["fourteen"] = "fɔɹˈtiːn",
            ["fifteen"] = "fɪfˈtiːn", ["sixteen"] = "sɪksˈtiːn", ["seventeen"] = "ˈsɛvənˌtiːn",
            ["eighteen"] = "eɪˈtiːn", ["nineteen"] = "naɪnˈtiːn",
            ["twenty"] = "ˈtwɛnti", ["thirty"] = "ˈθəɹti", ["forty"] = "ˈfɔɹti",
            ["fifty"] = "ˈfɪfti", ["sixty"] = "ˈsɪksti", ["seventy"] = "ˈsɛvənti",
            ["eighty"] = "ˈeɪti", ["ninety"] = "ˈnaɪnti",
            ["hundred"] = "ˈhʌndɹəd", ["oh"] = "oʊ",
        };

    /// <summary>
    /// The digits as words.
    /// <para>
    /// <b>A leading zero is spoken digit by digit and that is not a quirk.</b> <c>007</c> is
    /// <em>zero zero seven</em> and never <em>seven</em>: in a designation the zeros are part of
    /// the name, and dropping them says a different name.
    /// </para>
    /// </summary>
    public static string Say(string digits)
    {
        if (string.IsNullOrEmpty(digits) || !digits.All(char.IsAsciiDigit))
        {
            return digits;
        }

        if (digits.Length > 1 && digits[0] == '0')
        {
            return string.Join(" ", digits.Select(d => Ones[d - '0']));
        }

        // Longer than four digits is an identifier rather than a quantity, and nobody says a
        // fourteen-digit market id as a number. Digit by digit is the only readable answer.
        if (digits.Length > 4)
        {
            return string.Join(" ", digits.Select(d => Ones[d - '0']));
        }

        var value = int.Parse(digits, System.Globalization.CultureInfo.InvariantCulture);

        return digits.Length == 4 ? FourDigits(value) : UpTo999(value);
    }

    /// <summary>
    /// <c>1985</c> as <em>nineteen eighty-five</em>, which is how a four-digit designation is read
    /// aloud — and how a year is, which is what most four-digit runs in a name look like.
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

        // "three eighty-five", not "three hundred and eighty-five" - the Commander's reading, and
        // the one anybody uses for a designation rather than a quantity.
        return rest == 0 ? hundreds + " hundred" : hundreds + " " + UpTo999(rest);
    }
}

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
    /// <para>
    /// <b>Copied from the shipped dictionary rather than written by ear, since 2026-08-28.</b> They
    /// were hand-written, and eighteen of the thirty put their stress mark in front of a consonant —
    /// <c>ˈθəɹti</c>, <c>ˈsɛvən</c>, <c>ˈhʌndɹəd</c> — which is the shape the dictionary has no
    /// instance of and which Kokoro renders as an intruded vowel. That fault was reported against
    /// names and fixed in <see cref="LetterToSound"/>; it lived here too, and here it matters more,
    /// because d47 says numbers constantly — ranges, distances, tonnages, percentages, credits.
    /// </para>
    /// <para>
    /// So these are now the dictionary's own entries for the same words, which makes them derived
    /// with a source rather than judged: <c>θˈɜːɾi</c>, <c>sˈɛvən</c>, <c>hˈʌndɹɪd</c>. It also
    /// settles the two the ear gets wrong — <c>thirteen</c> was <c>θəˈɹtiːn</c>, breaking the
    /// syllable inside the <c>ɹ</c>. The table stays rather than deferring to the dictionary at
    /// runtime, because it is the fallback for a build whose dictionary never downloaded, which is
    /// the case it was written for.
    /// </para>
    /// </summary>
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

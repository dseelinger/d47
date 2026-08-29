namespace D47.Core.Speech;

/// <summary>
/// A run of digits as a person would say it inside a name.
/// <para>
/// <b>Casually, not formally</b> — the Commander's example was <c>385</c> as <em>three
/// eighty-five</em> rather than <em>three hundred and eighty-five</em>. That is how anybody reads a
/// designation aloud, and it is shorter, which matters when a system name carries three of them.
/// </para>
/// <para>
/// <b>And with its decimal point, since #177.</b> A decimal is not all digits, so it fell past this
/// rung entirely and was spelled out — and the spelling rung has no sound for <c>.</c>, so it
/// dropped it: <c>5.79</c> was <em>five, seven, nine</em>. d47 says decimals constantly — ranges,
/// tonnages, percentages, credits — so every one of those was wrong in the same way. The shape is a
/// number's rather than a designation's, which is why it is owned here instead of patched into the
/// spelling.
/// </para>
/// <para>
/// <b>And with its grouping commas, since #183.</b> That was the same fault one character over, and
/// found by #177's own lane: <c>6,680</c> is not all digits either, so it fell to the spelling rung
/// too — which does have a reading for a comma, and said <em>six six eight zero</em> with a pause
/// in the middle of it. The comma says nothing and is dropped; the grouping it describes is
/// checked, so a token that only looks like a number still falls through honestly.
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

            // The decimal point, which is a number word now that this rung says one (#177). Its
            // reading is the dictionary's own, like every entry above it, so a build whose
            // dictionary never downloaded says it the same way.
            ["point"] = "pˈɔɪnt",

            // The scale words, which are number words now that a measured quantity takes the full
            // reading (#184). The dictionary's own entries, for the same reason as everything
            // above: this table is the fallback for a build whose dictionary never downloaded, and
            // the rules read "thousand" with a /θ/ and then guess at the rest.
            ["thousand"] = "θˈaʊzənd", ["million"] = "mˈɪliən",
            ["billion"] = "bˈɪliən", ["trillion"] = "tɹˈɪliən",
        };

    /// <summary>The decimal point as it is written. One per number, or it is not one.</summary>
    private const char Point = '.';

    /// <summary>The grouping comma, which since #183 is a number's punctuation rather than a word's.</summary>
    private const char Grouping = ',';

    /// <summary>
    /// Whether this token is a number's shape: digits, with one decimal point among them (#177)
    /// and with grouping commas between them (#183).
    /// <para>
    /// <b>The point is what puts this here rather than in the spelling rung.</b> A decimal is not
    /// all digits, so it used to fall past the number rung to the bottom of the ladder — and the
    /// spelling rung has no sound for a full stop, so it silently dropped it. <c>5.79 ly</c> was
    /// <em>five, seven, nine</em>.
    /// </para>
    /// <para>
    /// <b>The comma was the same fault one character over</b> (#183). <c>6,680</c> is not all
    /// digits either, so it fell the same way — and the spelling rung <em>does</em> have a reading
    /// for a comma, which made it worse rather than better: it was said <em>six six eight zero</em>
    /// with a pause where the grouping was. d47 writes grouped numbers wherever credits, tonnages
    /// and distances get large, which is most of where they appear.
    /// </para>
    /// <para>
    /// <b>The grouping is validated rather than tolerated.</b> A comma every three digits is a
    /// number; a comma anywhere else is not, and <c>6,68</c> or <c>12,34,567</c> falls through to
    /// the ladder honestly instead of being read as though the comma were not there. That is the
    /// difference between owning a shape and guessing at one.
    /// </para>
    /// <para>
    /// <b>One point, deliberately.</b> Two of them is a version rather than a decimal —
    /// <c>0.90.0</c> — which is a different reading, and one nobody has asked for.
    /// </para>
    /// </summary>
    public static bool Looks(string? token)
    {
        if (token is not { Length: > 0 } || !token.Any(char.IsAsciiDigit))
        {
            return false;
        }

        var point = token.IndexOf(Point, StringComparison.Ordinal);

        // One point at most. Two is a version number, which is a different reading.
        if (point != token.LastIndexOf(Point))
        {
            return false;
        }

        var whole = point < 0 ? token : token[..point];
        var fraction = point < 0 ? string.Empty : token[(point + 1)..];

        // A grouping comma groups the whole part. One inside the fraction is not grouping
        // anything, so it is not this shape.
        return fraction.All(char.IsAsciiDigit) && IsGrouped(whole);
    }

    /// <summary>
    /// Whether the whole part is digits, grouped legally where it is grouped at all (#183).
    /// <para>
    /// Ungrouped, any run of digits is admitted, exactly as it was before — the length rules live
    /// in <see cref="Whole"/> and are none of this method's business. Grouped, the shape is
    /// one to three digits and then a comma and three digits, repeated: <c>6,680</c> and
    /// <c>1,234,567</c> are numbers, and <c>6,68</c>, <c>1,2345</c>, <c>,680</c> and
    /// <c>12,34,567</c> are not.
    /// </para>
    /// <para>
    /// Empty is legal, because <c>.79</c> has no whole part — the ragged end #177 ruled on.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// The digits as words, with the decimal point spoken where there is one.
    /// <para>
    /// <b>A leading zero is spoken digit by digit and that is not a quirk.</b> <c>007</c> is
    /// <em>zero zero seven</em> and never <em>seven</em>: in a designation the zeros are part of
    /// the name, and dropping them says a different name.
    /// </para>
    /// <para>
    /// <b>The fraction is said digit by digit and never as a number</b> — <c>5.79</c> is <em>five
    /// point seven nine</em>, because <em>five point seventy-nine</em> is a different quantity to
    /// anybody listening. That is the one place a decimal parts company with the casual reading
    /// above, and it parts company with it in every English dialect.
    /// </para>
    /// <para>
    /// <b>The two ragged ends, ruled while in here</b> (#177 asked for them). A leading point says
    /// no whole part rather than inventing a zero for it — <c>.79</c> is <em>point seven nine</em>,
    /// which is what a person reading that aloud says. A trailing point is a full stop somebody
    /// left inside the token rather than a decimal point, so the number is said without it; in
    /// practice it never arrives, because a token's trailing full stop is trimmed off as phrasing
    /// before the ladder ever sees it.
    /// </para>
    /// <para>
    /// <b>The grouping commas are punctuation and say nothing</b> (#183). They are dropped before
    /// the whole part is read, so <c>6,680</c> says exactly what <c>6680</c> says — which is the
    /// invariant that makes a comma grouping rather than content.
    /// </para>
    /// <para>
    /// <b>And a token that wears either mark is a measured quantity, not a designation</b> (#184).
    /// See <see cref="IsMeasured"/> for the ruling and what it costs.
    /// </para>
    /// </summary>
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
    /// <b>Whether this token is a measured quantity rather than a designation</b> — the ruling #184
    /// asked for, taken on 2026-08-29.
    /// <para>
    /// <b>The question.</b> A run of digits has been read casually since the Commander ruled it:
    /// <c>385</c> is <em>three eighty-five</em>, which is how anybody reads a designation aloud and
    /// is shorter, and <c>1985</c> is <em>nineteen eighty-five</em>. That reading is right for
    /// <c>COL 385 SECTOR B0-GQPI</c> and it followed the digits into places that are not
    /// designations at all: <c>1234.5 ly</c> was said <em>twelve thirty-four point five</em>, which
    /// is a year and a fraction rather than a distance.
    /// </para>
    /// <para>
    /// <b>The ruling: the token's own punctuation decides, and nothing else does.</b> A decimal
    /// point with digits after it, or a grouping comma, makes it a quantity and its whole part
    /// takes the full reading — <em>one thousand two hundred thirty-four point five</em>. Bare
    /// digits keep the casual designation reading, unchanged.
    /// </para>
    /// <para>
    /// <b>Why the punctuation and not the unit word</b>, which #184 offered as the other candidate.
    /// The unit is not visible here: <see cref="SpokenUnits"/> (#155) rewrites <c>ly</c> to
    /// <em>light years</em> in <c>SpeechPipeline</c> <em>before</em> the ladder sees a single
    /// token, so by the time this rung is reached the abbreviation is gone and what follows the
    /// number is two ordinary English words. Reading it would mean either running this decision
    /// ahead of that rewrite or keeping a second list of unit words here — and a second list is a
    /// second thing to disagree with the first, which is the failure this file already records
    /// against hand-written number words. The punctuation needs neither: it is on the token, so the
    /// decision is decidable from the token alone, which is what makes it testable.
    /// </para>
    /// <para>
    /// <b>What the ruling costs, stated so it can be overturned cheaply.</b> A bare quantity with a
    /// unit after it keeps the casual reading — <c>1234 tonnes</c> is still <em>twelve thirty-four
    /// tonnes</em>. That is the one case this gets arguably wrong, and it is one predicate away
    /// from being got the other way: if the Commander wants the unit to count, the change is to
    /// this method and to nothing else.
    /// </para>
    /// </summary>
    private static bool IsMeasured(string digits, string fraction) =>
        fraction.Length > 0 || digits.Contains(Grouping, StringComparison.Ordinal);

    /// <summary>
    /// A whole part with its grouping commas taken out, which is the only thing they were ever
    /// doing (#183). <see cref="Looks"/> has already refused a grouping that was not a grouping,
    /// so nothing malformed reaches here to be quietly straightened.
    /// </summary>
    private static string Ungrouped(string whole) =>
        whole.Contains(Grouping, StringComparison.Ordinal)
            ? whole.Replace(Grouping.ToString(), string.Empty, StringComparison.Ordinal)
            : whole;

    /// <summary>
    /// The whole part: the casual reading this rung has always given a run of digits, or the full
    /// one where the token said it was a measured quantity (#184 — see <see cref="IsMeasured"/>).
    /// </summary>
    private static string Whole(string digits, bool measured)
    {
        // <b>A leading zero is part of a name whichever reading is asked for</b>, so this comes
        // first. "007" is "zero zero seven" and dropping the zeros says a different name; a
        // quantity is not written with them.
        if (digits.Length > 1 && digits[0] == '0')
        {
            return Digits(digits);
        }

        if (measured)
        {
            return Measured(digits);
        }

        // Longer than four digits is an identifier rather than a quantity, and nobody says a
        // fourteen-digit market id as a number. Digit by digit is the only readable answer.
        //
        // This is the designation reading's rule and stays here rather than moving up: a measured
        // quantity has already said it is one, so its length is not evidence about what it is.
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

    /// <summary>
    /// The scale words, largest first, which is the order they are said in.
    /// <para>
    /// It stops at <em>trillion</em> because that is where a figure d47 could be given stops being
    /// a quantity anybody follows by ear, and because eighteen digits is where a <c>long</c> stops
    /// being safe. Past it, the digits are read out one at a time — the same answer this rung has
    /// always given a run it cannot say as a number.
    /// </para>
    /// </summary>
    private static readonly (long Scale, string Word)[] Scales =
    [
        (1_000_000_000_000L, "trillion"),
        (1_000_000_000L, "billion"),
        (1_000_000L, "million"),
        (1_000L, "thousand"),
    ];

    /// <summary>
    /// The full reading a measured quantity takes (#184): <c>1234</c> is <em>one thousand two
    /// hundred thirty-four</em>.
    /// <para>
    /// <b>No <em>and</em>, which is a decision rather than an oversight.</b> The casual reading
    /// above already says <em>three hundred</em> and never <em>three hundred and</em>, and this
    /// rung has no accent to consult — <see cref="Phonemiser"/> picks one per voice, long after the
    /// words are chosen. Saying it one way in both readings is the only answer that cannot
    /// disagree with itself.
    /// </para>
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
    /// Up to 999, said in full — <em>one hundred twenty-eight</em> rather than the casual
    /// <em>one twenty-eight</em> <see cref="UpTo999"/> gives. The two differ only above a hundred,
    /// which is exactly where the casual reading drops the word.
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

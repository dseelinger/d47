using System.Globalization;
using System.Text;

namespace D47.Core.Audio;

/// <summary>
/// Digits, written out as the words a voice should say them in
/// (remediation.md, "ElevenLabs switches Warden to German mid-callout").
/// <para>
/// <b>Why this exists.</b> A multilingual synthesiser decides what language a line is in from
/// the line itself, and a bare numeral is the one token that carries no language at all. The
/// reported case was a material milestone — "Adaptive Encryptors Capture at 75 percent. 88 of
/// 100." — which came back with the tail spoken in German. Giving the model "eighty-eight of
/// one hundred" leaves nothing in the sentence that is not English.
/// </para>
/// <para>
/// <b>What is spoken is not what is read.</b> This is applied at the synthesis seam and
/// nowhere else: the transcript, the captions and the log keep the numerals, which are what a
/// person reading a page wants. A caption saying "eighty-eight of one hundred" would be a
/// regression in every case except the one this fixes.
/// </para>
/// <para>
/// <b>Conservative on purpose, and the unit is the word.</b> Any whitespace-delimited word with
/// a letter anywhere in it is passed through untouched — <c>c2-23</c>, <c>b6-2</c>,
/// <c>eleven_turbo_v2_5</c>, <c>24kHz</c> are identifiers and part-words, not quantities, and a
/// procedurally generated system name is the commonest thing d47 says out loud that has digits
/// in it. Judging each digit run by its immediate neighbours was not enough: the <c>23</c> of
/// <c>c2-23</c> touches a hyphen on one side and nothing on the other, and came out as
/// "c2-twenty-three".
/// </para>
/// </summary>
public static class SpokenNumbers
{
    private static readonly string[] Ones =
    [
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
        "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen",
        "eighteen", "nineteen",
    ];

    private static readonly string[] Tens =
    [
        "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety",
    ];

    /// <summary>
    /// The scale names, smallest first. Stops at trillion: beyond that the number is longer
    /// than the sentence it is in, and a credit balance that large is read out as digits by
    /// everything else too.
    /// </summary>
    private static readonly (long Value, string Name)[] Scales =
    [
        (1_000_000_000_000L, "trillion"),
        (1_000_000_000L, "billion"),
        (1_000_000L, "million"),
        (1_000L, "thousand"),
    ];

    /// <summary>
    /// The largest number this will write out. Above it the digits are left alone, because
    /// "nine hundred and eighty-seven quadrillion" is not an improvement on anything.
    /// </summary>
    private const long Ceiling = 999_999_999_999_999L;

    /// <summary>
    /// The same line with its free-standing numbers written as words. Text with no digits in it
    /// is returned unchanged and untouched.
    /// </summary>
    public static string Expand(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Any(char.IsAsciiDigit))
        {
            return text;
        }

        var built = new StringBuilder(text.Length + 16);
        var i = 0;

        while (i < text.Length)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                built.Append(text[i]);
                i++;
                continue;
            }

            var wordStart = i;

            while (i < text.Length && !char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            var word = text[wordStart..i];

            // A letter anywhere in the word makes the whole word an identifier rather than a
            // quantity, and identifiers are said as written.
            built.Append(word.Any(char.IsLetter) ? word : Numerals(word));
        }

        return built.ToString();
    }

    /// <summary>One word with no letters in it, with any numerals inside it written out.</summary>
    private static string Numerals(string word)
    {
        var built = new StringBuilder(word.Length + 16);
        var i = 0;

        while (i < word.Length)
        {
            if (!char.IsAsciiDigit(word[i]))
            {
                built.Append(word[i]);
                i++;
                continue;
            }

            var start = i;

            // The whole numeral, group separators and a decimal part included. A comma or a
            // point only continues the number when a digit follows it, so "100." ends at the
            // full stop and "1,000" does not.
            while (i < word.Length
                   && (char.IsAsciiDigit(word[i])
                       || ((word[i] is ',' or '.') && i + 1 < word.Length && char.IsAsciiDigit(word[i + 1]))))
            {
                i++;
            }

            var numeral = word[start..i];

            built.Append(Say(numeral) ?? numeral);
        }

        return built.ToString();
    }

    /// <summary>One numeral as words, or null when it is not one this will attempt.</summary>
    private static string? Say(string numeral)
    {
        var point = numeral.IndexOf('.', StringComparison.Ordinal);

        var whole = (point < 0 ? numeral : numeral[..point]).Replace(",", string.Empty);
        var fraction = point < 0 ? string.Empty : numeral[(point + 1)..].Replace(",", string.Empty);

        if (!long.TryParse(whole, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            || value > Ceiling)
        {
            return null;
        }

        var said = Whole(value);

        if (fraction.Length == 0)
        {
            return said;
        }

        // Digit by digit after the point, which is how a decimal is read aloud: "12.75" is
        // "twelve point seven five", never "twelve point seventy-five".
        var digits = new StringBuilder(said).Append(" point");

        foreach (var digit in fraction)
        {
            digits.Append(' ').Append(Ones[digit - '0']);
        }

        return digits.ToString();
    }

    private static string Whole(long value)
    {
        if (value < 20)
        {
            return Ones[value];
        }

        if (value < 100)
        {
            var unit = value % 10;
            return unit == 0 ? Tens[value / 10] : $"{Tens[value / 10]}-{Ones[unit]}";
        }

        if (value < 1_000)
        {
            var rest = value % 100;

            // "and" between the hundreds and the rest, which is how this is said in the English
            // d47 is written in and what keeps "one hundred and one" from running together.
            return rest == 0
                ? $"{Ones[value / 100]} hundred"
                : $"{Ones[value / 100]} hundred and {Whole(rest)}";
        }

        foreach (var (scale, name) in Scales)
        {
            if (value < scale)
            {
                continue;
            }

            var rest = value % scale;

            return rest == 0
                ? $"{Whole(value / scale)} {name}"
                : $"{Whole(value / scale)} {name} {Whole(rest)}";
        }

        return value.ToString(CultureInfo.InvariantCulture);
    }
}

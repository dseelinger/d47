namespace D47.Core.Speech;

/// <summary>
/// Roman numerals said as numbers, but only where the surrounding words say one is meant (#138).
/// </summary>
public static class SpokenNumerals
{
    /// <summary>The letters a numeral is made of, and what each is worth.</summary>
    private static int Digit(char letter) => letter switch
    {
        'I' => 1,
        'V' => 5,
        'X' => 10,
        'L' => 50,
        'C' => 100,
        'D' => 500,
        'M' => 1000,
        _ => 0,
    };

    private static readonly (int Value, string Symbol)[] Ladder =
    [
        (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"),
        (100, "C"), (90, "XC"), (50, "L"), (40, "XL"),
        (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I"),
    ];

    /// <summary>What a numeral is worth, or null when the letters are not a well-formed one.</summary>
    public static int? Value(string numeral)
    {
        ArgumentNullException.ThrowIfNull(numeral);

        if (numeral.Length == 0)
        {
            return null;
        }

        var total = 0;
        var highest = 0;

        // Right to left, so a smaller letter standing before a larger one subtracts.
        for (var i = numeral.Length - 1; i >= 0; i--)
        {
            var digit = Digit(numeral[i]);

            if (digit == 0)
            {
                return null;
            }

            total += digit < highest ? -digit : digit;
            highest = Math.Max(highest, digit);
        }

        return total > 0 && string.Equals(Render(total), numeral, StringComparison.Ordinal)
            ? total
            : null;
    }

    private static string Render(int value)
    {
        var built = new System.Text.StringBuilder();

        foreach (var (worth, symbol) in Ladder)
        {
            while (value >= worth)
            {
                built.Append(symbol);
                value -= worth;
            }
        }

        return built.ToString();
    }

    /// <summary>
    /// The line with its mark and class numerals written out as words, and everything else untouched.
    /// </summary>
    public static string Expand(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        // Nothing to do on the overwhelming majority of lines, and this is on the path of every sentence the
        // local voice says.
        if (!text.Contains("Mk", StringComparison.OrdinalIgnoreCase)
            && !text.Contains("class", StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        // Split without removing empties, so the line is rebuilt exactly as it arrived apart from what is
        // deliberately changed.
        var tokens = text.Split(' ');

        for (var i = 0; i < tokens.Length; i++)
        {
            var (lead, body, tail) = Parts(tokens[i]);

            // 1.
            if (Joined(body) is { } joined)
            {
                tokens[i] = lead + "Mark " + joined + tail;
                continue;
            }

            if (i + 1 >= tokens.Length)
            {
                continue;
            }

            var (nextLead, nextBody, nextTail) = Parts(tokens[i + 1]);

            if (Value(nextBody) is not { } number)
            {
                continue;
            }

            var word = SpokenNumber.Say(number.ToString(System.Globalization.CultureInfo.InvariantCulture));

            // 2.
            if (IsMk(body))
            {
                tokens[i] = lead + "Mark" + tail;
                tokens[i + 1] = nextLead + word + nextTail;
                continue;
            }

            // 3. "Mark II" written out. **Bare `I` is excluded here and nowhere else**, because "Mark I saw
            // him" is an English sentence and "Mark one saw him" is what converting it would produce.
            if (string.Equals(body, "Mark", StringComparison.OrdinalIgnoreCase))
            {
                if (number > 1)
                {
                    tokens[i + 1] = nextLead + word + nextTail;
                }

                continue;
            }

            // 4. "Class I gas giant" through "Class V gas giant", which Elite also writes as "Sudarsky class
            // I gas giant". **Narrowed to the phrase rather than the word**: on its own, "class" plus a
            // numeral would convert "the class I attended", which is a sentence a persona could easily say.
            if (string.Equals(body, "class", StringComparison.OrdinalIgnoreCase)
                && i + 2 < tokens.Length
                && Parts(tokens[i + 2]).Body.StartsWith("gas", StringComparison.OrdinalIgnoreCase))
            {
                tokens[i + 1] = nextLead + word + nextTail;
            }
        }

        return string.Join(" ", tokens);
    }

    /// <summary><c>MkIII</c> as three, or null when this token is not a mark and a numeral run together.</summary>
    private static string? Joined(string body)
    {
        var rest = Prefix(body, "Mark") ?? Prefix(body, "Mk");

        if (rest is not { Length: > 0 })
        {
            return null;
        }

        // "Mk.III" as readily as "MkIII".
        if (rest[0] == '.')
        {
            rest = rest[1..];
        }

        return Value(rest) is { } number
            ? SpokenNumber.Say(number.ToString(System.Globalization.CultureInfo.InvariantCulture))
            : null;
    }

    private static string? Prefix(string body, string mark) =>
        body.Length > mark.Length && body.StartsWith(mark, StringComparison.OrdinalIgnoreCase)
            ? body[mark.Length..]
            : null;

    private static bool IsMk(string body) =>
        string.Equals(body, "Mk", StringComparison.OrdinalIgnoreCase)
        || string.Equals(body, "Mk.", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A token split into the punctuation around it and the word inside, so <c>"(MkII)."</c> is
    /// recognised and comes back out with its brackets.
    /// </summary>
    private static (string Lead, string Body, string Tail) Parts(string token)
    {
        var body = token.TrimEnd('.', ',', '!', '?', ';', ':', ')', ']', '"', '\'');

        // A trailing period is punctuation on "II." and part of the word on "Mk." — kept when what is left is
        // a mark, trimmed otherwise.
        if (token.Length > body.Length
            && token[body.Length] == '.'
            && IsMk(body + "."))
        {
            body = token[..(body.Length + 1)];
        }

        var tail = token[body.Length..];
        var head = body.TrimStart('(', '[', '"', '\'');
        var lead = body[..(body.Length - head.Length)];

        return (lead, head, tail);
    }
}

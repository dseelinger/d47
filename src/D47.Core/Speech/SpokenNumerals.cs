namespace D47.Core.Speech;

/// <summary>
/// Roman numerals said as numbers, but only where the surrounding words say one is meant
/// (<a href="https://github.com/dseelinger/d47/issues/138">#138</a>).
/// <para>
/// <b>The local voice spelled them, because there is nothing else it could have done.</b>
/// <see cref="Phonemiser"/>'s ladder ends in <em>anything left is spelled out</em>, and a roman
/// numeral is letters with no digits and no vowels to parse — so <c>MkIII</c> came out <em>em kay
/// eye eye eye</em> and <c>Mk II</c> came out <em>em kay</em> then <em>eye eye</em>. Neither is a
/// thing anybody says, and 74 entries in the shipped tables carry one: Cobra MkIII, Krait MkII,
/// Python MkII, Kestrel Mk II, and every armour row underneath each of them.
/// </para>
/// <para>
/// <b>Two things joined, because either alone is still wrong.</b> <c>Mk</c> is <em>Mark</em> — not a
/// word the dictionary holds, and <em>em kay three</em> is no better than what it replaced. So the
/// pair reads <em>Cobra Mark Three</em>, which is what a Commander says out loud. And the numeral is
/// a <b>cardinal</b>: <em>Mark Three</em>, never <em>Mark the Third</em>.
/// </para>
/// <para>
/// <b>A general "any run of I V X L C D M is a numeral" rule would be wrong, and quietly.</b> Those
/// letters spell English words — <c>I</c>, <c>MIX</c>, <c>DID</c>, <c>CIVIC</c>, <c>MILD</c>,
/// <c>LIVID</c> — and converting them would turn ordinary prose into numbers in a voice, which is
/// the worst place to find out. The repository's own habit points at the answer: the announced-attack
/// allowlist exists because <em>"anything matching on 'this sounds hostile' cries wolf a hundred
/// times per real event"</em>. So this fires only where the context says numeral, and the three
/// contexts are stated below with what narrows each.
/// </para>
/// <para>
/// It runs as a pass over the whole line rather than inside the per-segment ladder, because the
/// context spans segments: <c>MkII</c> arrives as one and <c>Mk II</c> as two, and both have to
/// produce the same sound. What it emits is ordinary English, so the ladder then says
/// <em>Mark</em> and <em>three</em> out of the dictionary like any other words — which is also what
/// keeps a real word from ever being captured by a second set of rules.
/// </para>
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

    /// <summary>
    /// What a numeral is worth, or null when the letters are not a well-formed one.
    /// <para>
    /// <b>Parsed and then rendered back, and the two must agree.</b> That round trip is what makes
    /// this strict rather than merely plausible: the subtractive parse alone accepts <c>MILD</c>,
    /// <c>DID</c> and <c>CIVIC</c> as some number or other, and re-rendering the number gives
    /// <c>MCDXLIX</c>, <c>CMXCIX</c> and <c>CXCIII</c> — none of which is what arrived, so none of
    /// them is a numeral. It is a shorter and more obviously correct test than the canonical-form
    /// regular expression, which has to be read three times to be believed.
    /// </para>
    /// <para>
    /// <c>MIX</c> survives it, being a genuine 1009. That is not a hole: nothing here converts a
    /// numeral that is not standing in one of the three contexts below, and no context ever puts
    /// <em>mix</em> where a mark number goes.
    /// </para>
    /// </summary>
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
    /// The line with its mark and class numerals written out as words, and everything else
    /// untouched.
    /// </summary>
    public static string Expand(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        // Nothing to do on the overwhelming majority of lines, and this is on the path of every
        // sentence the local voice says.
        if (!text.Contains("Mk", StringComparison.OrdinalIgnoreCase)
            && !text.Contains("class", StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        // Split without removing empties, so the line is rebuilt exactly as it arrived apart from
        // what is deliberately changed.
        var tokens = text.Split(' ');

        for (var i = 0; i < tokens.Length; i++)
        {
            var (lead, body, tail) = Parts(tokens[i]);

            // 1. Joined: "MkIII", "MkII", and the same written out as "MarkIII". Unrestricted,
            //    because neither spelling is a word that could arrive by accident.
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

            // 2. Spaced: "Mk II", "Mk. II", "Kestrel Mk II". `Mk` is not an English word, so it
            //    needs no further narrowing.
            if (IsMk(body))
            {
                tokens[i] = lead + "Mark" + tail;
                tokens[i + 1] = nextLead + word + nextTail;
                continue;
            }

            // 3. "Mark II" written out. **Bare `I` is excluded here and nowhere else**, because
            //    "Mark I saw him" is an English sentence and "Mark one saw him" is what converting
            //    it would produce. Every mark number in the shipped tables is II or higher, so the
            //    exclusion costs nothing real.
            if (string.Equals(body, "Mark", StringComparison.OrdinalIgnoreCase))
            {
                if (number > 1)
                {
                    tokens[i + 1] = nextLead + word + nextTail;
                }

                continue;
            }

            // 4. "Class I gas giant" through "Class V gas giant", which Elite also writes as
            //    "Sudarsky class I gas giant". **Narrowed to the phrase rather than the word**: on
            //    its own, "class" plus a numeral would convert "the class I attended", which is a
            //    sentence a persona could easily say. The whole population is the five Sudarsky
            //    bodies and every one of them is followed by "gas", so requiring it loses nothing
            //    and closes the only false positive this context has.
            if (string.Equals(body, "class", StringComparison.OrdinalIgnoreCase)
                && i + 2 < tokens.Length
                && Parts(tokens[i + 2]).Body.StartsWith("gas", StringComparison.OrdinalIgnoreCase))
            {
                tokens[i + 1] = nextLead + word + nextTail;
            }
        }

        return string.Join(" ", tokens);
    }

    /// <summary>
    /// <c>MkIII</c> as <em>three</em>, or null when this token is not a mark and a numeral run
    /// together. The numeral must be upper case, which every one of the 74 table entries is, and
    /// which is one more thing a word arriving by accident would have to satisfy.
    /// </summary>
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
    /// recognised and comes back out with its brackets. The same characters
    /// <see cref="Phonemiser"/> trims, since this runs immediately before it.
    /// </summary>
    private static (string Lead, string Body, string Tail) Parts(string token)
    {
        var body = token.TrimEnd('.', ',', '!', '?', ';', ':', ')', ']', '"', '\'');

        // A trailing period is punctuation on "II." and part of the word on "Mk." — kept when what
        // is left is a mark, trimmed otherwise.
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

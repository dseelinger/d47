using System.Text;

namespace D47.Core.Listening;

/// <summary>
/// A spoken name reduced to its consonant sounds, so a transcriber's spelling of a name ("Quora",
/// "Kovas") reads the same as the name ("Cora", "COVAS").
/// </summary>
public static class PhoneticKey
{
    /// <summary>Spellings of one sound, replaced in this order.</summary>
    private static readonly (string From, string To)[] Spellings =
    [
        ("ph", "f"),
        ("qu", "k"),
        ("ck", "k"),
        ("x", "ks"),
        ("ch", "x"),
        ("sh", "x"),
    ];

    /// <summary>The key for a name; empty when it has no letters.</summary>
    public static string Of(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var letters = new string([.. text.ToLowerInvariant().Where(char.IsAsciiLetterLower)]);

        foreach (var (from, to) in Spellings)
        {
            letters = letters.Replace(from, to, StringComparison.Ordinal);
        }

        var key = new StringBuilder(letters.Length);

        for (var i = 0; i < letters.Length; i++)
        {
            var next = i + 1 < letters.Length ? letters[i + 1] : '\0';

            var sound = letters[i] switch
            {
                'c' => next is 'e' or 'i' or 'y' ? 's' : 'k',
                'q' or 'g' => 'k',
                'v' => 'f',
                'b' => 'p',
                'd' => 't',
                'z' => 's',
                var letter => letter,
            };

            // Vowels and "h" after the first letter are dropped.
            if (i > 0 && sound is 'a' or 'e' or 'i' or 'o' or 'u' or 'y' or 'h')
            {
                continue;
            }

            if (key.Length > 0 && key[^1] == sound)
            {
                continue;
            }

            key.Append(sound);
        }

        return key.ToString();
    }

    /// <summary>
    /// How many sounds apart a heard key may be from a name's key and still name it: none for a key of
    /// one or two sounds, one up to five, two beyond.
    /// </summary>
    public static int Tolerance(string nameKey)
    {
        ArgumentNullException.ThrowIfNull(nameKey);

        return nameKey.Length switch
        {
            <= 2 => 0,
            <= 5 => 1,
            _ => 2,
        };
    }

    /// <summary>The number of single-sound insertions, deletions or substitutions between two keys.</summary>
    public static int Distance(string a, string b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;

            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;

                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}

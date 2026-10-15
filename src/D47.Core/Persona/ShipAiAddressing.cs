namespace D47.Core.Persona;

/// <summary>Whether an utterance opens by naming the ship's AI.</summary>
public static class ShipAiAddressing
{
    /// <summary>A name that addresses the ship's AI whichever core is aboard.</summary>
    public const string CallSign = "D47";

    /// <summary>
    /// What is left after the longest of <paramref name="names"/> that opens <paramref name="input"/>, empty for the
    /// name alone, or null when none does. Letters and digits are compared, folded to lower case, so "D-47" is "D47";
    /// the name must end at a word boundary.
    /// </summary>
    public static string? Opens(string input, IEnumerable<string> names)
    {
        var map = new List<int>(input.Length);
        var flat = new System.Text.StringBuilder(input.Length);

        for (var i = 0; i < input.Length; i++)
        {
            if (char.IsLetterOrDigit(input[i]))
            {
                flat.Append(char.ToLowerInvariant(input[i]));
                map.Add(i);
            }
        }

        var said = flat.ToString();

        foreach (var name in names.Select(Flatten).Where(name => name.Length > 0).OrderByDescending(name => name.Length))
        {
            if (!said.StartsWith(name, StringComparison.Ordinal))
            {
                continue;
            }

            var end = map[name.Length - 1] + 1;

            if (end < input.Length && (char.IsLetterOrDigit(input[end]) || input[end] is '\'' or '’'))
            {
                continue;
            }

            var rest = input[end..];
            var start = rest.TakeWhile(character => !char.IsLetterOrDigit(character)).Count();

            return rest[start..].Trim();
        }

        return null;
    }

    private static string Flatten(string name) =>
        new([.. name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);
}

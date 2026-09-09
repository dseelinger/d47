using System.Text.RegularExpressions;

namespace D47.Core.Speech;

/// <summary>Unit abbreviations written out as words, for the provider only (#155).</summary>
public static class SpokenUnits
{
    /// <summary>
    /// Every unit that is spoken, with what to say for one of it and for any other number of it.
    /// </summary>
    private static readonly Dictionary<string, (string One, string Many)> Units =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ly"] = ("light year", "light years"),
            ["ls"] = ("light second", "light seconds"),
            ["t"] = ("tonne", "tonnes"),
            ["MW"] = ("megawatt", "megawatts"),
            ["cr"] = ("credit", "credits"),
        };

    /// <summary>A number, the spaces after it, and a unit — with a token boundary on both ends.</summary>
    private static readonly Regex Anchored = new(
        @"(?<![\p{L}\d])(?<number>\d+(?:,\d{3})*(?:\.\d+)?)(?<gap>[ \t]+)(?<unit>ly|ls|t|MW|cr)(?![\p{L}\d])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>One line as it should be spoken.</summary>
    public static string Rewrite(string? line) =>
        string.IsNullOrEmpty(line) ? line ?? string.Empty : Anchored.Replace(line, Say);

    /// <summary>One match, said.</summary>
    private static string Say(Match match)
    {
        var number = match.Groups["number"].Value;
        var (one, many) = Units[match.Groups["unit"].Value];

        return number + match.Groups["gap"].Value + (number == "1" ? one : many);
    }
}

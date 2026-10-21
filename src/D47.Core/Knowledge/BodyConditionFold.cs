using System.Text.RegularExpressions;

namespace D47.Core.Knowledge;

/// <summary>
/// Folds spansh's and the journal's words for a body's conditions to one spelling; mirrors
/// <c>tools/body_conditions.py</c>.
/// </summary>
internal static class BodyConditionFold
{
    private static readonly Regex Letters = new("[a-z]+", RegexOptions.Compiled);

    private static readonly HashSet<string> AtmosphereStopwords =
        new(StringComparer.Ordinal) { "thin", "thick", "hot", "atmosphere" };

    public static string NormalisePlanetType(string? raw) =>
        string.Concat(Letters.Matches((raw ?? string.Empty).ToLowerInvariant())
            .Select(match => match.Value == "world" ? "body" : match.Value));

    public static string NormaliseAtmosphere(string? raw)
    {
        var text = (raw ?? string.Empty).Trim().ToLowerInvariant();

        if (text.Length == 0 || text == "no atmosphere")
        {
            return "none";
        }

        text = text.Replace("sulphur", "sulfur", StringComparison.Ordinal);

        return string.Concat(Letters.Matches(text).Select(match => match.Value)
            .Where(word => !AtmosphereStopwords.Contains(word)));
    }

    public static string NormaliseVolcanism(string? raw)
    {
        var text = (raw ?? string.Empty).Trim().ToLowerInvariant();

        if (text.Length == 0 || text == "no volcanism")
        {
            return "none";
        }

        return string.Concat(Letters.Matches(text).Select(match => match.Value)
            .Where(word => word != "volcanism"));
    }
}

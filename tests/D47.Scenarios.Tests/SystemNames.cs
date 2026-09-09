using System.Text.RegularExpressions;

namespace D47.Scenarios.Tests;

/// <summary>What a resolver can say about a name.</summary>
public enum SystemVerdict
{
    Exists,

    /// <summary>The resolver looked and the galaxy does not contain it.</summary>
    DoesNotExist,

    /// <summary>Nobody could say.</summary>
    Unknown,
}

/// <summary>Whether a system name names something real.</summary>
public interface ISystemResolver
{
    SystemVerdict Resolve(string systemName);
}

/// <summary>Pulling system names out of a reply, so the invention check is a property rather than a string comparison.</summary>
public static partial class SystemNames
{
    /// <summary>The procedural grammar, as it appears inside a sentence.</summary>
    [GeneratedRegex(
        @"\b([A-Z][A-Za-z]*(?:[ '-][A-Z][A-Za-z]*)* [A-Z][A-Z]-[A-Z] [a-h](?:\d+-)?\d+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex Procedural { get; }

    /// <summary>Every system-shaped name in <paramref name="text"/>, deduplicated, in order.</summary>
    public static IReadOnlyList<string> Candidates(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var found = new List<string>();

        foreach (Match match in Procedural.Matches(text))
        {
            var name = match.Groups[1].Value.Trim();

            if (seen.Add(name))
            {
                found.Add(name);
            }
        }

        return found;
    }
}

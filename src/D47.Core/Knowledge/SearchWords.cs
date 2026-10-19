using System.Globalization;

namespace D47.Core.Knowledge;

/// <summary>One readable line for a search tool and its arguments.</summary>
public static class SearchWords
{
    private static readonly HashSet<string> NotFilters = new(StringComparer.Ordinal) { "near", "limit" };

    /// <summary>The kind, the filters and the reference, such as "Stations · Interstellar Factors · near Sol".</summary>
    public static string Describe(string tool, IReadOnlyDictionary<string, string> arguments, string? reference)
    {
        if (tool == "describe_system")
        {
            var system = reference ?? (arguments.TryGetValue("system", out var named) ? named : null);

            return string.IsNullOrWhiteSpace(system) ? "System" : $"System · {system}";
        }

        var parts = new List<string>
        {
            tool switch
            {
                "search_systems" => "Systems",
                "search_stations" => "Stations",
                "search_outfitting" => "Outfitting",
                "find_body" => "Bodies",
                "find_signal" => "Signals",
                _ => tool,
            },
        };

        foreach (var (name, value) in arguments)
        {
            if (NotFilters.Contains(name)
                || string.IsNullOrWhiteSpace(value)
                || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(name.Replace('_', ' '));
            }
            else if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                parts.Add($"{name.Replace('_', ' ')} {value}");
            }
            else
            {
                parts.AddRange(value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
            }
        }

        if (!string.IsNullOrWhiteSpace(reference))
        {
            parts.Add($"near {reference}");
        }

        return string.Join(" · ", parts);
    }
}

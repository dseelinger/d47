using System.Text;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Which mission types the board has and the circumstances their names were recorded under.</summary>
internal static class MissionTypes
{
    private const int NearestCount = 3;

    private const string Caveat = "These are the likeliest circumstances, not a promise that a board offers one.";

    public static string Describe(string? type, RankState? ranks)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return DescribeGroups();
        }

        var names = Resolve(type);

        return names.Count == 0 ? DescribeUnknown(type) : DescribeType(names, ranks);
    }

    /// <summary>The names a spoken or internal mission type covers: by family, by spoken words, or by one exact name.</summary>
    public static List<MissionName> Resolve(string type)
    {
        var key = Fold(type);

        return MissionCatalogue.All
            .Where(name => Fold(name.Family) == key
                           || Fold(name.Family) == "mission" + key
                           || Fold(name.Words) == key
                           || Fold(name.Name) == key)
            .ToList();
    }

    private static string DescribeGroups()
    {
        var lines = new List<string> { "The mission board has these groups and types." };

        foreach (var group in MissionCatalogue.All.GroupBy(name => name.Group).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var words = group.Select(name => name.Words).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase);
            lines.Add($"{group.Key}: {string.Join(", ", words)}.");
        }

        return string.Join('\n', lines);
    }

    private static string DescribeType(List<MissionName> names, RankState? ranks)
    {
        var words = names.Select(name => name.Words).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var groups = names.Select(name => name.Group).Distinct(StringComparer.Ordinal).ToList();
        var said = new StringBuilder($"{SpokenText.Capitalised(JoinAnd(words))} missions are in the {JoinAnd(groups)} group. ");

        var states = Distinct(names, name => name.State);
        var governments = Distinct(names, name => name.Government);
        var economies = Distinct(names, name => name.Economy);
        var navies = Distinct(names, name => name.Rank);
        var engineers = names.Any(name => name.Engineer.Length > 0);

        if (states.Count + governments.Count + economies.Count + navies.Count == 0 && !engineers)
        {
            return said.Append("Their names record no circumstances, so I cannot say where one turns up.").ToString();
        }

        if (states.Count > 0)
        {
            said.Append($"They turn up where a faction is in {JoinAnd(states)}. ");
        }

        if (governments.Count > 0)
        {
            said.Append($"They turn up under {JoinAnd(governments)} governments. ");
        }

        if (economies.Count > 0)
        {
            said.Append($"They turn up in {JoinAnd(economies)} economies. ");
        }

        foreach (var navy in navies)
        {
            said.Append($"Some need {(navy == "Empire" ? "an" : "a")} {navy} navy rank. {Holding(navy, ranks)} ");
        }

        if (engineers)
        {
            said.Append("Some come from engineer bases. ");
        }

        return said.Append(Caveat).ToString();
    }

    private static string Holding(string navy, RankState? ranks)
    {
        if (ranks is not { IsKnown: true })
        {
            return "I do not know your ranks yet.";
        }

        return ranks.For(navy) is { Rank: > 0 } standing
            ? $"You hold {standing.Say(withPercent: false)}."
            : $"You hold no {navy} rank.";
    }

    /// <summary>That no mission type has this name, with the nearest that do.</summary>
    public static string DescribeUnknown(string type)
    {
        var key = Fold(type);
        var nearest = MissionCatalogue.All
            .Select(name => name.Words)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(words => Fold(words).Contains(key, StringComparison.Ordinal) || key.Contains(Fold(words), StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(words => Distance(Fold(words), key))
            .ThenBy(words => words, StringComparer.Ordinal)
            .Take(NearestCount);

        return $"I do not know a mission type called {type.Trim()}. The nearest are {JoinAnd(nearest.ToList())}.";
    }

    private static List<string> Distinct(IEnumerable<MissionName> names, Func<MissionName, string> pick) =>
        names.Select(pick).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase).ToList();

    private static string Fold(string text) => new([.. text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);

    public static string JoinAnd(IReadOnlyList<string> items) => items.Count switch
    {
        0 => string.Empty,
        1 => items[0],
        _ => $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}",
    };

    private static int Distance(string a, string b)
    {
        var row = Enumerable.Range(0, b.Length + 1).ToArray();

        for (var i = 1; i <= a.Length; i++)
        {
            var diagonal = row[0];
            row[0] = i;

            for (var j = 1; j <= b.Length; j++)
            {
                var above = row[j];
                row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), diagonal + (a[i - 1] == b[j - 1] ? 0 : 1));
                diagonal = above;
            }
        }

        return row[b.Length];
    }
}

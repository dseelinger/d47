using D47.Core.Configuration;
using D47.Core.Interface;

namespace D47.App.Tests;

/// <summary>Checks written paths against the settings layout and the destinations a furnished panel declares.</summary>
internal sealed class PanelPathChecker(
    IReadOnlyList<PanelDestination> destinations, IReadOnlyDictionary<PanelTab, string> tabWords)
{
    private const string SettingsWord = "Settings";

    /// <summary>Why the path is not a real place, or null when it is one. The last part may carry words after its title.</summary>
    public string? Failure(WrittenPath written)
    {
        var path = Completed(written);
        var (word, first) = FirstPart(path.Parts[0]);

        if (word is null)
        {
            return $"'{path.Parts[0]}' is not a tab or Settings";
        }

        var parts = new List<string> { first! };
        parts.AddRange(path.Parts.Skip(1));

        return string.Equals(word, SettingsWord, StringComparison.Ordinal)
            ? SettingsFailure(parts[1..], path.Group)
            : TabFailure(word, parts, path.Group);
    }

    /// <summary>The path as it is checked, with the first part cut to the tab word it ends with.</summary>
    public string Key(WrittenPath written)
    {
        var path = Completed(written);
        var (word, _) = FirstPart(path.Parts[0]);

        return word is null ? path.ToString() : new WrittenPath([word, .. path.Parts.Skip(1)], path.Group).ToString();
    }

    /// <summary>
    /// A path whose last part stopped at the comma inside a place title, such as "Memory, notes and debrief", read again
    /// with the whole title, and the group that follows it.
    /// </summary>
    private static WrittenPath Completed(WrittenPath path)
    {
        var last = path.Parts[^1];
        var text = last + path.Following;

        var title = SettingsLayout.Areas
            .SelectMany(area => area.Places)
            .Select(place => place.Title)
            .FirstOrDefault(candidate => candidate.Contains(',', StringComparison.Ordinal)
                && candidate.StartsWith(last, StringComparison.OrdinalIgnoreCase)
                && text.StartsWith(candidate, StringComparison.OrdinalIgnoreCase));

        if (title is null)
        {
            return path;
        }

        var after = text[title.Length..];
        var group = after.StartsWith(", group ", StringComparison.Ordinal)
            ? PanelPathReader.GroupIn(after)
            : null;

        return new WrittenPath([.. path.Parts.Take(path.Parts.Count - 1), title], group, after);
    }

    private (string? Word, string? Part) FirstPart(string written)
    {
        var word = tabWords.Values.Append(SettingsWord)
            .Where(known => EndsWith(written, known))
            .OrderByDescending(known => known.Length)
            .FirstOrDefault();

        return (word, word);
    }

    private static bool EndsWith(string text, string word) =>
        text.EndsWith(word, StringComparison.OrdinalIgnoreCase)
        && (text.Length == word.Length || text[text.Length - word.Length - 1] == ' ');

    private static bool Names(string written, string title) =>
        written.StartsWith(title, StringComparison.OrdinalIgnoreCase)
        && (written.Length == title.Length || written[title.Length] is ' ' or '\'' or '’');

    private static string? SettingsFailure(IReadOnlyList<string> rest, string? group)
    {
        if (rest.Count == 0)
        {
            return null;
        }

        var all = SettingsLayout.Areas.SelectMany(area => area.Places.Select(place => (area, place))).ToList();
        var last = rest.Count - 1;

        IEnumerable<SettingsPlace> places = rest.Count switch
        {
            1 => all.Where(pair => Names(rest[0], pair.place.Title)).Select(pair => pair.place),
            2 => all.Where(pair => Exact(rest[0], pair.area.Title) && Names(rest[1], pair.place.Title)).Select(pair => pair.place),
            _ => [],
        };

        var matched = places.ToList();

        if (matched.Count > 0)
        {
            return group is null || matched.Any(place => place.Groups.Any(g => Names(group, g.Title)))
                ? null
                : $"'{group}' is not a group of {string.Join(" / ", matched.Select(place => place.Title))}";
        }

        var joined = string.Join(PanelPathReader.Separator, rest);

        if (group is null && SettingsLayout.Tabs.Any(tab => Names(joined, tab.Title)))
        {
            return null;
        }

        return $"'{rest[last]}' is not a place under Settings" + (rest.Count > 2 ? " (too many parts)" : "");
    }

    private string? TabFailure(string word, IReadOnlyList<string> parts, string? group)
    {
        if (group is not null)
        {
            return "a group is named on a tab path";
        }

        var tab = tabWords.First(pair => string.Equals(pair.Value, word, StringComparison.Ordinal)).Key;
        var onTab = destinations.Where(d => d.Tab == tab).ToList();

        if (parts.Count == 1)
        {
            return null;
        }

        var last = parts.Count == 2 ? 1 : 2;

        if (parts.Count > 3)
        {
            return "more than tab, root and sub-page";
        }

        var roots = onTab.Where(d => d.SubPage is null && Called(parts[1], d.Root, parts.Count == 2)).ToList();

        if (roots.Count == 0)
        {
            return $"'{parts[1]}' is not a root of {word}";
        }

        if (parts.Count == 2)
        {
            return null;
        }

        return onTab.Any(d => d.SubPage is { } page
                && roots.Any(root => root.Root.Key == d.Root.Key)
                && Called(parts[last], page, last: true))
            ? null
            : $"'{parts[last]}' is not a sub-page declared under {parts[1]}";
    }

    private static bool Exact(string written, string title) => string.Equals(written, title, StringComparison.OrdinalIgnoreCase);

    private static bool Called(string written, NavCrumb crumb, bool last) =>
        new[] { crumb.Word }.Concat(crumb.Spoken).Any(name => last ? Names(written, name) : Exact(written, name));
}

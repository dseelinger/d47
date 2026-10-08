namespace D47.Core.Interface;

/// <summary>A place in the panel named in a message: the characters that name it, its tab, and its root where the text names one.</summary>
public sealed record PanelPlace(int Start, int Length, PanelTab Tab, string? RootKey = null);

/// <summary>Finds the places in the panel a message names, so they can be drawn as links (#951).</summary>
public static class PanelPlaces
{
    private const string The = "the ";
    private const string TabWord = " tab";

    /// <summary>
    /// Every place <paramref name="text"/> names that <paramref name="nav"/> has, in order: a path such as
    /// "Asset Mgmt › Ships" or "Settings > About", resolved as far as its parts name a tab and a root of it;
    /// "Settings" as a capitalised word; and "the Navigation tab". <paramref name="words"/> holds each tab's
    /// word as the tab bar draws it.
    /// </summary>
    public static IReadOnlyList<PanelPlace> Find(
        string text, PanelNavigator nav, IReadOnlyDictionary<PanelTab, string> words)
    {
        var tabs = words
            .Where(pair => pair.Value.Length > 0 && nav.Has(pair.Key))
            .OrderByDescending(pair => pair.Value.Length)
            .ToList();

        var found = new List<PanelPlace>();

        if (tabs.Count == 0 || string.IsNullOrEmpty(text))
        {
            return found;
        }

        var at = 0;

        while (at < text.Length)
        {
            if (!StartsWord(text, at) || Place(text, at, nav, tabs) is not { } place)
            {
                at++;
                continue;
            }

            found.Add(place);
            at = place.Start + place.Length;
        }

        return found;
    }

    private static PanelPlace? Place(
        string text, int at, PanelNavigator nav, List<KeyValuePair<PanelTab, string>> tabs)
    {
        if (Matches(text, at, The))
        {
            var named = at + The.Length;

            foreach (var (tab, word) in tabs)
            {
                if (Matches(text, named, word)
                    && Matches(text, named + word.Length, TabWord)
                    && EndsWord(text, named + word.Length + TabWord.Length))
                {
                    return new PanelPlace(named, word.Length + TabWord.Length, tab);
                }
            }
        }

        foreach (var (tab, word) in tabs)
        {
            if (!Matches(text, at, word) || !EndsWord(text, at + word.Length))
            {
                continue;
            }

            var end = at + word.Length;

            if (Separator(text, end) is { } part)
            {
                return Longest(text, part, nav.Roots(tab)) is { } root
                    ? new PanelPlace(at, root.End - at, tab, root.Key)
                    : new PanelPlace(at, word.Length, tab);
            }

            return tab == PanelTab.Settings && char.IsUpper(text[at])
                ? new PanelPlace(at, word.Length, tab)
                : null;
        }

        return null;
    }

    /// <summary>Where the next part of a path begins, or null when no separator follows.</summary>
    private static int? Separator(string text, int at)
    {
        var index = Spaces(text, at);

        if (index >= text.Length || text[index] is not ('›' or '>'))
        {
            return null;
        }

        return Spaces(text, index + 1);
    }

    /// <summary>The root of the tab whose word or spoken name is longest at <paramref name="at"/>.</summary>
    private static (string Key, int End)? Longest(string text, int at, IReadOnlyList<NavCrumb> roots)
    {
        (string Key, int End)? best = null;

        foreach (var root in roots)
        {
            foreach (var name in root.Spoken.Prepend(root.Word))
            {
                var end = at + name.Length;

                if (name.Length > 0
                    && Matches(text, at, name)
                    && EndsWord(text, end)
                    && (best is null || end > best.Value.End))
                {
                    best = (root.Key, end);
                }
            }
        }

        return best;
    }

    private static int Spaces(string text, int at)
    {
        while (at < text.Length && text[at] is ' ' or ' ')
        {
            at++;
        }

        return at;
    }

    private static bool Matches(string text, int at, string word) =>
        at + word.Length <= text.Length
        && string.Compare(text, at, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) == 0;

    private static bool StartsWord(string text, int at) => at == 0 || !char.IsLetterOrDigit(text[at - 1]);

    private static bool EndsWord(string text, int at) => at >= text.Length || !char.IsLetterOrDigit(text[at]);
}

namespace D47.Core.Activities;

/// <summary>One activity a Commander can be asked about, with the words they use for it.</summary>
public sealed record ActivityEntry(string Key, string Name, IReadOnlyList<string> Phrases);

/// <summary>The fixed list of activities the ledger dates.</summary>
public static class ActivityCatalogue
{
    public static readonly IReadOnlyList<ActivityEntry> All =
    [
        new("mining", "Mining", ["mining", "mine", "laser mining", "core mining", "deep core mining", "cracking asteroids", "refining"]),
        new("exploration", "Exploration", ["exploration", "exploring", "explore", "mapping", "cartography", "selling exploration data"]),
        new("bounty-hunting", "Bounty hunting", ["bounty hunting", "bounties", "bounty hunt", "pirate hunting", "res sites"]),
        new("combat-zones", "Combat zones", ["combat zones", "combat zone", "conflict zones", "cz", "czs", "war zones"]),
        new("powerplay", "Powerplay", ["powerplay", "power play", "merits"]),
        new("on-foot", "On-foot work", ["on-foot work", "on foot", "settlement work", "collecting on foot"]),
        new("exobiology", "Exobiology", ["exobiology", "exobio", "biology", "bio scanning", "organic scans"]),
        new("trading", "Trading", ["trading", "trade", "trade runs", "hauling cargo"]),
        new("engineering", "Engineering", ["engineering", "crafting", "rolling blueprints", "upgrading modules"]),
        new("carrier", "Fleet carrier", ["fleet carrier", "carrier", "carrier jumps", "jumping the carrier"]),
        new("missions", "Missions", ["missions", "mission running"]),
        new("search-and-rescue", "Search and rescue", ["search and rescue", "sar", "rescue", "escape pods"]),
        new("fighter", "Ship-launched fighter", ["ship-launched fighter", "fighter", "slf", "fighter bay"]),
        new("colonisation", "Colonisation", ["colonisation", "colonization", "colony", "construction", "building a colony"]),
    ];

    /// <summary>The entry with <paramref name="key"/>, or null.</summary>
    public static ActivityEntry? ByKey(string key) => All.FirstOrDefault(entry => entry.Key == key);

    /// <summary>
    /// The entry whose key, name or phrase appears in <paramref name="text"/> as whole words, or null.
    /// Case-insensitive; the longest match wins.
    /// </summary>
    public static ActivityEntry? Find(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        ActivityEntry? best = null;
        var bestLength = 0;

        foreach (var entry in All)
        {
            foreach (var term in Terms(entry))
            {
                if (term.Length > bestLength && ContainsWords(text, term))
                {
                    best = entry;
                    bestLength = term.Length;
                }
            }
        }

        return best;
    }

    /// <summary>Every word the catalogue matches for <paramref name="entry"/>.</summary>
    public static IEnumerable<string> Terms(ActivityEntry entry) =>
        entry.Phrases.Append(entry.Key).Append(entry.Name);

    private static bool ContainsWords(string text, string term)
    {
        var from = 0;

        while (text.IndexOf(term, from, StringComparison.OrdinalIgnoreCase) is var at and >= 0)
        {
            var end = at + term.Length;

            if ((at == 0 || !char.IsLetterOrDigit(text[at - 1]))
                && (end == text.Length || !char.IsLetterOrDigit(text[end])))
            {
                return true;
            }

            from = at + 1;
        }

        return false;
    }
}

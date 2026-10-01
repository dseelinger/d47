using D47.Core.Adventures;
using D47.Core.Stories;

namespace D47.Core.Messages;

/// <summary>Whether the adventure or story a message is keyed to still exists.</summary>
public static class MessageOwnership
{
    /// <summary>A clue key is owned while its story is in the catalog; any other key while some Commander has that adventure.</summary>
    public static bool Owned(string key, AdventureStore adventures, StoryCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(adventures);
        ArgumentNullException.ThrowIfNull(catalog);

        if (key.StartsWith(StoryClueCallout.KeyPrefix, StringComparison.Ordinal))
        {
            return StoryClueCallout.Parse(key) is { } clue && catalog.Find(clue.StoryId) is not null;
        }

        return adventures.Commanders.Any(commander => adventures.Find(commander, key) is not null);
    }
}

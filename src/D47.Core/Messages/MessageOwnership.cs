using D47.Core.Adventures;
using D47.Core.Stories;

namespace D47.Core.Messages;

/// <summary>Whether the adventure or story a message is keyed to still exists.</summary>
public static class MessageOwnership
{
    /// <summary>
    /// A story's clue, ending and line keys are owned while the catalog has the story and some Commander's record of it
    /// is running, paused or finished. A chapter key listed by such a story is owned. Any other key is owned while some
    /// Commander has that adventure, unless the adventure is a chapter that no live story lists.
    /// </summary>
    public static bool Owned(string key, AdventureStore adventures, StoryStore stories, Func<StoryCatalog> catalog)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(adventures);
        ArgumentNullException.ThrowIfNull(stories);
        ArgumentNullException.ThrowIfNull(catalog);

        var live = stories.All()
            .Where(entry => entry.Story.State is StoryState.Running or StoryState.Paused or StoryState.Finished
                && catalog().Find(entry.Story.Id) is not null)
            .Select(entry => entry.Story)
            .ToList();

        if (key.StartsWith(StoryEnding.KeyPrefix, StringComparison.Ordinal))
        {
            return IsLive(live, key[StoryEnding.KeyPrefix.Length..]);
        }

        if (key.StartsWith(StoryLines.KeyPrefix, StringComparison.Ordinal))
        {
            return IsLive(live, key[StoryLines.KeyPrefix.Length..]);
        }

        if (key.StartsWith(StoryClueCallout.KeyPrefix, StringComparison.Ordinal))
        {
            return StoryClueCallout.Parse(key) is { } clue && IsLive(live, clue.StoryId);
        }

        if (live.Any(story => story.Chapters.Contains(key, StringComparer.OrdinalIgnoreCase)))
        {
            return true;
        }

        return adventures.Commanders.Any(commander => adventures.Find(commander, key) is { StoryId: null });
    }

    private static bool IsLive(List<Story> live, string storyId) =>
        live.Any(story => string.Equals(story.Id, storyId, StringComparison.OrdinalIgnoreCase));
}

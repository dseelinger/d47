namespace D47.Core.Conversation;

/// <summary>
/// The Commander's own account of themselves, in the two halves a real biography has (Phase 43, "The
/// sheet always, the story sometimes").
/// </summary>
public static class CommanderStory
{
    /// <summary>One ambient remark in this many carries the story.</summary>
    public const int StoryEvery = 4;

    /// <summary>Whether the call with this index carries the story as well as the sheet.</summary>
    public static bool TellsStory(int? variant) =>
        variant is { } index && Math.Abs(index) % StoryEvery == 0;

    /// <summary>The text for position 4, or null when there is nothing to say.</summary>
    public static string? Compose(string? sheet, string? story, bool withStory)
    {
        var hasSheet = !string.IsNullOrWhiteSpace(sheet);
        var hasStory = withStory && !string.IsNullOrWhiteSpace(story);

        if (!hasSheet && !hasStory)
        {
            return null;
        }

        if (hasSheet && hasStory)
        {
            return sheet!.Trim() + "\n\n" + story!.Trim();
        }

        return hasSheet ? sheet!.Trim() : story!.Trim();
    }
}

using D47.Core.Stories;

namespace D47.Core.Messages;

/// <summary>What Your story lists from the messages: the lines waiting for an answer, newest first.</summary>
public sealed record StoryLog(IReadOnlyList<D47Message> Waiting)
{
    /// <summary>Picks the lines of <paramref name="messages"/> that <paramref name="waiting"/> accepts, newest first.</summary>
    public static StoryLog Of(IEnumerable<D47Message> messages, Func<D47Message, bool> waiting)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(waiting);

        return new StoryLog([.. messages.OrderByDescending(message => message.Sent).Where(waiting)]);
    }

    /// <summary>The story a line, ending, reaction or clue key belongs to; null for any other key, a chapter's included.</summary>
    public static string? StoryIdOf(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key.StartsWith(StoryEnding.KeyPrefix, StringComparison.Ordinal))
        {
            return key[StoryEnding.KeyPrefix.Length..];
        }

        if (key.StartsWith(StoryLines.KeyPrefix, StringComparison.Ordinal))
        {
            return key[StoryLines.KeyPrefix.Length..];
        }

        if (StoryReactionCallout.Parse(key) is var (reaction, _))
        {
            return reaction.StoryId;
        }

        return StoryClueCallout.Parse(key)?.StoryId;
    }
}

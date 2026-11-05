using D47.Core.Stories;

namespace D47.Core.Messages;

/// <summary>What Your story lists from the messages: lines waiting for an answer, then the lines of no stock story run, both newest first.</summary>
public sealed record StoryLog(IReadOnlyList<D47Message> Waiting, IReadOnlyList<D47Message> Other)
{
    /// <summary>
    /// Splits <paramref name="messages"/>. <paramref name="waiting"/> picks the lines shown first; <paramref name="ofARun"/>
    /// picks the lines a stock story run's record keeps, which its page on the Story bench shows instead.
    /// </summary>
    public static StoryLog Of(IEnumerable<D47Message> messages, Func<D47Message, bool> ofARun, Func<D47Message, bool> waiting)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(ofARun);
        ArgumentNullException.ThrowIfNull(waiting);

        var newestFirst = messages.OrderByDescending(message => message.Sent).ToList();
        var waitingNow = newestFirst.Where(waiting).ToList();

        return new StoryLog(waitingNow, [.. newestFirst.Except(waitingNow).Where(message => !ofARun(message))]);
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

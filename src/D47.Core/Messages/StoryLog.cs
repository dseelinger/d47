using D47.Core.Goals;
using D47.Core.Stories;

namespace D47.Core.Messages;

/// <summary>One story's lines in the Story log, newest first.</summary>
public sealed record StoryLogGroup(string Name, IReadOnlyList<D47Message> Messages);

/// <summary>The Story log: lines waiting for an answer, then one group per story.</summary>
public sealed record StoryLog(IReadOnlyList<D47Message> Waiting, IReadOnlyList<StoryLogGroup> Groups)
{
    public const string EarlierStories = "Earlier stories";

    public const string NotTiedToAStory = "Not tied to a story";

    /// <summary>
    /// Groups <paramref name="messages"/> by story. <paramref name="waiting"/> picks the lines shown first and left
    /// out of their group. <paramref name="resolve"/> names the story for an adventure key as stored, or for
    /// <see cref="StoryGoal.KeyPrefix"/> plus the story id when the key is a story's line, ending or clue; it returns null
    /// for a key that no longer resolves, which is filed under Earlier stories. Groups run by their newest line, with Not tied to a story last.
    /// </summary>
    public static StoryLog Of(IEnumerable<D47Message> messages, Func<string, string?> resolve, Func<D47Message, bool> waiting)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(resolve);
        ArgumentNullException.ThrowIfNull(waiting);

        var newestFirst = messages.OrderByDescending(message => message.Sent).ToList();
        var waitingNow = newestFirst.Where(waiting).ToList();
        var rest = newestFirst.Except(waitingNow).ToList();

        var named = new Dictionary<string, List<D47Message>>(StringComparer.Ordinal);
        var order = new List<string>();
        var untied = new List<D47Message>();

        foreach (var message in rest)
        {
            if (message.AdventureKey is not { } key)
            {
                untied.Add(message);
            }
            else
            {
                var name = resolve(ResolverKey(key)) ?? EarlierStories;

                if (!named.TryGetValue(name, out var list))
                {
                    named[name] = list = [];
                    order.Add(name);
                }

                list.Add(message);
            }
        }

        var groups = order.Select(name => new StoryLogGroup(name, named[name])).ToList();

        if (untied.Count > 0)
        {
            groups.Add(new StoryLogGroup(NotTiedToAStory, untied));
        }

        return new StoryLog(waitingNow, groups);
    }

    private static string ResolverKey(string key)
    {
        if (key.StartsWith(StoryEnding.KeyPrefix, StringComparison.Ordinal))
        {
            return StoryGoal.KeyPrefix + key[StoryEnding.KeyPrefix.Length..];
        }

        if (key.StartsWith(StoryLines.KeyPrefix, StringComparison.Ordinal))
        {
            return StoryGoal.KeyPrefix + key[StoryLines.KeyPrefix.Length..];
        }

        return StoryClueCallout.Parse(key) is { } clue ? StoryGoal.KeyPrefix + clue.StoryId : key;
    }
}

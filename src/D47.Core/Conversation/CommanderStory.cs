namespace D47.Core.Conversation;

/// <summary>
/// The Commander's own account of themselves, in the two halves a real biography has (Phase 43,
/// "The sheet always, the story sometimes").
/// <para>
/// A <b>character sheet</b> — name, origin, year, build, accent — is some forty tokens, stable,
/// and relevant to nearly anything said. A <b>story</b> runs to thirteen hundred and is relevant
/// occasionally. In a turn the distinction barely matters, because position 4 is cached; in a
/// flavour call there is no such shelter, and thirteen hundred tokens carried to produce one
/// sentence about a docking bay would make ambient remarks the most expensive thing d47 does.
/// So the sheet goes every time and the story goes one call in <see cref="StoryEvery"/>.
/// </para>
/// <para>
/// <b>Which call is decided by an index the caller supplies, never by a clock or a seed.</b>
/// It is the same index <see cref="Callouts.AmbientLines.Pick"/> is given, for the same reason:
/// no Core component reads a clock or a random source, and a recorded session has to replay to
/// the line it produced live.
/// </para>
/// </summary>
public static class CommanderStory
{
    /// <summary>
    /// One ambient remark in this many carries the story. At the default fifteen-minute interval
    /// that is about once an hour, which is "occasionally" written as a number.
    /// </summary>
    public const int StoryEvery = 4;

    /// <summary>
    /// Whether the call with this index carries the story as well as the sheet. Null — a line
    /// that was never one of a numbered set — is never a story call: the sheet is the default
    /// and the story is the exception.
    /// </summary>
    public static bool TellsStory(int? variant) =>
        variant is { } index && Math.Abs(index) % StoryEvery == 0;

    /// <summary>
    /// The text for position 4, or null when there is nothing to say. The sheet first, because it
    /// is the half that is always present, then the story when asked for. Either half blank is
    /// simply absent rather than a heading over nothing.
    /// </summary>
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

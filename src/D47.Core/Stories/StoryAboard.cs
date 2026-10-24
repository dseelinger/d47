namespace D47.Core.Stories;

/// <summary>Which of a story's cast are aboard the Commander's ship, and so heard in the cockpit rather than over comms.</summary>
public static class StoryAboard
{
    /// <summary>
    /// The cast ids aboard for a chapter at <paramref name="stage"/>: each member whose <see cref="StorySpeaker.Aboard"/>
    /// span takes in any beat the chapter writer is handed at that stage (<see cref="StoryClues.BeatKeys"/>). A stage
    /// hands out its beats together, so a span that starts or ends inside one covers that whole stage's chapters.
    /// </summary>
    public static IReadOnlyList<string> Of(StorySecret secret, StoryPacing pacing, StoryStage stage, bool beaconInReach, int? finaleChapter)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(pacing);

        var handed = StoryClues.BeatKeys(pacing, stage, beaconInReach, finaleChapter).Select(Order).Where(at => at >= 0).ToList();

        if (handed.Count == 0)
        {
            return [];
        }

        var first = handed.Min();
        var last = handed.Max();

        return
        [
            .. secret.Cast
                .Where(member => member.Aboard is { } aboard
                    && (aboard.Boards is not { } boards || Order(boards) <= last)
                    && (aboard.Leaves is not { } leaves || Order(leaves) > first))
                .Select(member => member.Id),
        ];
    }

    /// <summary>A beat key's place in story order, or -1 for a key that is not a beat.</summary>
    public static int Order(string beat)
    {
        for (var at = 0; at < StoryBeats.Keys.Count; at++)
        {
            if (string.Equals(StoryBeats.Keys[at], beat, StringComparison.Ordinal))
            {
                return at;
            }
        }

        return -1;
    }
}

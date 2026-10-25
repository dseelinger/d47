using D47.Core.Adventures;

namespace D47.Core.Stories;

/// <summary>A boarding or leaving in a cast member's span that a chapter may place: the member, the direction and the beat.</summary>
public sealed record StorySwitch(string Id, bool Boards, string Beat);

/// <summary>Which of a story's cast are aboard the Commander's ship, and so heard in the cockpit rather than over comms.</summary>
public static class StoryAboard
{
    /// <summary>The cast ids aboard by span: each member with a span whose boarding has happened, or who boards at the start, and whose leaving has not.</summary>
    public static IReadOnlyList<string> BySpan(StorySecret secret, Story story)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(story);

        return [.. secret.Cast.Where(member => IsAboard(member, story)).Select(member => member.Id)];
    }

    /// <summary>
    /// The cast ids aboard at <paramref name="beat"/> of a chapter, -1 for the opening: its <see cref="Adventure.Aboard"/>, plus
    /// each boarding and minus each leaving at the opening and at every beat up to and including <paramref name="beat"/>.
    /// </summary>
    public static IReadOnlyList<string> At(Adventure chapter, int beat)
    {
        ArgumentNullException.ThrowIfNull(chapter);

        var aboard = new List<string>(chapter.Aboard ?? []);

        Apply(chapter.OpeningBoards, chapter.OpeningLeaves);

        for (var at = 0; at <= beat && at < chapter.Beats.Count; at++)
        {
            Apply(chapter.Beats[at].Boards, chapter.Beats[at].Leaves);
        }

        return aboard;

        void Apply(IReadOnlyList<string> boards, IReadOnlyList<string> leaves)
        {
            aboard.AddRange(boards.Where(id => !aboard.Contains(id, StringComparer.Ordinal)));
            aboard.RemoveAll(id => leaves.Contains(id, StringComparer.Ordinal));
        }
    }

    /// <summary>
    /// The boardings and leavings a chapter handed <paramref name="handed"/> may place: each not yet happened whose beat is handed
    /// out, a leaving only for a member aboard by span or boarding in the same chapter.
    /// </summary>
    public static IReadOnlyList<StorySwitch> Due(StorySecret secret, Story story, IReadOnlyList<string> handed)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(story);
        ArgumentNullException.ThrowIfNull(handed);

        var boarding = secret.Cast
            .Where(member => member.Aboard is { Boards: { } boards } && !story.Boarded.Contains(member.Id, StringComparer.Ordinal) && handed.Contains(boards, StringComparer.Ordinal))
            .Select(member => new StorySwitch(member.Id, true, member.Aboard!.Boards!))
            .ToList();

        var leaving = secret.Cast
            .Where(member => member.Aboard is { Leaves: { } leaves } && !story.Left.Contains(member.Id, StringComparer.Ordinal) && handed.Contains(leaves, StringComparer.Ordinal)
                && (IsAboard(member, story) || boarding.Any(due => due.Id == member.Id)))
            .Select(member => new StorySwitch(member.Id, false, member.Aboard!.Leaves!));

        return [.. boarding, .. leaving];
    }

    /// <summary>
    /// The story with each boarding and leaving a chapter handed <paramref name="handed"/> has passed applied: one not yet
    /// happened whose beat comes before every beat handed out. Nothing is applied for a chapter handed no beats, or for a
    /// beat every finale chapter is handed.
    /// </summary>
    public static Story Missed(Story story, StorySecret secret, IReadOnlyList<string> handed)
    {
        ArgumentNullException.ThrowIfNull(story);
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(handed);

        var orders = handed.Select(Order).Where(at => at >= 0).ToList();

        if (orders.Count == 0)
        {
            return story;
        }

        var first = orders.Min();
        var everyFinale = StoryClues.BeatKeys(story.Pacing, StoryStage.Finale, true, 1)
            .Intersect(StoryClues.BeatKeys(story.Pacing, StoryStage.Finale, true, story.Pacing.FinaleChapters), StringComparer.Ordinal)
            .ToList();

        bool Passed(string? beat) => beat is not null && Order(beat) is var at && at >= 0 && at < first && !everyFinale.Contains(beat, StringComparer.Ordinal);

        var boarded = secret.Cast.Where(member => Passed(member.Aboard?.Boards)).Select(member => member.Id);
        var left = secret.Cast.Where(member => Passed(member.Aboard?.Leaves)).Select(member => member.Id);

        return With(story, boarded, left);
    }

    /// <summary>The story with a chapter's boardings and leavings folded in: those at its opening and at each of its first <paramref name="fired"/> beats.</summary>
    public static Story Folded(Story story, Adventure chapter, int fired)
    {
        ArgumentNullException.ThrowIfNull(story);
        ArgumentNullException.ThrowIfNull(chapter);

        var beats = chapter.Beats.Take(Math.Max(fired, 0)).ToList();

        return With(story, [.. chapter.OpeningBoards, .. beats.SelectMany(beat => beat.Boards)], [.. chapter.OpeningLeaves, .. beats.SelectMany(beat => beat.Leaves)]);
    }

    /// <summary>
    /// The chapter with only the boardings and leavings in <paramref name="due"/> kept, in the cast's spelling, each at its first
    /// place; a leaving is kept only for a member aboard as the chapter opens or boarding at an earlier beat.
    /// </summary>
    public static Adventure Placed(Adventure chapter, IReadOnlyList<StorySwitch> due)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        ArgumentNullException.ThrowIfNull(due);

        var aboard = new HashSet<string>(chapter.Aboard ?? [], StringComparer.Ordinal);
        var boarded = new HashSet<string>(StringComparer.Ordinal);
        var left = new HashSet<string>(StringComparer.Ordinal);

        var (openingBoards, openingLeaves) = Keep(chapter.OpeningBoards, chapter.OpeningLeaves);
        var beats = new List<AdventureBeat>();

        foreach (var beat in chapter.Beats)
        {
            var (boards, leaves) = Keep(beat.Boards, beat.Leaves);
            beats.Add(beat with { Boards = boards, Leaves = leaves });
        }

        return chapter with { OpeningBoards = openingBoards, OpeningLeaves = openingLeaves, Beats = beats };

        (IReadOnlyList<string> Boards, IReadOnlyList<string> Leaves) Keep(IReadOnlyList<string> boards, IReadOnlyList<string> leaves)
        {
            var keptLeaves = leaves
                .Select(id => Match(id, false))
                .OfType<string>()
                .Where(id => (aboard.Contains(id) || boarded.Contains(id)) && left.Add(id))
                .ToList();
            var keptBoards = boards
                .Select(id => Match(id, true))
                .OfType<string>()
                .Where(id => !left.Contains(id) && boarded.Add(id))
                .ToList();

            return (keptBoards, keptLeaves);
        }

        string? Match(string written, bool boards) =>
            due.FirstOrDefault(entry => entry.Boards == boards && string.Equals(entry.Id, written.Trim(), StringComparison.OrdinalIgnoreCase))?.Id;
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

    private static bool IsAboard(StorySpeaker member, Story story) =>
        member.Aboard is { } aboard
        && (aboard.Boards is null || story.Boarded.Contains(member.Id, StringComparer.Ordinal))
        && !(aboard.Leaves is not null && story.Left.Contains(member.Id, StringComparer.Ordinal));

    private static Story With(Story story, IEnumerable<string> boarded, IEnumerable<string> left) => story with
    {
        Boarded = [.. story.Boarded.Union(boarded, StringComparer.Ordinal)],
        Left = [.. story.Left.Union(left, StringComparer.Ordinal)],
    };
}

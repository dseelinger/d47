using D47.Core.Adventures;
using D47.Core.Knowledge;

namespace D47.Core.Stories;

/// <summary>
/// A boarding or leaving in a cast member's span that a chapter may place: the member, the direction and the beat;
/// <paramref name="Passed"/> for a boarding held for a seat whose beat has passed; <paramref name="Seats"/> for a boarding the
/// current ship has no seat for, the crew a hull needs to seat it.
/// </summary>
public sealed record StorySwitch(string Id, bool Boards, string Beat, bool Passed = false, int? Seats = null);

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
    /// The boardings and leavings a chapter handed <paramref name="handed"/> may place: each boarding not yet happened whose beat is
    /// handed out or that is held for a seat, those held first, with <see cref="StorySwitch.Seats"/> set on each that <paramref name="hull"/> has
    /// no free seat for; and each leaving
    /// not yet happened whose beat is handed out, only for a member aboard by span or boarding in the same chapter.
    /// </summary>
    public static IReadOnlyList<StorySwitch> Due(StorySecret secret, Story story, IReadOnlyList<string> handed, string? hull = null)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(story);
        ArgumentNullException.ThrowIfNull(handed);

        bool Held(StorySpeaker member) => story.AwaitingSeat.Contains(member.Id, StringComparer.Ordinal);

        var free = FreeSeats(secret, story, hull);
        var aboard = BySpan(secret, story).Count;

        var boarding = secret.Cast
            .Where(member => member.Aboard is { Boards: { } boards }
                && !story.Boarded.Contains(member.Id, StringComparer.Ordinal)
                && (handed.Contains(boards, StringComparer.Ordinal) || (Held(member) && !story.Left.Contains(member.Id, StringComparer.Ordinal))))
            .OrderBy(member => Held(member) ? 0 : 1)
            .ThenBy(member => Held(member) ? story.AwaitingSeat.ToList().IndexOf(member.Id) : Order(member.Aboard!.Boards!))
            .Select((member, index) => new StorySwitch(
                member.Id,
                true,
                member.Aboard!.Boards!,
                Held(member) && !handed.Contains(member.Aboard.Boards!, StringComparer.Ordinal),
                index < free ? null : 2 + aboard + index))
            .ToList();

        var leaving = secret.Cast
            .Where(member => member.Aboard is { Leaves: { } leaves } && !story.Left.Contains(member.Id, StringComparer.Ordinal) && handed.Contains(leaves, StringComparer.Ordinal)
                && (IsAboard(member, story) || boarding.Any(due => due.Id == member.Id)))
            .Select(member => new StorySwitch(member.Id, false, member.Aboard!.Leaves!));

        return [.. boarding, .. leaving];
    }

    /// <summary>
    /// The story with each boarding and leaving a chapter handed <paramref name="handed"/> has passed applied: one not yet
    /// happened whose beat comes before every beat handed out. Leavings first, then boardings in beat order: a member is boarded
    /// while <paramref name="hull"/> has a free seat and otherwise held in <see cref="Story.AwaitingSeat"/>. Nothing is applied for a
    /// chapter handed no beats, or for a beat every finale chapter is handed.
    /// </summary>
    public static Story Missed(Story story, StorySecret secret, IReadOnlyList<string> handed, string? hull = null)
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

        var left = secret.Cast.Where(member => Passed(member.Aboard?.Leaves)).Select(member => member.Id);
        var result = With(story, [], left);

        foreach (var member in secret.Cast.Where(member => Passed(member.Aboard?.Boards)).OrderBy(member => Order(member.Aboard!.Boards!)))
        {
            if (result.AwaitingSeat.Contains(member.Id, StringComparer.Ordinal))
            {
                continue;
            }

            if (result.Left.Contains(member.Id, StringComparer.Ordinal) || FreeSeats(secret, result, hull) > 0)
            {
                result = With(result, [member.Id], []);
            }
            else if (!result.Boarded.Contains(member.Id, StringComparer.Ordinal))
            {
                result = result with { AwaitingSeat = [.. result.AwaitingSeat, member.Id] };
            }
        }

        return result;
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
    /// place; a leaving is kept only for a member aboard as the chapter opens or boarding at an earlier beat. A boarding with
    /// <see cref="StorySwitch.Seats"/> is kept only on or after the first objective that boards a hull with that many seats.
    /// </summary>
    public static Adventure Placed(Adventure chapter, IReadOnlyList<StorySwitch> due)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        ArgumentNullException.ThrowIfNull(due);

        var aboard = new HashSet<string>(chapter.Aboard ?? [], StringComparer.Ordinal);
        var boarded = new HashSet<string>(StringComparer.Ordinal);
        var left = new HashSet<string>(StringComparer.Ordinal);

        var crew = 0;

        var (openingBoards, openingLeaves) = Keep(chapter.OpeningBoards, chapter.OpeningLeaves);
        var beats = new List<AdventureBeat>();

        foreach (var beat in chapter.Beats)
        {
            crew = beat.Steps
                .Where(step => step.Trigger.Kind == TriggerKind.Board)
                .Select(step => EliteSpecifications.Ship(step.Trigger.ShipType)?.Crew ?? int.MaxValue)
                .Prepend(crew)
                .Max();
            var (boards, leaves) = Keep(beat.Boards, beat.Leaves);
            beats.Add(beat with { Boards = boards, Leaves = leaves });
        }

        return chapter with { OpeningBoards = openingBoards, OpeningLeaves = openingLeaves, Beats = beats };

        (IReadOnlyList<string> Boards, IReadOnlyList<string> Leaves) Keep(IReadOnlyList<string> boards, IReadOnlyList<string> leaves)
        {
            var keptLeaves = leaves
                .Select(id => Match(id, false)?.Id)
                .OfType<string>()
                .Where(id => (aboard.Contains(id) || boarded.Contains(id)) && left.Add(id))
                .ToList();
            var keptBoards = boards
                .Select(id => Match(id, true))
                .OfType<StorySwitch>()
                .Where(entry => (entry.Seats ?? 0) <= crew)
                .Select(entry => entry.Id)
                .Where(id => !left.Contains(id) && boarded.Add(id))
                .ToList();

            return (keptBoards, keptLeaves);
        }

        StorySwitch? Match(string written, bool boards) =>
            due.FirstOrDefault(entry => entry.Boards == boards && string.Equals(entry.Id, written.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Of <paramref name="aboard"/>, those <paramref name="hull"/> seats besides the Commander, in <paramref name="aboard"/>'s order.
    /// Seats go first to those in <paramref name="opening"/>, then to the rest, each group in cast order. Everyone is seated in a
    /// hull with no known crew.
    /// </summary>
    public static IReadOnlyList<string> Seated(StorySecret secret, IReadOnlyList<string> aboard, IReadOnlyCollection<string> opening, string? hull)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(aboard);
        ArgumentNullException.ThrowIfNull(opening);

        if (EliteSpecifications.Ship(hull)?.Crew is not { } crew)
        {
            return aboard;
        }

        var seated = aboard
            .OrderBy(id => opening.Contains(id, StringComparer.Ordinal) ? 0 : 1)
            .ThenBy(id => CastOrder(secret, id))
            .Take(Math.Max(crew - 1, 0))
            .ToHashSet(StringComparer.Ordinal);

        return [.. aboard.Where(seated.Contains)];
    }

    /// <summary>Of <paramref name="aboard"/>, those <see cref="Seated"/> leaves without a seat in <paramref name="hull"/>, heard over comms.</summary>
    public static IReadOnlyList<string> Seatless(StorySecret secret, IReadOnlyList<string> aboard, IReadOnlyCollection<string> opening, string? hull)
    {
        var seated = Seated(secret, aboard, opening, hull);

        return [.. aboard.Where(id => !seated.Contains(id, StringComparer.Ordinal))];
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

    private static int CastOrder(StorySecret secret, string id)
    {
        for (var at = 0; at < secret.Cast.Count; at++)
        {
            if (string.Equals(secret.Cast[at].Id, id, StringComparison.Ordinal))
            {
                return at;
            }
        }

        return int.MaxValue;
    }

    private static int FreeSeats(StorySecret secret, Story story, string? hull) =>
        EliteSpecifications.Ship(hull)?.Crew is { } crew
            ? Math.Max(crew - 1 - BySpan(secret, story).Count, 0)
            : int.MaxValue;

    private static bool IsAboard(StorySpeaker member, Story story) =>
        member.Aboard is { } aboard
        && (aboard.Boards is null || story.Boarded.Contains(member.Id, StringComparer.Ordinal))
        && !(aboard.Leaves is not null && story.Left.Contains(member.Id, StringComparer.Ordinal));

    private static Story With(Story story, IEnumerable<string> boarded, IEnumerable<string> left) => story with
    {
        Boarded = [.. story.Boarded.Union(boarded, StringComparer.Ordinal)],
        Left = [.. story.Left.Union(left, StringComparer.Ordinal)],
        AwaitingSeat = [.. story.AwaitingSeat.Except(boarded, StringComparer.Ordinal)],
    };
}

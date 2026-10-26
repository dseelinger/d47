using System.Text.Json.Serialization;
using D47.Core.Adventures;
using D47.Core.Knowledge;

namespace D47.Core.Stories;

/// <summary>
/// A boarding or leaving in a cast member's span that a chapter may place: the member, the direction and the beat;
/// <paramref name="Passed"/> for a boarding held for a seat whose beat has passed; <paramref name="Seats"/> for a boarding the
/// current ship has no seat for, the crew a hull needs to seat it; <paramref name="Chapter"/> for one that has happened, the key of
/// the chapter it was folded in from, or null for one applied as passed.
/// </summary>
public sealed record StorySwitch(
    string Id,
    bool Boards,
    string Beat,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Passed = false,
    int? Seats = null,
    string? Chapter = null);

/// <summary>Which of a story's cast are aboard the Commander's ship, and so heard in the cockpit rather than over comms.</summary>
public static class StoryAboard
{
    /// <summary>
    /// The cast ids aboard by span: each member whose latest switch that has happened, in span order, is a boarding, or who
    /// boards at the start and has no switch that has happened.
    /// </summary>
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
    /// The boardings and leavings a chapter handed <paramref name="handed"/> may place, each a member's next switch: a boarding
    /// whose beat is handed out or that is held for a seat, those held first, with <see cref="StorySwitch.Seats"/> set on each that
    /// <paramref name="hull"/> has no free seat for; and a leaving whose beat is handed out, of the open span of a member aboard by
    /// span or of the span a boarding here opens.
    /// </summary>
    public static IReadOnlyList<StorySwitch> Due(StorySecret secret, Story story, IReadOnlyList<string> handed, string? hull = null)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(story);
        ArgumentNullException.ThrowIfNull(handed);

        bool Held(StorySpeaker member) => story.AwaitingSeat.Contains(member.Id, StringComparer.Ordinal);

        var free = FreeSeats(secret, story, hull);
        var aboard = BySpan(secret, story).Count;
        var next = secret.Cast.Select(member => (Member: member, At: Latest(member, story) + 1)).ToList();

        var boarding = next
            .Where(entry => Switches(entry.Member).ElementAtOrDefault(entry.At) is { Boards: true } due
                && (handed.Contains(due.Beat, StringComparer.Ordinal) || Held(entry.Member)))
            .Select(entry => (entry.Member, entry.At, Switches(entry.Member)[entry.At].Beat))
            .OrderBy(entry => Held(entry.Member) ? 0 : 1)
            .ThenBy(entry => Held(entry.Member) ? story.AwaitingSeat.ToList().IndexOf(entry.Member.Id) : Order(entry.Beat))
            .Select((entry, index) => (entry.At, Switch: new StorySwitch(
                entry.Member.Id,
                true,
                entry.Beat,
                Held(entry.Member) && !handed.Contains(entry.Beat, StringComparer.Ordinal),
                index < free ? null : 2 + aboard + index)))
            .ToList();

        var leaving = next
            .Select(entry => boarding.FirstOrDefault(due => due.Switch.Id == entry.Member.Id) is { Switch: not null } boards
                ? Switches(entry.Member).ElementAtOrDefault(boards.At + 1)
                : Switches(entry.Member).ElementAtOrDefault(entry.At))
            .OfType<StorySwitch>()
            .Where(due => !due.Boards && handed.Contains(due.Beat, StringComparer.Ordinal));

        return [.. boarding.Select(entry => entry.Switch), .. leaving];
    }

    /// <summary>
    /// The story with each switch a chapter handed <paramref name="handed"/> has passed applied: a member's next switches, in span
    /// order, whose beats come before every beat handed out. Those up to a member's last passed leaving are recorded first, less a
    /// boarding held for a seat, which is skipped; then each last passed boarding, in beat order, is recorded while
    /// <paramref name="hull"/> has a free seat and otherwise held in <see cref="Story.AwaitingSeat"/>. Nothing is applied for a
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

        bool Passed(string beat) => Order(beat) is var at && at >= 0 && at < first && !everyFinale.Contains(beat, StringComparer.Ordinal);

        var result = CarriedOver(story, secret);
        var recorded = new List<StorySwitch>();
        var boardings = new List<StorySwitch>();

        foreach (var member in secret.Cast)
        {
            var passed = Switches(member).Skip(Latest(member, result) + 1).TakeWhile(due => Passed(due.Beat)).ToList();

            if (passed.Count == 0)
            {
                continue;
            }

            var held = result.AwaitingSeat.Contains(member.Id, StringComparer.Ordinal);
            var settled = passed[^1].Boards ? passed.Count - 1 : passed.Count;

            recorded.AddRange(passed.Take(settled).Where((due, at) => !(at == 0 && due.Boards && held)));

            if (passed[^1].Boards)
            {
                boardings.Add(passed[^1]);
            }
        }

        result = With(result, recorded);

        foreach (var boarding in boardings.OrderBy(due => Order(due.Beat)))
        {
            if (result.AwaitingSeat.Contains(boarding.Id, StringComparer.Ordinal))
            {
                continue;
            }

            result = FreeSeats(secret, result, hull) > 0
                ? With(result, [boarding])
                : result with { AwaitingSeat = [.. result.AwaitingSeat, boarding.Id] };
        }

        return result;
    }

    /// <summary>
    /// The story with a chapter's boardings and leavings folded in: those at its opening and at each of its first
    /// <paramref name="fired"/> beats, in order, each recorded as the member's next switch when that is in the same direction.
    /// A switch this chapter already folded in is not recorded again.
    /// </summary>
    public static Story Folded(Story story, StorySecret secret, Adventure chapter, int fired)
    {
        ArgumentNullException.ThrowIfNull(story);
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(chapter);

        var result = CarriedOver(story, secret);
        var places = chapter.Beats.Take(Math.Max(fired, 0)).Select(beat => (beat.Boards, beat.Leaves)).Prepend((chapter.OpeningBoards, chapter.OpeningLeaves));

        foreach (var (boards, leaves) in places)
        {
            foreach (var (id, boarding) in boards.Select(id => (id, true)).Concat(leaves.Select(id => (id, false))))
            {
                if (secret.Cast.FirstOrDefault(member => member.Id == id) is not { } member
                    || result.Switched.Any(done => done.Id == id && done.Boards == boarding && done.Chapter == chapter.Key)
                    || Switches(member).ElementAtOrDefault(Latest(member, result) + 1) is not { } next
                    || next.Boards != boarding)
                {
                    continue;
                }

                result = With(result, [next with { Chapter = chapter.Key }]);
            }
        }

        return result;
    }

    /// <summary>
    /// The story with <see cref="Story.Boarded"/> and <see cref="Story.Left"/>, from a story saved before
    /// <see cref="Story.Switched"/>, carried over into it: each id in the first as that member's first-span boarding, each in the
    /// second as their first-span leaving. The story itself when it has neither.
    /// </summary>
    public static Story CarriedOver(Story story, StorySecret secret)
    {
        ArgumentNullException.ThrowIfNull(story);
        ArgumentNullException.ThrowIfNull(secret);

        if (story.Boarded is null && story.Left is null)
        {
            return story;
        }

        var carried = secret.Cast
            .SelectMany(member => Legacy(member, story))
            .Where(due => !story.Switched.Any(done => Same(done, due)));

        return story with { Switched = [.. story.Switched, .. carried], Boarded = null, Left = null };
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
        member.Aboard is { Count: > 0 } spans
        && (Latest(member, story) is var latest && latest >= 0 ? Switches(member)[latest].Boards : spans[0].Boards is null);

    /// <summary>A member's switches in span order: each span's boarding, unless it boards at the start, then its leaving, unless it leaves at the end.</summary>
    private static IReadOnlyList<StorySwitch> Switches(StorySpeaker member) =>
        [.. (member.Aboard ?? []).SelectMany(span => new[]
        {
            span.Boards is { } boards ? new StorySwitch(member.Id, true, boards) : null,
            span.Leaves is { } leaves ? new StorySwitch(member.Id, false, leaves) : null,
        }).OfType<StorySwitch>()];

    /// <summary>The index in <see cref="Switches"/> of the member's latest switch that has happened, or -1 for none.</summary>
    private static int Latest(StorySpeaker member, Story story)
    {
        var happened = story.Switched.Concat(Legacy(member, story)).Where(done => done.Id == member.Id).ToList();
        var switches = Switches(member);

        for (var at = switches.Count - 1; at >= 0; at--)
        {
            if (happened.Any(done => Same(done, switches[at])))
            {
                return at;
            }
        }

        return -1;
    }

    /// <summary>The member's first-span switches that <see cref="Story.Boarded"/> and <see cref="Story.Left"/> record.</summary>
    private static IEnumerable<StorySwitch> Legacy(StorySpeaker member, Story story)
    {
        if (member.Aboard is not [var span, ..])
        {
            yield break;
        }

        if (span.Boards is { } boards && story.Boarded?.Contains(member.Id, StringComparer.Ordinal) == true)
        {
            yield return new StorySwitch(member.Id, true, boards);
        }

        if (span.Leaves is { } leaves && story.Left?.Contains(member.Id, StringComparer.Ordinal) == true)
        {
            yield return new StorySwitch(member.Id, false, leaves);
        }
    }

    private static bool Same(StorySwitch one, StorySwitch other) =>
        one.Id == other.Id && one.Boards == other.Boards && one.Beat == other.Beat;

    /// <summary>The story with each of <paramref name="happened"/> not yet recorded added to <see cref="Story.Switched"/>, and each member it names off <see cref="Story.AwaitingSeat"/>.</summary>
    private static Story With(Story story, IReadOnlyList<StorySwitch> happened)
    {
        var added = happened
            .Select(due => new StorySwitch(due.Id, due.Boards, due.Beat, Chapter: due.Chapter))
            .Where(due => !story.Switched.Any(done => Same(done, due)))
            .ToList();

        return added.Count == 0 ? story : story with
        {
            Switched = [.. story.Switched, .. added],
            AwaitingSeat = [.. story.AwaitingSeat.Except(added.Select(due => due.Id), StringComparer.Ordinal)],
        };
    }
}

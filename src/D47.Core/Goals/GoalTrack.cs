using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;

namespace D47.Core.Goals;

/// <summary>One step of a goal: a rank, a Powerplay stretch or a story line. <paramref name="Weight"/> sets its width.</summary>
public sealed record GoalSegment(string Name, double Weight);

/// <summary>A run of <paramref name="Count"/> segments starting at <paramref name="First"/>, labelled on the track.</summary>
public sealed record GoalChapter(string Label, int First, int Count);

/// <summary>
/// A goal drawn as one track: its segments, their chapters, and where the Commander stands. Segments before
/// <see cref="Current"/> are passed; <see cref="Current"/> equal to the segment count means the goal is done.
/// </summary>
public sealed record GoalTrack
{
    public required IReadOnlyList<GoalSegment> Segments { get; init; }

    public required IReadOnlyList<GoalChapter> Chapters { get; init; }

    /// <summary>The index of the segment the Commander is in.</summary>
    public required int Current { get; init; }

    /// <summary>0 to 1 into the current segment, or null where the position within it is unknown.</summary>
    public double? Fill { get; init; }

    public bool IsDone => Current >= Segments.Count;

    public double TotalWeight => Segments.Sum(segment => segment.Weight);

    /// <summary>0 to 1 over the whole goal.</summary>
    public double Whole => IsDone
        ? 1
        : Math.Clamp((Segments.Take(Current).Sum(segment => segment.Weight) + Segments[Current].Weight * (Fill ?? 0)) / TotalWeight, 0, 1);

    /// <summary>Where segment <paramref name="index"/> starts, 0 to 1 over the whole goal.</summary>
    public double StartOf(int index) => Segments.Take(index).Sum(segment => segment.Weight) / TotalWeight;

    public double ShareOf(GoalChapter chapter) =>
        Segments.Skip(chapter.First).Take(chapter.Count).Sum(segment => segment.Weight) / TotalWeight;

    /// <summary>The chapter holding the current segment, or null when the goal is done.</summary>
    public GoalChapter? CurrentChapter => IsDone
        ? null
        : Chapters.FirstOrDefault(chapter => Current >= chapter.First && Current < chapter.First + chapter.Count);

    /// <summary>A career or navy ladder: one equal segment per rank below the top.</summary>
    public static GoalTrack Ladder(string career, int rank, double? rung, string top)
    {
        var count = new RankStanding(career, 0).Top;
        var segments = Enumerable.Range(0, count)
            .Select(index => new GoalSegment(new RankStanding(career, index).Describe(), 1))
            .ToList();

        var isCareer = new RankStanding(career, 0).IsCareer;

        List<GoalChapter> chapters = isCareer
            ? [new("To Elite", 0, RankStanding.Elite), new("To " + top, RankStanding.Elite, count - RankStanding.Elite)]
            : [new("To " + top, 0, count)];

        return new GoalTrack
        {
            Segments = segments,
            Chapters = chapters,
            Current = Math.Clamp(rank, 0, count),
            Fill = rank >= count ? null : rung,
        };
    }

    /// <summary>
    /// The Powerplay ladder: one segment for each rank from 2 to 10, then one for each of 11–20 up to 91–100,
    /// each as wide as the merits it takes. A segment is named for the rank it reaches.
    /// </summary>
    public static GoalTrack Powerplay(long merits)
    {
        var stretches = new List<(string Name, int From, int To)>();

        for (var rank = 2; rank <= 10; rank++)
        {
            stretches.Add(($"Rank {rank}", rank - 1, rank));
        }

        for (var start = 11; start <= 91; start += 10)
        {
            stretches.Add(($"Ranks {start}–{start + 9}", start - 1, start + 9));
        }

        var segments = stretches
            .Select(stretch => new GoalSegment(stretch.Name, Needed(stretch.To) - Needed(stretch.From)))
            .ToList();

        var current = stretches.FindIndex(stretch => merits < Needed(stretch.To));
        var done = current < 0;

        return new GoalTrack
        {
            Segments = segments,
            Chapters =
            [
                new("2–10", 0, 9),
                .. Enumerable.Range(0, 9).Select(index => new GoalChapter($"{11 + 10 * index}–{20 + 10 * index}", 9 + index, 1)),
            ],
            Current = done ? segments.Count : current,
            Fill = done
                ? null
                : Math.Clamp((merits - Needed(stretches[current].From)) / (double)(Needed(stretches[current].To) - Needed(stretches[current].From)), 0, 1),
        };
    }

    /// <summary>A story: one segment per clue and finale line, in chapters by stage and then the finale.</summary>
    public static GoalTrack Story(StoryPacing pacing, int linesGiven)
    {
        ArgumentNullException.ThrowIfNull(pacing);

        var clues = pacing.ClueDays.Count;
        var segments = new List<GoalSegment>();
        var chapters = new List<GoalChapter>();
        StoryStage? previous = null;

        for (var clue = 1; clue <= clues; clue++)
        {
            var stage = pacing.Stages[clue];

            if (stage != previous)
            {
                chapters.Add(new GoalChapter(StoryClues.StageName(stage), segments.Count, 0));
                previous = stage;
            }

            chapters[^1] = chapters[^1] with { Count = chapters[^1].Count + 1 };
            segments.Add(new GoalSegment($"Clue {clue}", 1));
        }

        chapters.Add(new GoalChapter(StoryClues.StageName(StoryStage.Finale), segments.Count, pacing.FinaleChapters));

        for (var chapter = 1; chapter <= pacing.FinaleChapters; chapter++)
        {
            segments.Add(new GoalSegment(pacing.FinaleChapters == 1 ? "Finale" : $"Finale chapter {chapter}", 1));
        }

        return new GoalTrack
        {
            Segments = segments,
            Chapters = chapters,
            Current = Math.Clamp(linesGiven, 0, segments.Count),
        };
    }

    private static int Needed(int rank) => PowerplayRanks.MeritsNeeded(rank) ?? 0;
}

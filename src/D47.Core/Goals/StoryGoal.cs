using System.Globalization;
using D47.Core.Adventures;
using D47.Core.Stories;

namespace D47.Core.Goals;

/// <summary>The Goals page's entry for a stock story, read from the story's own state.</summary>
public static class StoryGoal
{
    public const string KeyPrefix = "story.";

    /// <summary>
    /// The goal for the Commander's current story, else their last finished one; null for a story that was abandoned or
    /// switched. <paramref name="chapter"/> is the standing of the story's current chapter; <paramref name="goals"/> is
    /// every built-in arc, read only for a Goals story.
    /// </summary>
    public static GoalStanding? Of(IReadOnlyList<Story> stories, Func<string, AdventureStanding?> chapter, Func<IReadOnlyList<GoalStanding>>? goals = null)
    {
        ArgumentNullException.ThrowIfNull(stories);
        ArgumentNullException.ThrowIfNull(chapter);

        var story = stories.FirstOrDefault(story => story.IsCurrent)
            ?? stories.Where(story => story.State == StoryState.Finished).OrderBy(story => story.StoppedAt).LastOrDefault();

        if (story is null)
        {
            return null;
        }

        var finished = story.State == StoryState.Finished;

        return new GoalStanding
        {
            Arc = new GoalArc
            {
                Key = KeyPrefix + story.Id,
                Name = story.Title,
                Done = "Its last chapter is finished.",
                Kind = GoalKind.Story,
                Unit = "clues",
            },
            Have = story.CluesGiven,
            Need = story.Pacing.Lines,
            Source = GoalSource.Live,
            Started = story.PickedAt,
            Note = finished ? null : Note(story, story.CurrentChapter is { } key ? chapter(key) : null, goals),
            Track = GoalTrack.Story(story.Pacing, finished ? story.Pacing.Lines : story.CluesGiven),
            IsDone = finished,
        };
    }

    private static string Note(Story story, AdventureStanding? chapter, Func<IReadOnlyList<GoalStanding>>? goals)
    {
        var place = $"{StoryClues.StageName(StoryClues.Stage(story))}, clue {story.CluesGiven} of {story.Pacing.Lines}";

        var note = story.State == StoryState.Paused
            ? $"Paused at {place}"
            : story.IsOff ? $"Switched off at {place}" : place;

        if (Waiting(story, goals) is { } waiting)
        {
            note = $"{note}. {waiting}";
        }

        var now = story.State == StoryState.Running && chapter is { Adventure.IsActive: true, CurrentTrigger: { } trigger }
            ? trigger.Progress(chapter.Counted)?.Replace(": ", ", ", StringComparison.Ordinal) ?? trigger.Describe()
            : null;

        return now is null ? note : $"{note}. Now: {Lower(now)}";
    }

    /// <summary>What a Goals story's next clue or finale waits for in Goals progress, or null when progress holds neither.</summary>
    private static string? Waiting(Story story, Func<IReadOnlyList<GoalStanding>>? goals)
    {
        if (story.GoalsAtPick is null || story.BeaconScanAt is null || story.FinaleFrom is not null || goals is null)
        {
            return null;
        }

        var standings = goals();

        if (story.CluesGiven < story.Pacing.ClueDays.Count)
        {
            var threshold = StoryClues.GoalsThreshold(story, story.CluesGiven);
            var overall = GoalClimb.Overall(standings);

            return StoryClues.GoalsHeld(story, story.CluesGiven, overall)
                ? $"Clue {(story.CluesGiven + 1).ToString(CultureInfo.InvariantCulture)} waits for Goals progress of "
                  + $"{Percent(threshold, Math.Ceiling)}%; you are at {Percent(overall, Math.Floor)}%"
                : null;
        }

        return GoalClimb.AllDone(standings)
            ? null
            : $"The finale waits for every Goal: {standings.Count(standing => standing.IsDone).ToString(CultureInfo.InvariantCulture)} "
              + $"of {standings.Count.ToString(CultureInfo.InvariantCulture)} done";
    }

    /// <summary>A share as a whole percent, rounded the given way, so a held threshold never reads as reached.</summary>
    private static string Percent(double share, Func<double, double> round) =>
        round(Math.Round(share * 100, 9)).ToString(CultureInfo.InvariantCulture);

    private static string Lower(string text) => char.ToLowerInvariant(text[0]) + text[1..];
}

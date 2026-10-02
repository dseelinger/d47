using D47.Core.Adventures;
using D47.Core.Stories;

namespace D47.Core.Goals;

/// <summary>The Goals page's entry for a stock story, read from the story's own state.</summary>
public static class StoryGoal
{
    public const string KeyPrefix = "story.";

    /// <summary>
    /// The goal for the Commander's current story, else their last finished one; null for a story that was abandoned or
    /// switched. <paramref name="chapter"/> is the standing of the story's current chapter.
    /// </summary>
    public static GoalStanding? Of(IReadOnlyList<Story> stories, Func<string, AdventureStanding?> chapter)
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
            Note = finished ? null : Note(story, story.CurrentChapter is { } key ? chapter(key) : null),
            IsDone = finished,
        };
    }

    private static string Note(Story story, AdventureStanding? chapter)
    {
        var place = $"{StoryClues.StageName(StoryClues.Stage(story))}, clue {story.CluesGiven} of {story.Pacing.Lines}";

        var note = story.State == StoryState.Paused
            ? $"Paused at {place}"
            : story.IsOff ? $"Switched off at {place}" : place;

        var now = story.State == StoryState.Running && chapter is { Adventure.IsActive: true, CurrentBeat: { } beat }
            ? beat.Trigger.Progress(chapter.Counted)?.Replace(": ", ", ", StringComparison.Ordinal) ?? beat.Trigger.Describe()
            : null;

        return now is null ? note : $"{note}. Now: {Lower(now)}";
    }

    private static string Lower(string text) => char.ToLowerInvariant(text[0]) + text[1..];
}

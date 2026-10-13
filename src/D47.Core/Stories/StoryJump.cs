namespace D47.Core.Stories;

/// <summary>The point a story is moved forward to: a clue by its 0-based index, or the finale.</summary>
public readonly record struct StoryJumpTarget(int? Clue)
{
    public static StoryJumpTarget Finale => new(null);

    public static StoryJumpTarget AtClue(int clue) => new(clue);
}

/// <summary>Moves a story forward so a chosen clue, or the finale, is due now.</summary>
public static class StoryJump
{
    /// <summary>Why the story cannot be moved to the target, or null.</summary>
    public static string? Refusal(Story story, StoryJumpTarget target)
    {
        ArgumentNullException.ThrowIfNull(story);

        var clues = story.Pacing.ClueDays.Count;

        if (!story.IsCurrent)
        {
            return "No story is running.";
        }

        if (story.FinaleFrom is not null)
        {
            return "The story is already in its finale.";
        }

        if (target.Clue is not { } clue)
        {
            return null;
        }

        if (clue < 0 || clue >= clues)
        {
            return "The story has no such clue.";
        }

        return clue < story.CluesGiven ? "That clue has been given." : null;
    }

    /// <summary>
    /// The story with the target due at <paramref name="now"/>, or null when <see cref="Refusal"/> gives a reason. The
    /// beacon scan moves to where the story has run for the target's day, paused and switched-off time left out.
    /// </summary>
    public static Story? Jump(Story story, StoryJumpTarget target, DateTimeOffset now)
    {
        if (Refusal(story, target) is not null)
        {
            return null;
        }

        var pacing = story.Pacing;
        var day = TimeSpan.FromDays(target.Clue is { } clue ? pacing.ClueDays[clue] : pacing.FinaleDay);
        var moved = story with
        {
            CluesGiven = target.Clue ?? pacing.ClueDays.Count,
            ClueSession = null,
            ClueChapter = null,
        };

        // Moving the scan back brings more paused and switched-off time into the count, so the scan is moved again by
        // what is still short. The time counted never falls, and never passes the target day.
        var scan = now - day;

        for (var attempt = 0; attempt < 64; attempt++)
        {
            var since = (moved with { BeaconScanAt = scan }).SinceBeacon(now) ?? TimeSpan.Zero;

            if (since >= day)
            {
                break;
            }

            scan -= day - since;
        }

        return moved with { BeaconScanAt = scan };
    }
}

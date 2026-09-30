using System.Globalization;
using System.Text;
using D47.Core.Callouts;

namespace D47.Core.Stories;

/// <summary>A clue that is due: the story it belongs to and its place in the hidden layer.</summary>
public sealed record StoryClueDue(string StoryId, int Index);

/// <summary>When the hidden layer's clues are spoken, and the hidden layer as every speaker reads it.</summary>
public static class StoryClues
{
    /// <summary>Real days after the beacon scan, paused days not counted, before each clue may be spoken.</summary>
    public static readonly IReadOnlyList<int> Days = [7, 60, 365];

    /// <summary>The fewest play sessions between two clues.</summary>
    public const int SessionsApart = 4;

    /// <summary>The rule every speaker of a hidden story follows.</summary>
    public const string Rule =
        "Hint at it; never state it. You may mislead the Commander about the story, never about the game: fuel, "
        + "cargo, credits, routes, rank and danger are always as the game state gives them.";

    /// <summary>The clue the running story owes the Commander now, or null.</summary>
    public static StoryClueDue? Due(Story story, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(story);

        if (story.State != StoryState.Running
            || story.CluesGiven >= Days.Count
            || story.SinceBeacon(now) is not { } since
            || since < TimeSpan.FromDays(Days[story.CluesGiven])
            || (story.ClueSession is { } last && story.Sessions - last < SessionsApart))
        {
            return null;
        }

        return new StoryClueDue(story.Id, story.CluesGiven);
    }

    /// <summary>The clue at <paramref name="index"/>, oldest first.</summary>
    public static string? Text(StorySecret secret, int index)
    {
        ArgumentNullException.ThrowIfNull(secret);

        return index switch
        {
            0 => secret.Weeks,
            1 => secret.Months,
            2 => secret.Year,
            _ => null,
        };
    }

    /// <summary>The hidden layer as the narrator, the cores, chatter and the chapter writer all read it.</summary>
    public static string Brief(Story story, StorySecret secret)
    {
        ArgumentNullException.ThrowIfNull(story);
        ArgumentNullException.ThrowIfNull(secret);

        var text = new StringBuilder();
        text.AppendLine(
            $"The hidden story of \"{story.Title}\", the stock story the Commander is playing. They do not know it; "
            + "you do.");
        text.AppendLine(Rule);
        StoryCard.Line(text, "The secret", secret.Secret);
        StoryCard.Line(text, "Where it ends", secret.End);

        var given = Enumerable.Range(0, Math.Min(story.CluesGiven, Days.Count))
            .Select(index => Text(secret, index))
            .Where(clue => !string.IsNullOrWhiteSpace(clue))
            .ToList();

        if (given.Count == 0)
        {
            text.AppendLine("The Commander has had no clue yet. Bring in none of your own.");
        }
        else
        {
            text.AppendLine("The clues the Commander has had, which you may echo. Bring in no other:");

            for (var at = 0; at < given.Count; at++)
            {
                text.AppendLine($"{(at + 1).ToString(CultureInfo.InvariantCulture)}. {given[at]}");
            }
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>What the model is asked to write when a clue is spoken.</summary>
    public static FlavourBrief Speaking(string clue, bool narrated) => narrated
        ? FlavourBriefs.Narration with
        {
            Instruction =
                FlavourBriefs.Narration.Instruction
                + " Let this narration carry a clue to the hidden story, as a detail the Commander could miss. "
                + $"Hint at what it points to; never state it. The clue: {clue}",
        }
        : new FlavourBrief
        {
            Instruction =
                "Pass the Commander a clue to the hidden story, in your own voice, as something they could miss. "
                + "Hint at what it points to; never state it. One to three sentences. Do not ask a question and do "
                + $"not give advice. The clue: {clue}",
            NeedsPersona = true,
            NeedsGameState = true,
            NeedsAboutMe = true,
            NeedsScenario = true,
            NeedsStory = true,
        };
}

using System.Globalization;
using System.Text;
using D47.Core.Audio;
using D47.Core.Callouts;

namespace D47.Core.Stories;

/// <summary>A clue that is due: the story it belongs to and its place in the hidden layer, 0 to 13 for a clue and 14 to 17 for a finale line.</summary>
public sealed record StoryClueDue(string StoryId, int Index);

/// <summary>Where a story stands in its beat sheet.</summary>
public enum StoryStage
{
    ActOne,
    BreakIntoTwo,
    FunAndGames,
    Midpoint,
    BadGuysCloseIn,
    AllIsLost,
    DarkNightOfTheSoul,
    Finale,
}

/// <summary>When the hidden layer's clues are spoken, and the hidden layer as every speaker reads it.</summary>
public static class StoryClues
{
    /// <summary>Real days after the beacon scan, paused days not counted, before each of the fourteen clues may be spoken.</summary>
    public static readonly IReadOnlyList<int> Days = [7, 14, 21, 28, 60, 90, 120, 150, 180, 210, 240, 270, 300, 330];

    /// <summary>Days after the beacon scan, counted as for <see cref="Days"/>, before the finale may begin.</summary>
    public const int FinaleDay = 360;

    /// <summary>Every clue: the fourteen, then one for each finale chapter.</summary>
    public static int Count => Days.Count + StorySecret.FinaleCount;

    /// <summary>Whether the Narrator speaks a clue: always while the core aboard is stock, otherwise when the Narrator is on.</summary>
    public static bool Narrated(Persona.Persona? core, bool narratorOn) => core?.Stock == true || narratorOn;

    /// <summary>Whether a speaker reads the hidden layer: every speaker but a stock core's own voice.</summary>
    public static bool ReadsHiddenStory(VoiceRole speaker, Persona.Persona core)
    {
        ArgumentNullException.ThrowIfNull(core);

        return !(speaker == VoiceRole.ShipAi && core.Stock);
    }

    /// <summary>The fewest play sessions between two of the fourteen clues.</summary>
    public const int SessionsApart = 1;

    /// <summary>The rule every speaker of a hidden story follows.</summary>
    public const string Rule =
        "Hint at it; never state it. You may mislead the Commander about the story, never about the game: fuel, "
        + "cargo, credits, routes, rank and danger are always as the game state gives them.";

    /// <summary>
    /// The clue the running story owes the Commander now, or null. One of the fourteen waits for its day, a new
    /// session and a chapter finished since the last; a finale clue is due as its chapter begins.
    /// </summary>
    public static StoryClueDue? Due(Story story, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(story);

        if (story.State != StoryState.Running)
        {
            return null;
        }

        if (story.FinaleChapter is { } finale)
        {
            return story.CluesGiven < Days.Count + Math.Min(finale, StorySecret.FinaleCount)
                ? new StoryClueDue(story.Id, story.CluesGiven)
                : null;
        }

        if (story.CluesGiven >= Days.Count
            || story.SinceBeacon(now) is not { } since
            || since < TimeSpan.FromDays(Days[story.CluesGiven])
            || (story.ClueSession is { } session && story.Sessions - session < SessionsApart)
            || (story.ClueChapter is { } chapter && story.Chapters.Count <= chapter))
        {
            return null;
        }

        return new StoryClueDue(story.Id, story.CluesGiven);
    }

    /// <summary>Whether the next chapter written opens the finale: every one of the fourteen clues given, and the finale's day reached.</summary>
    public static bool FinaleDue(Story story, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(story);

        return story.FinaleFrom is null
            && story.CluesGiven >= Days.Count
            && story.SinceBeacon(now) is { } since
            && since >= TimeSpan.FromDays(FinaleDay);
    }

    /// <summary>Whether the story's last finale chapter is under way and its clue given, so the story finishes when the chapter does.</summary>
    public static bool AtTheEnd(Story story)
    {
        ArgumentNullException.ThrowIfNull(story);

        return story.FinaleChapter >= StorySecret.FinaleCount && story.CluesGiven >= Count;
    }

    /// <summary>Where the story stands in its beat sheet: act one before the beacon scan, then by the clues given.</summary>
    public static StoryStage Stage(Story story)
    {
        ArgumentNullException.ThrowIfNull(story);

        if (story.FinaleChapter is not null)
        {
            return StoryStage.Finale;
        }

        if (story.BeaconScanAt is null)
        {
            return StoryStage.ActOne;
        }

        return story.CluesGiven switch
        {
            0 => StoryStage.BreakIntoTwo,
            <= 8 => StoryStage.FunAndGames,
            9 => StoryStage.Midpoint,
            <= 12 => StoryStage.BadGuysCloseIn,
            13 => StoryStage.AllIsLost,
            _ => StoryStage.DarkNightOfTheSoul,
        };
    }

    /// <summary>
    /// The beat-sheet lines the chapter writer gets at a stage, by their sealed keys. Act one stops before
    /// <c>breakIntoTwo</c> while the beacon is out of reach; the last finale chapter adds <c>finalImage</c>.
    /// </summary>
    public static IReadOnlyList<(string Key, string Line)> Beats(StoryBeats beats, StoryStage stage, bool beaconInReach = true, int? finaleChapter = null)
    {
        ArgumentNullException.ThrowIfNull(beats);

        string[] keys = stage switch
        {
            StoryStage.ActOne when beaconInReach => ["openingImage", "themeStated", "setUp", "catalyst", "debate", "breakIntoTwo"],
            StoryStage.ActOne => ["openingImage", "themeStated", "setUp", "catalyst", "debate"],
            StoryStage.BreakIntoTwo => ["breakIntoTwo", "bStory"],
            StoryStage.FunAndGames => ["bStory", "funAndGames"],
            StoryStage.Midpoint => ["midpoint"],
            StoryStage.BadGuysCloseIn => ["badGuysCloseIn"],
            StoryStage.AllIsLost => ["allIsLost"],
            StoryStage.DarkNightOfTheSoul => ["darkNightOfTheSoul"],
            _ when finaleChapter >= StorySecret.FinaleCount => ["breakIntoThree", "finale", "finalImage"],
            _ => ["breakIntoThree", "finale"],
        };

        return
        [
            .. beats.All
                .Where(beat => keys.Contains(beat.Key, StringComparer.Ordinal) && !string.IsNullOrWhiteSpace(beat.Line))
                .Select(beat => (beat.Key, beat.Line!)),
        ];
    }

    /// <summary>The text of the clue at <paramref name="index"/>: the fourteen, then the finale lines.</summary>
    public static string? Text(StorySecret secret, int index)
    {
        ArgumentNullException.ThrowIfNull(secret);

        return index < Days.Count
            ? secret.Clues.ElementAtOrDefault(index)?.Text
            : secret.Finale.ElementAtOrDefault(index - Days.Count)?.Text;
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

        var given = Enumerable.Range(0, Math.Min(story.CluesGiven, Count))
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

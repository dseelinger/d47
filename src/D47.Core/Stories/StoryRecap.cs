using System.Globalization;
using System.Text;
using D47.Core.Adventures;
using D47.Core.Callouts;

namespace D47.Core.Stories;

/// <summary>One line said at an objective that has fired, and who said it; null for narration.</summary>
public sealed record RecapLine(string? Speaker, string Text);

/// <summary>An objective that has fired: its title and the lines said when it did.</summary>
public sealed record RecapObjective(string Title, IReadOnlyList<RecapLine> Lines);

/// <summary>
/// Everything a recap may say about one story. It holds no objective that has not fired, not the current objective's line,
/// and none of the hidden layer beyond the clues given.
/// </summary>
public sealed record RecapFacts
{
    public required string Title { get; init; }

    /// <summary>A stock story's backstory in the Commander's words.</summary>
    public string? Backstory { get; init; }

    /// <summary>A short story's premise.</summary>
    public string? Premise { get; init; }

    /// <summary>The objectives fired so far in every chapter, oldest first.</summary>
    public IReadOnlyList<RecapObjective> Fired { get; init; } = [];

    /// <summary>The clues given so far, oldest first.</summary>
    public IReadOnlyList<string> Clues { get; init; } = [];

    /// <summary>Where the story waits, as the place and the act, or null when nothing waits.</summary>
    public string? HandOff { get; init; }

    public bool Paused { get; init; }
}

/// <summary>What the model is given for a recap: the speaker brief, the Commander's sheet or name, and the instruction.</summary>
public sealed record RecapAsk(string Speaker, string? AboutMe, string Instruction);

/// <summary>The Narrator's recap of the story under way, said only when the Commander asks.</summary>
public static class StoryRecap
{
    public const string NoStory = "No story is under way.";

    /// <summary>The place and the act a beat waits on — "Next: dock at Maren Anchorage in Dyson's Hollow." — with a counted beat's total so far.</summary>
    public static string HandOff(AdventureTrigger trigger, int counted)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        return trigger.Progress(counted) is { } progress
            ? $"Next: {trigger.Describe()}. {progress}."
            : $"Next: {trigger.Describe()}.";
    }

    /// <summary>The first <paramref name="fired"/> objectives of a chapter, each speaker named by <paramref name="name"/>.</summary>
    public static IReadOnlyList<RecapObjective> Fired(Adventure chapter, int fired, Func<string?, string?> name)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        ArgumentNullException.ThrowIfNull(name);

        return [.. chapter.Beats.Take(fired).Select(beat => new RecapObjective(
            beat.Title,
            [.. beat.Lines.Where(line => !string.IsNullOrWhiteSpace(line.Text)).Select(line => new RecapLine(name(line.Speaker), line.Text.Trim()))]))];
    }

    /// <summary>A short story's recap: its premise, the objectives fired and where it waits.</summary>
    public static RecapFacts Of(AdventureStanding standing)
    {
        ArgumentNullException.ThrowIfNull(standing);

        var adventure = standing.Adventure;

        return new RecapFacts
        {
            Title = adventure.Name,
            Premise = adventure.Spine?.Premise,
            Fired = Fired(adventure, standing.Fired.Count, _ => null),
            HandOff = standing.CurrentBeat is { } beat ? HandOff(beat.Trigger, standing.Counted) : null,
        };
    }

    /// <summary>The model's input for a recap. <paramref name="sheetOrName"/> is the character sheet, or the Commander's name.</summary>
    public static RecapAsk Ask(RecapFacts facts, string? sheetOrName)
    {
        ArgumentNullException.ThrowIfNull(facts);

        var text = new StringBuilder(
            "Recap the Commander's story so far, as its narrator. Two to four sentences, in the third person and the past tense, "
            + "the way a novel's narrator writes. Refer to the Commander by the name the character sheet gives, with the pronouns it "
            + "gives; where it gives no pronouns, use the name and no pronouns. Never say \"I\", \"me\" or \"we\", never speak to "
            + "the Commander, and never offer, ask or wait for anything. Say only what the facts below say: invent no events, say "
            + "nothing of what comes later, and write no dialogue. End on where the story waits now, when the facts say.");

        text.AppendLine().AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"The story: {facts.Title}.");

        if (facts.Paused)
        {
            text.AppendLine("The Commander has paused it.");
        }

        Line(text, "The Commander's backstory, in their own words", facts.Backstory);
        Line(text, "Premise", facts.Premise);

        if (facts.Fired.Count == 0)
        {
            text.AppendLine("No objective has been reached yet.");
        }
        else
        {
            text.AppendLine("The objectives reached so far, oldest first, with what was said at each:");

            for (var at = 0; at < facts.Fired.Count; at++)
            {
                var objective = facts.Fired[at];
                var said = string.Join(' ', objective.Lines.Select(line => line.Speaker is { } speaker ? $"{speaker}: \"{line.Text}\"" : line.Text));

                text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{at + 1}. {objective.Title}. {said}").TrimEnd());
            }
        }

        if (facts.Clues.Count > 0)
        {
            text.AppendLine("The clues the Commander has had, oldest first:");

            foreach (var clue in facts.Clues)
            {
                text.Append("- ").AppendLine(clue);
            }
        }

        Line(text, "Where it waits now", facts.HandOff);

        return new RecapAsk(FlavourBriefs.Narration.Speaker!, sheetOrName, text.ToString().TrimEnd());
    }

    /// <summary>The recap said without a model: the last fired objective's first line as written, then where the story waits.</summary>
    public static string Fixed(RecapFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        string?[] parts = [facts.Fired.LastOrDefault(objective => objective.Lines.Count > 0)?.Lines[0].Text, facts.HandOff];
        var said = string.Join(' ', parts.Where(part => !string.IsNullOrWhiteSpace(part)));

        return said.Length > 0 ? said : $"{facts.Title} is under way.";
    }

    /// <summary>What is said: the model's recap, or the fixed text when there is none or it is in the first person.</summary>
    public static string Sayable(string? written, RecapFacts facts) =>
        string.IsNullOrWhiteSpace(written) || AdventureGenerator.FirstPerson(written) ? Fixed(facts) : written.Trim();

    private static void Line(StringBuilder text, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"{label}: {value.Trim()}");
        }
    }
}

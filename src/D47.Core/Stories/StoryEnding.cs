using D47.Core.Callouts;
using D47.Core.Persona;

namespace D47.Core.Stories;

/// <summary>A finished story whose ending message has not been posted yet.</summary>
public sealed record StoryEndingDue(string StoryId, string Title, string End, IReadOnlyList<StoryOption> Options);

/// <summary>What answering an ending produced: a refusal, or the story's last line and the cores' waking lines.</summary>
public sealed record StoryAnswer(string? Refusal, string After, IReadOnlyList<string> Wakings)
{
    public static StoryAnswer Refused(string refusal) => new(refusal, string.Empty, []);
}

/// <summary>The key of a line a story says outside its chapters and its ending.</summary>
public static class StoryLines
{
    public const string KeyPrefix = "story.line.";

    public static string Key(string storyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storyId);

        return KeyPrefix + storyId;
    }
}

/// <summary>The ending message of a finished story.</summary>
public static class StoryEnding
{
    public const string KeyPrefix = "story.end.";

    public static string Key(string storyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storyId);

        return KeyPrefix + storyId;
    }

    /// <summary>The ending as the model is asked to say it.</summary>
    public static FlavourBrief Speaking(string end, bool narrated) => narrated
        ? FlavourBriefs.Narration with
        {
            Instruction =
                FlavourBriefs.Narration.Instruction
                + " The Commander's story has reached its end. Narrate that ending in a few sentences, "
                + $"in your own words. The ending: {end}",
        }
        : new FlavourBrief
        {
            Instruction =
                "The Commander's story has reached its end. Tell them how it ended, in your own voice, in a few sentences. "
                + $"Do not ask a question and do not offer choices. The ending: {end}",
            NeedsPersona = true,
            NeedsGameState = true,
            NeedsAboutMe = true,
            NeedsScenario = true,
            NeedsStory = true,
        };

    /// <summary>The line said as an option brings a core aboard.</summary>
    public static string Waking(string personaId) => string.Equals(personaId, PersonaCatalog.Heretic.Id, StringComparison.Ordinal)
        ? GuardianCores.Line(CoreWaking.Heretic)
        : GuardianCores.Line(CoreWaking.Cores, PersonaCatalog.Resolve(personaId));
}

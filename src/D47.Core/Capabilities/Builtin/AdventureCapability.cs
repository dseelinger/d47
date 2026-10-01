using D47.Core.Stories;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Adventures — stories the Commander flies, told by the ship's AI (Phase 47).</summary>
public static class AdventureCapability
{
    public const string Id = "adventures";

    public const string PauseTool = "pause_story";

    public const string ResumeTool = "resume_story";

    public const string AnswerEndingTool = "answer_story_ending";

    private const string NoStory = "No story is running.";

    /// <summary>Switches the Commander's story on or off, returning a refusal or null. The app sets it once the story exists.</summary>
    public sealed class StorySwitch
    {
        public Func<bool, string?> Set { get; set; } = _ => NoStory;
    }

    /// <summary>Answers a finished story's ending by the option's position, one from the first, or null when there is one option.</summary>
    public sealed class EndingAnswer
    {
        public Func<int?, StoryAnswer> Answer { get; set; } = _ => StoryAnswer.Refused("No ending is waiting for an answer.");
    }

    public static CapabilityDescriptor Create(StorySwitch? storySwitch = null, EndingAnswer? endingAnswer = null) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "Adventures",
        Summary =
            "Stories the Commander flies, written by them or by the ship's AI, and advanced by "
            + "their own journal. Driven from the Adventures tab; nothing here is callable by the "
            + "model. The Commander can pause and resume a running story by voice.",

        // The phrases that genuinely work.
        Examples =
        [
            "show me the adventures",
            "open the adventures tab",
            "pause the story",
            "resume the story",
            "accept the ending",
            "choose ending two",
        ],

        // None.
        Keywords = [],
        Display = new CapabilityDisplay { PanelTitle = "Adventures", Order = 59, ShowOnPanel = false },

        Tools =
        [
            Switch(PauseTool, "Pause the Commander's running story: no beats, nudges, clues or story chatter until it is resumed.", false, ["pause the story", "pause my story"], storySwitch),
            Switch(ResumeTool, "Resume the Commander's running story after a pause.", true, ["resume the story", "resume my story"], storySwitch),
            Answer(endingAnswer),
        ],
    };

    private static ToolDefinition Answer(EndingAnswer? endingAnswer) => new()
    {
        Name = AnswerEndingTool,
        Description = "Answer the ending of a finished story with one of its options, numbered from one. The Commander's choice alone.",
        Parameters =
        [
            new ToolParameter
            {
                Name = "option",
                Type = ToolParameterType.Integer,
                Description = "The option's position in the ending message, from one. Leave out when the ending has one option.",
            },
        ],
        Commands =
        [
            new ToolCommandPhrase("accept the ending", new Dictionary<string, string>(StringComparer.Ordinal)),
            .. new[] { "one", "two", "three", "four" }.Select((word, index) => new ToolCommandPhrase(
                $"choose ending {word}",
                new Dictionary<string, string>(StringComparer.Ordinal) { ["option"] = (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) })),
        ],

        // The ending is the Commander's; the model is refused.
        Protected = true,
        Handler = (arguments, _) =>
        {
            int? option = arguments.TryGetInt32("option", out var number) ? number : null;
            var answer = endingAnswer?.Answer(option) ?? StoryAnswer.Refused("No ending is waiting for an answer.");

            return Task.FromResult(answer.Refusal is { } refusal ? ToolResult.Error(refusal) : ToolResult.Ok("Your answer is recorded."));
        },
    };

    private static ToolDefinition Switch(string name, string description, bool on, string[] phrases, StorySwitch? storySwitch) => new()
    {
        Name = name,
        Description = description,
        Commands = [.. phrases.Select(phrase => new ToolCommandPhrase(phrase, new Dictionary<string, string>(StringComparer.Ordinal)))],

        // The Commander's act; the model is refused.
        Protected = true,
        Handler = (_, _) => Task.FromResult(
            (storySwitch?.Set(on) ?? NoStory) is { } refusal
                ? ToolResult.Error(refusal)
                : ToolResult.Ok(on ? "The story is back on." : "The story is paused. Say 'resume the story' when you want it back.")),
    };
}

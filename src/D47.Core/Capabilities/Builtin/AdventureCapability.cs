namespace D47.Core.Capabilities.Builtin;

/// <summary>Adventures — stories the Commander flies, told by the ship's AI (Phase 47).</summary>
public static class AdventureCapability
{
    public const string Id = "adventures";

    public const string PauseTool = "pause_story";

    public const string ResumeTool = "resume_story";

    private const string NoStory = "No story is running.";

    /// <summary>Switches the Commander's story on or off, returning a refusal or null. The app sets it once the story exists.</summary>
    public sealed class StorySwitch
    {
        public Func<bool, string?> Set { get; set; } = _ => NoStory;
    }

    public static CapabilityDescriptor Create(StorySwitch? storySwitch = null) => new()
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
        ],

        // None.
        Keywords = [],
        Display = new CapabilityDisplay { PanelTitle = "Adventures", Order = 59, ShowOnPanel = false },

        Tools =
        [
            Switch(PauseTool, "Pause the Commander's running story: no beats, nudges, clues or story chatter until it is resumed.", false, ["pause the story", "pause my story"], storySwitch),
            Switch(ResumeTool, "Resume the Commander's running story after a pause.", true, ["resume the story", "resume my story"], storySwitch),
        ],
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

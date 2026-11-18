namespace D47.Core.Conversation;

/// <summary>What a model charge paid for, as the id written on its <see cref="SpendEntry"/> row.</summary>
public static class SpendPurposes
{
    public const string Conversation = "conversation";
    public const string StoryChapter = "story-chapter";
    public const string ShortStory = "short-story";
    public const string StoryLines = "story-lines";
    public const string StoryDetour = "story-detour";
    public const string NpcChatter = "npc-chatter";
    public const string RewordedCallout = "reworded-callout";
    public const string CoreGreeting = "core-greeting";
    public const string WebLookup = "web-lookup";
    public const string Campaign = "campaign";
    public const string Debrief = "debrief";
    public const string Logbook = "logbook";
    public const string VoiceCasting = "voice-casting";
    public const string Checklist = "checklist";
    public const string ShipPlan = "ship-plan";

    /// <summary>Every id this build writes, with the name a Commander reads.</summary>
    public static IReadOnlyList<(string Id, string Name)> All { get; } =
    [
        (Conversation, "Conversation"),
        (StoryChapter, "Story chapters"),
        (ShortStory, "Short stories"),
        (StoryLines, "Story narration"),
        (StoryDetour, "Story detours"),
        (NpcChatter, "NPC chatter"),
        (RewordedCallout, "Reworded callouts"),
        (CoreGreeting, "Core greetings"),
        (WebLookup, "Web lookups"),
        (Campaign, "Campaign writing"),
        (Debrief, "Debrief"),
        (Logbook, "Logbook"),
        (VoiceCasting, "Voice casting"),
        (Checklist, "Checklist watches"),
        (ShipPlan, "Ship plans"),
    ];

    /// <summary>The name for an id: "Not recorded" for a row written without one, and the id itself for one this build does not list.</summary>
    public static string NameOf(string? id) =>
        id is null ? "Not recorded"
        : All.FirstOrDefault(entry => entry.Id == id) is { Name: { } name } ? name
        : id;
}

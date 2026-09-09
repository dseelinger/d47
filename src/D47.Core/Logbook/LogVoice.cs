namespace D47.Core.Logbook;

/// <summary>Whose log it is (Phase 33, item 3).</summary>
public enum LogVoice
{
    /// <summary>The Commander, in their own words.</summary>
    FirstPerson,

    /// <summary>The selected persona, writing about the Commander.</summary>
    ShipsAi,

    /// <summary>The Commander's own account, with the persona interjecting.</summary>
    FirstPersonWithCommentary,
}

/// <summary>Ids, labels, and the rule that governs the two voices needing a personality.</summary>
public static class LogVoices
{
    public static IReadOnlyList<string> Ids { get; } = ["first-person", "ships-ai", "first-person-with-commentary"];

    public static string IdOf(LogVoice voice) => voice switch
    {
        LogVoice.ShipsAi => "ships-ai",
        LogVoice.FirstPersonWithCommentary => "first-person-with-commentary",
        _ => "first-person",
    };

    public static LogVoice Parse(string? id) => (id ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "ships-ai" => LogVoice.ShipsAi,
        "first-person-with-commentary" => LogVoice.FirstPersonWithCommentary,
        _ => LogVoice.FirstPerson,
    };

    public static string LabelOf(string id) => Parse(id) switch
    {
        LogVoice.ShipsAi => "D47 writes about you",
        LogVoice.FirstPersonWithCommentary => "You write, D47 chips in",
        _ => "You write it, in your own words",
    };

    /// <summary>Whether this voice needs a persona at all.</summary>
    public static bool NeedsPersona(LogVoice voice) => voice is not LogVoice.FirstPerson;

    /// <summary>What voice actually writes, given whether there is a personality to write in.</summary>
    public static LogVoice Resolve(LogVoice asked, bool personalityEnabled) =>
        personalityEnabled || !NeedsPersona(asked) ? asked : LogVoice.FirstPerson;

    /// <summary>What the file's header says happened, when the voice asked for is not the voice used.</summary>
    public static string? Degraded(LogVoice asked, LogVoice used) =>
        asked == used
            ? null
            : $"You asked for \"{LabelOf(IdOf(asked))}\". Personality is switched off, so this was written "
              + $"as \"{LabelOf(IdOf(used))}\" instead — D47 does not write as somebody else when it has "
              + "been told not to be anybody.";
}

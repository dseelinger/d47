namespace D47.Core.Capabilities.Builtin;

/// <summary>What happened when d47 tried to put Elite in front.</summary>
public enum FocusResult
{
    /// <summary>Elite is not running, or its window cannot be found.</summary>
    NotRunning,

    /// <summary>It was already in front.</summary>
    AlreadyThere,

    /// <summary>It is in front now.</summary>
    Raised,

    /// <summary>Windows refused.</summary>
    Refused,
}

/// <summary>Putting Elite back in front (docs/plans/change-requests.md item 10).</summary>
public static class FocusCapability
{
    public const string Id = "focus";

    /// <summary>
    /// The verb, thing, place family — <c>set elite to front</c>, <c>put the game in focus</c> —
    /// generated rather than typed (asked for 2026-08-23).
    /// </summary>
    private static readonly string[] Placements =
        [.. from verb in new[] { "set", "put", "bring", "move" }
            from thing in new[] { "elite", "the game", "game" }
            from place in new[] { "to front", "to the front", "in front", "in focus", "into focus" }
            select $"{verb} {thing} {place}"];

    /// <summary>The phrases that raise the game.</summary>
    public static readonly string[] Phrases =
    [
        "set focus to game",
        "set focus to the game",
        "set focus to elite",
        "focus the game",
        "focus on the game",
        "focus game",
        "focus elite",
        "switch to elite",
        "switch to the game",
        "go to elite",
        "go to the game",
        "back to the game",
        "bring up elite",
        "bring up the game",
        "show me the game",
        .. Placements,
    ];

    /// <summary><param name="raise"> How to put Elite in front.</summary>
    /// <param name="raise">How to put Elite in front.</param>
    public static CapabilityDescriptor Create(Func<Task<FocusResult>>? raise) => new()
    {
        Id = Id,
        Group = "Interface",
        Name = "Focus the game",
        Summary = "Bring Elite Dangerous to the front, so flight commands can be sent again.",
        Examples = ["set focus to game", "focus the game", "take me back to Elite"],
        Keywords = [.. Phrases.Select(phrase => new CapabilityKeyword(phrase))],

        // No settings rows and no panel card: it is one action with nothing to configure.
        Display = new CapabilityDisplay { PanelTitle = "Focus the game", Order = 47, ShowOnPanel = false },
        Tools =
        [
            new ToolDefinition
            {
                Name = "focus_the_game",
                Description =
                    "Bring Elite Dangerous to the foreground. Reachable by spoken phrase only, never by "
                    + "the model.",
                Protected = true,
                Handler = async (_, _) => raise is null
                    ? ToolResult.Ok("I have no way to reach the game window on this machine.")
                    : ToolResult.Ok(Describe(await raise().ConfigureAwait(false))),
            },
        ],
    };

    /// <summary>What to say about it, out loud, including when nothing happened.</summary>
    public static string Describe(FocusResult result) => result switch
    {
        FocusResult.Raised => "Elite is in front.",
        FocusResult.AlreadyThere => "Elite is already in front.",
        FocusResult.NotRunning => "I cannot find Elite — it does not look like it is running.",
        _ => "Windows would not let me bring Elite forward from the background. Its taskbar "
             + "button should be flashing; click that, or alt-tab.",
    };
}

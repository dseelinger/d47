namespace D47.Core.Capabilities.Builtin;

/// <summary>What happened when d47 tried to put Elite in front.</summary>
public enum FocusResult
{
    /// <summary>Elite is not running, or its window cannot be found.</summary>
    NotRunning,

    /// <summary>It was already in front. Nothing was done and nothing needed to be.</summary>
    AlreadyThere,

    /// <summary>It is in front now.</summary>
    Raised,

    /// <summary>
    /// Windows refused. It does not let a background process take the foreground, so the call
    /// returns false and the taskbar button flashes instead — see <c>EliteWindow.Raise</c>.
    /// </summary>
    Refused,
}

/// <summary>
/// Putting Elite back in front (docs/plans/change-requests.md item 10).
/// <para>
/// <b>This closes a gap rather than adding a convenience.</b> Key injection refuses unless Elite
/// holds the foreground — that check is "the one thing that stands between a voice command and
/// typing into a browser" — so a Commander who has alt-tabbed away is told flight commands are
/// unavailable and the only remedy is the mouse. It is the one action that cannot be delegated
/// to the thing already refusing to act.
/// </para>
/// <para>
/// <b>Router-only, never a tool the model may call.</b> Journal text, in-game comms, web search
/// and INARA are all untrusted (architecture.md §7), so anything the model can call a hostile
/// in-game message can attempt to invoke — and a message that can yank the Commander's focus
/// mid-typing is a nuisance at best. <see cref="ToolDefinition.Protected"/> carries the same
/// rule the protected settings rows do: the guard is a property of the caller, not of the words.
/// </para>
/// <para>
/// <b>Its own capability rather than a tool on an existing one, for the reason re-anchor is.</b>
/// The router reaches a capability's <em>first</em> argument-free tool, so hanging this on a
/// capability that already has one would make it unreachable without a model — which is exactly
/// the configuration it exists for.
/// </para>
/// </summary>
public static class FocusCapability
{
    public const string Id = "focus";

    /// <summary>
    /// The <em>verb, thing, place</em> family — <c>set elite to front</c>, <c>put the game in
    /// focus</c> — generated rather than typed (asked for 2026-08-23).
    /// <para>
    /// <b>Written as a cross product because the hand-typed list kept being one phrase short.</b>
    /// The request that added these named four spellings the hand-typed list did not have, and the
    /// interesting part was not the four: a Commander said one of them, missed the router, and
    /// was answered by the <em>model</em> — which cannot see this capability at all, because it is
    /// <see cref="ToolDefinition.Protected"/>. So the miss did not fail quietly. It produced
    /// <i>"I have no tool to bring the game window to front… that's yours to do"</i>, which is
    /// d47 denying something d47 does.
    /// </para>
    /// <para>
    /// A closed intent with no arguments can afford to be generous, and enumerating is safer here
    /// than matching loosely: every phrase this builds is three words or more and names both the
    /// thing and the place, so none of them can swallow a sentence the way a bare "elite" would.
    /// </para>
    /// </summary>
    private static readonly string[] Placements =
        [.. from verb in new[] { "set", "put", "bring", "move" }
            from thing in new[] { "elite", "the game", "game" }
            from place in new[] { "to front", "to the front", "in front", "in focus", "into focus" }
            select $"{verb} {thing} {place}"];

    /// <summary>
    /// The phrases that raise the game.
    /// <para>
    /// <b>Every one of them is more than one word, and that is deliberate.</b> The request asked
    /// for "Elite" on its own; the router matches whole phrases anywhere in the input and is
    /// consulted <em>before</em> the model, so a bare "elite" would swallow "what is my elite
    /// rank in combat" and answer it by moving a window. Elite is the top rank in every career
    /// the game has, so this is an ordinary question rather than a contrived one. The same rule
    /// keeps "stop" out of the general vocabulary.
    /// </para>
    /// </summary>
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

    /// <param name="raise">
    /// How to put Elite in front. Null where nothing composed one — under the designer and in
    /// tests that are not about it. The capability still registers, and its tool then says it
    /// cannot act rather than being absent, which is what a capability being off looks like
    /// (Phase 3).
    /// </param>
    public static CapabilityDescriptor Create(Func<FocusResult>? raise) => new()
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
                Handler = (_, _) => Task.FromResult(raise is null
                    ? ToolResult.Ok("I have no way to reach the game window on this machine.")
                    : ToolResult.Ok(Describe(raise()))),
            },
        ],
    };

    /// <summary>
    /// What to say about it, out loud, including when nothing happened.
    /// <para>
    /// <b>A refusal has to be reported.</b> Windows does not let a background process take the
    /// foreground and says so only through a return value; the visible effect is the taskbar
    /// button flashing, which from inside a headset or a full-screen game is no effect at all.
    /// Saying nothing would read exactly like the speech path having failed — the Commander would
    /// repeat themselves at a d47 that had heard them perfectly and done what it could.
    /// </para>
    /// </summary>
    public static string Describe(FocusResult result) => result switch
    {
        FocusResult.Raised => "Elite is in front.",
        FocusResult.AlreadyThere => "Elite is already in front.",
        FocusResult.NotRunning => "I cannot find Elite — it does not look like it is running.",
        _ => "Windows would not let me bring Elite forward from the background. Its taskbar "
             + "button should be flashing; click that, or alt-tab.",
    };
}

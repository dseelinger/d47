using D47.Core.Journal;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// "TheApp knows where you are" (list.md Phase 2) — the first capability the journal spine
/// makes possible. Needs no model: the answer comes straight out of <see cref="GameStateStore"/>.
/// </summary>
public static class JournalCapability
{
    public const string Id = "journal";

    public static CapabilityDescriptor Create(GameStateStore gameState)
    {
        return new CapabilityDescriptor
        {
            Id = Id,
            Group = "Foundation",
            Name = "Journal",
            Summary = "Report the Commander's current system, body and docking state from the journal.",
            Examples = ["where am I", "what system is this", "am I docked"],
            // Phrases, not words. "where" and "system" on their own match "Where is Iran?" and
            // "what's your operating system" — questions this capability has no business
            // answering, answered from journal data with total confidence. Every phrase here has
            // to be one that can only be a request for the Commander's own position.
            Keywords =
            [
                "where am i",
                "where i am",
                "what system",
                "which system",
                "current system",
                "my location",
                "am i docked",
                "what body",
            ],
            Display = new CapabilityDisplay { PanelTitle = "Location", Order = 20 },
            Tools =
            [
                new ToolDefinition
                {
                    Name = "get_location",
                    Description =
                        "Report the current Commander's star system, body and docking state, from the journal.",
                    Handler = (_, _) => Task.FromResult(ToolResult.Ok(Describe(gameState))),
                },
            ],
        };
    }

    private static string Describe(GameStateStore gameState)
    {
        var active = gameState.Active;

        if (active is null)
        {
            return "No Elite Dangerous journal has been detected yet.";
        }

        var location = active.Location;

        if (location.StarSystem is null)
        {
            return $"{active.Identity.Name} — no position recorded yet this session.";
        }

        var where = location.Body is not null && location.Body != location.StarSystem
            ? $"{location.StarSystem}, near {location.Body}"
            : location.StarSystem;

        var docking = location switch
        {
            { Docked: true, StationName: { } station } => $", docked at {station}",
            { Docked: true } => ", docked",
            _ => "",
        };

        return $"{active.Identity.Name} is in {where}{docking}.";
    }
}

using System.Text;
using D47.Core.Journal;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Who else is aboard (list.md Phase 11, "Ship Crew").
/// <para>
/// Only what the journal reports. Elite hires, fires, assigns and ranks NPC pilots and says
/// nothing else about them — there is no engineer, no gunner and no navigator in this game, and
/// a shipped roster of fictional posts would be exactly the invented game data the guardrails
/// exist to prevent.
/// </para>
/// </summary>
public static class CrewCapability
{
    public const string Id = "crew";

    public static CapabilityDescriptor Create(Func<CommanderGameState?> state) => new()
    {
        Id = Id,
        Group = "Ship",
        Name = "Crew",
        Summary = "Report the pilots you have hired, who is on duty, and how to talk to them.",
        Examples = ["who is aboard", "who is my crew", "who is on duty"],
        Keywords =
        [
            "who is aboard",
            "who's aboard",
            "who is my crew",
            "my crew",
            "crew roster",
            "who is on duty",
            "who's on duty",
        ],
        Display = new CapabilityDisplay { PanelTitle = "Crew", Order = 26, ShowOnPanel = false },
        Tools =
        [
            new ToolDefinition
            {
                Name = "describe_crew",
                Description =
                    "Report the NPC pilots the Commander has hired, their combat ranks, which one is "
                    + "currently assigned to the fighter bay, and which ship each was assigned aboard.",
                Handler = (_, _) => Task.FromResult(ToolResult.Ok(Describe(state()))),
            },
        ],
    };

    public static string Describe(CommanderGameState? state)
    {
        if (state?.Crew is not { Any: true } crew)
        {
            // Not "you have no crew". A Commander who hired somebody before d47 was watching has
            // a crew d47 has not seen, and those are different answers.
            return "I have not seen you hire any crew this session. If you have, I would not know "
                   + "about them until you next hire, fire or reassign somebody.";
        }

        var report = new StringBuilder();
        report.AppendLine($"{crew.Members.Count} pilot(s) on the books:");

        foreach (var member in crew.Members)
        {
            var duty = member.Active ? "on duty in the fighter bay" : "off duty";
            var posting = member.PostedTo is { Length: > 0 } ship ? $", assigned aboard {ship}" : string.Empty;

            report.AppendLine($"  {member.Describe()} — {duty}{posting}");
        }

        report.AppendLine();
        report.AppendLine("Address one of them by name to talk to them directly, for example \"" +
                          crew.Members[0].Name + ", status\".");

        return report.ToString().TrimEnd();
    }
}

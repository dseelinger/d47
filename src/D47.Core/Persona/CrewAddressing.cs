using D47.Core.Journal;

namespace D47.Core.Persona;

/// <summary>A turn the Commander addressed to a crew member rather than to the ship's AI.</summary>
/// <param name="Member">Who they spoke to.</param>
/// <param name="Question">What they said, with the name taken off the front.</param>
public sealed record CrewAddressed(CrewMember Member, string Question);

/// <summary>
/// Working out whether the Commander was talking to the ship's AI or to somebody in the fighter bay
/// (Phase 11, "Ship Crew").
/// </summary>
public static class CrewAddressing
{
    /// <summary>The crew member this input opens by naming, or null.</summary>
    public static CrewAddressed? Match(string? input, ShipCrew crew)
    {
        if (string.IsNullOrWhiteSpace(input) || !crew.Any)
        {
            return null;
        }

        var trimmed = input.TrimStart();

        // Longest name first, so a crew of "Vance" and "Vance Ilo" cannot have the shorter one claim the
        // longer one's turns.
        foreach (var member in crew.Members.OrderByDescending(m => m.Name.Length))
        {
            if (Opens(trimmed, member.Name) is { } question)
            {
                return new CrewAddressed(member, question);
            }
        }

        return null;
    }

    /// <summary>What is left after the name, or null if the input does not open with it.</summary>
    private static string? Opens(string input, string name)
    {
        if (!input.StartsWith(name, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rest = input[name.Length..];

        // The name has to end at a word boundary.
        if (rest.Length > 0 && (char.IsLetterOrDigit(rest[0]) || rest[0] == '\''))
        {
            return null;
        }

        return rest.TrimStart(',', ':', '-', ' ', '.', '?', '!').Trim();
    }

    /// <summary>The prompt block for a crew member.</summary>
    /// <param name="shipName">What the ship is called, so they can refer to it.</param>
    public static string Brief(CrewMember member, string? shipName) =>
        $"""
         You are {member.Name}, a pilot the Commander hired to fly the fighter aboard
         {shipName ?? "this ship"}. You are a human being with a job, not an artificial
         intelligence, and you never claim to be one.

         Your combat rating is {member.CombatRank ?? "unrecorded"}. {(member.Active
             ? "You are the crew member currently on duty in the fighter bay."
             : "You are off duty at the moment, on shore leave.")}

         You answer briefly and professionally, the way somebody on an intercom does. You do not
         narrate, you do not describe your own personality, and you do not speak for the ship's
         AI — it is a separate voice aboard and it can answer for itself.

         You know only what a crew member aboard this ship would know. You have no access to
         galactic databases, no tools, and no way to look anything up. Asked something outside
         what you can see from where you sit, you say so plainly.
         """;
}

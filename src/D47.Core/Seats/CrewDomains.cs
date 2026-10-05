using D47.Core.Audio;
using D47.Core.Callouts;

namespace D47.Core.Seats;

/// <summary>Which crew role speaks each callout when the ship flown has that role's seat filled.</summary>
public static class CrewDomains
{
    /// <summary>The role each callout id belongs to. Every callout id is here or in <see cref="Kept"/>.</summary>
    public static IReadOnlyDictionary<string, CrewRole> Roles { get; } = new Dictionary<string, CrewRole>(StringComparer.Ordinal)
    {
        ["checklist"] = CrewRole.FirstOfficer,
        ["missions"] = CrewRole.FirstOfficer,
        ["promotion"] = CrewRole.FirstOfficer,
        ["rebuy"] = CrewRole.FirstOfficer,
        ["community-goal-sales"] = CrewRole.FirstOfficer,
        ["trading-mode"] = CrewRole.FirstOfficer,

        ["fuel"] = CrewRole.Helm,
        ["limpets"] = CrewRole.Helm,

        ["messages"] = CrewRole.Comms,

        ["discovery"] = CrewRole.ScienceOfficer,
        ["biology"] = CrewRole.ScienceOfficer,
        ["surveyed-biology"] = CrewRole.ScienceOfficer,
        ["mapping"] = CrewRole.ScienceOfficer,
        ["emissions"] = CrewRole.ScienceOfficer,
        ["footfall"] = CrewRole.ScienceOfficer,
        ["sampling"] = CrewRole.ScienceOfficer,
        ["materials"] = CrewRole.ScienceOfficer,
        ["prospector"] = CrewRole.ScienceOfficer,
        ["core-asteroid"] = CrewRole.ScienceOfficer,

        ["danger"] = CrewRole.SecurityOfficer,
        ["announced-attack"] = CrewRole.SecurityOfficer,
        ["kills"] = CrewRole.SecurityOfficer,
        ["rival-territory"] = CrewRole.SecurityOfficer,

        ["route"] = CrewRole.Navigation,
        ["hyperspace-tunnel-delay"] = CrewRole.Navigation,
        ["arrival"] = CrewRole.Navigation,
    };

    /// <summary>Callout ids the core always speaks, whatever seats are filled.</summary>
    public static IReadOnlySet<string> Kept { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "ambient",
        "continuity",
        "domain",
        "lore",
        "scenes",
        "session",
        "session-length",
        "adventure",
        "story-clue",
        "carrier",
        "narrator",
        "npc-chatter",
        "carrier-fuel",
        "carrier-upkeep",
        "hold-full",
        "mining-summary",
        "powerplay-merits",
        "powerplay-salvage",
        "ring-hotspots",
        "unsold-data-at-risk",
        "reminders",
        "fighter",
        "community-goal-expiry",
        "outstanding-crimes",
    };

    /// <summary>
    /// The announcement spoken by the seat that holds its callout's role, or unchanged when the core says it:
    /// a line already given to another speaker, a kept callout, or a role with no seat on <paramref name="seats"/>.
    /// </summary>
    public static Announcement Recast(string calloutId, Announcement announcement, ShipSeats? seats)
    {
        ArgumentNullException.ThrowIfNull(announcement);

        if (announcement.Voice != VoiceRole.ShipAi
            || announcement.Speaker is not null
            || announcement.Pinned is not null
            || !Roles.TryGetValue(calloutId, out var role)
            || seats?.Seats.FirstOrDefault(seat => seat.Role == role) is not { } seat)
        {
            return announcement;
        }

        return announcement with { Voice = VoiceRole.Crew, Speaker = seat.Name, Seat = seat.Id };
    }
}

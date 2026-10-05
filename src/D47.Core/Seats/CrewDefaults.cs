using D47.Core.Knowledge;

namespace D47.Core.Seats;

public enum HullPurpose
{
    Combat,
    Trade,
    Exploration,
    Mining,
    Multipurpose,
}

/// <summary>The roster offered for a hull. Nothing is stored until the Commander accepts it.</summary>
public static class CrewDefaults
{
    private static readonly Dictionary<HullPurpose, CrewRole[]> Roles = new()
    {
        [HullPurpose.Combat] = [CrewRole.SecurityOfficer, CrewRole.Helm, CrewRole.Navigation],
        [HullPurpose.Trade] = [CrewRole.FirstOfficer, CrewRole.Navigation, CrewRole.Comms],
        [HullPurpose.Exploration] = [CrewRole.ScienceOfficer, CrewRole.Navigation, CrewRole.Helm],
        [HullPurpose.Mining] = [CrewRole.ScienceOfficer, CrewRole.Helm, CrewRole.FirstOfficer],
        [HullPurpose.Multipurpose] = [CrewRole.FirstOfficer, CrewRole.Navigation, CrewRole.SecurityOfficer],
    };

    private static readonly Dictionary<string, HullPurpose> Purposes = Group(
        (HullPurpose.Combat,
        [
            "federation_dropship", "federation_dropship_mkii", "federation_gunship", "ferdelance", "mamba",
            "python_nx", "typex", "typex_3", "vulture", "federation_corvette", "type9_military", "typex_2",
        ]),
        (HullPurpose.Trade, ["independant_trader", "orca", "belugaliner", "cutter", "panthermkii", "type9"]),
        (HullPurpose.Exploration, ["asp", "asp_scout", "krait_light", "mandalay"]),
        (HullPurpose.Mining, ["lakonminer"]),
        (HullPurpose.Multipurpose,
        [
            "adder", "cobramkiii", "cobramkiv", "empire_trader", "python", "cobramkv", "krait_mkii", "anaconda",
        ]));

    /// <summary>Four names per role. The offered one is picked from the ship id.</summary>
    private static readonly Dictionary<CrewRole, string[]> Names = new()
    {
        [CrewRole.FirstOfficer] = ["Marlow", "Okafor", "Brandt", "Teague"],
        [CrewRole.Helm] = ["Vasquez", "Quill", "Ashdown", "Rourke"],
        [CrewRole.Comms] = ["Linden", "Soto", "Pryce", "Halloran"],
        [CrewRole.ScienceOfficer] = ["Imani", "Voss", "Calder", "Nakamura"],
        [CrewRole.SecurityOfficer] = ["Kessler", "Dray", "Mbeki", "Sorensen"],
        [CrewRole.Navigation] = ["Fenwick", "Adeyemi", "Lund", "Castellan"],
    };

    /// <summary>A hull not in the purpose table is multipurpose.</summary>
    public static HullPurpose PurposeOf(string? hull) =>
        EliteSpecifications.Ship(hull) is { } ship && Purposes.TryGetValue(ship.Symbol, out var purpose)
            ? purpose
            : HullPurpose.Multipurpose;

    /// <summary>The hulls the purpose table names.</summary>
    public static IReadOnlyCollection<string> Hulls => Purposes.Keys;

    /// <summary>Empty when the hull offers no seats or its count is unknown.</summary>
    public static IReadOnlyList<CrewSeat> Offer(string? hull, int shipId)
    {
        var count = CrewSeats.CountFor(hull) ?? 0;
        var roles = Roles[PurposeOf(hull)];
        var pick = (int)(((long)shipId % 4 + 4) % 4);

        return
        [
            .. roles.Take(count).Select(role =>
                new CrewSeat(CrewSeat.NewId(), role, null, Names[role][pick])),
        ];
    }

    private static Dictionary<string, HullPurpose> Group(params (HullPurpose Purpose, string[] Hulls)[] groups) =>
        groups
            .SelectMany(group => group.Hulls.Select(hull => (hull, group.Purpose)))
            .ToDictionary(pair => pair.hull, pair => pair.Purpose, StringComparer.Ordinal);
}

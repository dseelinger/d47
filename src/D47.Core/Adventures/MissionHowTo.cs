namespace D47.Core.Adventures;

/// <summary>What a mission family involves, in one or two sentences, for every family a story can draw.</summary>
public static class MissionHowTo
{
    private static readonly Dictionary<string, string> Lines = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Mission_Massacre"] =
            "Massacre missions are flown: destroy the stated number of the target faction's ships in the destination system, then hand the mission in at the station that issued it.",
        ["Mission_Assassinate"] =
            "Assassination missions are flown: find and kill the named target in the destination system, in space or at the surface settlement the mission names, then hand the mission in at the station that issued it.",
        ["Mission_OnFoot_Massacre"] =
            "On-foot massacre missions are done on foot: kill the stated number of the target faction's members, then hand the mission in at the station that issued it.",
        ["Mission_OnFoot_Onslaught"] =
            "On-foot onslaught missions are done on foot: clear the named settlement of the target faction's people, then hand the mission in at the station that issued it.",
        ["Mission_OnFoot_Assassination"] =
            "On-foot assassination missions are done on foot: kill the named person at the named settlement, then hand the mission in at the station that issued it.",
        ["Mission_OnFoot_Defence"] =
            "On-foot defence missions are done on foot: protect the named items at the named settlement from attackers, then hand the mission in at the station that issued it.",
        ["Mission_Courier"] =
            "Courier missions are flown and need no cargo space: carry the data to the named station, where the mission completes.",
        ["Mission_Delivery"] =
            "Delivery missions are flown and need free cargo space: the issuing station loads the cargo, and the mission completes when it is handed in at the named station.",
        ["Mission_Collect"] =
            "Collect missions are flown. The board names the commodity and the amount but not where to buy it: buy it at any market that sells it and hand it in at the station that issued the mission.",
        ["Mission_OnFoot_Delivery"] =
            "On-foot delivery missions are done on foot: carry one package to the named contact at the named settlement, then hand the mission in at the station that issued it.",
        ["Mission_OnFoot_Collect"] =
            "On-foot collect missions are done on foot: fetch one item from the named settlement, or from a named contact there, and hand it in at the station that issued the mission.",
        ["Mission_Mining"] =
            "Mining missions are flown: mine the named commodity and hand the stated tonnage in at the station that issued the mission.",
        ["Mission_PassengerBulk"] =
            "Passenger missions are flown and need passenger cabins: carry the passengers to the named station, where the mission completes.",
        ["Mission_PassengerVIP"] =
            "VIP passenger missions are flown and need a passenger cabin of the class the passenger asks for, which they do not share: carry them to the named station, where the mission completes.",
        ["Mission_Sightseeing"] =
            "Sightseeing missions are flown and need passenger cabins: take the passengers to each system the mission names, then back to the station that issued it.",
        ["Mission_LongDistanceExpedition"] =
            "Long-distance expedition missions are flown and need passenger cabins: carry the passengers on a long trip to the destination the mission names, where it completes.",
        ["Mission_Rescue"] =
            "Rescue missions are flown: recover the named people or goods in the destination system, from a target ship or a surface site, and hand them in at the station that issued the mission.",
        ["Mission_Salvage"] =
            "Salvage missions are flown: find the named items in the destination system, scoop them into the hold and hand them in at the station that issued the mission.",
        ["Mission_OnFoot_Salvage"] =
            "On-foot salvage missions are done on foot: take the named item from the crash site the mission marks and hand it in at the station that issued the mission.",
        ["Mission_OnFoot_Reboot"] =
            "On-foot reboot missions are done on foot: restore power at the named settlement, which takes a power regulator, then hand the mission in at the station that issued it.",
        ["Mission_OnFoot_RebootRestore"] =
            "On-foot restore missions are done on foot: restore power at the named settlement with a power regulator and prepare it for operation, then hand the mission in at the station that issued it.",
        ["Mission_Smuggle"] =
            "Smuggling missions are flown and need free cargo space: the issuing station loads illegal cargo, and the mission completes when it is handed in at the named station. A completed security scan on the way brings a fine.",
        ["Mission_OnFoot_Smuggle"] =
            "On-foot smuggling missions are done on foot: carry contraband to the named contact at the named settlement, then hand the mission in at the station that issued it.",
        ["Mission_Piracy"] =
            "Piracy missions are flown: steal the named commodity from other ships and hand the stolen cargo in at the station that issued the mission.",
        ["Mission_OnFoot_Heist"] =
            "On-foot heist missions are done on foot: steal the named item from a settlement or a hidden cache and hand it in at the station that issued the mission.",
        ["Mission_OnFoot_ProductionHeist"] =
            "On-foot production heist missions are done on foot: steal a production sample from the named settlement and hand it in at the station that issued the mission.",
        ["Mission_OnFoot_Sabotage"] =
            "On-foot sabotage missions are done on foot: cut the power or stop production at the named settlement, then hand the mission in at the station that issued it.",
        ["Mission_OnFoot_AssassinationIllegal"] =
            "Illegal on-foot assassination missions are done on foot: kill the named person at the named settlement, who is not wanted, so the kill is a crime; then hand the mission in at the station that issued it.",
        ["Mission_OnFoot_MassacreIllegal"] =
            "Illegal on-foot massacre missions are done on foot: kill the stated number of the target faction's personnel, which is a crime, then hand the mission in at the station that issued it.",
        ["Mission_OnFoot_OnslaughtIllegal"] =
            "Illegal on-foot onslaught missions are done on foot: clear the named settlement of the target faction's personnel, which is a crime, then hand the mission in at the station that issued it.",
        ["Mission_OnFoot_SalvageIllegal"] =
            "Illegal on-foot salvage missions are done on foot: take the named item from wreckage, which is theft, and hand it in at the station that issued the mission.",
    };

    /// <summary>The line for this exact family, ignoring case, or null for a family with none.</summary>
    public static string? For(string? family) =>
        family is not null && Lines.TryGetValue(family.Trim(), out var line) ? line : null;
}

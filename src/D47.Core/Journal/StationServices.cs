namespace D47.Core.Journal;

/// <summary>The tokens in a <c>Docked</c> event's <c>StationServices</c>, each as a name in a fixed order.</summary>
public static class StationServices
{
    private static readonly string[] Everywhere =
        ["dock", "autodock", "flightcontroller", "stationoperations", "stationMenu"];

    private static readonly (string Token, string Name)[] Ordered =
    [
        ("commodities", "Commodities market"),
        ("contacts", "Contacts"),
        ("missions", "Missions"),
        ("missionsgenerated", "Generated missions"),
        ("ondockmission", "On-dock missions"),
        ("outfitting", "Outfitting"),
        ("shipyard", "Shipyard"),
        ("blackmarket", "Black market"),
        ("exploration", "Universal Cartographics"),
        ("vistagenomics", "Vista Genomics"),
        ("refuel", "Refuel"),
        ("repair", "Repair"),
        ("rearm", "Restock"),
        ("tuning", "Tuning"),
        ("engineer", "Engineer"),
        ("techBroker", "Technology broker"),
        ("materialtrader", "Material trader"),
        ("searchrescue", "Search and rescue"),
        ("voucherredemption", "Redeem vouchers"),
        ("powerplay", "Powerplay"),
        ("facilitator", "Interstellar Factors"),
        ("crewlounge", "Crew lounge"),
        ("bartender", "Bartender"),
        ("shop", "Shop"),
        ("livery", "Livery"),
        ("modulepacks", "Module packs"),
        ("pioneersupplies", "Pioneer Supplies"),
        ("apexinterstellar", "Apex Interstellar"),
        ("frontlinesolutions", "Frontline Solutions"),
        ("squadronBank", "Squadron bank"),
        ("refinery", "Refinery"),
        ("carriermanagement", "Carrier management"),
        ("carrierfuel", "Carrier fuel"),
        ("carriervendor", "Carrier vendor"),
        ("colonisationcontribution", "Colonisation contributions"),
        ("registeringcolonisation", "Colonisation registry"),
        ("socialspace", "Social space"),
    ];

    /// <summary>Whether every station offers it, so it is not worth drawing.</summary>
    public static bool IsEverywhere(string token) => Everywhere.Contains(token, StringComparer.Ordinal);

    /// <summary>A token's name and place in the order; a token not in the table is spaced and capitalised, and last.</summary>
    public static (string Name, int Order) Describe(string token)
    {
        var at = Array.FindIndex(Ordered, entry => entry.Token == token);

        return at >= 0
            ? (Ordered[at].Name, at)
            : (JournalJson.Spoken(JournalText.Spaced(token)) ?? token, Ordered.Length);
    }
}

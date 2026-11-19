namespace D47.Core.Tests.Knowledge;

/// <summary>Writes <c>CodexBodies.tsv</c> rows for tests.</summary>
internal static class KindRows
{
    public static string Row(
        string key,
        string name,
        string family,
        string parent = "",
        string subType = "",
        string band = "",
        string terraformable = "",
        string atmosphere = "",
        string rings = "",
        string usualBand = "",
        string regions = "") =>
        string.Join('\t', key, name, family, parent, subType, band, terraformable, atmosphere, rings, usualBand, regions) + "\n";

    /// <summary>A rocky body tree down to one terraformable kind with a thin carbon dioxide atmosphere, and a neutron star.</summary>
    public static string RockyBodyAndNeutronStar(string rockyRegions = "18:300", string starRegions = "18:900,34:12") =>
        Row("star.neutron_star", "Neutron Star", "Stars", subType: "Neutron Star", regions: starRegions)
        + Row("planet.rocky_body", "Rocky body", "Terrestrial planets", subType: "Rocky body")
        + Row("planet.rocky_body.terraformable", "Rocky body, terraformable", "Terrestrial planets", "planet.rocky_body", "Rocky body", terraformable: "yes")
        + Row(
            "planet.rocky_body.terraformable.atmosphere",
            "Rocky body, terraformable, with atmosphere",
            "Terrestrial planets",
            "planet.rocky_body.terraformable",
            "Rocky body",
            terraformable: "yes")
        + Row(
            "planet.rocky_body.terraformable.atmosphere.thin_carbon_dioxide",
            "Rocky body, terraformable, Thin Carbon dioxide",
            "Terrestrial planets",
            "planet.rocky_body.terraformable.atmosphere",
            "Rocky body",
            terraformable: "yes",
            atmosphere: "Thin Carbon dioxide",
            regions: rockyRegions);
}

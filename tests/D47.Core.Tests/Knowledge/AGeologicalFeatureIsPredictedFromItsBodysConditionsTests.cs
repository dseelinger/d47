using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>The generated geology table, and what a body's own scan can predict from it.</summary>
public class AGeologicalFeatureIsPredictedFromItsBodysConditionsTests
{
    private const string Rows =
        "entry_id\tname\tplanet_types\tatmospheres\tvolcanism\n" +
        "1\tWater Ice Fumarole\ticybody;rockyicebody\tnone\tmajorwatergeysers;minorwatermagma\n" +
        "2\tWater Ice Geyser\ticybody\tnone;neon\tmajorwatergeysers\n" +
        "3\tIron Magma Lava Spout\trockybody\tnone\tminormetallicmagma\n";

    [Fact]
    public void AnIcyBodyWithMajorWaterGeysersAndNoAtmosphereGetsTheRowsListingThem()
    {
        var likely = GeologyTable.Parse(Rows).Likely("Icy body", "", "major water geysers volcanism");

        Assert.Equal(["Water Ice Fumarole", "Water Ice Geyser"], likely.Select(feature => feature.Name));
    }

    [Fact]
    public void AnIcyBodyDoesNotGetARowListingOnlyRockyBodies()
    {
        var likely = GeologyTable.Parse(Rows).Likely("Icy body", "", "major water geysers volcanism");

        Assert.DoesNotContain(likely, feature => feature.Name == "Iron Magma Lava Spout");
    }

    [Fact]
    public void ABodyWithNoVolcanismGetsNoFeature()
    {
        Assert.Empty(GeologyTable.Parse(Rows).Likely("Icy body", "", ""));
    }

    [Fact]
    public void TheJournalsWordsFoldToSpanshsWords()
    {
        Assert.Equal(
            BodyConditionFold.NormalisePlanetType("Icy world"),
            BodyConditionFold.NormalisePlanetType("Icy body"));
        Assert.Equal(
            BodyConditionFold.NormaliseVolcanism("Major Water Geysers"),
            BodyConditionFold.NormaliseVolcanism("major water geysers volcanism"));
        Assert.Equal(
            BodyConditionFold.NormaliseAtmosphere(null),
            BodyConditionFold.NormaliseAtmosphere(""));
    }

    [Fact]
    public void ARockyBodyWithMinorMetallicMagmaAndNoAtmosphereIncludesIronMagmaLavaSpoutAndSulphurDioxideGasVent()
    {
        var names = GeologyCatalogue.Table
            .Likely("Rocky body", "", "minor metallic magma volcanism")
            .Select(feature => feature.Name)
            .ToList();

        Assert.Contains("Iron Magma Lava Spout", names);
        Assert.Contains("Sulphur Dioxide Gas Vent", names);
    }

    [Fact]
    public void AnIcyBodyWithMajorWaterGeysersGivesWaterIceFumaroleAndWaterIceGeyser()
    {
        var names = GeologyCatalogue.Table
            .Likely("Icy body", "", "major water geysers volcanism")
            .Select(feature => feature.Name)
            .ToList();

        Assert.Contains("Water Ice Fumarole", names);
        Assert.Contains("Water Ice Geyser", names);
    }

    [Fact]
    public void ABodyScanIsLookedUpByItsOwnConditions()
    {
        var scan = new BodyScan("Fixture 3 a")
        {
            PlanetClass = "Icy body",
            Atmosphere = "",
            Volcanism = "major water geysers volcanism",
        };

        Assert.Contains(GeologyCatalogue.Likely(scan), feature => feature.Name == "Water Ice Geyser");
    }

    [Fact]
    public void TheShippedTableHasOneRowForEachGeologySurfaceFeatureInTheCodex()
    {
        var geology = CodexCatalogue.Table.All
            .Where(entry => entry.SubCategory == "Geology and anomalies"
                && !entry.Name.Contains("Lagrange", StringComparison.Ordinal)
                && !entry.Name.Contains("Anomaly", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(23, geology.Count);
        Assert.Equal(
            geology.Select(entry => (entry.EntryId, entry.Name)).Order(),
            GeologyCatalogue.Table.All.Select(feature => (feature.EntryId, feature.Name)).Order());
    }

    [Fact]
    public void ABodysGeologicalSignalsAreCounted()
    {
        Assert.Equal(3, Biology("""{"Type":"$SAA_SignalType_Geological;","Count":3}""").GeologicalCount);
    }

    [Fact]
    public void ABodyWithOnlyBiologicalSignalsHasNoGeologicalCount()
    {
        Assert.Equal(0, Biology("""{"Type":"$SAA_SignalType_Biological;","Count":2}""").GeologicalCount);
    }

    private static BodyBiology Biology(string signal)
    {
        var json = $$"""
            {"timestamp":"2026-08-16T10:00:00Z","event":"FSSBodySignals","BodyName":"Fixture 3 a",
             "SystemAddress":1000200,"BodyID":3,"Signals":[{{signal}}]}
            """;

        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));

        return BodySignals.Empty.Apply(parsed!).All.Single();
    }
}

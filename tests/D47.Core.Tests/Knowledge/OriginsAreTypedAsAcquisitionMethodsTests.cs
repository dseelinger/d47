using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>Materials.tsv's methods column, typed from EDEngineer's origin text (#177).</summary>
public class OriginsAreTypedAsAcquisitionMethodsTests
{
    private static readonly string[] SurfaceMined =
    [
        "bastnasite", "deuterium", "diamond", "helium", "helium3", "iridium", "magnesite",
        "olivine", "periclasedunite", "quartzpyroxenite", "ruby", "sapphire", "thortveitite",
    ];

    [Fact]
    public void PainiteIsRingMined()
    {
        var painite = MaterialCatalogue.Find("painite");

        Assert.NotNull(painite);
        Assert.Contains(AcquisitionMethod.RingMining, painite.Methods);
    }

    [Fact]
    public void ModularTerminalsIsOnlyAMissionReward()
    {
        var terminals = MaterialCatalogue.Find("modularterminals");

        Assert.NotNull(terminals);
        Assert.Equal([AcquisitionMethod.MissionReward], terminals.Methods);
    }

    [Fact]
    public void HardwareDiagnosticSensorIsSoldAtAMarket()
    {
        var sensor = MaterialCatalogue.Find("diagnosticsensor");

        Assert.NotNull(sensor);
        Assert.Contains(AcquisitionMethod.Market, sensor.Methods);
    }

    [Fact]
    public void EveryRealOriginTypesAtLeastOneMethod()
    {
        Assert.All(MaterialCatalogue.All, entry =>
        {
            if (entry.Origins.Count > 0)
            {
                Assert.True(entry.Methods.Count > 0, $"{entry.Name} has an origin but no method");
            }
        });
    }

    [Fact]
    public void TheThirteenSurfaceMinedCommoditiesAreCargoWithTheMethod()
    {
        foreach (var symbol in SurfaceMined)
        {
            var entry = MaterialCatalogue.Find(symbol);

            Assert.NotNull(entry);
            Assert.Equal(MaterialLedger.Cargo, entry.Ledger);
            Assert.Contains(AcquisitionMethod.SurfaceMining, entry.Methods);
        }
    }

    [Fact]
    public void SapphireIsOnlySurfaceMined()
    {
        var sapphire = MaterialCatalogue.Find("sapphire");

        Assert.NotNull(sapphire);
        Assert.Equal([AcquisitionMethod.SurfaceMining], sapphire.Methods);
    }

    [Fact]
    public void NoOtherRowClaimsSurfaceMining()
    {
        var surfaceMined = MaterialCatalogue.All
            .Where(entry => entry.Methods.Contains(AcquisitionMethod.SurfaceMining))
            .Select(entry => entry.Symbol)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(SurfaceMined.Order(StringComparer.Ordinal), surfaceMined);
    }
}

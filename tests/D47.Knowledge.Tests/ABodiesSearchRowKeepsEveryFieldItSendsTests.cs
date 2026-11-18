using D47.Core.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

public sealed class ABodiesSearchRowKeepsEveryFieldItSendsTests
{
    private static BodySummary Row(string name)
    {
        using var document = Fixture.Json("spansh-bodies-search-polonium-sol-full.json");
        return Assert.Single(SpanshResponse.ReadBodies(document).Bodies, body => body.Name == name);
    }

    [Fact]
    public void ARowKeepsItsOrbitalElementsInTheDumpsUnits()
    {
        var body = Row("Alpha Centauri B 1");

        Assert.Equal(3.23569988487269, body.OrbitalPeriod!.Value, 9);
        Assert.Equal(0.0418506303973275, body.SemiMajorAxis!.Value, 9);
        Assert.Equal(0, body.OrbitalEccentricity);
        Assert.Equal(0.052105, body.OrbitalInclination!.Value, 6);
        Assert.Equal(0.14498028069444, body.OrbitalSynchronicity!.Value, 9);
        Assert.Equal(4.949055, body.ArgOfPeriapsis!.Value, 6);
        Assert.Equal(0, body.AscendingNode);
        Assert.Equal(269.519263, body.MeanAnomaly!.Value, 6);
        Assert.Equal(3.38068016556713, body.RotationalPeriod!.Value, 9);
    }

    [Fact]
    public void AnAxisTiltIsReadInRadiansAndKeptInDegrees()
    {
        Assert.Equal(-0.219615 * 180 / Math.PI, Row("Sol").AxialTilt!.Value, 6);
    }

    [Fact]
    public void ARowKeepsItsCompositionsAndBodyFacts()
    {
        var body = Row("Ross 248 B 4");

        Assert.Equal(49.354195, body.AtmosphereComposition["Helium"], 6);
        Assert.Equal(50.645798, body.AtmosphereComposition["Neon"], 6);
        Assert.Equal(["Bacterial"], body.Genera);

        var metal = Row("Alpha Centauri B 1");

        Assert.Equal(100, metal.SolidComposition["Metal"]);
        Assert.Equal(0, metal.SolidComposition["Rock"]);
        Assert.Equal(0.674523, metal.EarthMasses!.Value, 6);
        Assert.Equal(4314.73, metal.Radius!.Value, 2);
        Assert.Equal(0, metal.SurfacePressure);
        Assert.Equal(33187, metal.ScanValue);
        Assert.Equal(18, metal.SignalCount);
        Assert.Equal(DateTimeOffset.Parse("2026-11-15T11:01:56Z"), metal.SignalsSeen);
        Assert.Equal(180145163803298155, metal.BodyAddress);
        Assert.Equal("Planet", metal.Type);
        Assert.Equal(["Life Support", "Limpets", "SRV Repair"], metal.SynthesisRecipes.Select(recipe => recipe.Name));
        Assert.All(metal.SynthesisRecipes, recipe => Assert.Equal("Basic", recipe.Level));
        Assert.Equal("Inner Orion Spur", metal.SystemRegion);
        Assert.Equal("Aisling Duval", metal.ControllingPower);
        Assert.Equal("Stronghold", metal.PowerState);
        Assert.Equal(DateTimeOffset.Parse("2026-11-17T22:58:23Z"), metal.ReportedAt);
    }

    [Fact]
    public void ARowKeepsEachLandmarkWithItsPosition()
    {
        var body = Row("Alpha Centauri B 1");

        Assert.Equal(22, body.Landmarks.Count);
        var vent = body.Landmarks[0];
        Assert.Equal(new BodyLandmark(1400414, "Gas Vent", "Silicate Vapour Gas Vent", null, -13.036003, 80.604256, 0), vent);
        Assert.Equal("Magenta", Row("Ross 248 B 4").Landmarks[0].Variant);
        Assert.Equal(1000000, Row("Ross 248 B 4").LandmarkValue);
    }

    [Fact]
    public void ARowKeepsItsParentChain()
    {
        var parents = Row("Alpha Centauri B 1").ParentBodies;

        Assert.Equal(new BodyParent("Star", 108087569765370219, "K (Yellow-Orange) Star"), parents[0]);
        Assert.Equal(["Star", "Null", "Null"], parents.Select(parent => parent.Kind));
    }

    [Fact]
    public void AStarKeepsItsBeltsAndStellarFields()
    {
        var sol = Row("Sol");

        Assert.Equal(new BeltProfile("Asteroid Belt", "Rocky", 4060500000000, 308170000000, 489190000000), Assert.Single(sol.Belts));
        Assert.True(sol.IsMainStar);
        Assert.Equal("Star", sol.Type);
        Assert.Equal("G2", sol.SpectralClass);
        Assert.Equal("V", sol.Luminosity);
        Assert.Equal(1, sol.SolarMasses);
        Assert.Equal(0.999999953989935, sol.SolarRadius!.Value, 9);
        Assert.Equal(4792, sol.Age);
    }

    [Fact]
    public void ARowKeepsEachStationOnItsBody()
    {
        var stations = Row("Luhman 16 A 1").Stations;

        Assert.Equal(8, stations.Count);
        var settlement = Assert.Single(stations, station => station.Name == "Cha Mineralogic Site");
        Assert.Equal("Luhman 16 A 1", settlement.Body);
        Assert.Equal("Luhman 16", settlement.SystemName);
        Assert.Equal("Settlement", settlement.Type);
        Assert.Equal("Pirate Attack", settlement.ControllingFactionState);
        Assert.Equal(3886137344, settlement.MarketId);
        Assert.Equal(1, settlement.SmallPads);
        Assert.True(settlement.HasMarket);
        Assert.Contains("Interstellar Factors Contact", settlement.Services);
    }

    [Fact]
    public void ARowWithoutAFieldLeavesItEmpty()
    {
        var body = Row("Duamta 1");

        Assert.Empty(body.Belts);
        Assert.Empty(body.Genera);
        Assert.Null(body.SpectralClass);
        Assert.False(body.IsMainStar);
    }
}

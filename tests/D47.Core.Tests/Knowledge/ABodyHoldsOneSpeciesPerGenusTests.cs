using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>What a body's biology could reach, from its scan and its biological signal count.</summary>
public class ABodyHoldsOneSpeciesPerGenusTests
{
    // Rocky, thin carbon dioxide, no volcanism, 0.5 g, 200 K, 0.02 atm: Bacterium Aurasus and Tela, Stratum
    // Cucumisis and Frigus.
    private static BodyScan TwoGenera() => new("Fixture 3 b")
    {
        PlanetClass = "Rocky body",
        Atmosphere = "thin carbon dioxide atmosphere",
        Volcanism = "",
        SurfaceGravity = 0.5 * 9.80665,
        SurfaceTemperature = 200,
        SurfacePressure = 0.02 * 101325,
        Landable = true,
    };

    private static long Value(string species) =>
        ExobiologyCatalogue.All.Single(entry => entry.Species == species).Value;

    [Fact]
    public void TwoSignalsAddTheHighestValueOfEachAdmittedGenus()
    {
        var estimate = BiologyPotential.For(TwoGenera(), 2, []);

        Assert.NotNull(estimate);
        Assert.Equal(Value("Stratum Cucumisis") + Value("Bacterium Tela"), estimate.BestCase);
        Assert.Equal(["Stratum", "Bacterium"], estimate.Genera);
        Assert.Null(estimate.Low);
        Assert.Null(estimate.High);
    }

    [Fact]
    public void TwoPossibleSpeciesOfOneGenusCountOnceAtTheHigherValue()
    {
        var estimate = BiologyPotential.For(TwoGenera(), 3, []);

        Assert.NotNull(estimate);
        Assert.Equal(Value("Stratum Cucumisis") + Value("Bacterium Tela"), estimate.BestCase);
    }

    [Fact]
    public void OneSignalTakesOnlyTheMostValuableGenus()
    {
        var estimate = BiologyPotential.For(TwoGenera(), 1, []);

        Assert.NotNull(estimate);
        Assert.Equal(Value("Stratum Cucumisis"), estimate.BestCase);
        Assert.Equal(["Stratum"], estimate.Genera);
    }

    [Fact]
    public void NamedGeneraDrawTheRangeFromThoseGeneraOnly()
    {
        var estimate = BiologyPotential.For(TwoGenera(), 2, ["Bacterium"]);

        Assert.NotNull(estimate);
        Assert.Equal(["Bacterium"], estimate.Genera);
        Assert.Equal(Value("Bacterium Aurasus"), estimate.Low);
        Assert.Equal(Value("Bacterium Tela"), estimate.High);
        Assert.Equal(Value("Bacterium Tela"), estimate.BestCase);
    }

    [Fact]
    public void ANamedGenusTheConditionsDoNotAdmitDrawsOnTheWholeGenus()
    {
        var estimate = BiologyPotential.For(TwoGenera(), 1, ["Fonticulua"]);
        var fonticulua = ExobiologyCatalogue.All.Where(entry => entry.Genus == "Fonticulua").ToList();

        Assert.NotNull(estimate);
        Assert.Equal(fonticulua.Min(entry => entry.Value), estimate.Low);
        Assert.Equal(fonticulua.Max(entry => entry.Value), estimate.High);
    }

    [Fact]
    public void AScanWithoutGravityGivesNoEstimate() =>
        Assert.Null(BiologyPotential.For(TwoGenera() with { SurfaceGravity = null }, 2, []));
}

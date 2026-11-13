using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class ASpeciesIsMatchedToItsRegionByRegionNotByStringTests
{
    private static readonly BodyConditions CarbonDioxideBody = new()
    {
        PlanetClass = "Rocky body",
        Atmosphere = "thin carbon dioxide atmosphere",
        Volcanism = "",
        Gravity = 0.1,
        Temperature = 177,
        Pressure = 0.05,
    };

    private static readonly BodyConditions AmmoniaBody = new()
    {
        PlanetClass = "Rocky body",
        Atmosphere = "thin ammonia atmosphere",
        Volcanism = "",
        Gravity = 0.1,
        Temperature = 160,
        Pressure = 0.005,
    };

    [Fact]
    public void TheFormidineRiftAsTheMapNamesItFindsTheSpeciesListedUnderThe()
    {
        var unfiltered = ExobiologyCatalogue.Possible(CarbonDioxideBody);
        var inRegion = ExobiologyCatalogue.Possible(CarbonDioxideBody, "Formidine Rift");

        Assert.Contains(unfiltered, entry => entry.Species == "Aleoida Arcus");
        var listed = unfiltered.Where(entry => entry.Regions.Contains("The Formidine Rift")).Select(entry => entry.Species);

        Assert.Contains(inRegion, entry => entry.Species == "Aleoida Arcus");
        Assert.Equal(listed, inRegion.Select(entry => entry.Species));
    }

    [Fact]
    public void ARegionASpeciesDoesNotListStillDropsIt()
    {
        Assert.Contains(ExobiologyCatalogue.Possible(AmmoniaBody), entry => entry.Species == "Aleoida Laminiae");
        Assert.DoesNotContain(
            ExobiologyCatalogue.Possible(AmmoniaBody, "Formidine Rift"),
            entry => entry.Species == "Aleoida Laminiae");
    }

    [Fact]
    public void ARegionNameTheMapDoesNotKnowDropsEverySpecies() =>
        Assert.Empty(ExobiologyCatalogue.Possible(CarbonDioxideBody, "Nowhere In Particular"));
}

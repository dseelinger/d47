using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class TheExobiologyFillCountsTheSpeciesListedForEachRegionTests
{
    [Fact]
    public void EveryRegionNameInTheCatalogueResolvesToARegion()
    {
        var names = ExobiologyCatalogue.All.SelectMany(entry => entry.Regions).Distinct(StringComparer.Ordinal).ToList();

        Assert.NotEmpty(names);
        Assert.All(names, name => Assert.True(GalacticRegions.IdOf(name) is not null, name));
    }

    [Theory]
    [InlineData("Inner Orion Spur")]
    [InlineData("Aquila's Halo")]
    [InlineData("The Formidine Rift")]
    public void ARegionsCountIsTheCatalogueRowsThatListIt(string region)
    {
        var id = GalacticRegions.IdOf(region)!.Value;
        var rows = ExobiologyCatalogue.All.Count(entry => entry.Regions.Contains(region, StringComparer.Ordinal));

        Assert.True(rows > 0);
        Assert.Equal(rows, ExobiologyCatalogue.SpeciesPerRegion()[id]);
    }

    [Fact]
    public void TheCodexAndTheMapNameTheFormidineRiftAsOneRegion()
    {
        Assert.Equal(GalacticRegions.IdOf("Formidine Rift"), GalacticRegions.IdOf("The Formidine Rift"));
        Assert.NotEqual(GalacticRegions.IdOf("The Abyss"), GalacticRegions.IdOf("The Void"));
    }

    [Theory]
    [InlineData(49, 0)]
    [InlineData(62, 0)]
    [InlineData(63, 1)]
    [InlineData(77, 2)]
    [InlineData(90, 2)]
    [InlineData(91, 3)]
    [InlineData(105, 3)]
    public void FourEqualBandsDivideTheRangeFromTheLowestCountToTheHighest(int count, int step)
    {
        Assert.Equal(step, ExobiologyCatalogue.Step(count, 49, 105));
    }

    [Fact]
    public void TheFewestSpeciesAreTheLowestStepAndTheMostTheHighest()
    {
        var counts = ExobiologyCatalogue.SpeciesPerRegion();
        var steps = ExobiologyCatalogue.RegionSteps();

        Assert.Equal(counts.Keys.Order(), steps.Keys.Order());
        Assert.Equal(0, steps[counts.MinBy(pair => pair.Value).Key]);
        Assert.Equal(ExobiologyCatalogue.FillSteps - 1, steps[counts.MaxBy(pair => pair.Value).Key]);
        Assert.True(steps.Values.Distinct().Count() > 1);
    }
}

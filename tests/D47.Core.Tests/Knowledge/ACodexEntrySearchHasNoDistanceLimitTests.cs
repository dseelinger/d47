using D47.Core.Capabilities.Builtin;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class ACodexEntrySearchHasNoDistanceLimitTests
{
    private static BodyQuery Parse(BodyRequest request)
    {
        Assert.True(BodyQuery.TryParse(request, out var query, out var failure), failure);

        return query;
    }

    private static readonly BodySearchResult None = new("Sol", 0, []);

    [Fact]
    public void ACodexEntryWithNoDistanceHasNoLimit() =>
        Assert.True(Parse(new() { CodexEntry = "Bacterium Cerbrus" }).NoDistanceLimit);

    [Fact]
    public void ACodexEntryWithADistanceIsBoundedAtTheCeiling()
    {
        var query = Parse(new() { CodexEntry = "Bacterium Cerbrus", MaxDistance = 5000 });

        Assert.False(query.NoDistanceLimit);
        Assert.Equal(BodyQuery.MaxDistanceCeiling, query.MaxDistance);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Norma Arm")]
    public void ASearchWithoutACodexEntryKeepsTheDefaultOf500(string? region)
    {
        var query = Parse(new() { Landable = true, Region = region });

        Assert.False(query.NoDistanceLimit);
        Assert.Equal(500, query.MaxDistance);
    }

    [Fact]
    public void NothingInARegionNamesTheRegionAndNoDistance()
    {
        var query = Parse(new() { CodexEntry = "Bacterium Cerbrus", Region = "Norma Arm" });

        Assert.Equal("Nothing in Norma Arm matches that, with Bacterium Cerbrus recorded.", GalaxyCapability.Describe(None, query));
    }

    [Fact]
    public void NothingWithNoRegionNamesNoDistance()
    {
        var query = Parse(new() { CodexEntry = "Bacterium Cerbrus" });

        Assert.Equal("Nothing matches that, with Bacterium Cerbrus recorded.", GalaxyCapability.Describe(None, query));
    }

    [Fact]
    public void ALimitedSearchStillSaysItsDistance()
    {
        var query = Parse(new() { CodexEntry = "Bacterium Cerbrus", MaxDistance = 100 });

        Assert.Equal("Nothing within 100 light years matches that, with Bacterium Cerbrus recorded.", GalaxyCapability.Describe(None, query));
    }

    [Fact]
    public void ARegionIsMatchedWithoutCaseOrALeadingThe()
    {
        Assert.Equal("Formidine Rift", Parse(new() { CodexEntry = "Bacterium Cerbrus", Region = "the formidine rift" }).Region);
        Assert.Equal(42, GalacticRegions.Names.Count);
    }
}

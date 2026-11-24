using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>The Systems search a High Grade Emission material's tile runs carries its group's conditions (#2735).</summary>
public sealed class AHighGradeEmissionSearchAsksForTheGroupsConditionsTests
{
    private static IReadOnlyDictionary<string, string> For(string symbol) =>
        EmissionRules.SearchArguments(EmissionRules.Holding(symbol)!);

    [Fact]
    public void ImperialShieldingAsksForEmpireWithin500LightYearsAndAMillionPeople()
    {
        var arguments = For("imperialshielding");

        Assert.Equal("Empire", arguments["allegiance"]);
        Assert.Equal("500", arguments["distance"]);
        Assert.Equal("1000000-", arguments["population"]);
        Assert.DoesNotContain("state", arguments.Keys);
    }

    [Fact]
    public void MilitaryGradeAlloysAlsoAsksForWarOrCivilWar()
    {
        Assert.Equal("War,Civil War", For("militarygradealloys")["state"]);
    }

    [Fact]
    public void PharmaceuticalIsolatorsAsksForIndependentOrAllianceInOutbreak()
    {
        var arguments = For("pharmaceuticalisolators");

        Assert.Equal("Independent,Alliance", arguments["allegiance"]);
        Assert.Equal("Outbreak", arguments["state"]);
    }

    [Fact]
    public void EveryGroupsSearchIsOneTheSystemsSearchAccepts()
    {
        foreach (var group in EmissionRules.Groups)
        {
            Assert.True(
                GalaxyCriteria.TryParse(GalaxySearchKind.Systems, EmissionRules.SearchArguments(group)
                    .Where(pair => pair.Key != "distance")
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal), out _, out var failure),
                failure);
        }
    }
}

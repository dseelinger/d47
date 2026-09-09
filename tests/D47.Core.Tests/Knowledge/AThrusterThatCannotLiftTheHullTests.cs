using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary> A thruster is not offered to a hull it cannot move. </summary>
public class AThrusterThatCannotLiftTheHullTests
{
    private static IReadOnlyList<ModuleSpecification> Thrusters(string hull) =>
        EliteSpecifications.Slot(hull, "MainEngines") is { } slot
            ? EliteSpecifications.ModulesFor(slot)
            : [];

    private static IReadOnlyList<string> Enhanced(string hull) =>
        [.. Thrusters(hull)
            .Where(module => module.Name == "Enhanced Performance Thrusters")
            .Select(module => module.Symbol)
            .Order(StringComparer.Ordinal)];

    [Theory]
    [InlineData("anaconda")]        // 400 t
    [InlineData("type9_military")]  // 1,200 t
    [InlineData("cutter")]          // 1,100 t
    [InlineData("federation_corvette")]
    public void ABigHullIsNotOfferedThrustersThatCannotLiftIt(string hull)
    {
        Assert.Empty(Enhanced(hull));

        // And it still has thrusters to choose from — the rule narrows the list rather than emptying it,
        // which is the failure a filter like this can have.
        Assert.NotEmpty(Thrusters(hull));
    }

    [Theory]
    [InlineData("sidewinder", 1)]   // 25 t, size 2 mount — the size 2 only
    [InlineData("eagle", 2)]        // 50 t, size 3 mount — both
    [InlineData("cobramkiii", 1)]   // 180 t, size 4 mount — the size 3 only, at 200 t
    public void ASmallHullStillGetsTheOnesItCanUse(string hull, int expected) =>
        Assert.Equal(expected, Enhanced(hull).Count);

    [Fact]
    public void TheRuleIsAboutMassAndNotAboutTheEnhancedOnes()
    {
        // Ordinary undersized thrusters are filtered by exactly the same arithmetic.
        var offered = Thrusters("anaconda");

        Assert.All(offered, module => Assert.Contains(
            module.Figures,
            figure => !string.Equals(figure.Name, "maximum mass", StringComparison.OrdinalIgnoreCase)
                      || double.Parse(figure.Value, System.Globalization.CultureInfo.InvariantCulture) >= 400));
    }

    [Fact]
    public void AHullWithNoMassRecordedIsNotFilteredOnAGuess()
    {
        // Four hulls have a slot layout and no ships row, so there is no mass to test against.
        Assert.Null(EliteSpecifications.Ship("explorer_nx"));
        Assert.NotEmpty(Enhanced("explorer_nx"));
    }
}

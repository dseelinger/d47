using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// A thruster is not offered to a hull it cannot move (reported 2026-08-20: <i>"If a ship cannot
/// buy Enhanced Performance Thrusters do not provide it as an option via Loadout"</i>).
/// <para>
/// <b>The size rule alone was not enough</b>, and could not be: a small module does fit a large
/// socket, so an Anaconda's size 7 thruster mount was offered the size 2 and size 3 Enhanced
/// Performance Thrusters along with everything else. Each carries the heaviest hull it will move
/// in its own figures — 120 tonnes and 200 — and the ships section carries every hull's mass, so
/// the arithmetic is in the shipped tables and needs no list anybody maintains.
/// </para>
/// </summary>
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

        // And it still has thrusters to choose from — the rule narrows the list rather than
        // emptying it, which is the failure a filter like this can have.
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
        // Ordinary undersized thrusters are filtered by exactly the same arithmetic. A size 3
        // stock thruster tops out at 200 tonnes too, and an Anaconda is 400.
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
        // A rule that cannot be evaluated must not refuse.
        Assert.Null(EliteSpecifications.Ship("explorer_nx"));
        Assert.NotEmpty(Enhanced("explorer_nx"));
    }
}

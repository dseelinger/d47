using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary> Where <c>find_material</c> sends a Commander for a high-grade-emission material. </summary>
public class FindMaterialAsksTheOneTableTests
{
    /// <summary>
    /// The origins text of every emission material, which is what the deleted implementation read.
    /// </summary>
    [Fact]
    public void TheOriginsProseIsNotSafeToMatchStatesAgainst()
    {
        var carrying = MaterialCatalogue.All
            .Where(entry => entry.Origins.Any(origin =>
                origin.Contains("High grade emissions", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var reward = carrying
            .Where(entry => string.Join(" ", entry.Origins).Contains("reward", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Not an assertion about a number that might legitimately move — an assertion that the hazard is real
        // and general, so nobody reintroduces the substring match.
        Assert.NotEmpty(reward);

        Assert.All(
            reward,
            entry => Assert.Contains(
                "war",
                string.Join(" ", entry.Origins),
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Imperial Shielding is Empire and no state at all.</summary>
    [Fact]
    public void ImperialShieldingCarriesNoState()
    {
        var group = EmissionRules.Holding("imperialshielding");

        Assert.NotNull(group);
        Assert.Equal("Empire", group!.Allegiance);
        Assert.Empty(group.States);
    }

    /// <summary>And the four state groups are Independent, never a superpower.</summary>
    [Theory]
    [InlineData("improvisedcomponents", "CivilUnrest")]
    [InlineData("militarygradealloys", "War")]
    [InlineData("protoradiolicalloys", "Boom")]
    [InlineData("pharmaceuticalisolators", "Outbreak")]
    public void AStateGroupIsIndependentAndCarriesItsOwnStates(string symbol, string state)
    {
        var group = EmissionRules.Holding(symbol);

        Assert.NotNull(group);
        Assert.Equal("Independent", group!.Allegiance);
        Assert.Contains(state, group.States, StringComparer.OrdinalIgnoreCase);

        // The one thing eight materials wrongly carried.
        if (!string.Equals(state, "War", StringComparison.OrdinalIgnoreCase))
        {
            Assert.DoesNotContain("War", group.States, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>There is one table.</summary>
    [Fact]
    public void TheCapabilityDerivesNoConditionsOfItsOwn()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "src", "D47.Core", "Capabilities", "Builtin", "EngineeringCapability.cs"));

        Assert.DoesNotContain("StateOf", source, StringComparison.Ordinal);
        Assert.Contains("EmissionRules.Holding", source, StringComparison.Ordinal);

        // The population floor is the capability's to apply, and it must come from the table rather than be
        // written out again as a number.
        Assert.Contains("EmissionRules.MinimumPopulation", source, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);

        while (at is not null && !File.Exists(Path.Combine(at.FullName, "CLAUDE.md")))
        {
            at = at.Parent;
        }

        Assert.NotNull(at);
        return at!.FullName;
    }
}

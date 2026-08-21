using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// Where <c>find_material</c> sends a Commander for a high-grade-emission material
/// (reported 2026-08-21).
/// <para>
/// The report: <i>"Put the closest system where I can find imperial shielding on the clipboard."</i>
/// → <i>"Scorpii Sector IW-W a1-1. 41.3 light years, Empire, in war"</i> — and the Commander's
/// verdict, <i>"2 out of 2 I've tried are wrong."</i>
/// </para>
/// <para>
/// <b>The cause was a second copy of the trigger table, derived by substring-matching each
/// material's origins prose.</b> Almost every origins string ends <c>"; Mission reward"</c>, and
/// <b>War</b> appears inside <b>reward</b> — so eight of the ten materials picked up a state that is
/// not theirs. These tests exist to keep there being one table.
/// </para>
/// </summary>
public class FindMaterialAsksTheOneTableTests
{
    /// <summary>
    /// The origins text of every emission material, which is what the deleted implementation read.
    /// <b>Eight of ten contain the word "reward"</b>, and that is the whole defect.
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

        // Not an assertion about a number that might legitimately move — an assertion that the
        // hazard is real and general, so nobody reintroduces the substring match.
        Assert.NotEmpty(reward);

        Assert.All(
            reward,
            entry => Assert.Contains(
                "war",
                string.Join(" ", entry.Origins),
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// <b>Imperial Shielding is Empire and no state at all.</b> The reported answer searched Empire
    /// <em>and</em> War, which is why it landed on a procedural system rather than a populous
    /// Imperial one.
    /// </summary>
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

    /// <summary>
    /// <b>There is one table.</b> The capability holds no second derivation of the conditions —
    /// which is what this whole defect was, and what a later convenience would recreate.
    /// </summary>
    [Fact]
    public void TheCapabilityDerivesNoConditionsOfItsOwn()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "src", "D47.Core", "Capabilities", "Builtin", "EngineeringCapability.cs"));

        Assert.DoesNotContain("StateOf", source, StringComparison.Ordinal);
        Assert.Contains("EmissionRules.Holding", source, StringComparison.Ordinal);

        // The population floor is the capability's to apply, and it must come from the table
        // rather than be written out again as a number.
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

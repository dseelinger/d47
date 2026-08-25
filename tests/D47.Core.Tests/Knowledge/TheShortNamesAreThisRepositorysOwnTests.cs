using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// d47's own short names for modules (docs/plans/change-requests.md 38).
/// <para>
/// <b>The collision is the whole reason there is a rule.</b> <c>PD</c> is Point Defence and it is
/// also the Power Distributor, and a tie-break a Commander has to remember is a tie-break that
/// will be got wrong at a workshop. The ruling of 2026-08-25 was truncation rather than a winner:
/// <b>Point Def.</b> and <b>Power Dist.</b>, and the collision does not arise.
/// </para>
/// </summary>
public class TheShortNamesAreThisRepositorysOwnTests
{
    /// <summary>The ruling, exactly.</summary>
    [Fact]
    public void TheTwoThatCollideAreTruncatedRatherThanInitialised()
    {
        Assert.Equal("Point Def.", ShortNames.Of("Point Defence"));
        Assert.Equal("Power Dist.", ShortNames.Of("Power Distributor"));
    }

    /// <summary>
    /// And no other pair may quietly do what those two did. Over every module the specification
    /// table knows, two different modules never come out with the same short name.
    /// <para>
    /// This is the test that makes the table safe to add to: the next entry either keeps every
    /// name distinct or this fails naming both halves of the clash.
    /// </para>
    /// </summary>
    [Fact]
    public void NoTwoModulesShareAShortName()
    {
        var taken = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in EliteSpecifications.Modules
                     .Where(module => !module.IsBulkhead)
                     .Select(module => module.Name)
                     .Distinct(StringComparer.Ordinal))
        {
            var brief = ShortNames.Of(name);

            Assert.False(
                taken.TryGetValue(brief, out var already) && !string.Equals(already, name, StringComparison.Ordinal),
                $"'{name}' and '{already}' both shorten to '{brief}'.");

            taken[brief] = name;
        }
    }

    /// <summary>
    /// The two the request names, and the two families where a pattern says it rather than a row
    /// per member.
    /// </summary>
    [Theory]
    [InlineData("Hull Reinforcement Package", "HRP")]
    [InlineData("Shield Booster", "SB")]
    [InlineData("6D Hull Reinforcement Package", "6D HRP")]
    [InlineData("Collector Limpet Controller", "Collector Limpet")]
    [InlineData("Economy Class Passenger Cabin", "Economy Cabin")]
    [InlineData("Guardian Hybrid Power Distributor", "Guardian Hybrid Power Dist.")]
    [InlineData("Prismatic Shield Generator", "Prismatic Shield Gen.")]
    [InlineData("Frame Shift Drive Interdictor", "FSD Interdictor")]
    public void TheTableSaysWhatItSays(string name, string expected) =>
        Assert.Equal(expected, ShortNames.Of(name));

    /// <summary>
    /// <b>Most modules are not in the table and that is the design.</b> A name already short
    /// enough for a column keeps every word: an initialism for one of these is a puzzle where a
    /// name used to be.
    /// </summary>
    [Theory]
    [InlineData("3E Pulse Laser, gimballed")]
    [InlineData("8E Cargo Rack")]
    [InlineData("5D Life Support")]
    public void AShortEnoughNameIsLeftAlone(string name) =>
        Assert.Equal(name, ShortNames.Of(name));

    /// <summary>
    /// <b>The blueprint usually repeats the module.</b> Struck off the end, it reads "Heavy Duty"
    /// — shorter, and comparable straight down the column, which is worth more than the width.
    /// </summary>
    [Theory]
    [InlineData("Heavy Duty Hull Reinforcement", "Hull Reinforcement Package", "Heavy Duty")]
    [InlineData("Blast Resistant Hull Reinforcement", "Hull Reinforcement Package", "Blast Resistant")]
    [InlineData("Shielded FSD", "Frame Shift Drive", "Shielded")]
    public void TheModuleComesOffTheEndOfItsBlueprint(string blueprint, string module, string expected) =>
        Assert.Equal(expected, ShortNames.Bare(blueprint, module));

    /// <summary>
    /// <b>Only off the end.</b> <i>Increased FSD Range</i> is about the range and the drive is not
    /// the last thing it says, so every word stays — dropping one would leave a blueprint name
    /// that means something else.
    /// </summary>
    [Theory]
    [InlineData("Increased FSD Range", "Frame Shift Drive")]
    [InlineData("Weapon Focused", "Power Distributor")]
    [InlineData("Reinforced Shields", "Shield Generator")]
    public void ABlueprintThatDoesNotEndWithItsModuleKeepsEveryWord(string blueprint, string module) =>
        Assert.Equal(blueprint, ShortNames.Bare(blueprint, module));

    /// <summary>
    /// And a blueprint that is <em>only</em> the module's name keeps it: an empty cell would read
    /// as no roll at all, which is the opposite of what it says.
    /// </summary>
    [Fact]
    public void ABlueprintThatIsNothingButTheModuleKeepsIt() =>
        Assert.Equal("Hull Reinforcement", ShortNames.Bare("Hull Reinforcement", "Hull Reinforcement Package"));

    /// <summary>A slot with nothing in it has no module to strike off anything.</summary>
    [Fact]
    public void WithNoModuleTheBlueprintIsUntouched() =>
        Assert.Equal("Heavy Duty Hull Reinforcement", ShortNames.Bare("Heavy Duty Hull Reinforcement", null));
}

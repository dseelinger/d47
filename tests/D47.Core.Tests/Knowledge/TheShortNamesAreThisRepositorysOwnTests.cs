using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>d47's own short names for modules.</summary>
public class TheShortNamesAreThisRepositorysOwnTests
{
    /// <summary>The ruling, exactly.</summary>
    [Fact]
    public void TheTwoThatCollideAreTruncatedRatherThanInitialised()
    {
        Assert.Equal("Point Def.", ShortNames.Of("Point Defence"));
        Assert.Equal("Power Dist.", ShortNames.Of("Power Distributor"));
    }

    /// <summary>And no other pair may silently do what those two did.</summary>
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
    /// The two the request names, and the two families where a pattern says it rather than a row per
    /// member.
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

    /// <summary>Most modules are not in the table and that is the design.</summary>
    [Theory]
    [InlineData("3E Pulse Laser, gimballed")]
    [InlineData("8E Cargo Rack")]
    [InlineData("5D Life Support")]
    public void AShortEnoughNameIsLeftAlone(string name) =>
        Assert.Equal(name, ShortNames.Of(name));

    /// <summary>The blueprint usually repeats the module.</summary>
    [Theory]
    [InlineData("Heavy Duty Hull Reinforcement", "Hull Reinforcement Package", "Heavy Duty")]
    [InlineData("Blast Resistant Hull Reinforcement", "Hull Reinforcement Package", "Blast Resistant")]
    [InlineData("Shielded FSD", "Frame Shift Drive", "Shielded")]
    public void TheModuleComesOffTheEndOfItsBlueprint(string blueprint, string module, string expected) =>
        Assert.Equal(expected, ShortNames.Bare(blueprint, module));

    /// <summary>Only off the end.</summary>
    [Theory]
    [InlineData("Increased FSD Range", "Frame Shift Drive")]
    [InlineData("Weapon Focused", "Power Distributor")]
    [InlineData("Reinforced Shields", "Shield Generator")]
    public void ABlueprintThatDoesNotEndWithItsModuleKeepsEveryWord(string blueprint, string module) =>
        Assert.Equal(blueprint, ShortNames.Bare(blueprint, module));

    /// <summary>
    /// And a blueprint that is only the module's name keeps it: an empty cell would read as no roll at
    /// all, which is the opposite of what it says.
    /// </summary>
    [Fact]
    public void ABlueprintThatIsNothingButTheModuleKeepsIt() =>
        Assert.Equal("Hull Reinforcement", ShortNames.Bare("Hull Reinforcement", "Hull Reinforcement Package"));

    /// <summary>A slot with nothing in it has no module to strike off anything.</summary>
    [Fact]
    public void WithNoModuleTheBlueprintIsUntouched() =>
        Assert.Equal("Heavy Duty Hull Reinforcement", ShortNames.Bare("Heavy Duty Hull Reinforcement", null));
}

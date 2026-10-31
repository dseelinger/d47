using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>A star's CLASS tile says what its letter, number and luminosity class mean; a body's Codex tile falls back to its family picture.</summary>
public sealed class AStarsClassIsSaidInD47sOwnWordsTests
{
    [Fact]
    public void AMainSequenceStarNamesItsColourAndEachPartOfItsClass()
    {
        var line = StarClassText.For("K7", "V")!;

        Assert.Equal("K7 V", line.Class);
        Assert.Equal("Orange main-sequence star", line.Name);
        Assert.Contains("K: orange", line.Meaning, StringComparison.Ordinal);
        Assert.Contains("7: from 0, hottest, to 9, coolest, within K.", line.Meaning, StringComparison.Ordinal);
        Assert.Contains("V: main sequence", line.Meaning, StringComparison.Ordinal);
    }

    [Fact]
    public void AClassWithNoColourKeepsItsOwnName()
    {
        Assert.Equal("Brown dwarf", StarClassText.For("T4", "V")!.Name);
        Assert.Equal("Neutron star", StarClassText.For("N", null)!.Name);
        Assert.Equal("N", StarClassText.For("N", null)!.Class);
    }

    [Fact]
    public void AStarWithNoClassHasNoLine() => Assert.Null(StarClassText.For(null, "V"));

    [Theory]
    [InlineData("Star", "K (Yellow-Orange) Star", "stars.jpg")]
    [InlineData("Planet", "Class I gas giant", "gas-giant-planets.jpg")]
    [InlineData("Planet", "Water giant", "gas-giant-planets.jpg")]
    [InlineData("Planet", "High metal content world", "terrestrial-planets.jpg")]
    [InlineData("Planet", null, "terrestrial-planets.jpg")]
    public void ABodyFallsBackToItsFamilyPicture(string type, string? subType, string file) =>
        Assert.Equal(file, CodexFamilies.FileOfBody(type, subType));

    [Fact]
    public void AGenusFallsBackToTheOrganicStructuresPicture() =>
        Assert.Equal("organic-structures.jpg", CodexFamilies.FileOfGenus);
}

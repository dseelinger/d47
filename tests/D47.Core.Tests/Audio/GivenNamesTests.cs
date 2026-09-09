using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary> Choosing a voice's sex from a sender's name. </summary>
public class GivenNamesTests
{
    [Theory]
    [InlineData("Marianne Hobbs")]
    [InlineData("Ilse Bruhn")]
    [InlineData("Jacqui Waugh")]
    [InlineData("Astrid")]
    [InlineData("'Bella' Ford")]
    public void AWomansNameReadsAsOne(string sender) => Assert.True(GivenNames.ReadsFemale(sender));

    [Theory]
    [InlineData("Mark Bennett")]
    [InlineData("John Tennyson")]
    [InlineData("Ray Watkinson")]
    [InlineData("Andrew")]
    public void AMansDoesNot(string sender) => Assert.False(GivenNames.ReadsFemale(sender));

    /// <summary>The family name is not evidence.</summary>
    [Fact]
    public void OnlyTheGivenNameIsRead()
    {
        Assert.False(GivenNames.ReadsFemale("Bennett Grace"));
        Assert.True(GivenNames.ReadsFemale("Grace Bennett"));
    }

    /// <summary>Nothing that is not a name at all.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Ray Gateway")]
    [InlineData("ShipName Police Federation")]
    [InlineData("Squidbrain")]
    public void SomethingThatIsNotAGivenNameIsNotAWomans(string? sender) =>
        Assert.False(GivenNames.ReadsFemale(sender));

    /// <summary>Ambiguous names are deliberately absent, and the default is what they fall to.</summary>
    [Theory]
    [InlineData("Sam Carter")]
    [InlineData("Alex Reyes")]
    [InlineData("Robin Hale")]
    public void AnAmbiguousNameTakesTheDefault(string sender) =>
        Assert.False(GivenNames.ReadsFemale(sender));

    /// <summary>Case is not a signal.</summary>
    [Fact]
    public void CaseDoesNotMatter() => Assert.True(GivenNames.ReadsFemale("ALICE Renard"));
}

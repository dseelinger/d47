using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// Choosing a voice's sex from a sender's name
/// (remediation.md, "Named NPCs should each use a different voice").
/// <para>
/// Elite records no sex for anybody — 914 journals, 696,000 events, 221 event kinds, and not one
/// carries a gender field. The names are all there is, so this is a list of the ones that read
/// as a woman's, and everything else takes a man's voice.
/// </para>
/// </summary>
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

    /// <summary>
    /// The family name is not evidence. Elite's senders are "given family", and a surname that
    /// happens to be somebody's given name elsewhere must not decide this.
    /// </summary>
    [Fact]
    public void OnlyTheGivenNameIsRead()
    {
        Assert.False(GivenNames.ReadsFemale("Bennett Grace"));
        Assert.True(GivenNames.ReadsFemale("Grace Bennett"));
    }

    /// <summary>
    /// Nothing that is not a name at all. Elite's NPC senders include ships, stations and
    /// whatever a Commander typed, and none of that should be read as anything.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Ray Gateway")]
    [InlineData("ShipName Police Federation")]
    [InlineData("Squidbrain")]
    public void SomethingThatIsNotAGivenNameIsNotAWomans(string? sender) =>
        Assert.False(GivenNames.ReadsFemale(sender));

    /// <summary>
    /// Ambiguous names are deliberately absent, and the default is what they fall to. Sam, Alex,
    /// Jo, Pip and Robin are men as often as women in this material.
    /// </summary>
    [Theory]
    [InlineData("Sam Carter")]
    [InlineData("Alex Reyes")]
    [InlineData("Robin Hale")]
    public void AnAmbiguousNameTakesTheDefault(string sender) =>
        Assert.False(GivenNames.ReadsFemale(sender));

    /// <summary>Case is not a signal. The generator writes "ALICE" as readily as "Alice".</summary>
    [Fact]
    public void CaseDoesNotMatter() => Assert.True(GivenNames.ReadsFemale("ALICE Renard"));
}

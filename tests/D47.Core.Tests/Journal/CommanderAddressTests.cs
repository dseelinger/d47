using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// Rank and surname: a crew addresses its owner, and rank plus full name is how a form letter
/// talks.
/// </summary>
public class CommanderAddressTests
{
    [Theory]
    [InlineData("John DeParagon", "Commander DeParagon")]
    [InlineData("JOHN DEPARAGON", "Commander DEPARAGON")]
    [InlineData("Fixture", "Commander Fixture")]
    [InlineData("  John DeParagon  ", "Commander DeParagon")]
    [InlineData("", "Commander")]
    [InlineData("   ", "Commander")]
    [InlineData(null, "Commander")]
    public void RankAndSurnameAlone(string? name, string said) =>
        Assert.Equal(said, CommanderAddress.Said(name));
}

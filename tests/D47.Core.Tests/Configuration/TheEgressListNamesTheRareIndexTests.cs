using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class TheEgressListNamesTheRareIndexTests
{
    [Fact]
    public void ItIsActiveOnDefaultSettingsAndNamesGitHubAndTheRelease()
    {
        var entry = Assert.Single(
            EgressDisclosure.For(new D47Settings(), llmKeyPresent: false),
            entry => entry.Id == EgressDisclosure.RareIndex);

        Assert.True(entry.Active);
        Assert.Equal("Rare index", entry.Name);
        Assert.Equal("github.com", entry.Destination);
        Assert.Contains("rares-1", entry.What, StringComparison.Ordinal);
        Assert.Contains("at most once in 3 hours", entry.What, StringComparison.Ordinal);
        Assert.Contains("no Commander name", entry.What, StringComparison.Ordinal);
    }
}

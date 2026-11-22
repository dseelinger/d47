using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class TheEgressListNamesTheGutenbergListTests
{
    [Fact]
    public void ItIsActiveOnDefaultSettingsAndNamesGitHubAndTheRelease()
    {
        var entry = Assert.Single(
            EgressDisclosure.For(new D47Settings(), llmKeyPresent: false),
            entry => entry.Id == EgressDisclosure.Books);

        Assert.True(entry.Active);
        Assert.Equal("github.com", entry.Destination);
        Assert.Contains("books-1", entry.What, StringComparison.Ordinal);
        Assert.Contains("more than 7 days old", entry.What, StringComparison.Ordinal);
        Assert.Contains("Nothing about you goes with it", entry.What, StringComparison.Ordinal);
    }
}

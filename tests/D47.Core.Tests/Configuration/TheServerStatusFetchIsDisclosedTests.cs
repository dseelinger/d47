using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class TheServerStatusFetchIsDisclosedTests
{
    [Fact]
    public void OffByDefaultItSaysNothingIsRequested()
    {
        var entry = Assert.Single(
            EgressDisclosure.For(new D47Settings(), llmKeyPresent: false),
            entry => entry.Id == EgressDisclosure.ServerStatus);

        Assert.False(entry.Active);
        Assert.Contains("nothing is requested", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void OnItNamesTheHostAndSaysItAsksWithEliteClosed()
    {
        var settings = new D47Settings { Knowledge = new KnowledgeSettings { ServerStatus = true } };

        var entry = Assert.Single(
            EgressDisclosure.For(settings, llmKeyPresent: false),
            entry => entry.Id == EgressDisclosure.ServerStatus);

        Assert.True(entry.Active);
        Assert.Equal("ed-server-status.orerve.net", entry.Destination);
        Assert.Contains("every ten minutes", entry.What, StringComparison.Ordinal);
        Assert.Contains("every minute", entry.What, StringComparison.Ordinal);
        Assert.Contains("whether or not Elite is running", entry.What, StringComparison.Ordinal);
        Assert.Contains("Nothing about you goes with it", entry.What, StringComparison.Ordinal);
    }
}

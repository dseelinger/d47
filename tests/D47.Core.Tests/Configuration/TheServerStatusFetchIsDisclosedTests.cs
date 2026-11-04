using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class TheServerStatusFetchIsDisclosedTests
{

    [Fact]
    public void OnADefaultInstallItNamesTheHostAndSaysItAsksWithEliteClosed()
    {
        var settings = new D47Settings();

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

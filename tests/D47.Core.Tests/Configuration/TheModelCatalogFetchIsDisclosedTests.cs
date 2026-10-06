using D47.Core.Configuration;

using Xunit;

namespace D47.Core.Tests.Configuration;

public class TheModelCatalogFetchIsDisclosedTests
{
    [Fact]
    public void OnItNamesTheHostAndSaysNothingAboutYouGoes()
    {
        var settings = new D47Settings();

        var entry = Assert.Single(
            EgressDisclosure.For(settings, llmKeyPresent: false),
            entry => entry.Id == EgressDisclosure.ModelUpdates);

        Assert.True(entry.Active);
        Assert.Equal("raw.githubusercontent.com", entry.Destination);
        Assert.Contains("every 24 hours", entry.What, StringComparison.Ordinal);
        Assert.Contains("no key, no journal content and no identifier", entry.What, StringComparison.Ordinal);
        Assert.Contains(
            "Model updates → raw.githubusercontent.com",
            EgressDisclosure.Describe(settings, false),
            StringComparison.Ordinal);
    }

    [Fact]
    public void OffItSaysNothingIsRequested()
    {
        var settings = new D47Settings { Models = new ModelSettings { RefreshCatalog = false } };

        var entry = Assert.Single(
            EgressDisclosure.For(settings, llmKeyPresent: false),
            entry => entry.Id == EgressDisclosure.ModelUpdates);

        Assert.False(entry.Active);
        Assert.Contains("nothing is requested", entry.What, StringComparison.Ordinal);
        Assert.Contains("Model updates → nothing sent", EgressDisclosure.Describe(settings, false), StringComparison.Ordinal);
    }
}

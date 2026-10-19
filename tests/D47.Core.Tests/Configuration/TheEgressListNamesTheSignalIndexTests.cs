using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class TheEgressListNamesTheSignalIndexTests
{
    private static EgressEntry Entry(bool on)
    {
        var settings = new D47Settings();

        return EgressDisclosure.Entry(
            EgressDisclosure.SignalIndex,
            settings with { Knowledge = settings.Knowledge with { SignalIndex = on } },
            llmKeyPresent: true);
    }

    [Fact]
    public void OffByDefaultItSaysNoIndexIsFetched()
    {
        var entry = Assert.Single(
            EgressDisclosure.For(new D47Settings(), llmKeyPresent: false),
            entry => entry.Id == EgressDisclosure.SignalIndex);

        Assert.False(entry.Active);
        Assert.Equal("Signal index", entry.Name);
        Assert.Contains("no index is fetched", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void OnItNamesGitHubAndSaysNothingAboutTheCommanderGoes()
    {
        var entry = Entry(on: true);

        Assert.True(entry.Active);
        Assert.Equal("github.com", entry.Destination);
        Assert.Contains("at most once in 20 hours", entry.What, StringComparison.Ordinal);
        Assert.Contains("no position, no key, no Commander name, no identifier", entry.What, StringComparison.Ordinal);
    }
}

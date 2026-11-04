using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class TheEgressListNamesTheSignalIndexTests
{
    private static EgressEntry Entry() =>
        EgressDisclosure.Entry(EgressDisclosure.SignalIndex, new D47Settings(), llmKeyPresent: true);

    [Fact]
    public void OnADefaultInstallItNamesGitHubAndSaysNothingAboutTheCommanderGoes()
    {
        var entry = Entry();

        Assert.True(entry.Active);
        Assert.Equal("github.com", entry.Destination);
        Assert.Contains("at most once in 20 hours", entry.What, StringComparison.Ordinal);
        Assert.Contains("no position, no key, no Commander name, no identifier", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void OnItSaysTheIndexIsAlsoFetchedWhenAStoryChapterIsWritten()
    {
        var entry = Entry();

        Assert.Contains("a story chapter is written", entry.What, StringComparison.Ordinal);
        Assert.Contains("a story chapter is written", entry.Summary, StringComparison.Ordinal);
    }
}

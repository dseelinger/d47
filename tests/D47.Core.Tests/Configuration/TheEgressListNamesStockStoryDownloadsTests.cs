using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class TheEgressListNamesStockStoryDownloadsTests
{
    private static EgressEntry Entry(bool on)
    {
        var settings = new D47Settings();

        return EgressDisclosure.Entry(
            EgressDisclosure.StockStories,
            settings with { Ui = settings.Ui with { StoryDownloads = on } },
            llmKeyPresent: true);
    }

    [Fact]
    public void OnItNamesTheListTheHiddenLayerAndThePicturesAndSendsNothingElse()
    {
        var entry = Entry(on: true);

        Assert.True(entry.Active);
        Assert.Equal("Stock stories", entry.Name);
        Assert.Equal(EgressDisclosure.GitHubReleasesEndpoint, entry.Destination);
        Assert.Contains("the list of stock stories when the Stories page first opens in a session", entry.What, StringComparison.Ordinal);
        Assert.Contains("the cast pictures shown on the list, kept on disk", entry.What, StringComparison.Ordinal);
        Assert.Contains("A story's own files, its hidden layer and every cast picture, are requested when you pick it", entry.What, StringComparison.Ordinal);
        Assert.Contains("no key, no Commander name, no position and nothing from your journal", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void OffItSaysNothingIsFetchedAndTheListHoldsOnlyStoriesOnDisk()
    {
        var entry = Entry(on: false);

        Assert.False(entry.Active);
        Assert.Equal(
            "Stock story downloads are off, so nothing is fetched and the Stories page lists only stories already on disk.",
            entry.What);
    }

    [Fact]
    public void TheEntryIsInTheFixedList() => Assert.Contains(EgressDisclosure.StockStories, EgressDisclosure.Ids);
}

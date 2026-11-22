using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class TheEgressListNamesPodcastsTests
{
    private static D47Settings With(params string[] feeds) =>
        new() { Podcasts = new PodcastSettings { Feeds = feeds } };

    [Fact]
    public void WithNoFeedsTheEntryIsSilent()
    {
        var entry = EgressDisclosure.Entry(EgressDisclosure.Podcasts, new D47Settings(), llmKeyPresent: false, podcastEpisodeHosts: ["cdn.example"]);

        Assert.False(entry.Active);
        Assert.Equal("Podcasts", entry.Name);
        Assert.Equal("No podcast feeds are added, so nothing is fetched.", entry.What);
    }

    [Fact]
    public void FeedsThatAreNotHttpOrHttpsLeaveTheEntrySilent()
    {
        var entry = EgressDisclosure.Entry(EgressDisclosure.Podcasts, With("ftp://feeds.example/rss", "not a url"), llmKeyPresent: false);

        Assert.False(entry.Active);
    }

    [Fact]
    public void WithFeedsItNamesTheFeedHostsAndTheEpisodeHosts()
    {
        var entry = EgressDisclosure.Entry(
            EgressDisclosure.Podcasts,
            With("https://feeds.example/show.xml", "http://other.example/rss"),
            llmKeyPresent: false,
            podcastEpisodeHosts: ["cdn.example", "tracking.example"]);

        Assert.True(entry.Active);
        Assert.Equal("feeds.example, other.example, cdn.example, tracking.example", entry.Destination);
        Assert.Contains("Each feed is fetched when you open the podcast page or ask for a refresh.", entry.What, StringComparison.Ordinal);
        Assert.Contains("which see your IP address and can redirect to other hosts", entry.What, StringComparison.Ordinal);
        Assert.Contains("no key, no Commander name, nothing from your journal", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void AHostSharedByAFeedAndItsEpisodesIsNamedOnce()
    {
        var entry = EgressDisclosure.Entry(
            EgressDisclosure.Podcasts,
            With("https://feeds.example/show.xml"),
            llmKeyPresent: false,
            podcastEpisodeHosts: ["FEEDS.example", "cdn.example"]);

        Assert.Equal("feeds.example, cdn.example", entry.Destination);
    }

    [Fact]
    public async Task TheEntryIsInTheFixedListAndTheToolAndRowReportIt()
    {
        var install = new MemoryInstall();
        var store = new SettingsStore(install.Paths, install.Files, NullLogger<SettingsStore>.Instance);
        var secrets = new SecretStore(install.Paths, new ReversibleProtector(), install.Files, NullLogger<SecretStore>.Instance);
        var settings = new SettingsService(store, secrets, With("https://feeds.example/show.xml"), NullLogger<SettingsService>.Instance);
        var privacy = PrivacyCapability.Create(settings, podcastEpisodeHosts: () => ["cdn.example"]);

        Assert.Contains(EgressDisclosure.Podcasts, EgressDisclosure.Ids);

        var report = await privacy.Tools.Single().Handler(
            D47.Core.Capabilities.ToolArguments.Empty, TestContext.Current.CancellationToken);
        Assert.Contains("Podcasts → feeds.example, cdn.example", report.Content, StringComparison.Ordinal);

        var row = privacy.Settings.Single(setting => setting.Key == $"egress.{EgressDisclosure.Podcasts}");
        Assert.StartsWith("Podcasts → feeds.example, cdn.example", row.Binding!.Read(settings.Current), StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyHttpAndHttpsFeedsAreAcceptedOnceEach()
    {
        var feeds = With("https://feeds.example/show.xml", " https://feeds.example/show.xml ", "file:///c:/feed.xml", "http://other.example/rss").Podcasts.FeedUris();

        Assert.Equal(["https://feeds.example/show.xml", "http://other.example/rss"], feeds.Select(feed => feed.AbsoluteUri));
    }
}

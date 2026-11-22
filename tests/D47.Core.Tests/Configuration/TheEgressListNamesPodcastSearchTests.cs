using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class TheEgressListNamesPodcastSearchTests
{
    [Fact]
    public void ItIsActiveOnDefaultSettingsAndNamesApplesDirectory()
    {
        var entry = Assert.Single(
            EgressDisclosure.For(new D47Settings(), llmKeyPresent: false),
            entry => entry.Id == EgressDisclosure.PodcastSearch);

        Assert.True(entry.Active);
        Assert.Equal("Podcast search", entry.Name);
        Assert.Equal("itunes.apple.com", entry.Destination);
        Assert.Contains("The words you search for go to Apple's podcast directory each time you search for a show", entry.What, StringComparison.Ordinal);
        Assert.Contains("whoever typed or said them", entry.What, StringComparison.Ordinal);
        Assert.Contains("no key, no Commander name, nothing from your journal", entry.What, StringComparison.Ordinal);
        Assert.Contains("Apple sees your IP address", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheToolAndTheRowReportIt()
    {
        var install = new MemoryInstall();
        var store = new SettingsStore(install.Paths, install.Files, NullLogger<SettingsStore>.Instance);
        var secrets = new SecretStore(install.Paths, new ReversibleProtector(), install.Files, NullLogger<SecretStore>.Instance);
        var settings = new SettingsService(store, secrets, new D47Settings(), NullLogger<SettingsService>.Instance);
        var privacy = PrivacyCapability.Create(settings);

        Assert.Contains(EgressDisclosure.PodcastSearch, EgressDisclosure.Ids);

        var report = await privacy.Tools.Single().Handler(
            D47.Core.Capabilities.ToolArguments.Empty, TestContext.Current.CancellationToken);
        Assert.Contains("Podcast search → itunes.apple.com", report.Content, StringComparison.Ordinal);

        var row = privacy.Settings.Single(setting => setting.Key == $"egress.{EgressDisclosure.PodcastSearch}");
        Assert.StartsWith("Podcast search → itunes.apple.com", row.Binding!.Read(settings.Current), StringComparison.Ordinal);
    }
}

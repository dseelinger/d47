using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class ASilentEgressRowShowsOnlyItsSummaryTests
{
    private static string ReadRow(string id, D47Settings settings)
    {
        var install = new MemoryInstall();
        var store = new SettingsStore(install.Paths, install.Files, NullLogger<SettingsStore>.Instance);
        var secrets = new SecretStore(install.Paths, new ReversibleProtector(), install.Files, NullLogger<SecretStore>.Instance);
        var service = new SettingsService(store, secrets, new D47Settings(), NullLogger<SettingsService>.Instance);
        var row = PrivacyCapability.Create(service).Settings.Single(setting => setting.Key == $"egress.{id}");

        return row.Binding!.Read(settings)!;
    }

    [Fact]
    public void AnInactiveRowIsItsSummaryAlone()
    {
        var settings = new D47Settings();
        var entry = EgressDisclosure.Entry(EgressDisclosure.OwnVoice, settings, llmKeyPresent: true);

        Assert.False(entry.Active);
        Assert.Equal(entry.Summary, ReadRow(EgressDisclosure.OwnVoice, settings));
    }

    [Fact]
    public void AnActiveRowKeepsItsLineAboveTheSummary()
    {
        var settings = new D47Settings();
        settings = settings with { Usage = settings.Usage with { Counts = true } };
        var entry = EgressDisclosure.Entry(EgressDisclosure.UsageCounts, settings, llmKeyPresent: true);

        Assert.True(entry.Active);
        Assert.Equal(entry.Line + "\n" + entry.Summary, ReadRow(EgressDisclosure.UsageCounts, settings));
    }
}

using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class TheEgressListNamesUsageCountsTests
{
    private static EgressEntry Entry(bool on)
    {
        var settings = new D47Settings();

        return EgressDisclosure.Entry(
            EgressDisclosure.UsageCounts,
            settings with { Usage = settings.Usage with { Counts = on } },
            llmKeyPresent: true);
    }

    [Fact]
    public void OnItNamesTheWorkerWhatIsSentAndWhatIsNot()
    {
        var entry = Entry(on: true);

        Assert.True(entry.Active);
        Assert.Equal("Usage counts", entry.Name);
        Assert.Equal(UsageCountSettings.Address, entry.Destination);
        Assert.Contains("no identifier of any kind", entry.What, StringComparison.Ordinal);
        Assert.Contains("nothing from your journal", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void OffItSaysNothingIsCountedOrSent()
    {
        var entry = Entry(on: false);

        Assert.False(entry.Active);
        Assert.Equal("Usage counts are off, so nothing is counted or sent.", entry.What);
    }

    [Fact]
    public void TheEntryIsInTheFixedListAndTheDefaultIsOn()
    {
        Assert.Contains(EgressDisclosure.UsageCounts, EgressDisclosure.Ids);
        Assert.True(new D47Settings().Usage.Counts);
        Assert.Equal("https://d47-usage.dseelinger.workers.dev", UsageCountSettings.Address);
    }

    [Fact]
    public void TheSettingRowIsLabelledAndTiedToTheEntry()
    {
        var install = new MemoryInstall();
        var store = new SettingsStore(install.Paths, install.Files, NullLogger<SettingsStore>.Instance);
        var secrets = new SecretStore(install.Paths, new ReversibleProtector(), install.Files, NullLogger<SecretStore>.Instance);
        var settings = new SettingsService(store, secrets, new D47Settings(), NullLogger<SettingsService>.Instance);
        var row = PrivacyCapability.Create(settings).Settings.Single(setting => setting.Key == PrivacyCapability.UsageCountsKey);
        var on = new D47Settings();

        Assert.Equal("Usage counts", row.Label);
        Assert.Equal(EgressDisclosure.UsageCounts, row.EgressId);
        var binding = row.Binding!;

        Assert.Equal("true", binding.Read(on));
        Assert.Equal("false", binding.Read(binding.Write!(on, "false")));
    }
}

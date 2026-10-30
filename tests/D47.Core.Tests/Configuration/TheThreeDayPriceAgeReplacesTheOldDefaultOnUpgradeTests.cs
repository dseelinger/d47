using D47.Core.Configuration;
using D47.Core.Storage;
using D47.Core.Updates;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Configuration;

[Trait("Category", "Integration")]
public class TheThreeDayPriceAgeReplacesTheOldDefaultOnUpgradeTests
{
    private static readonly ReleaseVersion Running = new(1, 60, 0);

    private static string File(string lastVersion, int hours) => $$"""
        {
          "schemaVersion": 1,
          "lastVersion": "{{lastVersion}}",
          "trade": { "maxPriceAgeHours": {{hours}} }
        }
        """;

    private static (SettingsStore Store, MemoryInstall Install) Stored(string lastVersion, int hours)
    {
        var install = new MemoryInstall();
        install.Files.WriteText(install.Paths.SettingsFile, File(lastVersion, hours));

        return (new SettingsStore(install.Paths, install.Files, NullLogger<SettingsStore>.Instance), install);
    }

    [Fact]
    public void AFreshSettingsFileHoldsThreeDays()
    {
        Assert.Equal(72, new D47Settings().Trade.MaxPriceAgeHours);
    }

    [Fact]
    public void AnOldDefaultOfThirtyDaysBecomesThreeDaysAndIsWrittenBack()
    {
        var (store, install) = Stored("1.59.0", 720);

        Assert.Equal(72, store.Load(Running).Trade.MaxPriceAgeHours);
        Assert.Contains("\"maxPriceAgeHours\": 72", install.Files.ReadText(install.Paths.SettingsFile), StringComparison.Ordinal);
    }

    [Fact]
    public void AnotherSavedAgeIsTheCommandersAndIsKept()
    {
        var (store, _) = Stored("1.59.0", 168);

        Assert.Equal(168, store.Load(Running).Trade.MaxPriceAgeHours);
    }

    [Fact]
    public void ThirtyDaysPickedAfterTheUpgradeIsKeptAtTheNextUpgrade()
    {
        var (store, _) = Stored("1.59.1", 720);

        Assert.Equal(720, store.Load(Running).Trade.MaxPriceAgeHours);
    }

    [Fact]
    public void LoadingTheUpgradedFileAgainChangesNothing()
    {
        var (store, install) = Stored("1.59.0", 720);
        store.Load(Running);
        var written = install.Files.ReadText(install.Paths.SettingsFile);

        Assert.Equal(72, store.Load(Running).Trade.MaxPriceAgeHours);
        Assert.Equal(written, install.Files.ReadText(install.Paths.SettingsFile));
    }
}

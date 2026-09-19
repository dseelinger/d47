using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Updates;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class AnUpgradeDeletesWhatItDoesNotKnowTests
{
    private static readonly ReleaseVersion Running = new(0, 170, 0);

    private static string File(string lastVersion) => $$"""
        {
          "schemaVersion": 1,
          "lastVersion": {{lastVersion}},
          "ui": { "theme": "dark", "mode": "window" },
          "callouts": { "habits": true }
        }
        """;

    private static SettingsStore StoreFor(TempInstall install) =>
        new(install.Paths, NullLogger<SettingsStore>.Instance);

    [Fact]
    public void AnUpgradeBacksTheFileUpAndDeletesTheUnknownKeys()
    {
        using var install = new TempInstall();
        var original = File("\"0.169.0\"");
        System.IO.File.WriteAllText(install.Paths.SettingsFile, original);

        var store = StoreFor(install);
        var settings = store.Load(Running);

        Assert.Empty(store.UnknownKeys);
        Assert.Equal(ThemeCatalog.Dark, settings.Ui.Theme);
        Assert.Equal("0.170.0", settings.LastVersion);

        var written = System.IO.File.ReadAllText(install.Paths.SettingsFile);
        Assert.DoesNotContain("habits", written, StringComparison.Ordinal);
        Assert.Contains("0.170.0", written, StringComparison.Ordinal);

        var restarted = StoreFor(install);
        restarted.Load();
        Assert.Empty(restarted.UnknownKeys);

        Assert.Equal(original, System.IO.File.ReadAllText(install.Paths.SettingsFile + ".0.169.0.bak"));
    }

    [Fact]
    public void AFileNoPublishedBuildHasStampedCountsAsAnUpgrade()
    {
        using var install = new TempInstall();
        System.IO.File.WriteAllText(install.Paths.SettingsFile, File("null"));

        var store = StoreFor(install);
        store.Load(Running);

        Assert.Empty(store.UnknownKeys);
        Assert.True(System.IO.File.Exists(install.Paths.SettingsFile + ".unversioned.bak"));
    }

    [Fact]
    public void ATestDriveKeepsAndNamesThem()
    {
        using var install = new TempInstall();
        System.IO.File.WriteAllText(install.Paths.SettingsFile, File("\"0.169.0\""));

        var store = StoreFor(install);
        store.Load();

        Assert.Equal(["ui.mode", "callouts.habits"], store.UnknownKeys);
        Assert.Contains("habits", System.IO.File.ReadAllText(install.Paths.SettingsFile), StringComparison.Ordinal);
    }

    [Fact]
    public void TheSameVersionKeepsAndNamesAKeyTypedByHand()
    {
        using var install = new TempInstall();
        System.IO.File.WriteAllText(install.Paths.SettingsFile, File("\"0.170.0\""));

        var store = StoreFor(install);
        store.Load(Running);

        Assert.Equal(["ui.mode", "callouts.habits"], store.UnknownKeys);
        Assert.Empty(Directory.GetFiles(install.Paths.Data, "*.bak"));
    }

    [Fact]
    public void ADowngradeKeepsWhatTheNewerBuildWrote()
    {
        using var install = new TempInstall();
        System.IO.File.WriteAllText(install.Paths.SettingsFile, File("\"0.171.0\""));

        var store = StoreFor(install);
        var settings = store.Load(Running);

        Assert.Equal(["ui.mode", "callouts.habits"], store.UnknownKeys);
        Assert.Contains("habits", System.IO.File.ReadAllText(install.Paths.SettingsFile), StringComparison.Ordinal);
        Assert.Equal("0.170.0", settings.LastVersion);
    }
}

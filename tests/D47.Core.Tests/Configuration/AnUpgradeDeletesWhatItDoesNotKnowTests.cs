using D47.Core.Configuration;
using D47.Core.Storage;
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

    private static SettingsStore StoreFor(MemoryInstall install) =>
        new(install.Paths, install.Files, NullLogger<SettingsStore>.Instance);

    [Fact]
    public void AnUpgradeBacksTheFileUpAndDeletesTheUnknownKeys()
    {
        var install = new MemoryInstall();
        var original = File("\"0.169.0\"");
        install.Files.WriteText(install.Paths.SettingsFile, original);

        var store = StoreFor(install);
        var settings = store.Load(Running);

        Assert.Empty(store.UnknownKeys);
        Assert.Equal(ThemeCatalog.Dark, settings.Ui.Theme);
        Assert.Equal("0.170.0", settings.LastVersion);

        var written = install.Files.ReadText(install.Paths.SettingsFile);
        Assert.DoesNotContain("habits", written, StringComparison.Ordinal);
        Assert.Contains("0.170.0", written, StringComparison.Ordinal);

        var restarted = StoreFor(install);
        restarted.Load();
        Assert.Empty(restarted.UnknownKeys);

        Assert.Equal(original, install.Files.ReadText(install.Paths.SettingsFile + ".0.169.0.bak"));
    }

    [Fact]
    public void AFileNoPublishedBuildHasStampedCountsAsAnUpgrade()
    {
        var install = new MemoryInstall();
        install.Files.WriteText(install.Paths.SettingsFile, File("null"));

        var store = StoreFor(install);
        store.Load(Running);

        Assert.Empty(store.UnknownKeys);
        Assert.True(install.Files.Stat(install.Paths.SettingsFile + ".unversioned.bak") is not null);
    }

    [Fact]
    public void ATestDriveKeepsAndNamesThem()
    {
        var install = new MemoryInstall();
        install.Files.WriteText(install.Paths.SettingsFile, File("\"0.169.0\""));

        var store = StoreFor(install);
        store.Load();

        Assert.Equal(["ui.mode", "callouts.habits"], store.UnknownKeys);
        Assert.Contains("habits", install.Files.ReadText(install.Paths.SettingsFile), StringComparison.Ordinal);
    }

    [Fact]
    public void TheSameVersionKeepsAndNamesAKeyTypedByHand()
    {
        var install = new MemoryInstall();
        install.Files.WriteText(install.Paths.SettingsFile, File("\"0.170.0\""));

        var store = StoreFor(install);
        store.Load(Running);

        Assert.Equal(["ui.mode", "callouts.habits"], store.UnknownKeys);
        Assert.Empty(install.Files.Enumerate(install.Paths.Data, "*.bak"));
    }

    [Fact]
    public void ADowngradeKeepsWhatTheNewerBuildWrote()
    {
        var install = new MemoryInstall();
        install.Files.WriteText(install.Paths.SettingsFile, File("\"0.171.0\""));

        var store = StoreFor(install);
        var settings = store.Load(Running);

        Assert.Equal(["ui.mode", "callouts.habits"], store.UnknownKeys);
        Assert.Contains("habits", install.Files.ReadText(install.Paths.SettingsFile), StringComparison.Ordinal);
        Assert.Equal("0.170.0", settings.LastVersion);
    }

    /// <summary>A theme the catalogue dropped (#389) opens as Elite rather than failing to load.</summary>
    [Fact]
    public void ARetiredThemeOpensAsElite()
    {
        var install = new MemoryInstall();
        install.Files.WriteText(
            install.Paths.SettingsFile,
            """{ "schemaVersion": 1, "ui": { "theme": "guardian" } }""");

        var store = StoreFor(install);
        var settings = store.Load(Running);

        Assert.Equal(ThemeCatalog.Elite, ThemeCatalog.Selected(settings.Ui.Theme).Id);
    }
}

using D47.Core.Configuration;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests;

public class AtomicFileTests
{
    [Fact]
    public void WriteLeavesNoPendingSibling()
    {
        using var install = new TempInstall();
        var target = Path.Combine(install.Root, "thing.json");

        AtomicFile.WriteAllText(target, "{}");

        Assert.True(File.Exists(target));
        Assert.False(File.Exists(target + AtomicFile.PendingSuffix));
    }

    [Fact]
    public void WriteOverExistingFileReplacesIt()
    {
        using var install = new TempInstall();
        var target = Path.Combine(install.Root, "thing.json");

        AtomicFile.WriteAllText(target, "first");
        AtomicFile.WriteAllText(target, "second");

        Assert.Equal("second", File.ReadAllText(target));
    }

    [Fact]
    public void AbandonedPendingFileDoesNotBlockTheNextWrite()
    {
        using var install = new TempInstall();
        var target = Path.Combine(install.Root, "thing.json");

        // What a crash mid-write leaves behind.
        File.WriteAllText(target + AtomicFile.PendingSuffix, "half-written");
        File.WriteAllText(target, "intact");

        AtomicFile.WriteAllText(target, "recovered");

        Assert.Equal("recovered", File.ReadAllText(target));
        Assert.False(File.Exists(target + AtomicFile.PendingSuffix));
    }
}

public class SettingsStoreTests
{
    private static SettingsStore StoreFor(TempInstall install) =>
        new(install.Paths, NullLogger<SettingsStore>.Instance);

    [Fact]
    public void MissingFileYieldsDefaultsBecauseThatIsAFirstRun()
    {
        using var install = new TempInstall();

        var settings = StoreFor(install).Load();

        Assert.Equal(1, settings.SchemaVersion);
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Information, settings.Logging.Default);
    }

    [Fact]
    public void SettingsSurviveARoundTrip()
    {
        using var install = new TempInstall();
        var store = StoreFor(install);

        store.Save(new D47Settings
        {
            Logging = new LoggingSettings
            {
                Default = Microsoft.Extensions.Logging.LogLevel.Warning,
                Subsystems = new Dictionary<string, Microsoft.Extensions.Logging.LogLevel>
                {
                    ["Journal"] = Microsoft.Extensions.Logging.LogLevel.Trace,
                },
            },
        });

        var reloaded = store.Load();

        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Warning, reloaded.Logging.Default);
        // The store normalises subsystem keys back to canonical casing regardless of how the
        // file spelled them, so every consumer can use the Subsystems constants to look up.
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Trace, reloaded.Logging.Subsystems["Journal"]);
    }

    [Fact]
    public void UnknownKeyIsRejectedRatherThanIgnored()
    {
        using var install = new TempInstall();
        File.WriteAllText(install.Paths.SettingsFile, """{"schemaVersion":1,"logging":{"default":"Warning"},"speling":true}""");

        var ex = Assert.Throws<SettingsLoadException>(() => StoreFor(install).Load());

        Assert.Contains("speling", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnknownSubsystemIsRejected()
    {
        using var install = new TempInstall();
        File.WriteAllText(
            install.Paths.SettingsFile,
            """{"schemaVersion":1,"logging":{"default":"Information","subsystems":{"Telepathy":"Debug"}}}""");

        var ex = Assert.Throws<SettingsLoadException>(() => StoreFor(install).Load());

        Assert.Contains("Telepathy", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MalformedFileFailsLoudly()
    {
        using var install = new TempInstall();
        File.WriteAllText(install.Paths.SettingsFile, "{ this is not json");

        Assert.Throws<SettingsLoadException>(() => StoreFor(install).Load());
    }
}

public class SecretStoreTests
{
    [Fact]
    public void SecretsSurviveARoundTripThroughTheFile()
    {
        using var install = new TempInstall();

        new SecretStore(install.Paths, new ReversibleProtector(), NullLogger<SecretStore>.Instance)
            .Set("inara.apiKey", "swordfish");

        var reopened = new SecretStore(install.Paths, new ReversibleProtector(), NullLogger<SecretStore>.Instance);

        Assert.True(reopened.TryGet("inara.apiKey", out var value));
        Assert.Equal("swordfish", value);
    }

    [Fact]
    public void SecretsAreNotStoredInPlaintext()
    {
        using var install = new TempInstall();

        new SecretStore(install.Paths, new ReversibleProtector(), NullLogger<SecretStore>.Instance)
            .Set("inara.apiKey", "swordfish");

        Assert.DoesNotContain("swordfish", File.ReadAllText(install.Paths.SecretsFile), StringComparison.Ordinal);
    }

    [Fact]
    public void MissingSecretIsAbsenceNotFailure()
    {
        using var install = new TempInstall();
        var store = new SecretStore(install.Paths, new ReversibleProtector(), NullLogger<SecretStore>.Instance);

        Assert.False(store.TryGet("never.set", out _));
        Assert.False(store.Has("never.set"));
    }

    [Fact]
    public void SecretFromAnotherUserReadsAsAbsentRatherThanThrowing()
    {
        using var install = new TempInstall();
        new SecretStore(install.Paths, new ReversibleProtector(), NullLogger<SecretStore>.Instance)
            .Set("inara.apiKey", "swordfish");

        // Same file, but nothing can decrypt it now. This is the copied-machine case, and it
        // has to read as a capability being off rather than as a crash.
        var elsewhere = new SecretStore(install.Paths, new NeverUnprotects(), NullLogger<SecretStore>.Instance);

        Assert.False(elsewhere.TryGet("inara.apiKey", out _));
        Assert.Contains("inara.apiKey", elsewhere.Names);
    }

    [Fact]
    public void UnreadableStoreStartsEmptyInsteadOfThrowing()
    {
        using var install = new TempInstall();
        File.WriteAllText(install.Paths.SecretsFile, "not json at all");

        var store = new SecretStore(install.Paths, new ReversibleProtector(), NullLogger<SecretStore>.Instance);

        Assert.Empty(store.Names);
    }
}

/// <summary>
/// Where a build writes. <b>Beside the executable</b> is the shipped rule and the only thing a
/// published build can do — the Debug redirect that keeps dev state out of <c>bin\</c> rides an
/// assembly attribute <c>D47.App.csproj</c> writes for that configuration alone, and nothing
/// outside the build can ask for it.
/// </summary>
public class AppPathsTests
{
    [Fact]
    public void WithoutTheDebugAttributeTheDataFolderIsBesideTheExecutable()
    {
        // No entry assembly here carries DevDataRoot, which is equally the situation of every
        // published d47.exe. If this ever fails, a redirect has become reachable outside a Debug
        // build — and the Commander's secrets have moved somewhere they were never meant to be.
        Assert.Equal(
            Path.GetFullPath(AppContext.BaseDirectory),
            AppPaths.ForRunningBuild().InstallRoot);
    }
}

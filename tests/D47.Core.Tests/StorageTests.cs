using D47.Core.Configuration;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests;

public class SettingsStoreTests
{
    private static SettingsStore StoreFor(MemoryInstall install) =>
        new(install.Paths, install.Files, NullLogger<SettingsStore>.Instance);

    [Fact]
    public void MissingFileYieldsDefaultsBecauseThatIsAFirstRun()
    {
        var install = new MemoryInstall();

        var settings = StoreFor(install).Load();

        Assert.Equal(1, settings.SchemaVersion);
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Information, settings.Logging.Default);
    }

    [Fact]
    public void SettingsSurviveARoundTrip()
    {
        var install = new MemoryInstall();
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
        // The store normalises subsystem keys back to canonical casing regardless of how the file spelled
        // them, so every consumer can use the Subsystems constants to look up.
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Trace, reloaded.Logging.Subsystems["Journal"]);
    }

 /// <summary>A typo still surfaces, by name — as a warning rather than a refusal.</summary>
    [Fact]
    public void UnknownKeyIsNamedRatherThanRefusingTheFile()
    {
        var install = new MemoryInstall();
        install.Files.WriteText(install.Paths.SettingsFile, """{"schemaVersion":1,"logging":{"default":"Warning"},"speling":true}""");

        var store = StoreFor(install);
        var settings = store.Load();

        Assert.Equal(["speling"], store.UnknownKeys);

        // And the rest of the file is honoured rather than thrown away with it.
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Warning, settings.Logging.Default);
    }

    [Fact]
    public void UnknownSubsystemIsRejected()
    {
        var install = new MemoryInstall();
        install.Files.WriteText(
            install.Paths.SettingsFile,
            """{"schemaVersion":1,"logging":{"default":"Information","subsystems":{"Telepathy":"Debug"}}}""");

        var ex = Assert.Throws<SettingsLoadException>(() => StoreFor(install).Load());

        Assert.Contains("Telepathy", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MalformedFileFailsLoudly()
    {
        var install = new MemoryInstall();
        install.Files.WriteText(install.Paths.SettingsFile, "{ this is not json");

        Assert.Throws<SettingsLoadException>(() => StoreFor(install).Load());
    }
}

public class SecretStoreTests
{
    [Fact]
    public void SecretsSurviveARoundTripThroughTheFile()
    {
        var install = new MemoryInstall();

        new SecretStore(install.Paths, new ReversibleProtector(), install.Files, NullLogger<SecretStore>.Instance)
            .Set("inara.apiKey", "swordfish");

        var reopened = new SecretStore(install.Paths, new ReversibleProtector(), install.Files, NullLogger<SecretStore>.Instance);

        Assert.True(reopened.TryGet("inara.apiKey", out var value));
        Assert.Equal("swordfish", value);
    }

    [Fact]
    public void SecretsAreNotStoredInPlaintext()
    {
        var install = new MemoryInstall();

        new SecretStore(install.Paths, new ReversibleProtector(), install.Files, NullLogger<SecretStore>.Instance)
            .Set("inara.apiKey", "swordfish");

        Assert.DoesNotContain("swordfish", install.Files.ReadText(install.Paths.SecretsFile), StringComparison.Ordinal);
    }

    [Fact]
    public void MissingSecretIsAbsenceNotFailure()
    {
        var install = new MemoryInstall();
        var store = new SecretStore(install.Paths, new ReversibleProtector(), install.Files, NullLogger<SecretStore>.Instance);

        Assert.False(store.TryGet("never.set", out _));
        Assert.False(store.Has("never.set"));
    }

    [Fact]
    public void SecretFromAnotherUserReadsAsAbsentRatherThanThrowing()
    {
        var install = new MemoryInstall();
        new SecretStore(install.Paths, new ReversibleProtector(), install.Files, NullLogger<SecretStore>.Instance)
            .Set("inara.apiKey", "swordfish");

        // Same file, but nothing can decrypt it now.
        var elsewhere = new SecretStore(install.Paths, new NeverUnprotects(), install.Files, NullLogger<SecretStore>.Instance);

        Assert.False(elsewhere.TryGet("inara.apiKey", out _));
        Assert.Contains("inara.apiKey", elsewhere.Names);
    }

    [Fact]
    public void UnreadableStoreStartsEmptyInsteadOfThrowing()
    {
        var install = new MemoryInstall();
        install.Files.WriteText(install.Paths.SecretsFile, "not json at all");

        var store = new SecretStore(install.Paths, new ReversibleProtector(), install.Files, NullLogger<SecretStore>.Instance);

        Assert.Empty(store.Names);
    }
}

/// <summary>Where a build writes.</summary>
public class AppPathsTests
{
    [Fact]
    public void WithoutTheDebugAttributeTheDataFolderIsBesideTheExecutable()
    {
        // No entry assembly here carries DevDataRoot, which is equally the situation of every published
        // d47.exe.
        Assert.Equal(
            Path.GetFullPath(AppContext.BaseDirectory),
            AppPaths.ForRunningBuild().InstallRoot);
    }
}

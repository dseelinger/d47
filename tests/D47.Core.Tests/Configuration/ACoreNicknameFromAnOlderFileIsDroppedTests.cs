using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class ACoreNicknameFromAnOlderFileIsDroppedTests
{
    private const string WithANickname = """
        {
          "schemaVersion": 1,
          "persona": { "id": "cora", "shipName": "Fred", "keepShipName": false }
        }
        """;

    [Fact]
    public void ThePersonaLoadsAndTheNicknameKeysAreNotNamedAsUnknown()
    {
        var install = new MemoryInstall();
        install.Files.WriteText(install.Paths.SettingsFile, WithANickname);

        var store = new SettingsStore(install.Paths, install.Files, NullLogger<SettingsStore>.Instance);
        var settings = store.Load();

        Assert.Equal("cora", settings.Persona.Id);
        Assert.Empty(store.UnknownKeys);

        store.Save(settings);

        var written = install.Files.ReadText(install.Paths.SettingsFile);
        Assert.DoesNotContain("shipName", written, StringComparison.Ordinal);
        Assert.DoesNotContain("keepShipName", written, StringComparison.Ordinal);
    }

    [Fact]
    public void AskingTheCoreToCallItselfSomethingElseChangesNothing()
    {
        var surface = TestSurface.For(new MemoryInstall());

        Assert.Null(surface.Settings.Find("persona.shipName"));
        Assert.False(surface.Settings.Apply("persona.shipName", "Fred", SettingsCaller.Model).Ok);
    }
}

using D47.Core.Audio;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>#478: a file saved with a bool per Guardian voice effect loads as the effect list.</summary>
public class AnOldGuardianVoiceFileKeepsItsTicksTests
{
    private const string WithTheBools = """
        {
          "schemaVersion": 1,
          "speech": { "guardianVoiceReverb": true, "guardianVoiceCylon": false }
        }
        """;

    private static SettingsStore StoreFor(TempInstall install) =>
        new(install.Paths, NullLogger<SettingsStore>.Instance);

    [Fact]
    public void TheEffectsItTickedAreTickedInTheDefaultOrder()
    {
        using var install = new TempInstall();
        File.WriteAllText(install.Paths.SettingsFile, WithTheBools);

        var store = StoreFor(install);
        var effects = GuardianVoice.Effects(store.Load().Speech.GuardianVoice);

        Assert.Equal(GuardianVoice.Table.Select(effect => effect.Id), effects.Select(effect => effect.Id));
        Assert.Equal(["reverb"], effects.Where(effect => effect.Ticked).Select(effect => effect.Id));
        Assert.Equal(GuardianVoice.Table.Select(effect => effect.DefaultLevel), effects.Select(effect => effect.Level));
        Assert.Empty(store.UnknownKeys);
    }

    [Fact]
    public void SavingItWritesTheListAndNotTheBools()
    {
        using var install = new TempInstall();
        File.WriteAllText(install.Paths.SettingsFile, WithTheBools);

        var store = StoreFor(install);
        store.Save(store.Load());

        var written = File.ReadAllText(install.Paths.SettingsFile);
        Assert.Contains("\"guardianVoice\"", written, StringComparison.Ordinal);
        Assert.DoesNotContain("guardianVoiceReverb", written, StringComparison.Ordinal);
        Assert.DoesNotContain("guardianVoiceCylon", written, StringComparison.Ordinal);

        var effects = GuardianVoice.Effects(StoreFor(install).Load().Speech.GuardianVoice);
        Assert.Equal(["reverb"], effects.Where(effect => effect.Ticked).Select(effect => effect.Id));
    }

    [Fact]
    public void AFileThatHasTheListIgnoresTheBools()
    {
        using var install = new TempInstall();
        File.WriteAllText(
            install.Paths.SettingsFile,
            """
            {
              "schemaVersion": 1,
              "speech": {
                "guardianVoiceReverb": true,
                "guardianVoice": { "effects": [ { "id": "cylon", "ticked": true, "level": 20 } ] }
              }
            }
            """);

        var store = StoreFor(install);
        var settings = store.Load();
        var effects = GuardianVoice.Effects(settings.Speech.GuardianVoice);

        Assert.Equal(["cylon"], effects.Where(effect => effect.Ticked).Select(effect => effect.Id));
        Assert.Null(settings.Speech.Extra);
        Assert.Empty(store.UnknownKeys);
    }
}

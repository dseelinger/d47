using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>Which subsystem a changed settings key re-applies.</summary>
public class EveryRowReachesItsSubsystemTests
{
    [Theory]
    [InlineData("llm.provider", SettingsSubsystem.LanguageModel)]
    [InlineData("llm.aboutMe", SettingsSubsystem.LanguageModel)]
    [InlineData("speech.provider", SettingsSubsystem.Speech)]
    [InlineData("speech.rate", SettingsSubsystem.Speech)]
    [InlineData("audio.music", SettingsSubsystem.Audio)]
    [InlineData("callouts.fuel", SettingsSubsystem.Callouts)]
    [InlineData("listening.mode", SettingsSubsystem.Listening)]
    [InlineData("persona.id", SettingsSubsystem.Persona)]
    public void AKeyReachesTheSubsystemItIsDownstreamOf(string key, SettingsSubsystem expected)
    {
        Assert.Equal(expected, SettingsFanout.For(key).Subsystem);
    }

    /// <summary>Case-insensitive on the prefix, as the chain it replaces was.</summary>
    [Fact]
    public void ThePrefixIsMatchedWhateverTheCase()
    {
        Assert.Equal(SettingsSubsystem.Speech, SettingsFanout.For("Speech.Rate").Subsystem);
        Assert.Equal(SettingsSubsystem.Listening, SettingsFanout.For("LISTENING.MODE").Subsystem);
    }

    /// <summary>
    /// Most of the settings file is not downstream of anything that needs re-applying — window
    /// placement, view state, checklist progress.
    /// </summary>
    [Theory]
    [InlineData("view.transcript")]
    [InlineData("window.placement")]
    [InlineData("")]
    [InlineData(null)]
    public void AKeyNothingReReadsReachesNothing(string? key)
    {
        Assert.Equal(SettingsSubsystem.None, SettingsFanout.For(key).Subsystem);
    }

    /// <summary>
    /// Clearing the Voice row is how a Commander asks for the voice d47 chose for this core back.
    /// </summary>
    [Fact]
    public void TheVoiceRowAlsoAsksForAVoiceForTheCoreAboard()
    {
        Assert.True(SettingsFanout.For(SpeechCapability.VoiceKey).ChooseVoiceForCoreAboard);
    }

    [Theory]
    [InlineData(SpeechCapability.RateKey)]
    [InlineData(SpeechCapability.ProviderKey)]
    [InlineData(SpeechCapability.OutputDeviceKey)]
    [InlineData("persona.id")]
    public void NoOtherRowDoes(string key)
    {
        // Moving the rate slider must not send d47 to a model to pick a voice.
        Assert.False(SettingsFanout.For(key).ChooseVoiceForCoreAboard);
    }

    [Fact]
    public void TheRootReAppliesExactlyTheseSixPrefixes()
    {
        var routed = TestSurface.For(new TempInstall()).Registry.All
            .SelectMany(capability => capability.Descriptor.Settings)
            .Select(row => SettingsFanout.For(row.Key).Subsystem)
            .Where(subsystem => subsystem != SettingsSubsystem.None)
            .Distinct()
            .OrderBy(subsystem => subsystem)
            .ToArray();

        Assert.Equal(
            [
                SettingsSubsystem.LanguageModel,
                SettingsSubsystem.Speech,
                SettingsSubsystem.Audio,
                SettingsSubsystem.Callouts,
                SettingsSubsystem.Listening,
                SettingsSubsystem.Persona,
            ],
            routed);
    }

    /// <summary>
    /// Every registered row that is the root's to re-apply reaches the subsystem named by its own
    /// prefix.
    /// </summary>
    [Fact]
    public void ARowUnderARoutedPrefixReachesThatPrefixesSubsystem()
    {
        var misrouted = TestSurface.For(new TempInstall()).Registry.All
            .SelectMany(capability => capability.Descriptor.Settings)
            .Select(row => row.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(key => (Key: key, Expected: ExpectedFor(key), Actual: SettingsFanout.For(key).Subsystem))
            .Where(row => row.Expected is not null && row.Expected != row.Actual)
            .Select(row => $"{row.Key} reached {row.Actual}, expected {row.Expected}")
            .ToArray();

        Assert.True(misrouted.Length == 0, string.Join("; ", misrouted));
    }

    /// <summary>The subsystem a key's own prefix names, or null when nothing here claims it.</summary>
    private static SettingsSubsystem? ExpectedFor(string key) => key.Split('.')[0].ToLowerInvariant() switch
    {
        "llm" => SettingsSubsystem.LanguageModel,
        "speech" => SettingsSubsystem.Speech,
        "audio" => SettingsSubsystem.Audio,
        "callouts" => SettingsSubsystem.Callouts,
        "listening" => SettingsSubsystem.Listening,
        "persona" => SettingsSubsystem.Persona,
        _ => null,
    };
}

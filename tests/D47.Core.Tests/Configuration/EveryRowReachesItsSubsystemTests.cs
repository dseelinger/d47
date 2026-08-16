using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// Which subsystem a changed settings key re-applies (list.md Phase 4, "Apply every setting
/// without a restart"; list.md Phase 19).
/// <para>
/// The promise is that every row takes effect immediately and that the startup path and the
/// change path cannot drift. That made this dispatch load-bearing, and it was an if/else chain of
/// string prefixes inside an event handler in <c>AppHost</c> — where a row routed to nothing is a
/// setting that silently does not work until the app is restarted, and where nothing could check.
/// </para>
/// </summary>
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

    /// <summary>
    /// Case-insensitive on the prefix, as the chain it replaces was. A capability naming its row
    /// <c>Speech.Rate</c> still has to reach the speech wiring.
    /// </summary>
    [Fact]
    public void ThePrefixIsMatchedWhateverTheCase()
    {
        Assert.Equal(SettingsSubsystem.Speech, SettingsFanout.For("Speech.Rate").Subsystem);
        Assert.Equal(SettingsSubsystem.Listening, SettingsFanout.For("LISTENING.MODE").Subsystem);
    }

    /// <summary>
    /// Most of the settings file is not downstream of anything that needs re-applying — window
    /// placement, view state, checklist progress. Those reach nothing, and reaching nothing is a
    /// real answer rather than a gap.
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
    /// It is the only speech row that does so.
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

    /// <summary>
    /// The six prefixes the composition root re-applies, pinned.
    /// <para>
    /// <b>The root is not the only subscriber, and this test exists because that is easy to
    /// forget.</b> <c>VrHost</c>, <c>MainWindow</c>, <c>SettingsView</c>, <c>ThemeManager</c> and
    /// <c>ZoomHost</c> each take their own subscription to <see cref="SettingsService.Changed"/>,
    /// so <c>vr.*</c>, <c>ui.*</c>, <c>hotkeys.*</c> and <c>logging.*</c> reaching nothing here is
    /// correct rather than a gap — and rows like <c>egress.*</c> and <c>actions.*</c> are read
    /// live at the point of use and need no re-apply at all.
    /// </para>
    /// <para>
    /// Which means "this row is routed nowhere" cannot distinguish a row handled elsewhere from a
    /// row nobody wired up, and a test asserting that every registered row reaches a subsystem
    /// asserts something false. What is worth pinning is the set itself: a seventh prefix here
    /// should be a deliberate edit to this list rather than something that arrives unnoticed.
    /// </para>
    /// </summary>
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
    /// Every registered row that <em>is</em> the root's to re-apply reaches the subsystem named
    /// by its own prefix. Asked of the registry rather than of a hand-kept list, so a capability
    /// adding a row under one of the six is covered the day it is added.
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

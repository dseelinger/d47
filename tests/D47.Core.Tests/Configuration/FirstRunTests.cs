using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>The guided key setup.</summary>
public class FirstRunTests
{
    private static readonly LlmProviderInfo NeedsKey =
        LlmProviderCatalog.Selected(LlmProviderCatalog.AnthropicId);

    private static LlmProviderInfo NoKeyNeeded => LlmProviderCatalog.Selected(LlmProviderCatalog.NoneId);

    [Fact]
    public void ItIsNeededWhenTheProvidersKeyIsMissing() =>
        Assert.True(FirstRun.IsNeeded(NeedsKey, _ => false));

    [Fact]
    public void ItIsNotNeededOnceTheKeyIsStored() =>
        Assert.False(FirstRun.IsNeeded(NeedsKey, _ => true));

    /// <summary>A provider that authenticates some other way is a complete configuration.</summary>
    [Fact]
    public void AProviderThatNeedsNoKeyIsNotAReasonToAsk() =>
        Assert.False(FirstRun.IsNeeded(NoKeyNeeded, _ => false));

    /// <summary>
    /// The setting naming a provider that no longer exists is itself a reason to guide: nothing will
    /// work until it is changed, and saying nothing leaves a Commander with a silent app.
    /// </summary>
    [Fact]
    public void AnUnknownProviderStillAsks() =>
        Assert.True(FirstRun.IsNeeded(provider: null, _ => true));

    /// <summary>The case a flag would get wrong, and the reason the item forbids one.</summary>
    [Fact]
    public void ARestoredSecretsFileThatWillNotDecryptStillAsks()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Secrets.Set(NeedsKey.KeySecretName!, "sk-from-the-old-machine");
        Assert.False(FirstRun.IsNeeded(NeedsKey, surface.Secrets.Has));

        // The same file, read by something that cannot decrypt it — which is exactly what a DPAPI-scoped blob
        // looks like to a different Windows account.
        var moved = new SecretStore(
            install.Paths,
            new NeverUnprotects(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SecretStore>.Instance);

        Assert.True(FirstRun.IsNeeded(NeedsKey, moved.Has));
    }

    [Fact]
    public void TheLanguageModelKeyLeadsAndIsTheOnlyRequiredStep()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var steps = FirstRun.Steps(
            surface.Registry,
            surface.Settings.Current,
            NeedsKey,
            surface.Secrets.Has,
            ConversationCapability.KeyRowFor(NeedsKey),
            [SpeechCapability.KeyRowFor(Core.Audio.TtsProviderCatalog.ElevenLabs)]);

        Assert.Equal(2, steps.Count);
        Assert.True(steps[0].Required);
        Assert.False(steps[1].Required);
        Assert.Equal(ConversationCapability.KeyRowFor(NeedsKey), steps[0].Row.Key);
    }

    /// <summary>The rows are the registry's own.</summary>
    [Fact]
    public void TheStepsCarryTheRealDescriptorRows()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var registered = surface.Registry.All
            .SelectMany(capability => capability.Descriptor.Settings)
            .ToDictionary(row => row.Key, StringComparer.OrdinalIgnoreCase);

        var steps = FirstRun.Steps(
            surface.Registry,
            surface.Settings.Current,
            NeedsKey,
            surface.Secrets.Has,
            ConversationCapability.KeyRowFor(NeedsKey),
            [SpeechCapability.KeyRowFor(Core.Audio.TtsProviderCatalog.ElevenLabs)]);

        foreach (var step in steps)
        {
            Assert.Same(registered[step.Row.Key], step.Row);
        }
    }

    /// <summary>
    /// Every step says what that key sends and where, computed rather than written beside the row.
    /// </summary>
    [Fact]
    public void EveryStepDisclosesItsEgress()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var steps = FirstRun.Steps(
            surface.Registry,
            surface.Settings.Current,
            NeedsKey,
            surface.Secrets.Has,
            ConversationCapability.KeyRowFor(NeedsKey),
            [SpeechCapability.KeyRowFor(Core.Audio.TtsProviderCatalog.ElevenLabs)]);

        Assert.All(steps, step =>
        {
            Assert.NotNull(step.Egress);
            Assert.NotEmpty(step.Egress!.What);
        });
    }

    [Fact]
    public void AKeyRowDisclosesItsOwnProviderRatherThanTheSelectedOne()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        // The state the fault was reported in: a voice provider is selected, and it is not the one whose key
        // is being offered.
        Assert.Equal(Core.Audio.TtsProviderCatalog.EdgeId, surface.Settings.Current.Speech.Provider);

        var steps = FirstRun.Steps(
            surface.Registry,
            surface.Settings.Current,
            NeedsKey,
            surface.Secrets.Has,
            ConversationCapability.KeyRowFor(NeedsKey),
            [SpeechCapability.KeyRowFor(Core.Audio.TtsProviderCatalog.ElevenLabs)]);

        var voiceKey = steps.Single(
            s => s.Row.Key == SpeechCapability.KeyRowFor(Core.Audio.TtsProviderCatalog.ElevenLabs));

        Assert.Equal(Core.Audio.TtsProviderCatalog.ElevenLabs.Destination, voiceKey.Egress!.Destination);
        Assert.DoesNotContain(Core.Audio.TtsProviderCatalog.Edge.Destination, voiceKey.Egress.Destination);
        Assert.Contains("ElevenLabs", voiceKey.Egress.What, StringComparison.Ordinal);
    }

    /// <summary>A row key that matches nothing is skipped rather than throwing.</summary>
    [Fact]
    public void AnOptionalRowThatDoesNotExistYetIsSkipped()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var steps = FirstRun.Steps(
            surface.Registry,
            surface.Settings.Current,
            NeedsKey,
            surface.Secrets.Has,
            ConversationCapability.KeyRowFor(NeedsKey),
            ["inara.apiKey.thatNobodyHasWrittenYet"]);

        Assert.Single(steps);
    }

    [Fact]
    public void AStoredKeyMarksItsStepSatisfiedRatherThanRemovingIt()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        surface.Secrets.Set(NeedsKey.KeySecretName!, "sk-test");

        var steps = FirstRun.Steps(
            surface.Registry,
            surface.Settings.Current,
            NeedsKey,
            surface.Secrets.Has,
            ConversationCapability.KeyRowFor(NeedsKey),
            []);

        Assert.Single(steps);
        Assert.True(steps[0].Satisfied);
    }

    /// <summary>Being offline says nothing about a key, so it must not hold a guided run.</summary>
    [Fact]
    public void OnlyARejectionBlocks()
    {
        Assert.True(SecretCheck.Rejected("no").Blocks);
        Assert.False(SecretCheck.Unreachable("offline").Blocks);
        Assert.False(SecretCheck.Works("fine").Blocks);
        Assert.False(SecretCheck.Untested.Blocks);
    }
}

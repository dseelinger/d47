using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// The guided key setup (Phase 16, "Ask for the keys on the first run that needs them").
/// <para>
/// Everything asserted here is about the <em>decision</em> rather than the window, which is why it
/// lives in Core: "should this be shown" and "what should it show" are answerable from state, and a
/// test that had to open a window could not cover the case that matters most — a Commander whose
/// secrets file will not decrypt.
/// </para>
/// </summary>
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

    /// <summary>
    /// A provider that authenticates some other way is a complete configuration. Guiding somebody
    /// to paste a key they do not need teaches them that d47 asks for things it does not use.
    /// </summary>
    [Fact]
    public void AProviderThatNeedsNoKeyIsNotAReasonToAsk() =>
        Assert.False(FirstRun.IsNeeded(NoKeyNeeded, _ => false));

    /// <summary>
    /// The setting naming a provider that no longer exists is itself a reason to guide: nothing
    /// will work until it is changed, and saying nothing leaves a Commander with a silent app.
    /// </summary>
    [Fact]
    public void AnUnknownProviderStillAsks() =>
        Assert.True(FirstRun.IsNeeded(provider: null, _ => true));

    /// <summary>
    /// <b>The case a flag would get wrong, and the reason the item forbids one.</b> A Commander
    /// who copied their <c>data\</c> folder to a new machine has a <c>secrets.json</c> full of
    /// values DPAPI cannot decrypt on this account. <see cref="SecretStore"/> reports those absent,
    /// so the state says "no usable key" and the guide opens — where a "have we shown this?" flag
    /// would have been set on the old machine and would suppress it here forever.
    /// </summary>
    [Fact]
    public void ARestoredSecretsFileThatWillNotDecryptStillAsks()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Secrets.Set(NeedsKey.KeySecretName!, "sk-from-the-old-machine");
        Assert.False(FirstRun.IsNeeded(NeedsKey, surface.Secrets.Has));

        // The same file, read by something that cannot decrypt it — which is exactly what a
        // DPAPI-scoped blob looks like to a different Windows account.
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

    /// <summary>
    /// The rows are the registry's own. A parallel copy of these fields is the second thing to
    /// keep in step and the one that drifts, which is what the item forbids in as many words.
    /// </summary>
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
    /// This is the one screen where a Commander is deciding whether to trust the thing.
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

    /// <summary>
    /// A key row discloses <em>its own</em> provider's destination, not the selected provider's.
    /// <para>
    /// The step above asserts that something is said, which is why this went unnoticed: with Edge
    /// selected — the state every fresh install is in — the ElevenLabs key row said "Spoken
    /// replies → speech.platform.bing.com" and described Microsoft's Edge Read Aloud service.
    /// Non-empty, computed, and false about every consequence of the key being pasted.
    /// </para>
    /// <para>
    /// Pinned by destination rather than by the whole sentence: the wording is the catalog's to
    /// change, the address is the claim being made.
    /// </para>
    /// </summary>
    [Fact]
    public void AKeyRowDisclosesItsOwnProviderRatherThanTheSelectedOne()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        // The state the fault was reported in: a voice provider is selected, and it is not the
        // one whose key is being offered.
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

    /// <summary>
    /// A row key that matches nothing is skipped rather than throwing. Inara's row arrives in a
    /// later phase, and a guided path that crashes because an optional row does not exist yet is
    /// worse than one that guides through what does.
    /// </summary>
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

    /// <summary>
    /// A stored key does not remove its step — it marks it satisfied. Reopening from About on a
    /// configured install is a supported path, since keys get rotated and revoked.
    /// </summary>
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

    /// <summary>
    /// Being offline says nothing about a key, so it must not hold a guided run. Only an outright
    /// rejection does — the item is explicit that none of this is a wall.
    /// </summary>
    [Fact]
    public void OnlyARejectionBlocks()
    {
        Assert.True(SecretCheck.Rejected("no").Blocks);
        Assert.False(SecretCheck.Unreachable("offline").Blocks);
        Assert.False(SecretCheck.Works("fine").Blocks);
        Assert.False(SecretCheck.Untested.Blocks);
    }
}

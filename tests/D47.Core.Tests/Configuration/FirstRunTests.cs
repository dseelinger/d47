using D47.Core.Configuration;
using D47.Core.Storage;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>When the setup wizard opens by itself.</summary>
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
    [Trait("Category", "Integration")]
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
            new NeverUnprotects(), install.Files,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SecretStore>.Instance);

        Assert.True(FirstRun.IsNeeded(NeedsKey, moved.Has));
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

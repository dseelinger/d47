using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>An opaque voice id is never shown in place of a voice's name.</summary>
public class AVoiceIsNamedNeverIdentifiedTests
{
    private const string ElevenId = "JBFqnCBsd6RMkjVDRZzb";

    private static readonly VoiceCatalogue Listed =
        VoiceCatalogue.Of([new VoiceInfo(ElevenId, "George", "british", "male")]);

    [Fact]
    public void AKnownVoiceIsItsName()
    {
        var label = Listed.LabelFor(ElevenId, TtsProviderCatalog.ElevenLabs);

        Assert.Contains("George", label, StringComparison.Ordinal);
        Assert.DoesNotContain(ElevenId, label, StringComparison.Ordinal);
    }

    /// <summary>The reported fault.</summary>
    [Fact]
    public void AnUnresolvedElevenLabsIdIsNeverShown()
    {
        var beforeTheListArrives = VoiceCatalogue.Silent.LabelFor(ElevenId, TtsProviderCatalog.ElevenLabs);
        var notInTheList = Listed.LabelFor("SomeOtherOpaqueId", TtsProviderCatalog.ElevenLabs);

        Assert.DoesNotContain(ElevenId, beforeTheListArrives, StringComparison.Ordinal);
        Assert.DoesNotContain("SomeOtherOpaqueId", notInTheList, StringComparison.Ordinal);

        // And the two situations read differently, because they are different: one is waiting and the other
        // is a voice this account does not have.
        Assert.NotEqual(beforeTheListArrives, notInTheList);
        Assert.Contains(TtsProviderCatalog.ElevenLabs.Name, beforeTheListArrives, StringComparison.Ordinal);
    }

    /// <summary>Edge keeps its fallback.</summary>
    [Fact]
    public void AnUnresolvedEdgeVoiceStillShowsWhatItIs()
    {
        const string edge = "en-US-AndrewMultilingualNeural";

        Assert.Equal(edge, VoiceCatalogue.Silent.LabelFor(edge, TtsProviderCatalog.Edge));
    }

    /// <summary>The flag itself, so the two providers cannot silently come to agree.</summary>
    [Fact]
    public void OnlyTheProviderWithUnreadableIdsDeclaresThem()
    {
        Assert.True(TtsProviderCatalog.ElevenLabs.VoiceIdsAreOpaque);
        Assert.False(TtsProviderCatalog.Edge.VoiceIdsAreOpaque);
    }
}

using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// <b>An opaque voice id is never shown in place of a voice's name.</b>
/// <para>
/// Reported 2026-08-20 against the Voice row, which read <c>JBFqnCBsd6RMkjVDRZzb</c> under a label
/// promising "which voice the core aboard speaks in". The lookup was already there; what was wrong
/// was the fallback, which handed the raw id to the screen whenever the catalogue could not
/// resolve it — and an ElevenLabs id says nothing whatever to a person.
/// </para>
/// </summary>
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

    /// <summary>
    /// The reported fault. The list is fetched when a key arrives and when the provider changes,
    /// so a voice chosen last session is unresolved until that returns — and for however long
    /// that takes, the row must not answer with the id.
    /// </summary>
    [Fact]
    public void AnUnresolvedElevenLabsIdIsNeverShown()
    {
        var beforeTheListArrives = VoiceCatalogue.Silent.LabelFor(ElevenId, TtsProviderCatalog.ElevenLabs);
        var notInTheList = Listed.LabelFor("SomeOtherOpaqueId", TtsProviderCatalog.ElevenLabs);

        Assert.DoesNotContain(ElevenId, beforeTheListArrives, StringComparison.Ordinal);
        Assert.DoesNotContain("SomeOtherOpaqueId", notInTheList, StringComparison.Ordinal);

        // And the two situations read differently, because they are different: one is waiting and
        // the other is a voice this account does not have.
        Assert.NotEqual(beforeTheListArrives, notInTheList);
        Assert.Contains(TtsProviderCatalog.ElevenLabs.Name, beforeTheListArrives, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Edge keeps its fallback.</b> <c>en-US-AndrewMultilingualNeural</c> is not pretty but it
    /// says who it is, and a Commander who typed one themselves should see what they typed. The
    /// rule is a property of the provider, not a blanket ban on showing an id.
    /// </summary>
    [Fact]
    public void AnUnresolvedEdgeVoiceStillShowsWhatItIs()
    {
        const string edge = "en-US-AndrewMultilingualNeural";

        Assert.Equal(edge, VoiceCatalogue.Silent.LabelFor(edge, TtsProviderCatalog.Edge));
    }

    /// <summary>The flag itself, so the two providers cannot quietly come to agree.</summary>
    [Fact]
    public void OnlyTheProviderWithUnreadableIdsDeclaresThem()
    {
        Assert.True(TtsProviderCatalog.ElevenLabs.VoiceIdsAreOpaque);
        Assert.False(TtsProviderCatalog.Edge.VoiceIdsAreOpaque);
    }
}

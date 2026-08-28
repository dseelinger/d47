using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// Four situations used to arrive as one empty list (Phase 19,
/// docs/spikes/elevenlabs-voice-sources.md §3).
/// <para>
/// The spike measured the distinction living at the seam and being discarded one line above it:
/// no key stored, a key the service refused with <c>401 invalid_api_key</c>, no answer at all,
/// and an account that genuinely holds no voices all became <c>[]</c>. The Commander saw the
/// same nothing for all four, under a sentence telling them to type the value they wanted —
/// which, for a voice id, they have no way of knowing.
/// </para>
/// </summary>
public class AnEmptyVoiceListSaysWhichEmptyTests
{
    private const string Provider = "ElevenLabs";

    [Fact]
    public void AListWithVoicesInItExplainsNothing()
    {
        var listed = VoiceCatalogue.Of([new VoiceInfo("JBFqnCBsd6RMkjVDRZzb", "George", "british", "male")]);

        Assert.Null(listed.WhyEmpty(Provider));
        Assert.Equal(1, listed.Count);
    }

    [Fact]
    public void NoKeyPointsAtTheRowThatFixesIt()
    {
        var said = VoiceCatalogue.NoKey("no key is stored").WhyEmpty(Provider);

        Assert.Contains("API key", said!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Provider, said, StringComparison.Ordinal);
    }

    [Fact]
    public void ARefusedKeySaysSoAndQuotesTheService()
    {
        // The service's own message is preferred over anything mapped from the status code, for
        // the same reason a refused synthesis prefers it: "Invalid API key" is actionable and
        // "401" is not. Measured against the live API on 2026-08-16.
        var said = VoiceCatalogue.KeyRejected("Invalid API key").WhyEmpty(Provider);

        Assert.Contains("refused", said!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Invalid API key", said, StringComparison.Ordinal);
    }

    [Fact]
    public void BeingUnreachableIsNotTheSameAsBeingRefused()
    {
        var unreachable = VoiceCatalogue.Unreachable("the remote name could not be resolved").WhyEmpty(Provider)!;
        var refused = VoiceCatalogue.KeyRejected("Invalid API key").WhyEmpty(Provider)!;

        // One means wait, the other means go and look at the key. A Commander who cannot tell
        // them apart re-pastes a perfectly good key at an outage.
        Assert.NotEqual(unreachable, refused);
        Assert.Contains("could not be reached", unreachable, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The one case where waiting, retrying and re-pasting the key all change nothing — so it
    /// must not read like any of the three that they do.
    /// </summary>
    [Fact]
    public void AnAccountThatGenuinelyHasNoVoicesSaysThatOutright()
    {
        var said = VoiceCatalogue.Of([]).WhyEmpty(Provider)!;

        Assert.Contains("answered", said, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refused", said, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("could not be reached", said, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EveryEmptyReadsDifferentlyFromEveryOther()
    {
        string[] said =
        [
            VoiceCatalogue.Of([]).WhyEmpty(Provider)!,
            VoiceCatalogue.NoKey("none").WhyEmpty(Provider)!,
            VoiceCatalogue.KeyRejected("Invalid API key").WhyEmpty(Provider)!,
            VoiceCatalogue.Unreachable("timed out").WhyEmpty(Provider)!,
        ];

        Assert.Equal(said.Length, said.Distinct(StringComparer.Ordinal).Count());
        Assert.All(said, sentence => Assert.EndsWith(".", sentence, StringComparison.Ordinal));
    }

    /// <summary>
    /// A failure with nothing to quote still finishes its sentence. The detail is the service's
    /// and the service does not always give one.
    /// </summary>
    [Fact]
    public void AFailureWithNothingToQuoteStillReadsAsASentence()
    {
        Assert.EndsWith(".", VoiceCatalogue.KeyRejected(string.Empty).WhyEmpty(Provider)!, StringComparison.Ordinal);
        Assert.EndsWith(".", new VoiceCatalogue([], VoiceListing.Unreachable).WhyEmpty(Provider)!, StringComparison.Ordinal);
    }

    [Fact]
    public void NoProviderSelectedIsAnEmptyThatIsNotAFailure()
    {
        // "None" is a supported choice. The row is absent then, so nothing reads this — but the
        // value has to exist, and it must not claim an outage.
        Assert.Equal(VoiceListing.Listed, VoiceCatalogue.Silent.Listing);
        Assert.Equal(0, VoiceCatalogue.Silent.Count);
    }
}

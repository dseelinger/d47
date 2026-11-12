using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// The codex pictures the Commander saves stay on this PC. The pictures "take a picture" makes are Elite's screenshots,
/// disclosed under Your screenshots, so this entry names no captures folder.
/// </summary>
public class TheCodexCapturesDisclosureSaysThePicturesStayOnThisPcTests
{
    [Fact]
    public void ItIsListedSilentWithTheCommandersWords()
    {
        var entry = EgressDisclosure.Entry(EgressDisclosure.CodexCaptures, new D47Settings(), llmKeyPresent: false);

        Assert.Contains(EgressDisclosure.CodexCaptures, EgressDisclosure.Ids);
        Assert.Equal("Your codex pictures", entry.Name);
        Assert.False(entry.Active);
        Assert.Equal(
            "The codex pictures you save are kept in data\\codex-pictures\\local on this PC. They are never sent anywhere.",
            entry.What);
        Assert.DoesNotContain("captures", entry.What, StringComparison.Ordinal);
    }
}

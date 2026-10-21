using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class TheCodexCapturesDisclosureSaysThePicturesStayOnThisPcTests
{
    [Fact]
    public void ItIsListedSilentWithTheCommandersWords()
    {
        var entry = EgressDisclosure.Entry(EgressDisclosure.CodexCaptures, new D47Settings(), llmKeyPresent: false);

        Assert.Contains(EgressDisclosure.CodexCaptures, EgressDisclosure.Ids);
        Assert.Equal("Your captures and codex pictures", entry.Name);
        Assert.False(entry.Active);
        Assert.Equal(
            "Pictures you take by saying \"take a picture\" are kept in data\\captures, and the codex pictures you save are kept in data\\codex-pictures\\local, on this PC. They are never sent anywhere.",
            entry.What);
    }
}

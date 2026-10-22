using D47.Core.Configuration;

using Xunit;

namespace D47.Core.Tests.Configuration;

public class TheBgsTickFetchIsDisclosedTests
{
    [Fact]
    public void OffByDefaultItSaysNothingIsSent()
    {
        var settings = new D47Settings();

        var entry = Assert.Single(
            EgressDisclosure.For(settings, llmKeyPresent: false),
            entry => entry.Id == EgressDisclosure.BgsTick);

        Assert.False(entry.Active);
        Assert.Contains("nothing is requested", entry.What, StringComparison.Ordinal);
        Assert.Contains("BGS tick → nothing sent", EgressDisclosure.Describe(settings, false), StringComparison.Ordinal);
    }

    [Fact]
    public void OnItNamesTheHostAndSaysTheRequestIsUnencrypted()
    {
        var settings = new D47Settings { Knowledge = new KnowledgeSettings { BgsTick = true } };

        var entry = Assert.Single(
            EgressDisclosure.For(settings, llmKeyPresent: false),
            entry => entry.Id == EgressDisclosure.BgsTick);

        Assert.True(entry.Active);
        Assert.Equal("tick.infomancer.uk", entry.Destination);
        Assert.Contains("unencrypted", entry.What, StringComparison.Ordinal);
        Assert.Contains("no key, no Commander name, no position", entry.What, StringComparison.Ordinal);
        Assert.Contains("BGS tick → tick.infomancer.uk", EgressDisclosure.Describe(settings, false), StringComparison.Ordinal);
    }

    [Fact]
    public void ItSitsAfterCommunityBiology()
    {
        var ids = EgressDisclosure.Ids.ToList();

        Assert.Equal(ids.IndexOf(EgressDisclosure.CommunityBiology) + 1, ids.IndexOf(EgressDisclosure.BgsTick));
    }
}

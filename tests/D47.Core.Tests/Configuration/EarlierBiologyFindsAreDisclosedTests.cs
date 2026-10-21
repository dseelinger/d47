using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>The Canonn lookup is disclosed as active only while its setting is on.</summary>
public class EarlierBiologyFindsAreDisclosedTests
{
    private static EgressEntry Entry(bool on) => EgressDisclosure.Entry(
        EgressDisclosure.CommunityBiology,
        new D47Settings { Knowledge = new KnowledgeSettings { CommunityBiology = on } },
        llmKeyPresent: false);

    [Fact]
    public void TheEntryIsActiveOnlyWithTheSettingOn()
    {
        var off = Entry(false);
        var on = Entry(true);

        Assert.False(off.Active);
        Assert.Equal("Earlier biology finds are off, so no system name is sent to Canonn.", off.What);
        Assert.True(on.Active);
        Assert.Equal("us-central1-canonn-api-236217.cloudfunctions.net", on.Destination);
        Assert.Equal("Earlier biology finds", on.Name);
    }

    [Fact]
    public void TheSettingIsOffByDefault() => Assert.False(new D47Settings().Knowledge.CommunityBiology);

    [Fact]
    public void TheEntryFollowsNotablePlacesInTheList()
    {
        var ids = EgressDisclosure.Ids.ToList();

        Assert.Equal(ids.IndexOf(EgressDisclosure.NotablePlaces) + 1, ids.IndexOf(EgressDisclosure.CommunityBiology));
    }
}

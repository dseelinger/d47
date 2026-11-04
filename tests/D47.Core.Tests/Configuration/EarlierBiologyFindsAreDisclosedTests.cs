using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>The Canonn lookup is disclosed as active only while its setting is on.</summary>
public class EarlierBiologyFindsAreDisclosedTests
{
    [Fact]
    public void TheEntryIsActiveOnADefaultInstall()
    {
        var entry = EgressDisclosure.Entry(EgressDisclosure.CommunityBiology, new D47Settings(), llmKeyPresent: false);

        Assert.True(entry.Active);
        Assert.Equal("us-central1-canonn-api-236217.cloudfunctions.net", entry.Destination);
        Assert.Equal("Earlier biology finds", entry.Name);
    }

    [Fact]
    public void TheEntryFollowsNotablePlacesInTheList()
    {
        var ids = EgressDisclosure.Ids.ToList();

        Assert.Equal(ids.IndexOf(EgressDisclosure.NotablePlaces) + 1, ids.IndexOf(EgressDisclosure.CommunityBiology));
    }
}

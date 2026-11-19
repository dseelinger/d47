using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>Reporting a region first to Canonn has its own entry, which names the Commander name.</summary>
public class ARegionFirstReportIsDisclosedTests
{
    [Fact]
    public void TheEntryNamesTheCanonnHostAndTheCommanderName()
    {
        var entry = EgressDisclosure.Entry(EgressDisclosure.RegionFirstReport, new D47Settings(), llmKeyPresent: false);

        Assert.True(entry.Active);
        Assert.Equal("us-central1-canonn-api-236217.cloudfunctions.net", entry.Destination);
        Assert.Equal("Region first reports", entry.Name);
        Assert.Contains("your Commander name", entry.What, StringComparison.Ordinal);
        Assert.Contains("your Commander name", entry.Summary, StringComparison.Ordinal);
        Assert.Contains("report it to Canonn", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void TheEarlierBiologyEntryStillSendsNoName()
    {
        var entry = EgressDisclosure.Entry(EgressDisclosure.CommunityBiology, new D47Settings(), llmKeyPresent: false);

        Assert.Contains("No commander name", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDataEgressAnswerListsIt()
    {
        var said = EgressDisclosure.Describe(new D47Settings(), llmKeyPresent: false);

        Assert.Contains(
            "Region first reports → us-central1-canonn-api-236217.cloudfunctions.net",
            said,
            StringComparison.Ordinal);
    }
}

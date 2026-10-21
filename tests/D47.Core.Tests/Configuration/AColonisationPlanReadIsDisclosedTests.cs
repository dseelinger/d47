using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class AColonisationPlanReadIsDisclosedTests
{
    [Fact]
    public void TheSwitchIsOffByDefault()
    {
        Assert.False(new D47Settings().Knowledge.ColonisationPlanner);
    }

    [Fact]
    public void OffItSaysNoSystemNameIsSent()
    {
        var entry = EgressDisclosure.Entry(EgressDisclosure.ColonisationPlanner, new D47Settings(), llmKeyPresent: false);

        Assert.False(entry.Active);
        Assert.Contains("Colonisation plans are off, so no system name is sent to the planner.", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void OnItNamesTheHostAndWhatIsSent()
    {
        var settings = new D47Settings { Knowledge = new KnowledgeSettings { ColonisationPlanner = true } };

        var entry = EgressDisclosure.Entry(EgressDisclosure.ColonisationPlanner, settings, llmKeyPresent: false);

        Assert.True(entry.Active);
        Assert.Equal(
            "ravencolonial100-awcbdvabgze4c5cq.canadacentral-01.azurewebsites.net (Raven Colonial)",
            entry.Destination);
        Assert.Contains("Not your Commander name, no key", entry.What, StringComparison.Ordinal);
        Assert.Contains(EgressDisclosure.ColonisationPlanner, EgressDisclosure.Ids);
    }
}

using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class AColonisationPlanReadIsDisclosedTests
{

    [Fact]
    public void OnADefaultInstallItNamesTheHostAndWhatIsSent()
    {
        var settings = new D47Settings();

        var entry = EgressDisclosure.Entry(EgressDisclosure.ColonisationPlanner, settings, llmKeyPresent: false);

        Assert.True(entry.Active);
        Assert.Equal(
            "ravencolonial100-awcbdvabgze4c5cq.canadacentral-01.azurewebsites.net (Raven Colonial)",
            entry.Destination);
        Assert.Contains("Not your Commander name, no key", entry.What, StringComparison.Ordinal);
        Assert.Contains(EgressDisclosure.ColonisationPlanner, EgressDisclosure.Ids);
    }
}

using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class AGuardianBrokerIsNeverSearchedInAColonyTests
{
    private static StationSearch Search(string broker) => new()
    {
        ReferenceSystem = "Sol",
        TechnologyBroker = broker,
        Filters = new Dictionary<string, string> { ["colonised"] = "true" },
    };

    [Fact]
    public void AGuardianSearchWithColonisedIsRefused()
    {
        Assert.False(StationQuery.TryParse(Search("Guardian"), out _, out var failure));
        Assert.Contains("never in a colonised system", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void AHumanSearchWithColonisedIsAccepted()
    {
        Assert.True(StationQuery.TryParse(Search("Human"), out var query, out var failure), failure);
        Assert.True(query.InColonisedSystems);
    }
}

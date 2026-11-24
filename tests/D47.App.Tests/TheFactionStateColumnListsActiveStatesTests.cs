using D47.App.Panel;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

public sealed class TheFactionStateColumnListsActiveStatesTests
{
    private static SystemSummary System(string? controlling, params FactionPresence[] presences) =>
        new() { Name = "Test", ControllingFaction = controlling, Factions = presences };

    [Fact]
    public void EveryActiveStateOfTheControllingFactionIsListed()
    {
        var system = System(
            "Equestrian Naval Fleet",
            new FactionPresence("Earth Defense Fleet", 0.1),
            new FactionPresence("equestrian naval fleet", 0.5) { ActiveStates = ["Boom", "Civil Liberty"], PendingStates = ["Expansion"] });

        Assert.Equal("Boom · Civil Liberty", SystemSearchPage.FactionStates(system));
    }

    [Fact]
    public void AControllingFactionWithNoActiveStateReadsNone()
    {
        var system = System("Mother Gaia", new FactionPresence("Mother Gaia", 0.4));

        Assert.Equal("None", SystemSearchPage.FactionStates(system));
    }

    [Fact]
    public void NoControllingPresenceHasNoState()
    {
        Assert.Null(SystemSearchPage.FactionStates(System(null, new FactionPresence("Mother Gaia", 0.4))));
        Assert.Null(SystemSearchPage.FactionStates(System("Other", new FactionPresence("Mother Gaia", 0.4))));
    }
}

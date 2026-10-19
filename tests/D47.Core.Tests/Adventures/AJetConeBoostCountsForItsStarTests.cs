using D47.Core.Adventures;
using D47.Core.Journal;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

public class AJetConeBoostCountsForItsStarTests
{
    private const string Neutron = """{ "timestamp":"@", "event":"JetConeBoost", "BoostValue":6.000000 }""";

    private const string WhiteDwarf = """{ "timestamp":"@", "event":"JetConeBoost", "BoostValue":1.500000 }""";

    private static AdventureStanding Fold(string? filter, string journalEvent)
    {
        var adventure = LanternRoute(Accepted) with
        {
            Beats =
            [
                Beat("The Lantern", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" }, "Scoop here."),
                Beat("The Boost", "catalyst", new AdventureTrigger { Kind = TriggerKind.Boost, Count = 1, Filter = filter }, "Done."),
            ],
        };

        var standing = AdventureFold.Apply(
            AdventureFold.Start(adventure),
            Event($$"""{ "timestamp":"{{Stamp(Accepted.AddMinutes(1))}}", "event":"FSDJump", "SystemAddress":{{Lantern}}, "StarSystem":"Ossen's Lantern" }"""));

        return AdventureFold.Apply(standing, Event(journalEvent.Replace("@", Stamp(Accepted.AddMinutes(2)), StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("neutron", true)]
    [InlineData(null, true)]
    [InlineData("whitedwarf", false)]
    public void ANeutronBoostCountsOnlyForNeutronOrUnfilteredObjectives(string? filter, bool counts) =>
        Assert.Equal(counts, Fold(filter, Neutron).IsDone);

    [Theory]
    [InlineData("whitedwarf", true)]
    [InlineData(null, true)]
    [InlineData("neutron", false)]
    public void AWhiteDwarfBoostCountsOnlyForWhiteDwarfOrUnfilteredObjectives(string? filter, bool counts) =>
        Assert.Equal(counts, Fold(filter, WhiteDwarf).IsDone);

    [Theory]
    [InlineData("neutron")]
    [InlineData("WhiteDwarf")]
    [InlineData(null)]
    public void ABoostObjectiveAcceptsItsStars(string? filter) =>
        Assert.Empty(AdventureValidation.CountedProblems("Beat 2", new AdventureTrigger { Kind = TriggerKind.Boost, Count = 1, Filter = filter }));

    [Theory]
    [InlineData("pulsar")]
    [InlineData("blackhole")]
    public void ABoostObjectiveForAnyOtherStarIsRefused(string filter) =>
        Assert.Single(AdventureValidation.CountedProblems("Beat 2", new AdventureTrigger { Kind = TriggerKind.Boost, Count = 1, Filter = filter }));

    [Fact]
    public void ABoostObjectiveIsCountedAndSaysSo()
    {
        var trigger = new AdventureTrigger { Kind = TriggerKind.Boost, Count = 2, Filter = "neutron" };

        Assert.True(trigger.IsCounted);
        Assert.Equal("Neutron boosts: 1 of 2", trigger.Progress(1));
        Assert.Equal("supercharge your frame shift drive in a neutron star's jet cone 2 times", trigger.Describe());
        Assert.Equal("boost off jet cones", RefusedActivities.Phrase(TriggerKind.Boost, null));
    }
}

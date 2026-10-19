using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public sealed class AHomeSystemIsRankedByTheCommandersCareersTests
{
    private static CareerWeights Weights(params (Career Career, int Rating)[] ratings) =>
        CareerWeights.From(ratings.ToDictionary(pair => pair.Career, pair => pair.Rating), CareerStatistics.Empty);

    private static BodySummary Ringed(string system, string reserve = "Pristine") => new()
    {
        Name = system + " A",
        SystemName = system,
        ReserveLevel = reserve,
        Rings = [new RingSummary(system + " A Ring", "Icy") { Hotspots = [("Painite", 2)] }],
    };

    private static BodySummary Living(string system) => new()
    {
        Name = system + " B",
        SystemName = system,
        Signals = [("Biological", 3)],
    };

    [Fact]
    public void AnArchitectIsAlwaysOfferedTheirOwnSystemWhateverScoresHigher()
    {
        var mine = new HomeSystemCandidate("Mine") { Claimed = true };
        var rich = new HomeSystemCandidate("Rich") { Bodies = [Ringed("Rich")], InSquadronSet = true, InPowerSet = true };

        var result = HomeSystemRanking.Rank([rich, mine], Weights((Career.Mining, 100)));

        var only = Assert.Single(result);
        Assert.Equal("Mine", only.Name);
        Assert.Equal(HomeSystemReason.Architect, only.Reason);
    }

    [Fact]
    public void SeveralClaimsAreRankedOnMembershipThenInterests()
    {
        var plain = new HomeSystemCandidate("Plain") { Claimed = true, Bodies = [Ringed("Plain")] };
        var member = new HomeSystemCandidate("Member") { Claimed = true, InPowerSet = true };
        var rich = new HomeSystemCandidate("Rich") { Claimed = true, Bodies = [Ringed("Rich"), Ringed("Rich")] };

        var result = HomeSystemRanking.Rank([plain, rich, member], Weights((Career.Mining, 100)));

        Assert.Equal(["Member", "Rich", "Plain"], result.Select(s => s.Name));
    }

    [Fact]
    public void ASystemInBothSetsOutranksOneInOnlyOneEvenWhenTheOtherScoresHigher()
    {
        var both = new HomeSystemCandidate("Both") { InSquadronSet = true, InPowerSet = true };
        var one = new HomeSystemCandidate("One") { InSquadronSet = true, Bodies = [Ringed("One")] };

        var result = HomeSystemRanking.Rank([one, both], Weights((Career.Mining, 100)));

        Assert.Equal(["Both", "One"], result.Select(s => s.Name));
        Assert.Equal(HomeSystemReason.SquadronAndPower, result[0].Reason);
        Assert.Equal(HomeSystemReason.Squadron, result[1].Reason);
    }

    [Fact]
    public void WhenNoSystemIsInBothSetsTheCombinedSetIsRankedOnInterests()
    {
        var squadron = new HomeSystemCandidate("Squadron") { InSquadronSet = true };
        var power = new HomeSystemCandidate("Power") { InPowerSet = true, Bodies = [Ringed("Power")] };

        var result = HomeSystemRanking.Rank([squadron, power], Weights((Career.Mining, 100)));

        Assert.Equal(["Power", "Squadron"], result.Select(s => s.Name));
        Assert.Equal(HomeSystemReason.Power, result[0].Reason);
    }

    [Fact]
    public void ASquadronAloneIsRankedOnInterests()
    {
        var plain = new HomeSystemCandidate("Plain") { InSquadronSet = true };
        var living = new HomeSystemCandidate("Living") { InSquadronSet = true, Bodies = [Living("Living")] };

        var result = HomeSystemRanking.Rank([plain, living], Weights((Career.Exobiology, 100)));

        Assert.Equal(["Living", "Plain"], result.Select(s => s.Name));
    }

    [Fact]
    public void APowerAloneIsRankedOnInterests()
    {
        var plain = new HomeSystemCandidate("Plain") { InPowerSet = true };
        var ringed = new HomeSystemCandidate("Ringed") { InPowerSet = true, Bodies = [Ringed("Ringed")] };

        var result = HomeSystemRanking.Rank([plain, ringed], Weights((Career.Mining, 100)));

        Assert.Equal(["Ringed", "Plain"], result.Select(s => s.Name));
        Assert.All(result, suggestion => Assert.Equal(HomeSystemReason.Power, suggestion.Reason));
    }

    [Fact]
    public void AMiningHeavyWeightRanksRingHotspotsAboveNone()
    {
        var bare = new HomeSystemCandidate("Bare") { Bodies = [Living("Bare")] };
        var ringed = new HomeSystemCandidate("Ringed") { Bodies = [Ringed("Ringed")] };

        var result = HomeSystemRanking.Rank([bare, ringed], Weights((Career.Mining, 100), (Career.Exobiology, 1)));

        Assert.Equal(["Ringed", "Bare"], result.Select(s => s.Name));
        Assert.Equal(HomeSystemReason.Interests, result[0].Reason);
        Assert.Equal(Career.Mining, result[0].Strengths[0].Career);
    }

    [Fact]
    public void AnExobiologyHeavyWeightRanksBiologicalSignalsAboveNone()
    {
        var bare = new HomeSystemCandidate("Bare") { Bodies = [Ringed("Bare")] };
        var living = new HomeSystemCandidate("Living") { Bodies = [Living("Living")] };

        var result = HomeSystemRanking.Rank([bare, living], Weights((Career.Exobiology, 100), (Career.Mining, 1)));

        Assert.Equal(["Living", "Bare"], result.Select(s => s.Name));
    }

    [Fact]
    public void PristineReservesScoreAboveDepletedOnes()
    {
        var depleted = new HomeSystemCandidate("Depleted") { Bodies = [Ringed("Depleted", "Depleted")] };
        var pristine = new HomeSystemCandidate("Pristine") { Bodies = [Ringed("Pristine")] };

        var result = HomeSystemRanking.Rank([depleted, pristine], Weights((Career.Mining, 100)));

        Assert.Equal(["Pristine", "Depleted"], result.Select(s => s.Name));
    }

    [Fact]
    public void CrimeNeverChangesTheOrder()
    {
        var bare = new HomeSystemCandidate("Bare");
        var ringed = new HomeSystemCandidate("Ringed") { Bodies = [Ringed("Ringed")] };

        var low = HomeSystemRanking.Rank([bare, ringed], Weights((Career.Mining, 60), (Career.Crime, 1)));
        var high = HomeSystemRanking.Rank([bare, ringed], Weights((Career.Mining, 60), (Career.Crime, 100)));

        Assert.Equal(low.Select(s => s.Name), high.Select(s => s.Name));
        Assert.DoesNotContain(high.SelectMany(s => s.Strengths), strength => strength.Career == Career.Crime);
    }

    [Fact]
    public void OnlyTheTopThreeAreOfferedWithTheirTwoStrongestCareers()
    {
        var candidates = Enumerable.Range(1, 5)
            .Select(n => new HomeSystemCandidate($"S{n}") { Populated = true, LargePadMarkets = n, HasTourismStation = true, Bodies = [Ringed($"S{n}"), Living($"S{n}")] })
            .ToList();

        var result = HomeSystemRanking.Rank(candidates, Weights((Career.Mining, 90), (Career.Trading, 80), (Career.Exobiology, 70)));

        Assert.Equal(3, result.Count);
        Assert.All(result, suggestion => Assert.Equal(2, suggestion.Strengths.Count));
    }
}

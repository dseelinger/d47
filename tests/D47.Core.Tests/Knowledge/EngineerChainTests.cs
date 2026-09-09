using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>The referral chain, against the real shipped table.</summary>
public class EngineerChainTests
{
    [Fact]
    public void EveryEngineerIsInTheTableAndMostAreReachedThroughSomebody()
    {
        Assert.Equal(38, EngineerDirectory.All.Count);

        var referred = EngineerDirectory.All.Where(e => e.NeedsReferral).ToArray();

        Assert.Equal(27, referred.Length);

        // The eleven anybody can walk up to.
        Assert.Equal(11, EngineerDirectory.All.Count(e => !e.NeedsReferral));
    }

    [Fact]
    public void EveryReferrerIsAnEngineerTheDirectoryCanActuallyFind()
    {
        // EDDiscovery writes "Tod McQuinn" in a referral and "Tod 'The Blaster' McQuinn" as the entry.
        foreach (var engineer in EngineerDirectory.All)
        {
            foreach (var referrer in engineer.ReferredBy)
            {
                Assert.NotNull(EngineerDirectory.ByName(referrer));
            }
        }

        Assert.Contains("Tod 'The Blaster' McQuinn", EngineerDirectory.ByName("Selene Jean")!.ReferredBy);
    }

    [Fact]
    public void BillTurnerIsReachedThroughSeleneJean()
    {
        // The one conflict between the two sources.
        var bill = EngineerDirectory.ByName("Bill Turner");

        Assert.NotNull(bill);
        Assert.Equal(["Selene Jean"], bill.ReferredBy);
        Assert.Equal(EngineeringRules.ReferralGrade, bill.ReferralGrade);
    }

    [Fact]
    public void ShipReferralsStateAGradeAndOnFootOnesDoNot()
    {
        // Odyssey engineers unlock on a count of modifications, and no source states a grade for them.
        var shipSide = EngineerDirectory.ByName("Broo Tarquin");
        var onFoot = EngineerDirectory.ByName("Kit Fowler");

        Assert.NotNull(shipSide);
        Assert.NotNull(onFoot);
        Assert.Equal(EngineeringRules.ReferralGrade, shipSide.ReferralGrade);
        Assert.True(onFoot.NeedsReferral);
        Assert.Null(onFoot.ReferralGrade);

        // Every grade that is stated is the same one, which is what both sources say.
        Assert.All(
            EngineerDirectory.All.Where(e => e.ReferralGrade is not null),
            e => Assert.Equal(EngineeringRules.ReferralGrade, e.ReferralGrade));
    }

    [Fact]
    public void OneEngineerIsReachedThroughAnyOfThree()
    {
        // Yi Shen.
        var yiShen = EngineerDirectory.ByName("Yi Shen");

        Assert.NotNull(yiShen);
        Assert.Equal(3, yiShen.ReferredBy.Count);
        Assert.Contains("Baltanos", yiShen.ReferredBy);
    }

    [Fact]
    public void TheChainReachesEverybodyFromSomewhere()
    {
        // Walking back from any engineer must terminate at one nobody has to recommend.
        foreach (var engineer in EngineerDirectory.All)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var current = engineer;

            while (current is { NeedsReferral: true })
            {
                Assert.True(seen.Add(current.Name), $"{engineer.Name} loops at {current.Name}");
                current = EngineerDirectory.ByName(current.ReferredBy[0]);
            }

            Assert.NotNull(current);
        }
    }

    [Fact]
    public void AnEngineerSaysWhereTheyAreAndWhatTheyWant()
    {
        var farseer = EngineerDirectory.ByName("Farseer");

        Assert.NotNull(farseer);
        Assert.Equal("6 A", farseer.Body);
        Assert.False(farseer.NeedsReferral);
        Assert.NotNull(farseer.Meeting);
        Assert.NotNull(farseer.Unlock);
        Assert.NotNull(farseer.Reputation);

        // The tribute is still the material list, and the prose is the same fact in words.
        var bill = EngineerDirectory.ByName("Bill Turner")!;

        Assert.Contains("Bromellite", bill.UnlockCost);
        Assert.Contains("Bromellite", bill.Unlock);
    }

    [Fact]
    public void AChainSaysHowARankIsRaisedRatherThanWhatItCosts()
    {
        // "Rank 5 with Farseer" is only an answer if d47 can also say how a rank goes up.
        Assert.Contains("working with them", EngineeringRules.RankRises, StringComparison.Ordinal);

        // The table's own advice per engineer survives and is separate: how reputation with this one rises
        // fastest is a fact about them rather than a price on a grade.
        Assert.NotNull(EngineerDirectory.ByName("Farseer")!.Reputation);
    }
}

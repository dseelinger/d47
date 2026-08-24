using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The referral chain, against the real shipped table.
/// <para>
/// It was written down as unobtainable once — a correct measurement of three files turned into a
/// wrong claim about the world. These check that it is present, that it is joined up, and that
/// the two places the sources do not simply agree are handled deliberately rather than by luck.
/// </para>
/// </summary>
public class EngineerChainTests
{
    [Fact]
    public void EveryEngineerIsInTheTableAndMostAreReachedThroughSomebody()
    {
        Assert.Equal(38, EngineerDirectory.All.Count);

        var referred = EngineerDirectory.All.Where(e => e.NeedsReferral).ToArray();

        Assert.Equal(27, referred.Length);

        // The eleven anybody can walk up to. If this ever hits 38 or 0, the parse has stopped
        // finding referrals and is reporting the absence as a fact about the game.
        Assert.Equal(11, EngineerDirectory.All.Count(e => !e.NeedsReferral));
    }

    [Fact]
    public void EveryReferrerIsAnEngineerTheDirectoryCanActuallyFind()
    {
        // EDDiscovery writes "Tod McQuinn" in a referral and "Tod 'The Blaster' McQuinn" as the
        // entry. A referral left as written names somebody d47 cannot then look up, which turns
        // "who unlocks Selene Jean" into a dead end.
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
        // The one conflict between the two sources. EDDiscovery says "Common knowledge"; the wiki
        // says Selene Jean and a journal trace agrees. Overridden in the generator, on purpose,
        // and asserted here so the override cannot quietly stop applying.
        var bill = EngineerDirectory.ByName("Bill Turner");

        Assert.NotNull(bill);
        Assert.Equal(["Selene Jean"], bill.ReferredBy);
        Assert.Equal(EngineeringRules.ReferralGrade, bill.ReferralGrade);
    }

    [Fact]
    public void ShipReferralsStateAGradeAndOnFootOnesDoNot()
    {
        // Odyssey engineers unlock on a count of modifications, and no source states a grade for
        // them. Null is the honest answer; a 3 would be a requirement d47 invented.
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
        // Yi Shen. Modelled as a list rather than a single name, because collapsing it would
        // report two of the three routes as not existing.
        var yiShen = EngineerDirectory.ByName("Yi Shen");

        Assert.NotNull(yiShen);
        Assert.Equal(3, yiShen.ReferredBy.Count);
        Assert.Contains("Baltanos", yiShen.ReferredBy);
    }

    [Fact]
    public void TheChainReachesEverybodyFromSomewhere()
    {
        // Walking back from any engineer must terminate at one nobody has to recommend. A cycle
        // or a dangling name would be an unlock plan that never bottoms out.
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
        // "Rank 5 with Farseer" is only an answer if d47 can also say how a rank goes up. It used
        // to answer that with a credits figure from a wiki, which was never measured and was not
        // the route the Commander actually took (#26).
        Assert.Contains("working with them", EngineeringRules.RankRises, StringComparison.Ordinal);

        // The table's own advice per engineer survives and is separate: how reputation with this
        // one rises fastest is a fact about them rather than a price on a grade.
        Assert.NotNull(EngineerDirectory.ByName("Farseer")!.Reputation);
    }
}

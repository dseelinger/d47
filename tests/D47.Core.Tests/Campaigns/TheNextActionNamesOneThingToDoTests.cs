using D47.Core.Campaigns;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Campaigns.CampaignReports;

namespace D47.Core.Tests.Campaigns;

/// <summary>Each kind of front has one next action, and the work that does not count there is named once.</summary>
public sealed class TheNextActionNamesOneThingToDoTests
{
    private static Front FrontOf(FactionSystem system, FrontKind kind, SystemIntent? intent = null)
    {
        var campaign = intent is { } set ? Campaign.None with { Intents = Campaign.None.Intents.SetItem(system.Name, set) } : Campaign.None;

        return CampaignFronts.Of(Report(system), SystemStandings.Empty, campaign, Now).First(front => front.Kind == kind);
    }

    private static CampaignNextAction Next(Front front, CombatRecord? combat = null) =>
        CampaignNextAction.For(front, Us, combat ?? CombatRecord.None, BgsWork.None, Now);

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));

        return parsed!;
    }

    private static string Stamp(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

    private static CombatRecord Bond(DateTimeOffset at, long reward, string faction = Us) =>
        CombatRecord.None.Apply(Event($$"""{ "timestamp":"{{Stamp(at)}}", "event":"FactionKillBond", "Reward":{{reward}}, "AwardingFaction":"{{faction}}", "VictimFaction":"Lave Purple Gang" }"""));

    private static CombatRecord Bounty(DateTimeOffset at, long reward, string faction = Us) =>
        CombatRecord.None.Apply(Event($$"""{ "timestamp":"{{Stamp(at)}}", "event":"Bounty", "Target":"federation_dropship", "VictimFaction":"Lave Purple Gang", "Rewards":[{ "Faction":"{{faction}}", "Reward":{{reward}} }], "TotalReward":{{reward}} }"""));

    private static FactionSystem Fighting(string state) =>
        Place("Lave", 0.40, active: [state], others: [new FactionNeighbour("Lave Purple Gang", 0.39) { ActiveStates = [state] }]);

    [Fact]
    public void AWarSaysTradeDoesNotCount()
    {
        var action = Next(FrontOf(Fighting("War"), FrontKind.Conflict));

        Assert.Equal($"Fight for {Us} in a conflict zone in Lave.", action.Sentence);
        Assert.Equal("Trade does not count in a war (guide p.61).", action.NotCounting);
    }

    [Fact]
    public void ACivilWarSaysTradeDoesNotCount()
    {
        var action = Next(FrontOf(Fighting("Civil War"), FrontKind.Conflict));

        Assert.Contains("Trade does not count", action.NotCounting, StringComparison.Ordinal);
    }

    [Fact]
    public void AnElectionSaysCombatDoesNotCountAndAWeaponMayVoidMissions()
    {
        var action = Next(FrontOf(Fighting("Election"), FrontKind.Conflict));

        Assert.Equal($"Run missions or trade for {Us} in Lave.", action.Sentence);
        Assert.Equal("Combat does not count in an election, and firing a weapon may void some election missions (guide p.61).", action.NotCounting);
    }

    [Fact]
    public void UnredeemedCombatBondsForTheFactionAreWhatAWarNeedsRedeemed()
    {
        var action = Next(FrontOf(Fighting("War"), FrontKind.Conflict), Bond(Now.AddHours(-2), 250_000));

        Assert.Equal(
            $"Redeem your 250,000 credits in combat bonds for {Us} at a station in Lave, then fight for {Us} in a conflict zone there.",
            action.Sentence);
    }

    [Fact]
    public void BondsForAnotherFactionAreNotCounted()
    {
        var action = Next(FrontOf(Fighting("War"), FrontKind.Conflict), Bond(Now.AddHours(-2), 250_000, "Lave Purple Gang"));

        Assert.Equal($"Fight for {Us} in a conflict zone in Lave.", action.Sentence);
    }

    [Fact]
    public void BondsRedeemedSinceTheKillAreNotCounted()
    {
        var combat = Bond(Now.AddHours(-2), 250_000);
        var work = new BgsWork { Entries = [new BgsEntry(Now.AddHours(-1), 1, Us, BgsWorkKind.CombatBond, 250_000)] };

        var action = CampaignNextAction.For(FrontOf(Fighting("War"), FrontKind.Conflict), Us, combat, work, Now);

        Assert.Equal($"Fight for {Us} in a conflict zone in Lave.", action.Sentence);
    }

    [Fact]
    public void ALockdownSaysOnlyBountiesHelp()
    {
        var front = FrontOf(Place("Diso", 0.50, active: ["Lockdown"]), FrontKind.BadState);

        var action = Next(front);

        Assert.Equal($"Hunt bounties for {Us} in Diso and redeem them there.", action.Sentence);
        Assert.Equal("Only bounties help during a Lockdown (guide p.87).", action.NotCounting);
    }

    [Fact]
    public void ALockdownWithUnredeemedBountiesSaysToRedeemThem()
    {
        var front = FrontOf(Place("Diso", 0.50, active: ["Lockdown"]), FrontKind.BadState);

        var action = Next(front, Bounty(Now.AddHours(-3), 90_000));

        Assert.Equal($"Redeem your 90,000 credits in bounties for {Us} at a station in Diso.", action.Sentence);
    }

    [Fact]
    public void AnOutbreakSaysBountyVouchersDoNothing()
    {
        var action = Next(FrontOf(Place("Diso", 0.50, active: ["Outbreak"]), FrontKind.BadState));

        Assert.Equal($"Run missions or trade for {Us} in Diso.", action.Sentence);
        Assert.Equal("Bounty vouchers do nothing during an Outbreak (guide p.88).", action.NotCounting);
    }

    [Fact]
    public void AnotherBadStateHasNoNotCountingSentence()
    {
        var action = Next(FrontOf(Place("Diso", 0.50, active: ["Famine"]), FrontKind.BadState));

        Assert.Equal($"Keep {Us}'s influence in Diso up with missions and trade while Famine lasts.", action.Sentence);
        Assert.Null(action.NotCounting);
    }

    [Fact]
    public void ACoupThreatSaysToLiftInfluenceOverTheRival()
    {
        var front = FrontOf(Place("Lave", 0.40, others: [new FactionNeighbour("Lave Purple Gang", 0.52)]), FrontKind.Coup);

        Assert.Equal(
            $"Run missions and trade for {Us} in Lave to lift its influence over Lave Purple Gang.",
            Next(front).Sentence);
    }

    [Fact]
    public void ACoupThreatWithUnredeemedBountiesSaysToRedeemThemFirst()
    {
        var front = FrontOf(Place("Lave", 0.40, others: [new FactionNeighbour("Lave Purple Gang", 0.52)]), FrontKind.Coup);

        Assert.StartsWith($"Redeem your 50,000 credits in bounties for {Us} at a station in Lave, then run missions", Next(front, Bounty(Now.AddHours(-1), 50_000)).Sentence, StringComparison.Ordinal);
    }

    [Fact]
    public void ARetreatSaysToLiftInfluenceClearOfIt()
    {
        var front = FrontOf(Place("Lave", 0.05, active: ["Retreat"]), FrontKind.Retreat);

        Assert.Equal($"Run missions and trade for {Us} in Lave to lift its influence clear of retreat.", Next(front).Sentence);
    }

    [Fact]
    public void AThinMarginSaysToWidenTheLead()
    {
        var front = FrontOf(Place("Lave", 0.40, others: [new FactionNeighbour("Lave Purple Gang", 0.35)]), FrontKind.ThinMargin);

        Assert.Equal($"Run missions and trade for {Us} in Lave to widen its lead over Lave Purple Gang.", Next(front).Sentence);
    }

    [Fact]
    public void AnUnwantedExpansionSaysToStopAddingInfluence()
    {
        var front = FrontOf(Place("Lave", 0.72), FrontKind.UnwantedExpansion, SystemIntent.StayUnder75);

        Assert.Equal($"Stop adding to {Us}'s influence in Lave: it is near 75% and the intent is to stay under it.", Next(front).Sentence);
    }

    [Fact]
    public void AnExpansionUnderWaySaysToLeaveInfluenceAlone()
    {
        var front = FrontOf(Place("Lave", 0.78, active: ["Expansion"]), FrontKind.Expansion);

        Assert.Equal($"Leave {Us}'s influence in Lave as it is until the expansion completes.", Next(front).Sentence);
    }

    [Fact]
    public void AStaleReportSaysToVisit()
    {
        var front = FrontOf(Place("Lave", 0.60, reportedAt: Now.AddDays(-45)), FrontKind.Stale);

        Assert.Equal($"Visit Lave to refresh the report of {Us}'s standing there.", Next(front).Sentence);
    }

    [Fact]
    public void TheActionNamesItsFront()
    {
        var front = FrontOf(Fighting("War"), FrontKind.Conflict);

        Assert.Same(front, Next(front).Front);
    }
}

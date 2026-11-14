using D47.Core.Campaigns;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;
using static D47.Core.Tests.Campaigns.CampaignReports;

namespace D47.Core.Tests.Campaigns;

/// <summary>Each kind of front is detected from the faction report and the Commander's readings, and the list is ranked by code.</summary>
public sealed class CampaignFrontsAreComputedAndRankedTests
{
    private static Campaign With(params (string System, SystemIntent Intent)[] intents) =>
        new() { Intents = intents.Aggregate(Campaign.None.Intents, (held, pair) => held.SetItem(pair.System, pair.Intent)) };

    private static IReadOnlyList<Front> Fronts(FactionReport report, SystemStandings? standings = null, Campaign? campaign = null) =>
        CampaignFronts.Of(report, standings ?? SystemStandings.Empty, campaign ?? Campaign.None, Now);

    private static SystemStandings Standings(params JournalEvent[] events) =>
        events.Aggregate(SystemStandings.Empty, (standings, e) => standings.Apply(e));

    [Theory]
    [InlineData("War", true)]
    [InlineData("Civil War", false)]
    [InlineData("Election", true)]
    public void AConflictPendingOrActiveIsAFront(string state, bool active)
    {
        var lave = Place(
            "Lave",
            0.40,
            active: active ? [state] : null,
            pending: active ? null : [state],
            others: [new FactionNeighbour("Lave Purple Gang", 0.39) { ActiveStates = active ? [state] : [], PendingStates = active ? [] : [state] }]);

        var front = Assert.Single(Fronts(Report(lave)));

        Assert.Equal(FrontKind.Conflict, front.Kind);
        Assert.Equal("Lave Purple Gang", front.Opponent);
        Assert.Contains(active ? "active" : "pending", front.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void DaysWonAndStakesComeFromTheJournalWhenVisited()
    {
        var lave = Place("Lave", 0.40, active: ["War"], reportedAt: Now.AddDays(-2));
        var standings = Standings(Jump(
            "Lave",
            Now.AddHours(-3),
            $"{Faction(Us, 0.40, active: "War")},{Faction("Lave Purple Gang", 0.40, active: "War")}",
            $$"""{ "WarType":"war", "Status":"active", "Faction1":{ "Name":"{{Us}}", "Stake":"Lave Station", "WonDays":2 }, "Faction2":{ "Name":"Lave Purple Gang", "Stake":"", "WonDays":1 } }"""));

        var front = Assert.Single(Fronts(Report(lave), standings));

        Assert.Equal(FrontKind.Conflict, front.Kind);
        Assert.Equal("Lave Station", front.Stake);
        Assert.Equal(1.0, front.Margin);
        Assert.Equal("War against Lave Purple Gang, active. Days won 2 to 1; 4 of 7 wins it. At stake: Lave Station.", front.Reason);
    }

    [Fact]
    public void ARivalNearSixtyPercentInAControlledSystemIsACoupThreat()
    {
        var lave = Place("Lave", 0.40, others: [new FactionNeighbour("Lave Purple Gang", 0.52)]);

        var front = Assert.Single(Fronts(Report(lave)));

        Assert.Equal(FrontKind.Coup, front.Kind);
        Assert.Equal("Lave Purple Gang", front.Opponent);
        Assert.Equal("Lave Purple Gang is at 52%, near the 60% at which it can take control by a coup.", front.Reason);
    }

    [Theory]
    [InlineData("War", true)]
    [InlineData("War", false)]
    [InlineData("Election", true)]
    [InlineData("Election", false)]
    public void ACoupThreatIsSkippedWhileTheFactionIsInAConflictThere(string state, bool active)
    {
        var lave = Place(
            "Lave",
            0.40,
            active: active ? [state] : null,
            pending: active ? null : [state],
            others: [new FactionNeighbour("Lave Purple Gang", 0.52) { ActiveStates = active ? [state] : [], PendingStates = active ? [] : [state] }]);

        Assert.Equal([FrontKind.Conflict], Fronts(Report(lave)).Select(front => front.Kind));
    }

    [Fact]
    public void ARivalNearSixtyPercentWhereWeDoNotControlIsNoCoupThreat()
    {
        var lave = Place("Lave", 0.30, controls: false, others: [new FactionNeighbour("Lave Purple Gang", 0.52)]);

        Assert.DoesNotContain(Fronts(Report(lave)), front => front.Kind == FrontKind.Coup);
    }

    [Fact]
    public void ALeadUnderTheIntentsMarginIsAThinMargin()
    {
        var lave = Place("Lave", 0.40, others: [new FactionNeighbour("Lave Purple Gang", 0.35), new FactionNeighbour("Others", 0.25)]);

        var front = Assert.Single(Fronts(Report(lave)));

        Assert.Equal(FrontKind.ThinMargin, front.Kind);
        Assert.Equal(SystemIntent.Hold, front.Intent);
        Assert.Equal("Lave Purple Gang", front.Opponent);
        Assert.Equal(0.05, front.Margin!.Value, 6);
        Assert.Equal("The lead over Lave Purple Gang is 5 points, under the 10 points kept for hold.", front.Reason);
    }

    [Fact]
    public void ALeadOverTheIntentsMarginIsNoFront()
    {
        var lave = Place("Lave", 0.40, others: [new FactionNeighbour("Lave Purple Gang", 0.29)]);

        Assert.Empty(Fronts(Report(lave)));
    }

    [Theory]
    [InlineData(SystemIntent.LeaveAlone)]
    [InlineData(SystemIntent.TakeControl)]
    public void AnIntentThatDefendsNoLeadHasNoThinMargin(SystemIntent intent)
    {
        var lave = Place("Lave", 0.40, others: [new FactionNeighbour("Lave Purple Gang", 0.38)]);

        Assert.Empty(Fronts(Report(lave), campaign: With(("Lave", intent))));
    }

    [Fact]
    public void AConflictedSystemsThinMarginIsSkippedBecauseInfluenceIsFrozen()
    {
        var lave = Place(
            "Lave",
            0.40,
            pending: ["Election"],
            others: [new FactionNeighbour("Lave Purple Gang", 0.38) { PendingStates = ["Election"] }]);

        Assert.Equal([FrontKind.Conflict], Fronts(Report(lave)).Select(front => front.Kind));
    }

    [Fact]
    public void TheLeadIsReadFromOneSourceWhenTheJournalIsNewer()
    {
        var lave = Place("Lave", 0.40, others: [new FactionNeighbour("Lave Purple Gang", 0.10)], reportedAt: Now.AddDays(-2));
        var standings = Standings(Jump("Lave", Now.AddHours(-1), $"{Faction(Us, 0.40)},{Faction("Lave Purple Gang", 0.36)}"));

        var front = Assert.Single(Fronts(Report(lave), standings));

        Assert.Equal(FrontKind.ThinMargin, front.Kind);
        Assert.True(front.OwnReading);
        Assert.Equal(0.04, front.Margin!.Value, 6);
    }

    [Fact]
    public void InfluenceNearSeventyFiveWhereTheIntentIsToStayUnderIsUnwantedExpansion()
    {
        var lave = Place("Lave", 0.72);

        var front = Assert.Single(Fronts(Report(lave), campaign: With(("Lave", SystemIntent.StayUnder75))));

        Assert.Equal(FrontKind.UnwantedExpansion, front.Kind);
        Assert.Equal(0.03, front.Margin!.Value, 6);
    }

    [Fact]
    public void InfluenceNearSeventyFiveUnderHoldIsNoFront() =>
        Assert.Empty(Fronts(Report(Place("Lave", 0.72))));

    [Theory]
    [InlineData("Expansion", FrontKind.Expansion)]
    [InlineData("Retreat", FrontKind.Retreat)]
    public void ExpansionOrRetreatPendingOrActiveIsAFront(string state, FrontKind kind)
    {
        var active = Assert.Single(Fronts(Report(Place("Lave", 0.40, active: [state]))));
        var pending = Assert.Single(Fronts(Report(Place("Lave", 0.40, pending: [state]))));

        Assert.Equal(kind, active.Kind);
        Assert.Equal($"{state} is active here.", active.Reason);
        Assert.Equal(kind, pending.Kind);
        Assert.Equal($"{state} is pending here.", pending.Reason);
    }

    [Theory]
    [InlineData("Civil Unrest")]
    [InlineData("Infrastructure Failure")]
    [InlineData("Pirate Attack")]
    [InlineData("Terrorist Attack")]
    [InlineData("Lockdown")]
    [InlineData("Famine")]
    [InlineData("Blight")]
    [InlineData("Drought")]
    [InlineData("Outbreak")]
    [InlineData("Bust")]
    public void ABadStateFromTheGalaxyReportIsAFront(string state)
    {
        var front = Assert.Single(Fronts(Report(Place("Lave", 0.40, active: [state]))));

        Assert.Equal(FrontKind.BadState, front.Kind);
        Assert.Equal($"{FactionStates.Display(state)} is active.", front.Reason);
    }

    [Fact]
    public void ABadStateFromTheJournalIsAFront()
    {
        var lave = Place("Lave", 0.40, reportedAt: Now.AddDays(-2));
        var standings = Standings(Jump("Lave", Now.AddHours(-1), $"{Faction(Us, 0.40, active: "CivilUnrest", pending: "InfrastructureFailure")},{Faction("Lave Purple Gang", 0.10)}"));

        var front = Assert.Single(Fronts(Report(lave), standings));

        Assert.Equal(FrontKind.BadState, front.Kind);
        Assert.Equal("Civil unrest is active. Infrastructure failure is pending.", front.Reason);
    }

    [Fact]
    public void AGoodStateIsNoFront() =>
        Assert.Empty(Fronts(Report(Place("Lave", 0.40, active: ["Boom", "Civil Liberty"]))));

    [Fact]
    public void AReportOlderThanThirtyDaysOnAControlledSystemIsStale()
    {
        var front = Assert.Single(Fronts(Report(Place("Lave", 0.40, reportedAt: Now.AddDays(-41)))));

        Assert.Equal(FrontKind.Stale, front.Kind);
        Assert.Equal("The newest report of this system is 41 days old.", front.Reason);
    }

    [Fact]
    public void AnOldReportOnASystemLeftAloneIsNotStale() =>
        Assert.Empty(Fronts(Report(Place("Lave", 0.10, controls: false, others: [new FactionNeighbour("Lave Purple Gang", 0.60)], reportedAt: Now.AddDays(-41)))));

    [Fact]
    public void AnOldReportOnASystemWithAnIntentSetIsStale() =>
        Assert.Equal(
            FrontKind.Stale,
            Assert.Single(Fronts(
                Report(Place("Lave", 0.10, controls: false, others: [new FactionNeighbour("Lave Purple Gang", 0.60)], reportedAt: Now.AddDays(-41))),
                campaign: With(("Lave", SystemIntent.TakeControl)))).Kind);

    [Fact]
    public void ANewerJournalReadingMakesAnOldReportCurrent()
    {
        var lave = Place("Lave", 0.40, reportedAt: Now.AddDays(-41));
        var standings = Standings(Jump("Lave", Now.AddDays(-1), $"{Faction(Us, 0.40)},{Faction("Lave Purple Gang", 0.10)}"));

        Assert.Empty(Fronts(Report(lave), standings));
    }

    [Fact]
    public void AReportWithNoTimeIsNotStale() =>
        Assert.Empty(Fronts(Report(Place("Lave", 0.40) with { ReportedAt = null })));

    [Fact]
    public void FrontsAreRankedByKind()
    {
        var report = Report(
            Place("Stale", 0.40, reportedAt: Now.AddDays(-40)),
            Place("Expanding", 0.40, active: ["Expansion"]),
            Place("Unwanted", 0.72),
            Place("Bad", 0.40, active: ["Famine"]),
            Place("Thin", 0.40, others: [new FactionNeighbour("Rival", 0.35)]),
            Place("Retreating", 0.40, pending: ["Retreat"]),
            Place("Coup", 0.40, others: [new FactionNeighbour("Rival", 0.55)]),
            Place("Conflict", 0.40, active: ["War"], others: [new FactionNeighbour("Rival", 0.40) { ActiveStates = ["War"] }]));

        var fronts = Fronts(report, campaign: With(("Unwanted", SystemIntent.StayUnder75)));

        Assert.Equal(
            ["Conflict", "Coup", "Retreating", "Thin", "Bad", "Unwanted", "Expanding", "Stale"],
            fronts.Select(front => front.System));
        Assert.Equal(Enum.GetValues<FrontKind>(), fronts.Select(front => front.Kind));
    }

    [Fact]
    public void ATieIsBrokenByWhatIsAtStakeThenTheMargin()
    {
        var report = Report(
            Place("Wide", 0.40, others: [new FactionNeighbour("Rival", 0.32)]),
            Place("Narrow", 0.40, others: [new FactionNeighbour("Rival", 0.38)]),
            Place("Populous", 0.40, others: [new FactionNeighbour("Rival", 0.33)], population: 9_000_000));

        Assert.Equal(["Populous", "Narrow", "Wide"], Fronts(report).Select(front => front.System));
    }

    [Fact]
    public void AConflictWithAStakeOutranksOneWithout()
    {
        var bare = Place("Bare", 0.40, active: ["War"], others: [new FactionNeighbour("Rival", 0.40) { ActiveStates = ["War"] }], population: 9_000_000);
        var staked = Place("Staked", 0.40, active: ["War"], reportedAt: Now.AddDays(-2));
        var standings = Standings(Jump(
            "Staked",
            Now.AddHours(-1),
            $"{Faction(Us, 0.40, active: "War")},{Faction("Rival", 0.40, active: "War")}",
            $$"""{ "WarType":"war", "Status":"active", "Faction1":{ "Name":"{{Us}}", "Stake":"Hub Port", "WonDays":0 }, "Faction2":{ "Name":"Rival", "Stake":"", "WonDays":0 } }"""));

        Assert.Equal(["Staked", "Bare"], Fronts(Report(bare, staked), standings).Select(front => front.System));
    }
}

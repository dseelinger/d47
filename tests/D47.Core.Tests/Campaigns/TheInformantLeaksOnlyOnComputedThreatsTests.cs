using D47.Core.Campaigns;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;
using static D47.Core.Tests.Campaigns.CampaignReports;

namespace D47.Core.Tests.Campaigns;

/// <summary>
/// The Informant leaks only a threat computed from a rival's rise, names a real system, escalates while the rise goes on, and
/// is invented once.
/// </summary>
public sealed class TheInformantLeaksOnlyOnComputedThreatsTests
{
    private const string Rival = "Lave Purple Gang";

    private const string Squadron = "4401";

    private static FactionNeighbour Neighbour(double influence, string pending = "") =>
        new(Rival, influence) { PendingStates = pending.Length == 0 ? [] : [pending] };

    private static FactionReport At(string system, double rival, bool controls = true, string pending = "") =>
        Report(Place(system, 0.40, controls, others: [Neighbour(rival, pending)]));

    private static LeakView View(FactionReport? report, FactionReport? previous = null, SystemStandings? standings = null, DateTimeOffset? now = null, IReadOnlyList<Front>? fronts = null) =>
        new(Us, Squadron, report, previous, standings ?? SystemStandings.Empty, now ?? Now, fronts ?? []);

    private static Front RivalFront(string system, FrontKind kind = FrontKind.ThinMargin) =>
        new(kind, system, "reason", SystemIntent.Hold, true, Rival, "", 0.05, Now, false);

    [Fact]
    public void ARivalRisingTowardTheCoupLineInAControlledSystemIsLeakedWithThatSystem()
    {
        var step = CampaignLeaks.Step(null, View(At("Lave", 0.53), At("Lave", 0.46)));

        var line = Assert.Single(step.Lines);
        Assert.Equal(ArcAct.Leak, line.Act);
        Assert.Equal("Lave", line.System);
        Assert.Equal(Rival, line.Rival);
        Assert.Equal(step.Informant!.Name, line.Leader);
        Assert.Contains("53%", line.Text);
        Assert.Contains("7 points", line.Text);
        Assert.Single(step.Informant.Leaks);
    }

    [Fact]
    public void NothingIsLeakedWithoutAComputedThreat()
    {
        Assert.Empty(CampaignLeaks.Step(null, View(null)).Lines);
        Assert.Empty(CampaignLeaks.Step(null, View(At("Lave", 0.53))).Lines);
        Assert.Empty(CampaignLeaks.Step(null, View(At("Lave", 0.53), At("Lave", 0.53))).Lines);
        Assert.Empty(CampaignLeaks.Step(null, View(At("Lave", 0.46), At("Lave", 0.40))).Lines);
        Assert.Empty(CampaignLeaks.Step(null, View(At("Lave", 0.53), At("Lave", 0.60))).Lines);
        Assert.Empty(CampaignLeaks.Step(null, View(At("Lave", 0.53, controls: false), At("Lave", 0.46))).Lines);
        Assert.Null(CampaignLeaks.Step(null, View(At("Lave", 0.46), At("Lave", 0.40))).Informant);
    }

    [Fact]
    public void ARisingRivalPastTheCoupLineOrInAConflictIsNotLeaked()
    {
        Assert.Empty(CampaignLeaks.Step(null, View(At("Lave", 0.62), At("Lave", 0.55))).Lines);

        var standings = SystemStandings.Empty.Apply(Jump(
            "Lave",
            Now.AddHours(-1),
            $"{Faction(Us, 0.40, active: "War")},{Faction(Rival, 0.53, active: "War")}",
            $$"""{ "WarType":"war", "Status":"active", "Faction1":{ "Name":"{{Us}}", "Stake":"", "WonDays":0 }, "Faction2":{ "Name":"{{Rival}}", "Stake":"", "WonDays":0 } }"""));

        Assert.Empty(CampaignLeaks.Step(null, View(At("Lave", 0.53), At("Lave", 0.46), standings)).Lines);
    }

    [Fact]
    public void TheTrendComesFromTheSpansOfTheCommandersReadings()
    {
        var standings = SystemStandings.Empty
            .Apply(Jump("Lave", Now.AddDays(-4), $"{Faction(Us, 0.40)},{Faction(Rival, 0.44)}"))
            .Apply(Jump("Lave", Now.AddDays(-2), $"{Faction(Us, 0.40)},{Faction(Rival, 0.49)}"))
            .Apply(Jump("Lave", Now.AddHours(-1), $"{Faction(Us, 0.38)},{Faction(Rival, 0.54)}"));

        var step = CampaignLeaks.Step(null, View(At("Lave", 0.54), null, standings));

        var line = Assert.Single(step.Lines);
        Assert.Contains("54%", line.Text);
        Assert.Contains("10 points", line.Text);
    }

    [Fact]
    public void ARivalExpansionPendingIsLeakedWithItsSystem()
    {
        var step = CampaignLeaks.Step(null, View(At("Leesti", 0.72, pending: "Expansion"), fronts: [RivalFront("Leesti")]));

        var line = Assert.Single(step.Lines);
        Assert.Equal("Leesti", line.System);
        Assert.Contains("Expansion pending in Leesti", line.Text);
        Assert.Contains("72%", line.Text);
    }

    [Fact]
    public void AnExpansionPendingWhereNoFrontNamesTheRivalIsNotAThreat()
    {
        Assert.Empty(CampaignLeaks.Threats(View(At("Leesti", 0.07, pending: "Expansion"))));
        Assert.Empty(CampaignLeaks.Threats(View(At("Leesti", 0.07, pending: "Expansion"), fronts: [RivalFront("Lave")])));
    }

    [Fact]
    public void AnExpansionPendingWhereAThinMarginFrontNamesTheRivalIsOneThreat()
    {
        var threat = Assert.Single(CampaignLeaks.Threats(View(At("Leesti", 0.07, pending: "Expansion"), fronts: [RivalFront("Leesti")])));

        Assert.Equal(LeakKind.Expansion, threat.Kind);
    }

    [Fact]
    public void ARivalGainingInSeveralSystemsIsLeakedWithTheStrongestOfThem()
    {
        FactionReport Three(double lave, double leesti, double diso) => Report(
            Place("Lave", 0.40, controls: false, others: [Neighbour(lave)]),
            Place("Leesti", 0.40, controls: false, others: [Neighbour(leesti)]),
            Place("Diso", 0.40, controls: false, others: [Neighbour(diso)]));

        var step = CampaignLeaks.Step(null, View(Three(0.30, 0.38, 0.25), Three(0.25, 0.30, 0.20)));

        var line = Assert.Single(step.Lines);
        Assert.Equal("Leesti", line.System);
        Assert.Contains("gaining in 3", line.Text);
        Assert.Contains("Leesti, Lave, Diso", line.Text);

        Assert.Empty(CampaignLeaks.Step(null, View(Three(0.30, 0.38, 0.20), Three(0.25, 0.30, 0.20))).Lines);
    }

    [Fact]
    public void ALaterLeakEscalatesWhileTheRiseContinuesAndNotBefore()
    {
        var first = CampaignLeaks.Step(null, View(At("Lave", 0.50), At("Lave", 0.45)));
        Assert.Equal(ArcAct.Leak, Assert.Single(first.Lines).Act);

        var same = CampaignLeaks.Step(first.Informant, View(At("Lave", 0.53), At("Lave", 0.50)));
        Assert.Empty(same.Lines);

        var second = CampaignLeaks.Step(first.Informant, View(At("Lave", 0.56), At("Lave", 0.52)));
        var line = Assert.Single(second.Lines);
        Assert.Equal(ArcAct.Escalation, line.Act);
        Assert.Contains("An earlier leak put " + Rival + " at 50%", line.Text);
        Assert.Equal(2, Assert.Single(second.Informant!.Leaks).Level);
        Assert.Equal(first.Informant!.Name, second.Informant.Name);
    }

    [Fact]
    public void AThreatThatPassesIsForgottenSoItsReturnIsANewLeak()
    {
        var first = CampaignLeaks.Step(null, View(At("Lave", 0.50), At("Lave", 0.45)));

        var gone = CampaignLeaks.Step(first.Informant, View(At("Lave", 0.40), At("Lave", 0.45)));
        Assert.Empty(gone.Lines);
        Assert.Empty(gone.Informant!.Leaks);

        var back = CampaignLeaks.Step(gone.Informant, View(At("Lave", 0.50), At("Lave", 0.45)));
        Assert.Equal(ArcAct.Leak, Assert.Single(back.Lines).Act);
    }

    [Fact]
    public void TheInformantIsInventedOnceAndIsTheSameForASquadron()
    {
        Assert.Equal(CampaignLeaks.NameFor(Squadron), CampaignLeaks.NameFor(Squadron));

        var first = CampaignLeaks.Step(null, View(At("Lave", 0.50), At("Lave", 0.45), now: Now));
        var later = CampaignLeaks.Step(first.Informant, View(At("Leesti", 0.50), At("Leesti", 0.45), now: Now.AddDays(2)));

        Assert.Equal(first.Informant!.Name, later.Informant!.Name);
        Assert.Equal(Now, later.Informant.Since);
    }

    [Fact]
    public void TheBriefForbidsInventedFactsAndSpeaksAsAnInformant()
    {
        var line = Assert.Single(CampaignLeaks.Step(null, View(At("Lave", 0.50), At("Lave", 0.45))).Lines);

        var brief = CampaignArcs.Brief(line);

        Assert.Contains("informant", brief.Speaker);
        Assert.Contains(CampaignArcs.FictionRule, brief.Instruction);
        Assert.Contains(line.Text, brief.Instruction);
    }
}

using D47.Core.Campaigns;
using D47.Core.Knowledge;
using Xunit;
using static D47.Core.Tests.Campaigns.CampaignReports;

namespace D47.Core.Tests.Campaigns;

/// <summary>
/// A crisis opens when a bad state lands in a controlled system the previous report held without it, blames the strongest
/// rival there, is exploited once when that rival gains, and closes when the state ends.
/// </summary>
public sealed class TheCrisisArcOpensOnABadStateAndClosesWithItTests
{
    private const string Rival = "Lave Purple Gang";

    private const string Other = "Diso Ascendancy";

    private const string Squadron = "4401";

    private static FactionNeighbour[] Neighbours(double rival = 0.30, double other = 0.10) =>
        [new FactionNeighbour(Other, other), new FactionNeighbour(Rival, rival)];

    private static CrisisView View(FactionReport? report, FactionReport? previous) => new(Us, Squadron, report, previous, Now);

    private static OpenCrisis Open(bool exploited = false, string? blamed = Rival) => new("Lave", "Famine", blamed, Now.AddDays(-1), exploited);

    [Fact]
    public void ABadStateLandingInAControlledSystemOpensACrisisAndBlamesTheStrongestRival()
    {
        var before = Report(Place("Lave", 0.40, others: Neighbours()));
        var now = Report(Place("Lave", 0.40, active: ["Famine"], others: Neighbours()));

        var step = CampaignCrisis.Step([], View(now, before));

        var crisis = Assert.Single(step.Crises);
        var line = Assert.Single(step.Lines);

        Assert.Equal(("Lave", "Famine", Rival), (crisis.System, crisis.State, crisis.Blamed));
        Assert.Equal(ArcAct.Crisis, line.Act);
        Assert.Equal(CampaignLiaison.Speaker, line.Leader);
        Assert.Contains($"{Rival} is the strongest rival in Lave and is blamed.", line.Facts);
    }

    [Fact]
    public void WithNoPreviousReportWhatIsAlreadyThereIsTheBaseline()
    {
        var now = Report(Place("Lave", 0.40, active: ["Famine"], others: Neighbours()));

        var step = CampaignCrisis.Step([], View(now, null));

        Assert.Empty(step.Crises);
        Assert.Empty(step.Lines);
    }

    [Fact]
    public void AStateThePreviousReportAlreadyHeldOpensNothing()
    {
        var report = Report(Place("Lave", 0.40, active: ["Famine"], others: Neighbours()));

        Assert.Empty(CampaignCrisis.Step([], View(report, report)).Crises);
    }

    [Fact]
    public void ASystemTheFactionDoesNotControlOpensNothing()
    {
        var before = Report(Place("Lave", 0.20, controls: false, others: Neighbours()));
        var now = Report(Place("Lave", 0.20, controls: false, active: ["Famine"], others: Neighbours()));

        Assert.Empty(CampaignCrisis.Step([], View(now, before)).Lines);
    }

    [Fact]
    public void AStateThatIsNotBadOpensNothing()
    {
        var before = Report(Place("Lave", 0.40, others: Neighbours()));
        var now = Report(Place("Lave", 0.40, active: ["Boom"], others: Neighbours()));

        Assert.Empty(CampaignCrisis.Step([], View(now, before)).Lines);
    }

    [Fact]
    public void TheBlamedRivalExploitsTheCrisisOnceWhenItGains()
    {
        var before = Report(Place("Lave", 0.40, active: ["Famine"], others: Neighbours(rival: 0.30)));
        var now = Report(Place("Lave", 0.38, active: ["Famine"], others: Neighbours(rival: 0.33)));

        var first = CampaignCrisis.Step([Open()], View(now, before));
        var line = Assert.Single(first.Lines);

        Assert.Equal(ArcAct.Exploit, line.Act);
        Assert.Equal(Rival, line.Rival);
        Assert.Equal(CampaignArcs.Leader(Rival, Squadron), line.Leader);
        Assert.True(Assert.Single(first.Crises).Exploited);

        var later = Report(Place("Lave", 0.36, active: ["Famine"], others: Neighbours(rival: 0.36)));

        Assert.Empty(CampaignCrisis.Step(first.Crises, View(later, now)).Lines);
    }

    [Fact]
    public void ARivalThatDoesNotGainSaysNothing()
    {
        var before = Report(Place("Lave", 0.40, active: ["Famine"], others: Neighbours(rival: 0.30)));
        var now = Report(Place("Lave", 0.42, active: ["Famine"], others: Neighbours(rival: 0.28)));

        var step = CampaignCrisis.Step([Open()], View(now, before));

        Assert.Empty(step.Lines);
        Assert.False(Assert.Single(step.Crises).Exploited);
    }

    [Fact]
    public void TheCrisisClosesWhenTheStateEnds()
    {
        var before = Report(Place("Lave", 0.40, active: ["Famine"], others: Neighbours()));
        var now = Report(Place("Lave", 0.40, others: Neighbours()));

        var step = CampaignCrisis.Step([Open()], View(now, before));

        Assert.Empty(step.Crises);
        Assert.Equal(ArcAct.Relief, Assert.Single(step.Lines).Act);
    }

    [Fact]
    public void ACrisisWhoseSystemLeftTheReportIsDroppedWithoutAWord()
    {
        var now = Report(Place("Diso", 0.40, others: Neighbours()));

        var step = CampaignCrisis.Step([Open()], View(now, now));

        Assert.Empty(step.Crises);
        Assert.Empty(step.Lines);
    }

    [Fact]
    public void ACrisisWithNoRivalInTheSystemBlamesNobody()
    {
        var before = Report(Place("Lave", 0.40, others: []));
        var now = Report(Place("Lave", 0.40, active: ["Famine"], others: []));

        var step = CampaignCrisis.Step([], View(now, before));

        Assert.Null(Assert.Single(step.Crises).Blamed);
        Assert.DoesNotContain(Assert.Single(step.Lines).Facts, fact => fact.Contains("blamed", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(ArcAct.Crisis, "Liaison")]
    [InlineData(ArcAct.Relief, "Liaison")]
    [InlineData(ArcAct.Exploit, "who leads")]
    [InlineData(ArcAct.Legend, "Chronicler")]
    public void EveryNewActHasItsOwnSpeakerAndInstruction(ArcAct act, string speaker)
    {
        var line = new CampaignArcLine(act, Us, Rival, "Someone", "Lave", ["A fact."]);
        var brief = CampaignArcs.Brief(line);

        Assert.Contains(speaker, brief.Speaker, StringComparison.Ordinal);
        Assert.DoesNotContain("Vow revenge", brief.Instruction, StringComparison.Ordinal);
    }
}

using D47.Core.Campaigns;
using D47.Core.Knowledge;
using Xunit;
using static D47.Core.Tests.Campaigns.CampaignReports;

namespace D47.Core.Tests.Campaigns;

/// <summary>
/// The Ideology arc opens only when the faction's government differs from that of a rival leading a front against it, and
/// then acts on a new front, a front won and an election, each once.
/// </summary>
public sealed class TheIdeologyArcOpensOnDifferingGovernmentsTests
{
    private const string Rival = "Lave Purple Gang";

    private const string Squadron = "4401";

    private static FactionNeighbour Neighbour(string? government, string active = "") =>
        new(Rival, 0.30) { Government = government, ActiveStates = active.Length == 0 ? [] : [active] };

    private static FactionReport At(string? government, params string[] systems) =>
        Report([.. systems.Select(system => Place(system, 0.40, others: [Neighbour(government)]))]);

    private static Front Coup(string system = "Lave") =>
        new(FrontKind.Coup, system, "Rival is near a coup.", SystemIntent.Hold, true, Rival, "", 0.08, Now, false) { Lead = 0.05 };

    private static ArcView View(FactionReport? report, IReadOnlyList<Front> fronts, IReadOnlyList<ChronicleEntry>? chronicle = null) =>
        new(Us, Squadron, report, fronts, chronicle ?? [], Now);

    [Fact]
    public void ItOpensWhenTheRivalLeadingAFrontHasAnotherGovernment()
    {
        var step = CampaignIdeology.Step(null, View(At("Dictatorship", "Lave"), [Coup()]));

        var line = Assert.Single(step.Lines);
        Assert.Equal(ArcAct.Propaganda, line.Act);
        Assert.Equal(CampaignArcs.Leader(Rival, Squadron), line.Leader);
        Assert.Equal("Lave", line.System);
        Assert.Contains("Cooperative government", line.Text);
        Assert.Contains("Dictatorship government", line.Text);
        Assert.Equal(("Cooperative", "Dictatorship"), (step.Arc!.OurGovernment, step.Arc.TheirGovernment));
    }

    [Fact]
    public void ItDoesNotOpenOnTheSameUnknownOrMissingGovernment()
    {
        Assert.Null(CampaignIdeology.Step(null, View(At("Cooperative", "Lave"), [Coup()])).Arc);
        Assert.Null(CampaignIdeology.Step(null, View(At("cooperative", "Lave"), [Coup()])).Arc);
        Assert.Null(CampaignIdeology.Step(null, View(At(null, "Lave"), [Coup()])).Arc);
        Assert.Null(CampaignIdeology.Step(null, View(null, [Coup()])).Arc);
        Assert.Null(CampaignIdeology.Step(null, View(At("Dictatorship", "Lave"), [])).Arc);
        Assert.Null(CampaignIdeology.Step(null, View(Report(Place("Lave", 0.40, others: [Neighbour("Dictatorship")]) with { Name = "Lave" }) with { Government = null }, [Coup()])).Arc);
    }

    [Fact]
    public void AFrontInANewSystemIsBroadcastOnceAndTheOpeningFrontsAreTheBaseline()
    {
        var report = At("Dictatorship", "Lave", "Leesti");
        var opened = CampaignIdeology.Step(null, View(report, [Coup("Lave")])).Arc;

        Assert.Empty(CampaignIdeology.Step(opened, View(report, [Coup("Lave")])).Lines);

        var next = CampaignIdeology.Step(opened, View(report, [Coup("Lave"), Coup("Leesti")]));
        var line = Assert.Single(next.Lines);
        Assert.Equal(ArcAct.Propaganda, line.Act);
        Assert.Equal("Leesti", line.System);

        Assert.Empty(CampaignIdeology.Step(next.Arc, View(report, [Coup("Lave"), Coup("Leesti")])).Lines);
    }

    [Fact]
    public void AFrontWonAgainstTheRivalDrawsADefectionAndAnotherFactionDoesNot()
    {
        var report = At("Dictatorship", "Lave");
        var opened = CampaignIdeology.Step(null, View(report, [Coup()])).Arc;

        Entry("Won", Rival, out var won);
        Entry("Won", "Diso Ascendancy", out var other);
        Entry("Lost", Rival, out var lost);

        Assert.Empty(CampaignIdeology.Step(opened, View(report, [Coup()], [other, lost])).Lines);

        var line = Assert.Single(CampaignIdeology.Step(opened, View(report, [Coup()], [other, lost, won])).Lines);
        Assert.Equal(ArcAct.Defection, line.Act);
        Assert.Equal(CampaignIdeology.Defector(Rival), line.Leader);
        Assert.Contains("won the conflict in Lave against " + Rival, line.Text);
    }

    [Fact]
    public void AnElectionWithBothPresentIsFramedOncePerStateAndSpokenByTheLiaison()
    {
        var quiet = At("Dictatorship", "Lave");
        var opened = CampaignIdeology.Step(null, View(quiet, [Coup()])).Arc;
        var election = Report(Place("Lave", 0.40, active: ["Election"], others: [Neighbour("Dictatorship")]));

        var first = CampaignIdeology.Step(opened, View(election, [Coup()]));
        var line = Assert.Single(first.Lines);
        Assert.Equal(ArcAct.Election, line.Act);
        Assert.Equal(CampaignLiaison.Speaker, line.Leader);
        Assert.Contains("An election is active in Lave", line.Text);

        Assert.Empty(CampaignIdeology.Step(first.Arc, View(election, [Coup()])).Lines);

        var alone = Report(Place("Leesti", 0.40, active: ["Election"], others: [new FactionNeighbour("Diso Ascendancy", 0.2) { Government = "Dictatorship" }]));
        Assert.Empty(CampaignIdeology.Step(opened, View(alone, [Coup()])).Lines);
    }

    [Fact]
    public void ItSaysNothingWhileTheGovernmentsAgree()
    {
        var opened = CampaignIdeology.Step(null, View(At("Dictatorship", "Lave"), [Coup()])).Arc;

        Assert.Empty(CampaignIdeology.Step(opened, View(At("Cooperative", "Lave", "Leesti"), [Coup("Leesti")])).Lines);
    }

    private static void Entry(string outcome, string opponent, out ChronicleEntry entry) =>
        entry = new ChronicleEntry("Conflict", "Lave", opponent, outcome, [Now.AddDays(-3)], []);
}

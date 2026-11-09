using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Campaigns;
using D47.Core.Configuration;
using D47.Core.Knowledge;
using D47.Core.Storage;
using D47.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Campaigns.CampaignReports;

namespace D47.Core.Tests.Campaigns;

/// <summary>
/// The Nemesis is the rival fought most, then the one sharing the most systems, then the closest; its leader's name is
/// drawn from the rival and the squadron; and each act follows a front or chronicle change, never nothing.
/// </summary>
public sealed class TheNemesisArcActsOnlyOnRealTriggersTests
{
    private const string Rival = "Lave Purple Gang";

    private const string Other = "Diso Ascendancy";

    private const string Squadron = "4401";

    private static Front Coup(string system = "Lave", string opponent = Rival, double lead = 0.05) =>
        new(FrontKind.Coup, system, $"{opponent} is at 52%, near the 60% at which it can take control by a coup.", SystemIntent.Hold, true, opponent, "", 0.08, Now, false)
        {
            Lead = lead,
        };

    private static Front Thin(string system = "Lave", string opponent = Rival, double lead = 0.04) =>
        new(FrontKind.ThinMargin, system, $"The lead over {opponent} is 4 points, under the 10 points kept for hold.", SystemIntent.Hold, true, opponent, "", lead, Now, false)
        {
            Lead = lead,
        };

    private static Front War(int ours, int theirs, string system = "Lave", string opponent = Rival) =>
        new(FrontKind.Conflict, system, $"War against {opponent}, active.", SystemIntent.Hold, true, opponent, "", ours - theirs, Now, false)
        {
            Conflict = new FactionConflict(system, "War", true, OpponentKind.Named, opponent, [], true, ours, theirs, "", ""),
        };

    private static Front Retreat(string system = "Lave") =>
        new(FrontKind.Retreat, system, "Retreat is active here.", SystemIntent.Hold, true, null, "", 0.2, Now, false);

    private static ChronicleEntry Entry(string outcome, string opponent = Rival, string system = "Lave") =>
        new("Conflict", system, opponent, outcome, [Now.AddDays(-3)], []);

    private static ArcView View(IReadOnlyList<Front> fronts, IReadOnlyList<ChronicleEntry>? chronicle = null, FactionReport? report = null) =>
        new(Us, Squadron, report, fronts, chronicle ?? [], Now);

    private static CampaignArc Arc(int answered = 0) => new(ArcKind.Nemesis, Rival, CampaignArcs.Leader(Rival, Squadron), Now.AddDays(-5), answered);

    [Fact]
    public void TheNemesisIsTheRivalFoughtMost()
    {
        var report = Report(
            Place("Lave", 0.40, others: [new FactionNeighbour(Other, 0.35), new FactionNeighbour(Rival, 0.10)]),
            Place("Diso", 0.40, others: [new FactionNeighbour(Other, 0.30)]));

        Assert.Equal(Rival, CampaignArcs.Rivals(Us, report, [], [Entry("Won"), Entry("Lost", system: "Diso")])[0].Name);
    }

    [Fact]
    public void WithNoFightsTheNemesisSharesTheMostSystems()
    {
        var report = Report(
            Place("Lave", 0.40, others: [new FactionNeighbour(Other, 0.20), new FactionNeighbour(Rival, 0.38)]),
            Place("Diso", 0.40, others: [new FactionNeighbour(Other, 0.30)]));

        var ranked = CampaignArcs.Rivals(Us, report, [], []);

        Assert.Equal([Other, Rival], ranked.Select(rival => rival.Name));
        Assert.Equal(2, ranked[0].Shared);
    }

    [Fact]
    public void WithSystemsSharedEquallyTheNemesisIsTheClosestRival()
    {
        var report = Report(Place("Lave", 0.40, others: [new FactionNeighbour(Other, 0.20), new FactionNeighbour(Rival, 0.38)]));

        Assert.Equal(Rival, CampaignArcs.Rivals(Us, report, [], [])[0].Name);
    }

    [Fact]
    public void TheCampaignFactionIsNeverItsOwnNemesis()
    {
        var report = Report(Place("Lave", 0.40, others: [new FactionNeighbour(Us.ToUpperInvariant(), 0.39)]));

        Assert.Empty(CampaignArcs.Rivals(Us, report, [], []));
    }

    [Fact]
    public void TheLeadersNameIsDrawnFromTheRivalAndTheSquadronTheSameOnEveryPc()
    {
        Assert.Equal("Prefect Mbeki", CampaignArcs.Leader(Rival, Squadron));
        Assert.Equal(CampaignArcs.Leader(Rival, Squadron), CampaignArcs.Leader(Rival.ToUpperInvariant(), Squadron));
        Assert.NotEqual(CampaignArcs.Leader(Rival, Squadron), CampaignArcs.Leader(Other, Squadron));
    }

    [Fact]
    public void SquadronMembersSeedFromTheSquadronAndOthersFromTheCampaignFaction()
    {
        Assert.Equal(Squadron, CampaignArcs.SeedKey(Squadron, Us));
        Assert.Equal(Us, CampaignArcs.SeedKey(null, Us));
    }

    [Fact]
    public void NoArcBeginsUntilTheNemesisLeadsAFront()
    {
        var report = Report(Place("Lave", 0.40, others: [new FactionNeighbour(Rival, 0.38)]));

        var step = CampaignArcs.Step(null, [], View([Retreat()], report: report));

        Assert.Null(step.Arc);
        Assert.Empty(step.Lines);
    }

    [Fact]
    public void TheNemesisIsTheFirstRankedRivalThatLeadsAFront()
    {
        var report = Report(
            Place("Lave", 0.40, others: [new FactionNeighbour(Other, 0.03), new FactionNeighbour(Rival, 0.52)]),
            Place("Diso", 0.60, others: [new FactionNeighbour(Other, 0.03)]),
            Place("Leesti", 0.60, others: [new FactionNeighbour(Other, 0.03)]));

        Assert.Equal(Other, CampaignArcs.Rivals(Us, report, [Coup()], [])[0].Name);

        var step = CampaignArcs.Step(null, null, View([Coup()], report: report));

        Assert.Equal(Rival, step.Arc!.Rival);
        Assert.Equal(ArcAct.Introduction, Assert.Single(step.Lines).Act);
    }

    [Fact]
    public void TheNemesisIsIntroducedOnceWhenItFirstLeadsAFront()
    {
        var report = Report(Place("Lave", 0.40, others: [new FactionNeighbour(Rival, 0.52)]));
        var fronts = new[] { Retreat(), Coup() };

        var first = CampaignArcs.Step(null, null, View(fronts, report: report));

        var line = Assert.Single(first.Lines);
        Assert.Equal(ArcAct.Introduction, line.Act);
        Assert.Equal(new CampaignArc(ArcKind.Nemesis, Rival, "Prefect Mbeki", Now, 0), first.Arc);
        Assert.Equal(
            $"Prefect Mbeki leads {Rival}. {Rival} opposes {Us} in the coup threat in Lave. {fronts[1].Reason}",
            line.Text);

        var again = CampaignArcs.Step(first.Arc, fronts, View(fronts, report: report));

        Assert.Equal(first.Arc, again.Arc);
        Assert.Empty(again.Lines);
    }

    [Fact]
    public void AnIntroductionInASystemContestedBeforeCountsTheGrudge()
    {
        var chronicle = new[] { Entry("Won"), Entry("Lost") };

        var step = CampaignArcs.Step(null, null, View([War(0, 0)], chronicle));

        Assert.Contains($"This is the third time {Rival} and {Us} have contested Lave.", Assert.Single(step.Lines).Text, StringComparison.Ordinal);
        Assert.Equal(2, step.Arc!.Answered);
    }

    [Fact]
    public void ADayWonByTheNemesisIsBroadcast()
    {
        var step = CampaignArcs.Step(Arc(), [War(1, 1)], View([War(1, 2)]));

        var line = Assert.Single(step.Lines);
        Assert.Equal(ArcAct.Broadcast, line.Act);
        Assert.Equal($"{Rival} has won a day in the war in Lave. Days won stand at 2 for {Rival} and 1 for {Us}.", line.Text);
    }

    [Fact]
    public void TheNemesisTakingTheLeadIsBroadcastOnce()
    {
        var step = CampaignArcs.Step(Arc(), [Thin(lead: 0.03)], View([Thin(lead: -0.02)]));

        Assert.Equal($"{Rival} is now ahead of {Us} in Lave, by 2 points.", Assert.Single(step.Lines).Text);

        Assert.Empty(CampaignArcs.Step(Arc(), [Thin(lead: -0.02)], View([Coup(lead: -0.04)])).Lines);
    }

    [Fact]
    public void NothingIsBroadcastOnAFirstLookOrWhenNothingMoved()
    {
        Assert.Empty(CampaignArcs.Step(Arc(), null, View([War(1, 3), Thin("Diso", lead: -0.05)])).Lines);
        Assert.Empty(CampaignArcs.Step(Arc(), [War(1, 3)], View([War(1, 3)])).Lines);
        Assert.Empty(CampaignArcs.Step(Arc(), [War(1, 3)], View([War(2, 3)])).Lines);
        Assert.Empty(CampaignArcs.Step(Arc(), [Thin(lead: 0.05)], View([Thin(lead: 0.01)])).Lines);
    }

    [Fact]
    public void AnotherRivalsGainIsNotTheNemesissBroadcast()
    {
        Assert.Empty(CampaignArcs.Step(Arc(), [War(1, 1, opponent: Other)], View([War(1, 2, opponent: Other)])).Lines);
    }

    [Fact]
    public void ADefeatIsConcededTheFirstTimeInASystemAndAvengedAfter()
    {
        var conceded = Assert.Single(CampaignArcs.Step(Arc(), [], View([], [Entry("Won")])).Lines);

        Assert.Equal(ArcAct.Concession, conceded.Act);
        Assert.Equal($"{Us} won the conflict in Lave against {Rival}.", conceded.Text);

        var avenged = Assert.Single(CampaignArcs.Step(Arc(answered: 1), [], View([], [Entry("Lost"), Entry("Won")])).Lines);

        Assert.Equal(ArcAct.Revenge, avenged.Act);
        Assert.Equal($"{Us} won the conflict in Lave against {Rival}. This is the second time {Rival} and {Us} have contested Lave.", avenged.Text);
    }

    [Fact]
    public void EachChronicleEntryIsAnsweredOnce()
    {
        var first = CampaignArcs.Step(Arc(), [], View([], [Entry("Won")]));

        Assert.Equal(1, first.Arc!.Answered);
        Assert.Empty(CampaignArcs.Step(first.Arc, [], View([], [Entry("Won")])).Lines);
    }

    [Fact]
    public void ANemesisWinOrAnotherRivalsLossIsNotConceded()
    {
        Assert.Empty(CampaignArcs.Step(Arc(), [], View([], [Entry("Lost"), Entry("Won", opponent: Other), Entry("Ended")])).Lines);
    }

    [Fact]
    public void TheBriefLetsTheModelInventFictionButNoGameFact()
    {
        var line = Assert.Single(CampaignArcs.Step(Arc(), [War(1, 1)], View([War(1, 2)])).Lines);
        var brief = CampaignArcs.Brief(line);

        Assert.False(brief.NeedsPersona);
        Assert.False(brief.NeedsGameState);
        Assert.Contains($"You are Prefect Mbeki, who leads {Rival}", brief.Speaker, StringComparison.Ordinal);
        Assert.Contains(line.Text, brief.Instruction, StringComparison.Ordinal);
        Assert.Contains(CampaignArcs.FictionRule, brief.Instruction, StringComparison.Ordinal);
    }

    [Fact]
    public void AnArcLineIsAlwaysWrittenFromItsOwnBrief()
    {
        var line = Assert.Single(CampaignArcs.Step(Arc(), [], View([], [Entry("Won")])).Lines);
        var said = new Announcement(CampaignArcCallout.KeyPrefix + "concession", line.Text) { Arc = line };

        Assert.Equal(CampaignArcs.Brief(line), FlavourBriefs.For(said, personalityEnabled: true));
        Assert.True(new RewordChance(new Random(1)).ShouldReword(said, 0));
    }

    [Fact]
    public void TheArcIsKeptAcrossRestartsAndClearedWithANewFaction()
    {
        var files = new MemoryFileSystem();
        var store = new CampaignStore("campaigns.json", files, NullLogger<CampaignStore>.Instance);

        store.SetFaction("F1", Us, clearIntents: false);
        Assert.True(store.SetArc("F1", Arc(answered: 2)));

        var reloaded = new CampaignStore("campaigns.json", files, NullLogger<CampaignStore>.Instance);
        reloaded.Load();

        Assert.Equal(Arc(answered: 2), reloaded.For("F1").Arc);

        reloaded.SetFaction("F1", Us, clearIntents: false);
        Assert.NotNull(reloaded.For("F1").Arc);

        reloaded.SetFaction("F1", Other, clearIntents: true);
        Assert.Null(reloaded.For("F1").Arc);
    }

    [Fact]
    public void AnArcAloneKeepsTheCommandersRecord()
    {
        var files = new MemoryFileSystem();
        var store = new CampaignStore("campaigns.json", files, NullLogger<CampaignStore>.Instance);

        store.SetArc("F1", Arc());

        var reloaded = new CampaignStore("campaigns.json", files, NullLogger<CampaignStore>.Instance);
        reloaded.Load();

        Assert.Equal(Arc(), reloaded.For("F1").Arc);
    }

    [Fact]
    public void TheVoiceIsPickedByTheRivalsNameAndIsNeverTheLiaisons()
    {
        var speech = D47Settings.Defaults.Speech with { Provider = "edge", VoicesProvider = "edge" };
        string[] pool = ["en-US-AriaNeural", "en-GB-RyanNeural", "en-AU-NatashaNeural", "en-IE-ConnorNeural"];

        var voice = CampaignArcs.Voice(Rival, Us, speech, _ => new VoiceCast { Pool = pool });

        Assert.NotNull(voice);
        Assert.Equal(CampaignArcs.Link, voice.Link);
        Assert.Equal(voice, CampaignArcs.Voice(Rival, Us, speech, _ => new VoiceCast { Pool = pool }));

        var clash = speech with { LiaisonVoice = voice.VoiceId };

        Assert.NotEqual(voice.VoiceId, CampaignArcs.Voice(Rival, Us, clash, _ => new VoiceCast { Pool = pool })!.VoiceId);
    }
}

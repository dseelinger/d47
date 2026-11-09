using D47.Core.Audio;
using D47.Core.Campaigns;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;
using static D47.Core.Tests.Campaigns.CampaignReports;

namespace D47.Core.Tests.Campaigns;

/// <summary>Each Liaison beat's facts come from code, and its brief holds the model to them.</summary>
public sealed class TheLiaisonStatesOnlyTheFactsCodeGaveItTests
{
    private static readonly DateTimeOffset Tick = Now;

    private const string WarConflict =
        """{ "WarType":"war", "Status":"active", "Faction1":{ "Name":"Kestrel Reach Compact", "Stake":"Lave Station", "WonDays":2 }, "Faction2":{ "Name":"Lave Purple Gang", "Stake":"", "WonDays":1 } }""";

    private static SystemStandings Standings(params JournalEvent[] events) =>
        events.Aggregate(SystemStandings.Empty, (standings, journalEvent) => standings.Apply(journalEvent));

    private static Front Only(FactionSystem system, SystemStandings? standings = null) =>
        Assert.Single(CampaignFronts.Of(Report(system), standings ?? SystemStandings.Empty, Campaign.None, Now));

    private static Front Retreat() => Only(Place("Lave", 0.30, active: ["Retreat"]));

    private static Front War(SystemStandings? standings = null) =>
        Only(Place("Lave", 0.40, active: ["War"], reportedAt: Now.AddDays(-2), others: [new FactionNeighbour("Lave Purple Gang", 0.40) { ActiveStates = ["War"] }]), standings);

    private static SystemStandings WarSeen(DateTimeOffset at) => Standings(Jump(
        "Lave",
        at,
        $"{Faction(Us, 0.40, active: "War")},{Faction("Lave Purple Gang", 0.40, active: "War")}",
        WarConflict));

    private static BgsWork WorkInLave(params BgsEntry[] entries) =>
        BgsWork.None.Apply(Jump("Lave", Now.AddDays(-1), Faction(Us, 0.4))) with { Entries = entries };

    private static CampaignNextAction Action(Front front) =>
        CampaignNextAction.For(front, Us, CombatRecord.None, BgsWork.None, Now);

    private static IEnumerable<CampaignLiaisonLine> EveryBeat()
    {
        var retreat = Retreat();
        var war = War(WarSeen(Now.AddHours(1)));

        yield return CampaignLiaison.Briefing(retreat, Action(retreat), Us, "Cooperative");
        yield return CampaignLiaison.Dispatch(retreat, Us, WorkInLave(), SystemStandings.Empty, Tick, Now.AddHours(2), "Cooperative");
        yield return CampaignLiaison.MorningReport(retreat, Us, "Cooperative");
        yield return CampaignLiaison.HandIn(retreat, [new BgsEntry(Now, 1, Us, BgsWorkKind.Mission, 2)], Us, "Cooperative");
        yield return CampaignLiaison.FirstDayWon(war, Us, "Cooperative");
        yield return CampaignLiaison.MarginRestored(retreat, Us, "Cooperative");
        yield return CampaignLiaison.Resolution(new ChronicleEntry("Retreat", "Lave", null, "Resolved", [], []), retreat, null, Us, "Cooperative");
    }

    [Fact]
    public void EveryBeatsBriefCarriesItsFactsAndForbidsAnyOthers()
    {
        foreach (var line in EveryBeat())
        {
            var brief = CampaignLiaison.Brief(line);

            Assert.Contains(line.Text, brief.Instruction, StringComparison.Ordinal);
            Assert.Contains("Use these facts and no others", brief.Instruction, StringComparison.Ordinal);
            Assert.False(brief.NeedsPersona);
            Assert.False(brief.NeedsGameState);
            Assert.Contains(Us, brief.Speaker, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ABriefingGivesTheSituationTheMarginTheStakeAndTheRemedy()
    {
        var front = Retreat();

        var line = CampaignLiaison.Briefing(front, Action(front), Us, "Democracy");

        Assert.Equal(
            [
                "The front is the retreat in Lave.",
                "Retreat is active here.",
                "Kestrel Reach Compact's lead over the nearest rival there is 20 points.",
                "Kestrel Reach Compact controls Lave.",
                "Run missions and trade for Kestrel Reach Compact in Lave to lift its influence clear of retreat.",
            ],
            line.Facts);
        Assert.Equal("liaison officer", line.Part);
        Assert.Contains("voters", line.Manner, StringComparison.Ordinal);
    }

    [Fact]
    public void AWarBriefingSaysWhatDoesNotCountWithoutItsGuideReferenceAndGivesNoInfluence()
    {
        var front = War(WarSeen(Now.AddHours(-3)));

        var line = CampaignLiaison.Briefing(front, Action(front), Us, "Cooperative");
        var brief = CampaignLiaison.Brief(line);

        Assert.Contains("Trade does not count in a war.", line.Facts);
        Assert.Contains("Days won 2 to 1", line.Text, StringComparison.Ordinal);
        Assert.Equal("field officer", line.Part);
        Assert.Contains(CampaignLiaison.DaysWonRule, brief.Instruction, StringComparison.Ordinal);
        NoInfluence(line.Text);
        NoInfluence(brief.Instruction);
    }

    [Fact]
    public void ADispatchGivesTheWorkAndWhatMovedAsTwoFactsAndClaimsNoShare()
    {
        var front = Retreat();
        var standings = Standings(
            Jump("Lave", Tick.AddHours(-3), $"{Faction(Us, 0.412)},{Faction("Lave Purple Gang", 0.2)}"),
            Jump("Lave", Tick.AddHours(1), $"{Faction(Us, 0.43)},{Faction("Lave Purple Gang", 0.2)}"));
        var work = WorkInLave(
            new BgsEntry(Tick.AddHours(-2), 1, Us, BgsWorkKind.Mission, 3),
            new BgsEntry(Tick.AddMinutes(30), 1, Us, BgsWorkKind.Mission, 2),
            new BgsEntry(Tick.AddMinutes(40), 1, Us, BgsWorkKind.Trade, 120_000),
            new BgsEntry(Tick.AddMinutes(50), 1, "Lave Purple Gang", BgsWorkKind.Bounty, 9_000));

        var line = CampaignLiaison.Dispatch(front, Us, work, standings, Tick, Tick.AddHours(3), "Cooperative");
        var brief = CampaignLiaison.Brief(line);

        Assert.Equal(
            [
                "Since the tick you have completed 1 mission and sold 120,000 credits of cargo for Kestrel Reach Compact in Lave.",
                "By your reading from 2 hours ago, Kestrel Reach Compact's influence in Lave moved from 41.2% before the tick to 43%.",
            ],
            line.Facts);
        Assert.Contains(CampaignLiaison.NoShareRule, brief.Instruction, StringComparison.Ordinal);
        Assert.DoesNotContain("share", line.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("share", brief.Speaker, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("because", line.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ADispatchWithNothingHandedInSaysSo()
    {
        var front = Retreat();

        var line = CampaignLiaison.Dispatch(front, Us, WorkInLave(), SystemStandings.Empty, Tick, Tick.AddHours(1), "Cooperative");

        Assert.Equal(["Since the tick you have handed in nothing for Kestrel Reach Compact in Lave."], line.Facts);
    }

    [Fact]
    public void AConflictDispatchGivesDaysWonAndNeverInfluence()
    {
        var standings = WarSeen(Tick.AddHours(1));
        var front = War(standings);
        var work = WorkInLave(new BgsEntry(Tick.AddMinutes(30), 1, Us, BgsWorkKind.CombatBond, 50_000));

        var line = CampaignLiaison.Dispatch(front, Us, work, standings, Tick, Tick.AddHours(2), "Cooperative");
        var brief = CampaignLiaison.Brief(line);

        Assert.Equal(
            [
                "Since the tick you have redeemed 50,000 credits in combat bonds for Kestrel Reach Compact in Lave.",
                "By your reading from 1 hour ago, days won stand at 2 to 1.",
            ],
            line.Facts);
        Assert.True(line.Conflict);
        NoInfluence(line.Text);
        NoInfluence(brief.Instruction);
    }

    [Fact]
    public void AMorningReportAsksTheCommanderToFlyIn()
    {
        var line = CampaignLiaison.MorningReport(Retreat(), Us, null);

        Assert.Equal(
            ["The BGS tick has passed, and there is no reading of Lave newer than it.", "Fly in to Lave for the morning report."],
            line.Facts);
    }

    [Fact]
    public void AResolutionGivesTheOutcomeThenTheNextFront()
    {
        var war = War();
        var next = Retreat() with { System = "Diso" };

        var line = CampaignLiaison.Resolution(new ChronicleEntry("Conflict", "Lave", "Lave Purple Gang", "Won", [], []), war, next, Us, "Cooperative");

        Assert.Equal(
            ["The war in Lave is over.", "Kestrel Reach Compact won it.", "The next front proposed is the retreat in Diso: Retreat is active here."],
            line.Facts);
        Assert.Equal("field officer", line.Part);
    }

    [Fact]
    public void TheLiaisonsPartFollowsTheFront()
    {
        var election = War() with { Conflict = War().Conflict! with { Type = "Election" } };
        var famine = Only(Place("Lave", 0.40, active: ["Famine"]));

        Assert.Equal("campaign manager", CampaignLiaison.PartFor(election));
        Assert.Equal("relief coordinator", CampaignLiaison.PartFor(famine));
        Assert.Equal("liaison officer", CampaignLiaison.PartFor(Retreat()));
    }

    [Fact]
    public void NoMannerTalksOfAShare()
    {
        foreach (var government in new[] { "Anarchy", "Communism", "Confederacy", "Cooperative", "Corporate", "Democracy", "Dictatorship", "Feudal", "Patronage", "Prison Colony", "Theocracy" })
        {
            Assert.DoesNotContain("share", CampaignLiaison.MannerFor(government), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void TheLiaisonsMannerFollowsTheGovernment()
    {
        Assert.Contains("contracts", CampaignLiaison.MannerFor("Corporate"), StringComparison.Ordinal);
        Assert.Contains("commanding", CampaignLiaison.MannerFor("$government_Dictatorship;"), StringComparison.Ordinal);
        Assert.Equal("professional and courteous", CampaignLiaison.MannerFor(null));
    }

    [Fact]
    public void OnlyWorkThatHelpsOnTheFrontCounts()
    {
        var war = War();
        var lockdown = Only(Place("Lave", 0.40, active: ["Lockdown"]));

        Assert.True(CampaignLiaison.Counts(war, BgsWorkKind.CombatBond, 1));
        Assert.False(CampaignLiaison.Counts(war, BgsWorkKind.Trade, 1));
        Assert.True(CampaignLiaison.Counts(lockdown, BgsWorkKind.Bounty, 1));
        Assert.False(CampaignLiaison.Counts(lockdown, BgsWorkKind.Mission, 2));
        Assert.False(CampaignLiaison.Counts(Retreat(), BgsWorkKind.Mission, -1));
    }

    [Fact]
    public void TheVoiceIsPickedByTheFactionsNameAndIsTheSameInANewSession()
    {
        var speech = D47Settings.Defaults.Speech with { Provider = "edge", VoicesProvider = "edge" };
        string[] pool = ["en-US-AriaNeural", "en-GB-RyanNeural", "en-AU-NatashaNeural", "en-IE-ConnorNeural"];

        VoiceCast Session() => new() { Pool = pool };

        var first = CampaignLiaison.Voice(Us, speech, _ => Session());
        var second = CampaignLiaison.Voice(Us, speech, _ => Session());

        Assert.NotNull(first);
        Assert.Equal(first, second);
        Assert.Equal(CampaignLiaison.Link, first.Link);
        Assert.Contains(first.VoiceId, pool);
    }

    [Fact]
    public void TheChosenVoiceIsUsedWhileTheProviderOffersIt()
    {
        var speech = D47Settings.Defaults.Speech with { Provider = "edge", VoicesProvider = "edge", LiaisonVoice = "en-IE-ConnorNeural" };
        string[] pool = ["en-US-AriaNeural", "en-IE-ConnorNeural"];

        Assert.Equal("en-IE-ConnorNeural", CampaignLiaison.Voice(Us, speech, _ => new VoiceCast { Pool = pool })!.VoiceId);

        var gone = CampaignLiaison.Voice(Us, speech with { LiaisonVoice = "en-GB-RyanNeural" }, _ => new VoiceCast { Pool = pool })!;

        Assert.Equal(CampaignLiaison.Voice(Us, speech with { LiaisonVoice = null }, _ => new VoiceCast { Pool = pool })!.VoiceId, gone.VoiceId);
    }

    private static void NoInfluence(string text)
    {
        Assert.DoesNotContain("influence", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("%", text, StringComparison.Ordinal);
    }
}

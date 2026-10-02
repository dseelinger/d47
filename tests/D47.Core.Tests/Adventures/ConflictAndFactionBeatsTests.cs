using System.Globalization;
using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>Conflict and faction beats (#732).</summary>
public class ConflictAndFactionBeatsTests
{
    private const string Gold = "Havalokul Gold Drug Empire";

    private const string Labour = "Labour of Havalokul";

    private const string Spine = """
        {"name": "The Long Way", "premise": "A debt.", "want": "To pay it.", "stake": "Whether it can be.", "turn": "It is owed.", "ending": "Forgiven."}
        """;

    private static readonly string CivilWar = Dispute("civilwar", "active", 0, 0);

    private static string Dispute(string warType, string status, int goldDays, int labourDays) =>
        $$"""{ "WarType":"{{warType}}", "Status":"{{status}}", "Faction1":{ "Name":"{{Gold}}", "Stake":"", "WonDays":{{goldDays}} }, "Faction2":{ "Name":"{{Labour}}", "Stake":"", "WonDays":{{labourDays}} } }""";

    private static string Jump(string system, long address, string? conflict = null) =>
        $$"""{ "timestamp":"@", "event":"FSDJump", "StarSystem":"{{system}}", "SystemAddress":{{address}}, "StarPos":[0,0,0], "Factions":[ { "Name":"{{Labour}}", "Influence":0.4 } ]{{(conflict is null ? string.Empty : $", \"Conflicts\":[ {conflict} ]")}} }""";

    private static string Bond(string awarding) =>
        $$"""{ "timestamp":"@", "event":"FactionKillBond", "Reward":10000, "AwardingFaction":"{{awarding}}", "VictimFaction":"X" }""";

    private static string Mission(string faction, long influenceSystem, string marks) =>
        $$"""
        { "timestamp":"@", "event":"MissionCompleted", "Faction":"{{faction}}", "Name":"Mission_Courier", "MissionID":1,
          "FactionEffects":[ { "Faction":"{{faction}}", "Influence":[ { "SystemAddress":{{influenceSystem}}, "Trend":"UpGood", "Influence":"{{marks}}" } ] } ] }
        """;

    private static AdventureTrigger Fighting(int count, string? faction = null, string? warType = null) =>
        new() { Kind = TriggerKind.Conflict, Count = count, Faction = faction, Filter = warType };

    private static AdventureStanding Fold(AdventureTrigger beat, params string[] events)
    {
        var adventure = LanternRoute(Accepted) with
        {
            Beats =
            [
                Beat("The Work", "setup", beat, "Done."),
                Beat("After", "finale", new AdventureTrigger { Kind = TriggerKind.Bounty, Count = 99 }, "Over."),
            ],
        };

        var standing = AdventureFold.Start(adventure);
        var world = AdventureWorld.Empty;

        for (var index = 0; index < events.Length; index++)
        {
            var journalEvent = Event(events[index].Replace("@", Stamp(Accepted.AddMinutes(1 + index)), StringComparison.Ordinal));

            world = world.Apply(journalEvent);
            standing = AdventureFold.Apply(standing, journalEvent, world);
        }

        return standing;
    }

    [Fact]
    public void AConflictBeatFiresOnTheFifthBondForOneSideOfACivilWar()
    {
        var bonds = Enumerable.Repeat(Bond(Gold), 4).ToArray();

        Assert.Equal(4, Fold(Fighting(5), [Jump("Havalokul", 111, CivilWar), .. bonds]).Counted);
        Assert.Equal(1, Fold(Fighting(5), [Jump("Havalokul", 111, CivilWar), .. bonds, Bond(Gold)]).Current);
    }

    [Fact]
    public void AConflictBeatDoesNotCountBondsForTheOtherSide()
    {
        var standing = Fold(Fighting(5), Jump("Havalokul", 111, CivilWar), Bond(Gold), Bond(Labour), Bond(Gold), Bond(Labour));

        Assert.Equal(2, standing.Counted);
        Assert.Equal(Gold, standing.Side);
    }

    [Fact]
    public void AConflictBeatNamingASideCountsOnlyThatSide()
    {
        Assert.Equal(1, Fold(Fighting(5, Labour), Jump("Havalokul", 111, CivilWar), Bond(Gold), Bond(Labour)).Counted);
    }

    [Fact]
    public void ABondInASystemWithNoActiveOrPendingConflictDoesNotCount()
    {
        Assert.Equal(0, Fold(Fighting(5), Jump("Quiet", 333), Bond(Gold)).Counted);
        Assert.Equal(0, Fold(Fighting(5), Jump("Havalokul", 111, Dispute("civilwar", string.Empty, 1, 0)), Bond(Gold)).Counted);
        Assert.Equal(1, Fold(Fighting(5), Jump("Havalokul", 111, Dispute("war", "pending", 0, 0)), Bond(Gold)).Counted);
    }

    [Fact]
    public void AConflictBeatLimitedToElectionsIgnoresBonds()
    {
        Assert.Equal(0, Fold(Fighting(1, warType: "election"), Jump("Havalokul", 111, CivilWar), Bond(Gold)).Counted);
    }

    [Fact]
    public void AMissionForOneSideOfAnElectionCountsWithAnInfluenceMarkInItsSystem()
    {
        var election = Dispute("election", "active", 0, 0);

        Assert.Equal(1, Fold(Fighting(3), Jump("Pellaxe", 222, election), Mission(Gold, 222, "++")).Counted);
        Assert.Equal(0, Fold(Fighting(3), Jump("Pellaxe", 222, election), Mission(Gold, 999, "++")).Counted);
        Assert.Equal(0, Fold(Fighting(3), Jump("Pellaxe", 222, election), Mission("Someone Else", 222, "++")).Counted);
    }

    [Fact]
    public void AnElectionCanBeWorkedFromAnotherSystem()
    {
        var election = Dispute("election", "active", 0, 0);

        Assert.Equal(1, Fold(Fighting(3), Jump("Pellaxe", 222, election), Jump("Elsewhere", 444), Mission(Labour, 222, "+")).Counted);
    }

    [Fact]
    public void AConflictSeenEndedWithTheCommandersSideAheadIsRecordedAsWon()
    {
        var ended = Dispute("civilwar", string.Empty, 4, 2);
        var standing = Fold(Fighting(2), Jump("Havalokul", 111, CivilWar), Bond(Gold), Jump("Elsewhere", 444), Jump("Havalokul", 111, ended));

        var part = Assert.Single(standing.Parts);
        Assert.True(part.Won);
        Assert.Contains("won by the Commander's side", part.Describe());
    }

    [Fact]
    public void AConflictSeenEndedWithTheOtherSideAheadIsRecordedAsLost()
    {
        var standing = Fold(Fighting(2), Jump("Havalokul", 111, CivilWar), Bond(Gold), Jump("Havalokul", 111, Dispute("civilwar", string.Empty, 1, 5)));

        var part = Assert.Single(standing.Parts);
        Assert.True(part.Ended);
        Assert.False(part.Won);
        Assert.Equal(Labour, part.Winner);
    }

    [Fact]
    public void AConflictNotSeenToEndIsNotReportedEnded()
    {
        Assert.False(Assert.Single(Fold(Fighting(2), Jump("Havalokul", 111, CivilWar), Bond(Gold)).Parts).Ended);
    }

    [Fact]
    public void AConflictBeatNeedsTheWorldToCount()
    {
        var adventure = LanternRoute(Accepted) with { Beats = [Beat("The Work", "setup", Fighting(1), "Done.")] };

        var standing = AdventureFold.Apply(AdventureFold.Start(adventure), Event(Bond(Gold).Replace("@", Stamp(Accepted.AddMinutes(1)), StringComparison.Ordinal)));

        Assert.False(standing.IsDone);
    }

    [Fact]
    public void AFactionBeatCountsEachPlusMarkAndFiresAtTen()
    {
        var beat = new AdventureTrigger { Kind = TriggerKind.Faction, Count = 10, Faction = Labour };

        Assert.Equal(4, Fold(beat, Mission(Gold, 111, "++++"), Mission(Labour, 111, "++++")).Counted);
        Assert.Equal(8, Fold(beat, Mission(Labour, 111, "++++"), Mission(Labour, 222, "++++")).Counted);
        Assert.Equal(1, Fold(beat, Mission(Labour, 111, "++++"), Mission(Labour, 222, "++++"), Mission(Labour, 333, "++")).Current);
    }

    [Fact]
    public void AFactionBeatLimitedToOneSystemCountsOnlyThatSystem()
    {
        var beat = new AdventureTrigger { Kind = TriggerKind.Faction, Count = 10, Faction = Labour, SystemAddress = 222 };

        Assert.Equal(2, Fold(beat, Mission(Labour, 111, "++++"), Mission(Labour, 222, "++")).Counted);
    }

    [Fact]
    public void AFactionBeatCountsNoDownMark()
    {
        var down = Mission(Labour, 111, "--").Replace("UpGood", "DownBad", StringComparison.Ordinal);

        Assert.Equal(0, Fold(new AdventureTrigger { Kind = TriggerKind.Faction, Count = 3, Faction = Labour }, down).Counted);
    }

    [Fact]
    public void TheNewKindsAreWordsTheFileUsesAndNeedWhatTheyCountOn()
    {
        foreach (var word in new[] { "conflict", "faction" })
        {
            Assert.Contains(word, AdventureValidation.Kinds);
            Assert.True(AdventureValidation.TryKind(word, out _));
        }

        Assert.NotEmpty(AdventureValidation.CountedProblems("Beat 1", new AdventureTrigger { Kind = TriggerKind.Faction, Count = 3 }));
        Assert.NotEmpty(AdventureValidation.CountedProblems("Beat 1", Fighting(3, warType: "skirmish")));
        Assert.Empty(AdventureValidation.CountedProblems("Beat 1", Fighting(3, warType: "CivilWar")));
        Assert.False(new AdventureTrigger { Kind = TriggerKind.Faction, Count = 3 }.IsResolved);
    }

    [Fact]
    public void TheTriggersAreDescribedInWords()
    {
        Assert.Equal("take part 5 times in a civil war for " + Gold, Fighting(5, Gold, "civilwar").Describe());
        Assert.Equal("Contributions to an election: 2 of 5", Fighting(5, warType: "election").Progress(2));
        Assert.Equal("earn 10 influence marks for " + Labour, new AdventureTrigger { Kind = TriggerKind.Faction, Count = 10, Faction = Labour }.Describe());
    }

    private static async Task<string> BriefFor(AdventureChapter chapter, params string[] events)
    {
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(Spine));

        await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy(), 1, events)
            .GenerateAsync(new AdventureAsk(Chapter: chapter), Accepted, TestContext.Current.CancellationToken);

        return provider.Requests[0].Prompt.History[0].Text;
    }

    private static AdventureChapter Finished(AdventureTrigger beat, params ConflictPart[] parts) =>
        new(LanternRoute(Accepted) with { Beats = [Beat("The Work", "setup", beat, "Done.")] }, []) { Conflicts = parts };

    [Fact]
    public async Task TheNextChaptersBriefSaysWhoWonTheConflict()
    {
        var won = new ConflictPart("Havalokul", "civilwar", Gold, Labour, Ended: true, Winner: Gold);

        var brief = await BriefFor(Finished(Fighting(5), won));

        Assert.Contains($"The Commander took part in the civil war in Havalokul for {Gold} against {Labour}; it was won by the Commander's side.", brief);
    }

    [Fact]
    public async Task TheNextChaptersBriefSaysWhetherAFactionsInfluenceRoseBetweenVisits()
    {
        static string Visit(string day, double influence) =>
            $$"""{ "timestamp":"2026-08-{{day}}T10:00:00Z", "event":"FSDJump", "StarSystem":"Havalokul", "SystemAddress":111, "StarPos":[0,0,0], "Factions":[ { "Name":"Labour of Havalokul", "Influence":{{influence.ToString(CultureInfo.InvariantCulture)}} } ] }""";

        var beat = new AdventureTrigger { Kind = TriggerKind.Faction, Count = 10, Faction = Labour };

        var rose = await BriefFor(Finished(beat), Visit("23", 0.40), Visit("24", 0.46));
        var unseen = await BriefFor(Finished(beat), Visit("24", 0.46));

        Assert.Contains($"worked missions for {Labour}; between visits its influence rose by 6.0 points in Havalokul", rose);
        Assert.Contains("has not seen its influence on two visits a day apart", unseen);
    }

    [Fact]
    public async Task TheWriterIsToldTheKindsAndTheConflictsItCanSee()
    {
        const string Beats = """
            {"opening": "x", "reply": "ok", "beats": [{"title": "The Work", "function": "finale", "kind": "conflict", "count": 3, "faction": "Labour of Havalokul", "line": "Fight."}]}
            """;

        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(Beats),
            RoundScriptedLlmProvider.Saying(Beats));

        var outcome = await AdventureGeneratorTests.Generator(
                provider,
                new AdventureGeneratorTests.Galaxy(),
                1,
                Jump("Havalokul", 111, CivilWar).Replace("@", "2026-08-22T11:02:00Z", StringComparison.Ordinal))
            .GenerateAsync(new AdventureAsk(), Accepted, TestContext.Current.CancellationToken);

        var brief = provider.Requests[1].Prompt.History[0].Text;

        Assert.NotNull(outcome.Draft);
        Assert.Contains("\"conflict\"", brief);
        Assert.Contains("\"faction\"", brief);
        Assert.Contains($"- Havalokul: civilwar between {Gold} and {Labour} (active)", brief);

        var written = Assert.Single(outcome.Draft.Beats).Trigger;
        Assert.Equal((TriggerKind.Conflict, 3, Labour), (written.Kind, written.Count, written.Faction));
    }
}

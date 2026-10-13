using System.Globalization;
using D47.Core.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

public sealed class TheChapterWriterIsToldWhatHappenedTests
{
    private const string Spine = """
        {"name": "The Second Column", "premise": "The ledger has a sequel.", "want": "To find who signed.",
         "stake": "Whether a name is a debt.", "turn": "The name was hers.", "ending": "It is paid."}
        """;

    private const string Beats = """
        {"opening": "Somebody is paying again.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Anchorage", "function": "turn", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "To one name."},
          {"title": "The Column", "function": "resolution", "kind": "rank", "career": "Trader", "rank": 8, "line": "It balances."}
        ]}
        """;

    private const string Died =
        """{ "timestamp":"@", "event":"Died", "KillerName":"CMDR Secret", "KillerShip":"federation_gunship", "KillerRank":"Dangerous" }""";

    private const string Resurrect = """{ "timestamp":"@", "event":"Resurrect", "Option":"rebuy", "Cost":5432100, "Bankrupt":false }""";

    private const string Interdicted =
        """{ "timestamp":"@", "event":"Interdicted", "Submitted":false, "Interdictor":"Dedy Sofyan", "IsPlayer":true, "Faction":"Nobody" }""";

    private static string Bounty(long reward) =>
        $$"""{ "timestamp":"@", "event":"Bounty", "PilotName":"$npc_name_decorate_1;", "PilotName_Localised":"Hidden Pilot", "Target":"sidewinder", "TotalReward":{{reward.ToString(CultureInfo.InvariantCulture)}} }""";

    private static AdventureStanding Fold(IEnumerable<string> lines, DateTimeOffset? acceptedAt = null) =>
        Fold(AdventureFold.Start(LanternRoute(acceptedAt ?? Accepted)), lines, 1);

    private static AdventureStanding Fold(AdventureStanding standing, IEnumerable<string> lines, int minute)
    {
        foreach (var line in lines)
        {
            standing = AdventureFold.Apply(standing, Event(line.Replace("@", Stamp(Accepted.AddMinutes(minute++)), StringComparison.Ordinal)));
        }

        return standing;
    }

    [Fact]
    public void ADeathAndItsRebuyAreOneEntry()
    {
        var notable = Assert.Single(Fold([Died, Resurrect]).Notables);

        Assert.Equal(NotableKind.Death, notable.Kind);
        Assert.Contains("dangerous", notable.Text);
        Assert.Contains("5,432,100 credits", notable.Text);
    }

    [Fact]
    public void NothingBeforeAcceptanceIsRecorded()
    {
        Assert.Empty(Fold([Died], Accepted.AddHours(1)).Notables);
    }

    [Fact]
    public void AHullScareCountsOnlyOnceTheCommanderDocksWithoutDying()
    {
        const string Scare = """{ "timestamp":"@", "event":"HullDamage", "Health":0.12, "PlayerPilot":true }""";
        const string Deeper = """{ "timestamp":"@", "event":"HullDamage", "Health":0.07, "PlayerPilot":true }""";
        const string Fighter = """{ "timestamp":"@", "event":"HullDamage", "Health":0.02, "PlayerPilot":false }""";
        const string Dock = """{ "timestamp":"@", "event":"Docked", "StationName":"Some Station", "MarketID":1 }""";

        Assert.Empty(ChapterNotables.Told(Fold([Scare, Deeper, Fighter]).Notables));
        Assert.Contains("7%", Assert.Single(ChapterNotables.Told(Fold([Scare, Deeper, Fighter, Dock]).Notables)).Text);
        Assert.DoesNotContain(ChapterNotables.Told(Fold([Scare, Died, Dock]).Notables), entry => entry.Kind == NotableKind.HullScare);
    }

    [Fact]
    public void OnlyTheLargestOfASaleKindIsKept()
    {
        Assert.Equal(9000, Assert.Single(Fold([Bounty(500), Bounty(9000), Bounty(700)]).Notables).Amount);
    }

    [Fact]
    public void FirstDiscoveriesAreCountedAndNavBeaconScansAreNot()
    {
        const string New = """{ "timestamp":"@", "event":"Scan", "ScanType":"Detailed", "WasDiscovered":false }""";
        const string Known = """{ "timestamp":"@", "event":"Scan", "ScanType":"AutoScan", "WasDiscovered":true }""";
        const string Beacon = """{ "timestamp":"@", "event":"Scan", "ScanType":"NavBeaconDetail", "WasDiscovered":false }""";

        Assert.Equal(2, Assert.Single(Fold([New, Known, Beacon, New]).Notables).Amount);
    }

    [Fact]
    public void AnEngineerListWrittenAtLoginIsNotAnInvitation()
    {
        const string List = """{ "timestamp":"@", "event":"EngineerProgress", "Engineers":[{ "Engineer":"Felicity Farseer", "Progress":"Unlocked" }] }""";
        const string One = """{ "timestamp":"@", "event":"EngineerProgress", "Engineer":"Felicity Farseer", "Progress":"Invited" }""";

        Assert.Equal(NotableKind.Engineer, Assert.Single(Fold([List, One]).Notables).Kind);
    }

    [Fact]
    public void TheListIsTheSameFoldedInOnePassOrInTwoHalves()
    {
        string[] lines = [Died, Resurrect, Interdicted, Bounty(100), """{ "timestamp":"@", "event":"Promotion", "Combat":3 }"""];

        var halves = Fold(Fold(lines.Take(2)), lines.Skip(2), 3);

        Assert.Equal(Fold(lines).Notables, halves.Notables);
    }

    [Fact]
    public void TheWriterIsToldAtMostTwelveNewestFirstAndThreeDeaths()
    {
        var told = ChapterNotables.Told(Fold([.. Enumerable.Repeat(Died, 5), .. Enumerable.Repeat(Interdicted, 12)]).Notables);

        Assert.Equal(12, told.Count);
        Assert.Equal(NotableKind.Interdicted, told[0].Kind);
        Assert.Equal(told.OrderByDescending(entry => entry.At), told);
        Assert.Equal(3, ChapterNotables.Told(Fold(Enumerable.Repeat(Died, 5)).Notables).Count);
    }

    [Fact]
    public async Task TheNextChapterPromptListsWhatHappenedAndLeaksNoName()
    {
        var notables = Fold([Died, Resurrect, Interdicted, Bounty(2500)]).Notables;
        var chapter = AdventureChapter.Of([LanternRoute() with { Key = "first", Name = "The First Chapter" }], "first")! with { Notables = notables };
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(Beats));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short, Chapter: chapter), Accepted, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);

        var prompt = provider.Requests[0].Prompt.History[0].Text;

        Assert.Contains("What the Commander did while they flew that chapter, newest first:", prompt);
        Assert.Contains("destroyed by a dangerous", prompt);
        Assert.Contains("another Commander", prompt);
        Assert.Contains("2,500 credits", prompt);
        Assert.Contains("Refer to at least one of these", prompt);
        Assert.True(prompt.IndexOf("What the Commander did", StringComparison.Ordinal) > prompt.IndexOf("The chapter just finished", StringComparison.Ordinal));
        Assert.DoesNotContain("Secret", prompt);
        Assert.DoesNotContain("Dedy", prompt);
        Assert.DoesNotContain("Hidden Pilot", prompt);
        Assert.DoesNotContain("npc_name", prompt);
    }

    [Fact]
    public async Task WithNothingNotableThePromptHasNoSection()
    {
        var chapter = AdventureChapter.Of([LanternRoute() with { Key = "first" }], "first");
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(Beats));

        await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short, Chapter: chapter), Accepted, CancellationToken.None);

        Assert.DoesNotContain("What the Commander did while", provider.Requests[0].Prompt.History[0].Text);
    }
}

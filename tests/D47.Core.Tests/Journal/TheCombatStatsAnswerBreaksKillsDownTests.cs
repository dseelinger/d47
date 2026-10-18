using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class TheCombatStatsAnswerBreaksKillsDownTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-18T12:00:00Z");

    private const string Commander =
        """{"timestamp":"2026-09-01T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""";

    private const string Loadout =
        """{"timestamp":"2026-09-01T09:01:00Z","event":"Loadout","Ship":"anaconda","ShipID":7,"ShipName":"Grey Lady","ShipIdent":"GL-01"}""";

    private const string Jump =
        """{"timestamp":"2026-09-01T09:02:00Z","event":"FSDJump","StarSystem":"Aiaba","SystemAddress":11,"StarPos":[0,0,0],"JumpDist":5.0}""";

    private static string Bounty(string time, string target, long reward, string victim, string payer) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"Bounty\",\"Target\":\"" + target + "\",\"TotalReward\":" + reward
        + ",\"VictimFaction\":\"" + victim + "\",\"Rewards\":[{\"Faction\":\"" + payer + "\",\"Reward\":" + reward + "}]}";

    private static string Bond(string time, long reward) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"FactionKillBond\",\"Reward\":" + reward
        + ",\"AwardingFaction\":\"Fed Navy\",\"VictimFaction\":\"Pirates\"}";

    private static GameStateStore Store(params string[] events)
    {
        var store = new GameStateStore();

        foreach (var line in new[] { Commander, Loadout, Jump }.Concat(events))
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store;
    }

    private static async Task<string> Ask(
        GameStateStore store, string? json = null, HistoryState history = HistoryState.Done)
    {
        var capability = JournalCapability.Create(store, () => history, now: () => Now);
        var result = await CapabilityRegistry.Build([capability]).InvokeAsync(
            "get_combat_stats", ToolArguments.FromJson(json), TestContext.Current.CancellationToken);

        Assert.False(result.IsError);

        return result.Spoken;
    }

    private static GameStateStore Typical() => Store(
        Bounty("2026-10-17T10:00:00Z", "eagle", 1000, "Pirates", "Fed Navy"),
        Bounty("2026-10-17T10:05:00Z", "eagle", 1000, "Pirates", "Fed Navy"),
        Bounty("2026-10-10T10:05:00Z", "anaconda_class1", 5000, "Zed Gang", "Alliance"));

    [Fact]
    public async Task WithNoGroupingItNamesTheTopEntryOfEach()
    {
        var said = await Ask(Typical());

        Assert.StartsWith("On record since 1 September 2026: 3 kills, 7,000 credits awarded.", said);
        Assert.Contains("Most destroyed: Eagle, 2.", said);
        Assert.Contains("Most in Aiaba, 3.", said);
        Assert.Contains("Most from Grey Lady (Anaconda), 3.", said);
        Assert.Contains("Most against Pirates, 2.", said);
    }

    [Fact]
    public async Task AWeekLeavesOutAKillEightDaysOldAndAllKeepsIt()
    {
        var store = Store(
            Bounty("2026-10-17T10:00:00Z", "eagle", 1000, "Pirates", "Fed Navy"),
            Bounty("2026-10-10T10:00:00Z", "eagle", 1000, "Pirates", "Fed Navy"));

        Assert.StartsWith("In the last 7 days: 1 kill, 1,000 credits awarded.", await Ask(store, """{"period":"week"}"""));
        Assert.StartsWith("On record since 1 September 2026: 2 kills", await Ask(store, """{"period":"all"}"""));
    }

    [Fact]
    public async Task OnlyTheThreeBusiestEnemiesAreNamedAndTheRestCounted()
    {
        var store = Store(
            Bounty("2026-10-17T10:00:00Z", "eagle", 1, "P", "A"),
            Bounty("2026-10-17T10:01:00Z", "eagle", 1, "P", "A"),
            Bounty("2026-10-17T10:02:00Z", "viper", 1, "P", "A"),
            Bounty("2026-10-17T10:03:00Z", "cobramkiii", 1, "P", "A"),
            Bounty("2026-10-17T10:04:00Z", "anaconda", 1, "P", "A"),
            Bounty("2026-10-17T10:05:00Z", "python", 1, "P", "A"));

        var said = await Ask(store, """{"by":"enemy"}""");

        Assert.Contains("and 2 more ship types.", said);
        Assert.Contains("Eagle, 2 kills", said);
    }

    [Fact]
    public async Task ByEnemyNamesBondKillsAsOneEntry()
    {
        var store = Store(
            Bounty("2026-10-17T10:00:00Z", "eagle", 1000, "Pirates", "Fed Navy"),
            Bond("2026-10-17T10:01:00Z", 200),
            Bond("2026-10-17T10:02:00Z", 300));

        var said = await Ask(store, """{"by":"enemy"}""");

        Assert.Contains("2 in conflict zones, ship type not recorded, 500 credits", said);
    }

    [Fact]
    public async Task ByFactionGivesWhoWasKilledAndWhoPaid()
    {
        var said = await Ask(Typical(), """{"by":"faction"}""");

        Assert.Contains("Against: Pirates, 2 kills, 2,000 credits; Zed Gang, 1 kill, 5,000 credits.", said);
        Assert.Contains("Paid by: Alliance, 5,000 credits; Fed Navy, 2,000 credits.", said);
    }

    [Fact]
    public async Task ANameNarrowsTheGroupingWithoutRegardToCase()
    {
        var said = await Ask(Typical(), """{"by":"enemy","name":"anaconda"}""");

        Assert.Contains("Anaconda, 1 kill, 5,000 credits.", said);
        Assert.DoesNotContain("Eagle", said);
    }

    [Fact]
    public async Task ByShipNamesTheHullAndSystemsByName()
    {
        Assert.Contains("Grey Lady (Anaconda), 3 kills", await Ask(Typical(), """{"by":"ship"}"""));
        Assert.Contains("Aiaba, 3 kills", await Ask(Typical(), """{"by":"system"}"""));
    }

    [Fact]
    public async Task AnEmptyPeriodAndAnEmptyRecordEachSaySo()
    {
        var store = Store(Bounty("2026-09-10T10:00:00Z", "eagle", 1, "P", "A"));

        Assert.Equal("No kills in the last 7 days.", await Ask(store, """{"period":"week"}"""));
        Assert.Equal("No kills in the journals on this PC.", await Ask(Store()));
    }

    [Fact]
    public async Task WhileTheHistoryIsBeingReadItSaysSoAndGivesNoFigures()
    {
        Assert.Equal(
            "I have not finished reading the journal history yet.",
            await Ask(Typical(), history: HistoryState.Running));
    }

    [Fact]
    public async Task AHistoryThatDidNotFinishAnswersFromTheLiveRecordAndSaysSo()
    {
        var said = await Ask(Typical(), history: HistoryState.Failed);

        Assert.StartsWith("On record since", said);
        Assert.EndsWith("Reading the journal history did not finish, so this is only what the live journal shows.", said);
    }
}

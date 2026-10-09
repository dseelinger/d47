using D47.Core.Engineers;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using JournalEvidence = D47.Core.Journal.UnlockEvidence;

namespace D47.Core.Tests.Journal;

/// <summary>Missions, sales and visits counted towards an engineer from the first listing of them (#595).</summary>
public class TalliesCountFromTheFirstListingTests
{
    private const string Fid = "F1";

    private static readonly Engineer Yarden = EngineerDirectory.ByName("Yarden Bond")!;
    private static readonly Engineer Rosa = EngineerDirectory.ByName("Rosa Dayette")!;
    private static readonly Engineer Eleanor = EngineerDirectory.ByName("Eleanor Bresa")!;
    private static readonly Engineer Jude = EngineerDirectory.ByName("Jude Navarro")!;

    /// <summary>The 2025-11-04 journal: five sales before Yarden Bond is first listed, five after.</summary>
    private static readonly string[] SmearCampaign =
    [
        LoadGame("2025-11-03T22:38:09Z"),
        Progress("2025-11-03T22:38:09Z", ("Kit Fowler", 400004, "Invited")),
        Docked("2025-11-04T01:51:19Z", "Kigandan", 3701300480),
        Sale("2025-11-04T02:18:05Z", "smearcampaignplans"),
        Sale("2025-11-04T02:21:16Z", "smearcampaignplans"),
        Sale("2025-11-04T02:24:43Z", "smearcampaignplans"),
        Sale("2025-11-04T02:27:03Z", "smearcampaignplans"),
        Sale("2025-11-04T02:29:27Z", "smearcampaignplans"),
        Docked("2025-11-04T02:41:42Z", "Capoya", 128973671),
        Progress("2025-11-04T02:46:30Z", ("Yarden Bond", 400009, "Known"), ("Kit Fowler", 400004, "Unlocked")),
        LoadGame("2025-11-04T02:46:30Z"),
        Docked("2025-11-04T02:51:26Z", "Kigandan", 3701300480),
        Sale("2025-11-04T02:53:57Z", "smearcampaignplans"),
        Sale("2025-11-04T02:56:04Z", "smearcampaignplans"),
        Sale("2025-11-04T02:58:31Z", "smearcampaignplans"),
        Progress("2025-11-04T02:58:51Z", ("Yarden Bond", 400009, "Known"), ("Kit Fowler", 400004, "Unlocked")),
        LoadGame("2025-11-04T02:58:51Z"),
        """{ "timestamp":"2025-11-04T02:59:02Z", "event":"Location", "Docked":false, "OnFoot":true, "StarSystem":"Kigandan" }""",
        Sale("2025-11-04T03:01:03Z", "smearcampaignplans"),
        Sale("2025-11-04T03:03:00Z", "smearcampaignplans"),
    ];

    [Fact]
    public void FiveSalesAfterTheFirstListingReadAsFiveOfFiveMet()
    {
        var state = Live(SmearCampaign);

        var criterion = MeetingCriterion(Yarden, state);

        Assert.True(criterion.Met);
        Assert.Equal(new UnlockMeasure(5, 5, false), criterion.Measure);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void TheHistoryWalkAndTheLiveFoldCountEachSaleOnce()
    {
        using var install = new TempInstall();
        File.WriteAllLines(Path.Combine(install.Root, "Journal.2025-11-03T173732.01.log"), SmearCampaign);

        var backfill = new HistoryBackfill { Directory = install.Root, Loggers = NullLoggerFactory.Instance };
        backfill.Run(TestContext.Current.CancellationToken);

        Assert.Equal(5, backfill.Evidence![Fid].Tallies.For(Yarden.Id)?.Total);

        var restoredFirst = new GameStateStore { RestoreEvidence = fid => backfill.Evidence.GetValueOrDefault(fid) };
        Fold(restoredFirst, SmearCampaign);

        var restoredLate = new GameStateStore { RestoreEvidence = fid => backfill.Evidence.GetValueOrDefault(fid) };
        Fold(restoredLate, SmearCampaign);
        restoredLate.RestoreLate();

        Assert.Equal(5, restoredFirst.Active!.Tallies.For(Yarden.Id)?.Total);
        Assert.Equal(5, restoredLate.Active!.Tallies.For(Yarden.Id)?.Total);
        Assert.Equal(
            DateTimeOffset.Parse("2025-11-04T02:46:30Z", System.Globalization.CultureInfo.InvariantCulture),
            restoredLate.Active!.Tallies.For(Yarden.Id)?.Start);
    }

    [Fact]
    public void ARosaDayetteSaleOutsideColoniaDoesNotCount()
    {
        var state = Live(
            LoadGame("2026-01-01T10:00:00Z"),
            Progress("2026-01-01T10:00:00Z", (Rosa.Name, Rosa.Id, "Known")),
            Docked("2026-01-01T11:00:00Z", "Sol", 128016640),
            Sale("2026-01-01T11:01:00Z", "culinaryrecipes"),
            Docked("2026-01-01T12:00:00Z", "Colonia", 3228342528),
            Sale("2026-01-01T12:01:00Z", "culinaryrecipes"));

        Assert.Equal(1, state.Tallies.For(Rosa.Id)?.Total);
    }

    [Fact]
    public void TwoDisembarksAtOneColoniaSettlementCountOnce()
    {
        var state = Live(
            LoadGame("2026-01-01T10:00:00Z"),
            Progress("2026-01-01T10:00:00Z", (Eleanor.Name, Eleanor.Id, "Known")),
            """{ "timestamp":"2026-01-01T11:00:00Z", "event":"ApproachSettlement", "Name":"Somewhere", "MarketID":3900000001 }""",
            Disembark("2026-01-01T11:05:00Z", "Colonia"),
            Disembark("2026-01-01T11:20:00Z", "Colonia"),
            """{ "timestamp":"2026-01-01T12:00:00Z", "event":"ApproachSettlement", "Name":"Elsewhere", "MarketID":3900000002 }""",
            Disembark("2026-01-01T12:05:00Z", "Colonia"));

        Assert.Equal(2, state.Tallies.For(Eleanor.Id)?.Total);
    }

    [Fact]
    public void AMissionCountsOnceByItsIdAndOnlyUnderItsStem()
    {
        var state = Live(
            LoadGame("2026-01-01T10:00:00Z"),
            Progress("2026-01-01T10:00:00Z", (Jude.Name, Jude.Id, "Known")),
            Mission("2026-01-01T11:00:00Z", "Mission_OnFoot_Reboot_MB_name", 1),
            Mission("2026-01-01T11:00:00Z", "Mission_OnFoot_Reboot_MB_name", 1),
            Mission("2026-01-01T12:00:00Z", "Mission_OnFoot_RebootRestore_name", 2));

        Assert.Equal(1, state.Tallies.For(Jude.Id)?.Total);
    }

    [Fact]
    public void ACountBelowTheTargetIsUndecidedWithAReadingAndAMeasure()
    {
        var state = Live(SmearCampaign[..14]);

        var criterion = MeetingCriterion(Yarden, state);

        Assert.Null(criterion.Met);
        Assert.Equal("2 counted since 4 Nov 2025", criterion.Reading);
        Assert.Equal(new UnlockMeasure(2, 5, false), criterion.Measure);
    }

    [Fact]
    public void NoListingYetIsUndecidedWithNoReading()
    {
        var criterion = MeetingCriterion(Yarden, Live(SmearCampaign[..8]));

        Assert.Null(criterion.Met);
        Assert.Null(criterion.Reading);
    }

    [Fact]
    public void ANewCommanderClearsTheCounts()
    {
        const string created =
            """{ "timestamp":"2025-11-05T10:00:00Z", "event":"NewCommander", "FID":"F1", "Name":"Fixture", "Package":"Default" }""";

        var state = Live([.. SmearCampaign, created]);
        var walked = SmearCampaign.Append(created).Select(Event).Aggregate(JournalEvidence.Empty, (held, e) => held.Apply(e));

        Assert.Null(state.Tallies.For(Yarden.Id));
        Assert.False(walked.Tallies.IsKnown);
    }

    private static UnlockCriterion MeetingCriterion(Engineer engineer, CommanderGameState state) =>
        EngineerAccess.CriteriaFor(engineer, D47.Core.Engineers.UnlockEvidence.From(state))
            .Single(criterion => criterion.Text == engineer.Meeting);

    private static CommanderGameState Live(params string[] lines)
    {
        var store = new GameStateStore();
        Fold(store, lines);
        return store.Active!;
    }

    private static void Fold(GameStateStore store, IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            store.Apply(Event(line));
        }
    }

    private static string LoadGame(string at) =>
        $$"""{ "timestamp":"{{at}}", "event":"LoadGame", "FID":"{{Fid}}", "Commander":"Fixture", "Odyssey":true }""";

    private static string Progress(string at, params (string Name, int Id, string Progress)[] engineers) =>
        $$"""{ "timestamp":"{{at}}", "event":"EngineerProgress", "Engineers":[ {{string.Join(", ", engineers.Select(e =>
            $$"""{ "Engineer":"{{e.Name}}", "EngineerID":{{e.Id}}, "Progress":"{{e.Progress}}" }"""))}} ] }""";

    private static string Docked(string at, string system, long marketId) =>
        $$"""{ "timestamp":"{{at}}", "event":"Docked", "StationName":"Station", "StarSystem":"{{system}}", "MarketID":{{marketId}} }""";

    private static string Sale(string at, string symbol) =>
        $$"""{ "timestamp":"{{at}}", "event":"SellMicroResources", "TotalCount":1, "MicroResources":[ { "Name":"{{symbol}}", "Category":"Data", "Count":1 } ], "Price":1300, "MarketID":3701300480 }""";

    private static string Disembark(string at, string system) =>
        $$"""{ "timestamp":"{{at}}", "event":"Disembark", "SRV":false, "Taxi":false, "Multicrew":false, "ID":1, "StarSystem":"{{system}}", "Body":"Colonia 2", "OnStation":false, "OnPlanet":true }""";

    private static string Mission(string at, string name, long missionId) =>
        $$"""{ "timestamp":"{{at}}", "event":"MissionCompleted", "Faction":"Someone", "Name":"{{name}}", "MissionID":{{missionId}} }""";

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }
}

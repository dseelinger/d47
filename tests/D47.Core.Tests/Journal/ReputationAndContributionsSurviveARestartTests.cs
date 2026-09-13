using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>Faction reputation and engineer contributions recovered from older journals (#182).</summary>
public class ReputationAndContributionsSurviveARestartTests
{
    private const string Fid = "F1234567";

    [Fact]
    public void AFactionReadingFromAnOlderJournalIsRestoredWithItsTimestamp()
    {
        using var install = new TempInstall();
        Write(install, "Journal.2026-07-05T100000.01.log", LoadGame(Fid, "07-05"), ColoniaLocation);
        Write(install, "Journal.2026-09-05T100000.01.log", LoadGame(Fid, "09-05"), Jump("Beta", "Someone Else", 1.0, "09-05"));

        var store = RestoredStore(install);
        store.Apply(Event(LoadGame(Fid, "09-06")));

        Assert.Equal(
            new FactionReading(54.5139, DateTimeOffset.Parse("2026-07-05T11:00:00Z", System.Globalization.CultureInfo.InvariantCulture)),
            store.Active!.Reputation.Reading("Colonia Council"));
    }

    [Fact]
    public void ALiveReadingTakenAfterTheRestoreWins()
    {
        using var install = new TempInstall();
        Write(install, "Journal.2026-07-05T100000.01.log", LoadGame(Fid, "07-05"), ColoniaLocation);

        var backfill = Backfill(install);
        var store = StoreOver(backfill);

        store.Apply(Event(LoadGame(Fid, "09-06")));
        store.Apply(Event(Jump("Colonia", "Colonia Council", 60.0, "09-06")));

        backfill.Run(TestContext.Current.CancellationToken);
        store.RestoreLate();

        Assert.Equal(60.0, store.Active!.Reputation.Reading("Colonia Council")?.MyReputation);
    }

    [Fact]
    public void AContributionTotalFromAnOlderJournalIsRestored()
    {
        using var install = new TempInstall();
        Write(install, "Journal.2026-07-05T100000.01.log", LoadGame(Fid, "07-05"), Contribution(30));
        Write(install, "Journal.2026-09-05T100000.01.log", LoadGame(Fid, "09-05"));

        var store = RestoredStore(install);
        store.Apply(Event(LoadGame(Fid, "09-06")));

        Assert.Equal(30, store.Active!.Contributions.Total(300090, "Commodity", "kamitracigars"));
    }

    [Fact]
    public void ANewCommanderBetweenTheJournalsRestoresNeither()
    {
        using var install = new TempInstall();
        Write(install, "Journal.2026-07-05T100000.01.log", LoadGame(Fid, "07-05"), ColoniaLocation, Contribution(30));
        Write(
            install,
            "Journal.2026-08-01T100000.01.log",
            $$"""{"timestamp":"2026-08-01T10:00:00Z","event":"NewCommander","FID":"{{Fid}}","Name":"Fixture","Package":"Default3"}""");
        Write(install, "Journal.2026-09-05T100000.01.log", LoadGame(Fid, "09-05"));

        var store = RestoredStore(install);
        store.Apply(Event(LoadGame(Fid, "09-06")));

        Assert.Null(store.Active!.Reputation.Reading("Colonia Council"));
        Assert.Null(store.Active!.Contributions.Total(300090, "Commodity", "kamitracigars"));
    }

    [Fact]
    public void TwoCommandersInOneFolderRestoreSeparately()
    {
        using var install = new TempInstall();
        Write(install, "Journal.2026-07-05T100000.01.log", LoadGame("F1", "07-05"), ColoniaLocation);
        Write(install, "Journal.2026-07-06T100000.01.log", LoadGame("F2", "07-06"), Contribution(30));

        var store = RestoredStore(install);
        store.Apply(Event(LoadGame("F1", "09-06")));
        store.Apply(Event(LoadGame("F2", "09-06")));

        var first = store.All.Single(state => state.Identity.FrontierId == "F1");
        var second = store.All.Single(state => state.Identity.FrontierId == "F2");

        Assert.Equal(54.5139, first.Reputation.Reading("Colonia Council")?.MyReputation);
        Assert.Null(first.Contributions.Total(300090, "Commodity", "kamitracigars"));
        Assert.Null(second.Reputation.Reading("Colonia Council"));
        Assert.Equal(30, second.Contributions.Total(300090, "Commodity", "kamitracigars"));
    }

    [Fact]
    public void TheWalkIsTimedAsAStartupStep()
    {
        using var install = new TempInstall();
        var steps = new List<string>();

        var backfill = new HistoryBackfill
        {
            Directory = install.Root,
            Loggers = NullLoggerFactory.Instance,
            Step = name =>
            {
                steps.Add(name);
                return new CancellationTokenSource();
            },
        };

        backfill.Run(TestContext.Current.CancellationToken);

        Assert.Contains("unlock evidence backfill", steps);
        Assert.Empty(backfill.Evidence!);
    }

    private static GameStateStore RestoredStore(TempInstall install)
    {
        var backfill = Backfill(install);
        backfill.Run(TestContext.Current.CancellationToken);
        return StoreOver(backfill);
    }

    private static HistoryBackfill Backfill(TempInstall install) => new()
    {
        Directory = install.Root,
        Loggers = NullLoggerFactory.Instance,
    };

    private static GameStateStore StoreOver(HistoryBackfill backfill) => new()
    {
        RestoreEvidence = fid => backfill.Evidence?.GetValueOrDefault(fid),
    };

    private const string ColoniaLocation =
        """{"timestamp":"2026-07-05T11:00:00Z","event":"Location","StarSystem":"Colonia","Factions":[{"Name":"Colonia Council","MyReputation":54.5139}]}""";

    private static string LoadGame(string fid, string day) =>
        $$"""{"timestamp":"2026-{{day}}T10:00:00Z","event":"LoadGame","FID":"{{fid}}","Commander":"Fixture"}""";

    private static string Jump(string system, string faction, double reputation, string day) =>
        $$"""{"timestamp":"2026-{{day}}T12:00:00Z","event":"FSDJump","StarSystem":"{{system}}","Factions":[{"Name":"{{faction}}","MyReputation":{{reputation.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}]}""";

    private static string Contribution(long total) =>
        $$"""{"timestamp":"2026-07-05T12:00:00Z","event":"EngineerContribution","Engineer":"Liz Ryder","EngineerID":300090,"Type":"Commodity","Commodity":"kamitracigars","Quantity":15,"TotalQuantity":{{total}}}""";

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static void Write(TempInstall install, string name, params string[] lines) =>
        File.WriteAllLines(Path.Combine(install.Root, name), lines);
}

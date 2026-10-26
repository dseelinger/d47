using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Logbook;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Journal;

/// <summary>Entering or leaving an Operation is not a session start for a ledger, live or rebuilt from files.</summary>
public class LedgersStartSessionsOnlyAtRealLoginsTests
{
    private const string Fid = "F1";

    private const string Folder = @"C:\d47-test\operation";

    private static readonly DateTimeOffset Start = new(2026, 10, 20, 20, 0, 0, TimeSpan.Zero);

    private readonly MemoryFileSystem _files = new();

    private static string Line(TimeSpan since, string kind, string fields = "") =>
        $$"""{ "timestamp":"{{(Start + since).ToString("yyyy-MM-ddTHH:mm:ssZ")}}", "event":"{{kind}}", "FID":"{{Fid}}", "Commander":"Fixture"{{fields}} }""";

    private static string Sale(TimeSpan since) =>
        Line(since, "MarketSell", ", \"MarketID\":2, \"Type\":\"palladium\", \"Count\":10, \"SellPrice\":50000, \"TotalSale\":500000, \"AvgPricePaid\":40000");

    private static string Target(TimeSpan since) =>
        Line(since, "ShipTargeted", ", \"TargetLocked\":true, \"Ship_Localised\":\"Python\", \"PilotName\":\"$cmdr_decorate:#name=Ada;\", \"PilotName_Localised\":\"CMDR Ada\"");

    private static string Codex(TimeSpan since) =>
        Line(since, "CodexEntry", ", \"EntryID\":1201, \"Name\":\"$Codex_Ent_1201;\", \"Name_Localised\":\"Entry\", \"Category\":\"$Codex_Category_Biology;\", \"Category_Localised\":\"Biological\", \"SubCategory\":\"$Codex_SubCategory_Organic_Structures;\", \"Region\":\"$Codex_RegionName_18;\", \"System\":\"Sol\", \"SystemAddress\":1, \"BodyID\":5");

    /// <summary>A session start, then an Operation entry and exit, with the given lines before the entry.</summary>
    private static string[] Journal(params string[] beforeOperation) =>
    [
        Line(TimeSpan.Zero, "Commander"),
        Line(TimeSpan.Zero, "LoadGame", ", \"Credits\":1000"),
        Line(TimeSpan.Zero, "GameModeChange", ", \"GameMode\":\"MainGame\""),
        .. beforeOperation,
        Line(TimeSpan.FromMinutes(5), "Commander"),
        Line(TimeSpan.FromMinutes(5), "LoadGame", ", \"Credits\":2000"),
        Line(TimeSpan.FromMinutes(5), "GameModeChange", ", \"GameMode\":\"Operation\""),
        Line(TimeSpan.FromMinutes(40), "Commander"),
        Line(TimeSpan.FromMinutes(40), "LoadGame", ", \"Credits\":2500"),
        Line(TimeSpan.FromMinutes(40), "GameModeChange", ", \"GameMode\":\"MainGame\""),
    ];

    private static JournalEvent Parse(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static List<JournalEvent> Live(string[] lines) => [.. OperationMarker.Mark(lines.Select(Parse))];

    private string Write(string[] lines)
    {
        var path = Path.Combine(Folder, $"Journal.{Start:yyyy-MM-ddTHHmmss}.01.log");
        _files.WriteText(path, string.Join("\n", lines) + "\n");
        return path;
    }

    [Fact]
    public void SalesBeforeAnOperationStayInTheLiveSessionTotal()
    {
        var ledger = new CommodityLedger();
        ledger.Apply(Live(Journal(Sale(TimeSpan.FromMinutes(2)))));

        Assert.Equal(1, ledger.Session(Fid, "Palladium").Sales);
    }

    [Fact]
    public void SalesBeforeAnOperationStayInTheSessionTotalRebuiltFromFiles()
    {
        Write(Journal(Sale(TimeSpan.FromMinutes(2))));
        var ledger = new CommodityLedger();

        ledger.FoldHistory(_files, Folder, Start.AddDays(-1), NullLogger.Instance);

        Assert.Equal(1, ledger.Session(Fid, "Palladium").Sales);
    }

    [Fact]
    public void TheCodexSessionStartIsTheSessionStartLive()
    {
        var ledger = Live(Journal(Codex(TimeSpan.FromMinutes(2)))).Aggregate(CodexLedger.None, (held, journalEvent) => held.Apply(journalEvent));

        Assert.Equal(Start, ledger.SessionStart);
    }

    [Fact]
    public void TheCodexSessionStartIsTheSessionStartRebuiltFromFiles()
    {
        var path = Write(Journal(Codex(TimeSpan.FromMinutes(2))));

        var ledgers = CodexLedgerBackfill.FromHistory(_files, [path], NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.Equal(Start, ledgers[Fid].SessionStart);
    }

    [Fact]
    public void ACommanderTargetedBeforeAnOperationIsStillMetThisSessionLive()
    {
        var log = new CommanderLog(_files, NullLogger.Instance);

        log.Apply(Live(Journal(Target(TimeSpan.FromMinutes(2)))));

        Assert.NotNull(log.LastTargeted());
    }

    [Fact]
    public void ACommanderTargetedBeforeAnOperationIsStillMetThisSessionRebuiltFromFiles()
    {
        var log = new CommanderLog(_files, NullLogger.Instance);

        log.Fold(Journal(Target(TimeSpan.FromMinutes(2))));

        var encounter = log.LastTargeted();
        Assert.NotNull(encounter);
        Assert.False(log.IsBeforeThisSession(encounter));
    }

    [Fact]
    public void AnOperationIsOneSessionToTheAdventureNudgeLive()
    {
        var book = Begun();

        foreach (var journalEvent in Live(Journal()))
        {
            book.Observe(journalEvent, Fid);
        }

        Assert.Single(book.Standing(Fid, "the-lantern-route")!.Loads);
    }

    [Fact]
    public void AnOperationIsOneSessionToTheAdventureNudgeRebuiltFromFiles()
    {
        var path = Write(Journal());
        var book = Begun();

        book.WalkAgain();
        book.CatchUp([path]);

        Assert.Single(book.Standing(Fid, "the-lantern-route")!.Loads);
    }

    [Fact]
    public void TheLastSessionRangeStartsAtTheSessionStart()
    {
        var path = Write(Journal());

        var range = LogRanges.Resolve(_files, LogSpan.Session, Start.AddHours(2), [path], NullLogger.Instance);

        Assert.Equal(Start, range.From);
    }

    [Fact]
    public void ThePreviousSessionStartsAtTheSessionStart()
    {
        var path = Write(Journal());

        var range = LogRanges.PreviousSession(_files, Start.AddHours(2), [path], NullLogger.Instance);

        Assert.Equal(Start, range?.From);
    }

    private AdventureBook Begun()
    {
        var store = new AdventureStore(Path.Combine(Folder, "adventures.json"), _files, NullLogger<AdventureStore>.Instance);
        var book = new AdventureBook(store, NullLogger<AdventureBook>.Instance);
        book.Write(Fid, LanternRoute(Start.AddMinutes(-1)));
        return book;
    }
}

using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Logbook;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Logbook;

/// <summary>A codex entry kept as a possible first is written as one in the logbook; any other keeps the plain line.</summary>
public sealed class TheLogbookNamesAPossibleRegionFirstTests
{
    private const string Journal = @"C:\journals\Journal.2026-10-10T090000.01.log";

    private const long Organic = 2311001;

    private const long Unlisted = 2399999;

    private static readonly CodexTable Table = CodexTable.Parse(
        "# Table date: 2026-10-19\n"
        + Organic + "\tFungoida Bullarum - Green\tBiology\tOrganic\t4:12\n"
        + "1400102\tSulphur Dioxide Fumarole\tGeology\tGeology\t18:300\n");

    private static string Codex(string time, long id, string subCategory) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"CodexEntry\",\"EntryID\":" + id + ",\"Name\":\"$Codex_Ent_" + id
        + ";\",\"Name_Localised\":\"Entry " + id + "\",\"Category\":\"$Codex_Category_Biology;\",\"SubCategory\":\""
        + subCategory + "\",\"Region\":\"$Codex_RegionName_18;\",\"System\":\"Sol\",\"SystemAddress\":10477373803"
        + ",\"IsNewEntry\":true}";

    private static readonly string[] Lines =
    [
        "{\"timestamp\":\"2026-10-10T09:00:00Z\",\"event\":\"LoadGame\",\"FID\":\"F1\",\"Commander\":\"Fixture\",\"Ship\":\"sidewinder\",\"Credits\":1}",
        Codex("2026-10-10T10:00:00Z", Organic, "$Codex_SubCategory_Organic_Structures;"),
        Codex("2026-10-10T11:00:00Z", Unlisted, "$Codex_SubCategory_Organic_Structures;"),
        Codex("2026-10-10T12:00:00Z", 1400102, "$Codex_SubCategory_Geology_and_Anomalies;"),
        Codex("2026-10-10T13:00:00Z", Organic, "$Codex_SubCategory_Organic_Structures;"),
    ];

    private static List<string> Statements(RegionFirstBook? book)
    {
        var files = new MemoryFileSystem();
        files.WriteText(Journal, string.Join("\n", Lines));

        var range = LogRanges.Between(
            DateTimeOffset.Parse("2026-10-10T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset.Parse("2026-10-10T14:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

        return
        [
            .. new LogDigestBuilder(files, NullLogger<LogDigestBuilder>.Instance, book)
                .Build([Journal], range, frontierId: "F1")
                .Facts
                .Select(fact => fact.Statement)
                .Where(statement => statement.StartsWith("Logged", StringComparison.Ordinal)),
        ];
    }

    private static RegionFirstBook Recorded()
    {
        var book = new RegionFirstBook(@"C:\d47-data\region-firsts.json", new MemoryFileSystem(), NullLogger.Instance, _ => { });
        var ledger = Lines.Aggregate(CodexLedger.None, (held, line) =>
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            return held.Apply(parsed!);
        });

        book.Observe("F1", CodexLedger.None.With(ledger), Table);

        return book;
    }

    [Fact]
    public void ARecordedEntryIsWrittenAsAPossibleFirstAndTheRestPlainly()
    {
        Assert.Equal(
            [
                "Logged a possible region first: Entry 2311001 in the Inner Orion Spur, in Sol.",
                "Logged a possible first in the galaxy: Entry 2399999, in Sol.",
                "Logged a codex entry: Entry 1400102 in Sol.",
                "Logged a codex entry: Entry 2311001 in Sol.",
            ],
            Statements(Recorded()));
    }

    [Fact]
    public void WithNoRecordsEveryEntryIsPlain() =>
        Assert.All(Statements(null), statement => Assert.StartsWith("Logged a codex entry: ", statement, StringComparison.Ordinal));
}

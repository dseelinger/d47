using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>A possible region first is judged once against the table of the day and kept per Commander.</summary>
public class ARegionFirstIsKeptOnceJudgedTests
{
    private const string Path = @"C:\d47-data\region-firsts.json";

    private const long Organic = 2311001;

    private const long Geology = 1400102;

    private static readonly CodexTable WithoutRegion = CodexTable.Parse(
        "# Table date: 2026-10-19\n"
        + Organic + "\tFungoida Bullarum - Green\tBiology\tOrganic\t4:12\n"
        + Geology + "\tSulphur Dioxide Fumarole\tGeology\tGeology\t18:300\n");

    private static readonly CodexTable WithRegion = CodexTable.Parse(
        "# Table date: 2026-11-19\n"
        + Organic + "\tFungoida Bullarum - Green\tBiology\tOrganic\t4:12,18:1\n"
        + Geology + "\tSulphur Dioxide Fumarole\tGeology\tGeology\t18:300\n");

    private static readonly CodexTable Empty = CodexTable.Parse("# Table date: 2026-12-19\n");

    private readonly MemoryFileSystem _files = new();

    private readonly List<Action> _offTick = [];

    private RegionFirstBook Book() => new(Path, _files, NullLogger.Instance, _offTick.Add);

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Jump(string time, long address) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"FSDJump\",\"StarSystem\":\"Sol\",\"SystemAddress\":" + address
        + ",\"StarPos\":[1.5,-2.25,30.0]}";

    private static string Codex(string time, long id, string subCategory, long address = 10477373803) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"CodexEntry\",\"EntryID\":" + id + ",\"Name\":\"$Codex_Ent_" + id
        + ";\",\"Name_Localised\":\"Entry " + id + "\",\"Category\":\"$Codex_Category_Biology;\",\"SubCategory\":\""
        + subCategory + "\",\"Region\":\"$Codex_RegionName_18;\",\"Region_Localised\":\"Inner Orion Spur\""
        + ",\"System\":\"Sol\",\"SystemAddress\":" + address + ",\"BodyID\":5,\"Latitude\":-12.5,\"Longitude\":101.25"
        + ",\"IsNewEntry\":true}";

    private static CodexLedger Whole(params string[] lines) =>
        CodexLedger.None.With(lines.Aggregate(CodexLedger.None, (ledger, line) => ledger.Apply(Event(line))));

    private static readonly CodexLedger OneOrganic = Whole(
        Jump("2026-10-10T09:00:00Z", 10477373803),
        Codex("2026-10-10T10:00:00Z", Organic, "$Codex_SubCategory_Organic_Structures;"));

    private void RunOffTick()
    {
        foreach (var work in _offTick)
        {
            work();
        }

        _offTick.Clear();
    }

    [Fact]
    public void ARegionFirstIsRecordedAndKeptWhenALaterTableListsTheRegion()
    {
        Book().Observe("F1", OneOrganic, WithoutRegion);
        RunOffTick();

        var reopened = Book();
        reopened.Observe("F1", OneOrganic, WithRegion);
        RunOffTick();

        var record = Book().Find("F1", Organic, 18);
        Assert.Equal((Organic, 18, CodexFirst.Region, "2026-10-19"), (record?.EntryId, record?.RegionId, record?.First, record?.TableDate));
    }

    [Fact]
    public void ThePairIsNotJudgedAgain()
    {
        var book = Book();

        book.Observe("F1", OneOrganic, WithoutRegion);
        book.Observe("F1", Whole(
            Jump("2026-10-10T09:00:00Z", 10477373803),
            Codex("2026-10-10T10:00:00Z", Organic, "$Codex_SubCategory_Organic_Structures;"),
            Codex("2026-10-11T10:00:00Z", Organic, "$Codex_SubCategory_Organic_Structures;")), Empty);
        RunOffTick();

        Assert.Equal(CodexFirst.Region, Book().Find("F1", Organic, 18)?.First);
    }

    [Fact]
    public void APairJudgedNoneIsNotRecorded()
    {
        var book = Book();

        book.Observe("F1", Whole(Codex("2026-10-10T10:00:00Z", Geology, "$Codex_SubCategory_Geology_and_Anomalies;")), WithoutRegion);
        book.Observe("F2", OneOrganic, WithRegion);

        Assert.Null(book.Find("F1", Geology, 18));
        Assert.Null(book.Find("F2", Organic, 18));
        Assert.Empty(_offTick);
    }

    [Fact]
    public void RecordsAreKeptApartByFrontierId()
    {
        var book = Book();

        book.Observe("F1", OneOrganic, WithoutRegion);
        RunOffTick();

        Assert.NotNull(Book().Find("F1", Organic, 18));
        Assert.Null(Book().Find("F2", Organic, 18));
    }

    [Fact]
    public void NothingIsJudgedUntilTheLedgerIsWhole()
    {
        var book = Book();
        var live = CodexLedger.None.Apply(Event(Codex("2026-10-10T10:00:00Z", Organic, "$Codex_SubCategory_Organic_Structures;")));

        book.Observe("F1", live, WithoutRegion);
        Assert.Null(book.Find("F1", Organic, 18));

        book.Observe("F1", CodexLedger.None.With(live), WithoutRegion);
        Assert.NotNull(book.Find("F1", Organic, 18));
    }

    [Fact]
    public void AnEntryInTheGalaxyNoTableListsIsAGalaxyFirst()
    {
        var book = Book();

        book.Observe("F1", Whole(Codex("2026-10-10T10:00:00Z", 2399999, "$Codex_SubCategory_Organic_Structures;")), WithoutRegion);

        Assert.Equal(CodexFirst.Galaxy, book.Find("F1", 2399999, 18)?.First);
    }

    [Fact]
    public void TheRecordKeepsTheLineThePlaceAndThePosition()
    {
        Book().Observe("F1", OneOrganic, WithoutRegion);
        RunOffTick();

        var record = Book().Find("F1", Organic, 18)!;

        Assert.Equal(("Entry 2311001", "Inner Orion Spur", "Sol", 10477373803L), (record.Name, record.Region, record.System, record.SystemAddress));
        Assert.Equal(new StarPosition(1.5, -2.25, 30.0), record.StarPos);
        Assert.Equal((5, -12.5, 101.25), (record.BodyId, record.Latitude, record.Longitude));
        Assert.Equal(DateTimeOffset.Parse("2026-10-10T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture), record.At);
        Assert.Equal(Codex("2026-10-10T10:00:00Z", Organic, "$Codex_SubCategory_Organic_Structures;"), record.Journal);
        Assert.Null(record.Reported);
    }

    [Fact]
    public void APositionFromAnotherSystemIsNotKept()
    {
        var book = Book();

        book.Observe("F1", Whole(
            Jump("2026-10-10T09:00:00Z", 99),
            Codex("2026-10-10T10:00:00Z", Organic, "$Codex_SubCategory_Organic_Structures;")), WithoutRegion);

        Assert.Null(book.Find("F1", Organic, 18)?.StarPos);
    }

    [Fact]
    public void TheFileIsWrittenOffTheTick()
    {
        Book().Observe("F1", OneOrganic, WithoutRegion);

        Assert.Null(_files.ReadText(Path));
        Assert.Single(_offTick);

        RunOffTick();

        Assert.NotNull(_files.ReadText(Path));
    }

    [Fact]
    public void TheTableDateIsReadFromItsOwnLine()
    {
        Assert.Equal("2026-10-19", WithoutRegion.Date);
        Assert.Null(CodexTable.Parse("# Source: something, 2026-10-19.\n1\tA\tB\tC\t1:1\n").Date);
        Assert.NotNull(CodexCatalogue.Table.Date);
    }
}

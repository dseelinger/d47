using D47.Core.Configuration;
using D47.Core.Logbook;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Logbook;

/// <summary>Two Commanders flew on one machine; a log is for the one shown.</summary>
public class ALogCoversOnlyTheShownCommanderTests
{
    private const string Kestrel = "F7654321";

    private static readonly DateTimeOffset Morning = new(3311, 4, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Evening = new(3311, 4, 2, 19, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset NextDay = new(3311, 4, 3, 19, 0, 0, TimeSpan.Zero);

    private static readonly LogRange WholeDay = new()
    {
        Span = LogSpan.Between,
        From = Morning.AddHours(-1),
        To = Evening.AddHours(2),
        Label = "the day",
    };

    private readonly JournalCorpus _corpus = new();

    [Fact]
    public void EachCommandersDigestHoldsOnlyTheirOwnEvents()
    {
        TwoCommandersInOneDay();

        var jameson = Build(JournalCorpus.Cmdr);
        var kestrel = Build(Kestrel);

        Assert.Equal("Jameson", jameson.Commander);
        Assert.Equal("Kestrel", kestrel.Commander);

        var jamesonText = jameson.Render();
        var kestrelText = kestrel.Render();

        Assert.Contains("Deciat", jamesonText, StringComparison.Ordinal);
        Assert.DoesNotContain("Sol", jamesonText, StringComparison.Ordinal);
        Assert.Contains("Sol", kestrelText, StringComparison.Ordinal);
        Assert.DoesNotContain("Deciat", kestrelText, StringComparison.Ordinal);
    }

    [Fact]
    public void ACommanderWhoReturnsLaterInTheDayGetsBothStretches()
    {
        TwoCommandersInOneDay();
        _corpus.Journal(
            Evening.AddHours(1),
            JournalCorpus.Event(Evening.AddHours(1), "Commander", $"\"Name\":\"Jameson\",\"FID\":\"{JournalCorpus.Cmdr}\""),
            JournalCorpus.LoadGame(Evening.AddHours(1)),
            JournalCorpus.Jump(Evening.AddHours(1).AddMinutes(2), "Sothis", 5));

        var rendered = Build(JournalCorpus.Cmdr).Render();

        Assert.Contains("Deciat", rendered, StringComparison.Ordinal);
        Assert.Contains("Sothis", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("Sol", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void WithNoCommanderNamedEveryoneIsRead()
    {
        TwoCommandersInOneDay();

        var rendered = new LogDigestBuilder(_corpus.FileSystem, NullLogger<LogDigestBuilder>.Instance)
            .Build(_corpus.Files, WholeDay).Render();

        Assert.Contains("Deciat", rendered, StringComparison.Ordinal);
        Assert.Contains("Sol", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLastSessionIsTheShownCommandersNotTheNewestOnDisk()
    {
        TwoCommandersInOneDay();

        var range = LogRanges.Resolve(
            _corpus.FileSystem,
            LogSpan.Session,
            Evening.AddHours(3),
            _corpus.Files,
            NullLogger.Instance,
            JournalCorpus.Cmdr);

        Assert.Equal(Morning, range.From);
    }

    [Fact]
    public void TheDayListHasOnlyDaysTheShownCommanderFlew()
    {
        TwoCommandersInOneDay();
        _corpus.Journal(
            NextDay,
            Commander(NextDay, "Kestrel", Kestrel),
            JournalCorpus.Jump(NextDay.AddMinutes(1), "Achenar", 10));

        Assert.Equal([new DateOnly(3311, 4, 2)], Book(JournalCorpus.Cmdr).JournalDays());
        Assert.Equal([new DateOnly(3311, 4, 3), new DateOnly(3311, 4, 2)], Book(Kestrel).JournalDays());
        Assert.Equal(2, Book(null).JournalDays().Count);
    }

    [Fact]
    public void LogsAreKeptInTheCommandersOwnFolderAndOlderOnesStillShow()
    {
        const string root = @"C:\d47-test\commander-log";

        _corpus.FileSystem.WriteText(Path.Combine(root, "2026-09-01-session.md"), "old");

        var folder = new LogFolder(root, _corpus.FileSystem, NullLogger<LogFolder>.Instance, () => Kestrel);
        var written = folder.Write("new", WholeDay, Evening);

        Assert.Equal(Path.Combine(root, Kestrel), Path.GetDirectoryName(written));
        Assert.Equal(2, folder.Entries().Count);

        var other = new LogFolder(root, _corpus.FileSystem, NullLogger<LogFolder>.Instance, () => JournalCorpus.Cmdr);

        Assert.Equal(["2026-09-01-session.md"], other.Entries().Select(entry => entry.Name));
    }

    private void TwoCommandersInOneDay()
    {
        _corpus.Journal(
            Morning,
            Commander(Morning, "Jameson", JournalCorpus.Cmdr),
            JournalCorpus.LoadGame(Morning),
            JournalCorpus.Jump(Morning.AddMinutes(1), "Deciat", 8));

        _corpus.Journal(
            Evening,
            Commander(Evening, "Kestrel", Kestrel),
            JournalCorpus.Event(
                Evening,
                "LoadGame",
                $"\"Name\":\"Kestrel\",\"FID\":\"{Kestrel}\",\"Ship\":\"Python\",\"Ship_Localised\":\"Python\","
                + "\"ShipName\":\"Other\",\"Credits\":5"),
            JournalCorpus.Jump(Evening.AddMinutes(1), "Sol", 8));
    }

    private static string Commander(DateTimeOffset at, string name, string fid) =>
        JournalCorpus.Event(at, "Commander", $"\"Name\":\"{name}\",\"FID\":\"{fid}\"");

    private LogDigest Build(string frontierId) =>
        new LogDigestBuilder(_corpus.FileSystem, NullLogger<LogDigestBuilder>.Instance)
            .Build(_corpus.Files, WholeDay, null, frontierId);

    private LogbookBook Book(string? frontierId) =>
        new(
            new LogFolder(@"C:\d47-test\commander-log", _corpus.FileSystem, NullLogger<LogFolder>.Instance),
            new LogDigestBuilder(_corpus.FileSystem, NullLogger<LogDigestBuilder>.Instance),
            new LogWriter(NullLogger<LogWriter>.Instance),
            () => new LogbookSettings(),
            () => _corpus.Files,
            () => Evening,
            () => new LogbookContext { FrontierId = frontierId },
            NullLogger<LogbookBook>.Instance);
}

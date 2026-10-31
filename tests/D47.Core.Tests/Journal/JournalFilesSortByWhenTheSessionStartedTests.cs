using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class JournalFilesSortByWhenTheSessionStartedTests
{
    private const string Root = @"C:\d47-test\journals";

    private static readonly TimeZoneInfo Eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

    // 2026-11-01 is the fall-back night: 01:40 EDT is 05:40Z, and 01:10 EST is 06:10Z.
    private static readonly string BeforeTheChange = Path.Combine(Root, "Journal.2026-11-01T014000.01.log");

    private static readonly string AfterTheChange = Path.Combine(Root, "Journal.2026-11-01T011000.01.log");

    private readonly MemoryFileSystem _files = new();

    [Fact]
    public void BothNamesFallInTheHourTheClocksRepeat()
    {
        Assert.True(Eastern.IsAmbiguousTime(new DateTime(2026, 11, 1, 1, 40, 0, DateTimeKind.Unspecified)));
        Assert.True(Eastern.IsAmbiguousTime(new DateTime(2026, 11, 1, 1, 10, 0, DateTimeKind.Unspecified)));
    }

    [Fact]
    public void ASessionOpenedAfterTheClocksGoBackIsLatestThoughItsNameIsEarlier()
    {
        WriteSession(BeforeTheChange, "2026-11-01T05:40:00Z");
        WriteSession(AfterTheChange, "2026-11-01T06:10:00Z");

        Assert.Equal([BeforeTheChange, AfterTheChange], JournalFolder.Ordered(_files, Root, Eastern));
        Assert.Equal(AfterTheChange, JournalFolder.LatestFile(_files, Root, Eastern));
    }

    [Fact]
    public void TheSpineSwitchesToASessionOpenedAfterTheClocksGoBack()
    {
        WriteSession(BeforeTheChange, "2026-11-01T05:40:00Z");

        var spine = new JournalSpine(Root, _files, new GameStateStore(), NullLoggerFactory.Instance, zone: Eastern);
        spine.Poll();
        Assert.Equal(BeforeTheChange, spine.CurrentFile);

        WriteSession(AfterTheChange, "2026-11-01T06:10:00Z");
        spine.Poll();

        Assert.Equal(AfterTheChange, spine.CurrentFile);
    }

    [Fact]
    public void AnAmbiguousNameWhoseFileIsEmptyIsTheLaterInstant()
    {
        _files.WriteText(BeforeTheChange, string.Empty);

        Assert.Equal(
            new DateTimeOffset(2026, 11, 1, 6, 40, 0, TimeSpan.Zero),
            JournalFolder.StartedAt(_files, BeforeTheChange, Eastern));
    }

    [Fact]
    public void AnAmbiguousNameIsResolvedFromItsFirstLine()
    {
        WriteSession(BeforeTheChange, "2026-11-01T05:40:01Z");

        Assert.Equal(
            new DateTimeOffset(2026, 11, 1, 5, 40, 0, TimeSpan.Zero),
            JournalFolder.StartedAt(_files, BeforeTheChange, Eastern));
    }

    [Fact]
    public void AJournalNameIsReadAsLocalTimeInTheZoneGiven()
    {
        Assert.Equal(
            new DateTimeOffset(2026, 9, 3, 13, 51, 27, TimeSpan.Zero),
            JournalFolder.StartedAt(_files, @"C:\x\Journal.2026-09-03T095127.01.log", Eastern));

        Assert.Equal(
            new DateTimeOffset(2026, 1, 3, 14, 51, 27, TimeSpan.Zero),
            JournalFolder.StartedAt(_files, @"C:\x\Journal.2026-01-03T095127.01.log", Eastern));

        Assert.Null(JournalFolder.StartedAt(_files, @"C:\x\Journal.beta.log", Eastern));
    }

    [Fact]
    public void FirstFileAtWithAUtcSinceFindsTheFileWhoseLocalNameIsThatInstant()
    {
        string[] files =
        [
            Path.Combine(Root, "Journal.2026-10-13T184027.01.log"),
            Path.Combine(Root, "Journal.2026-10-13T203726.01.log"),
        ];

        Assert.Equal(0, JournalFolder.FirstFileAt(_files, files, new DateTimeOffset(2026, 10, 13, 22, 40, 27, TimeSpan.Zero), Eastern));
        Assert.Equal(1, JournalFolder.FirstFileAt(_files, files, new DateTimeOffset(2026, 10, 13, 22, 40, 28, TimeSpan.Zero), Eastern));
        Assert.Equal(-1, JournalFolder.FirstFileAt(_files, files, new DateTimeOffset(2026, 10, 14, 0, 37, 27, TimeSpan.Zero), Eastern));
    }

    [Fact]
    public void TwoPartsOfOneSessionKeepPartOrder()
    {
        var first = Path.Combine(Root, "Journal.2026-09-03T095127.01.log");
        var second = Path.Combine(Root, "Journal.2026-09-03T095127.02.log");
        _files.WriteText(second, string.Empty);
        _files.WriteText(first, string.Empty);

        Assert.Equal([first, second], JournalFolder.Ordered(_files, Root, Eastern));
    }

    [Fact]
    public void ANameWithNoStampSortsFirst()
    {
        var stamped = Path.Combine(Root, "Journal.2026-09-03T095127.01.log");
        var unstamped = Path.Combine(Root, "Journal.beta.log");
        _files.WriteText(stamped, string.Empty);
        _files.WriteText(unstamped, string.Empty);

        Assert.Equal([unstamped, stamped], JournalFolder.Ordered(_files, Root, Eastern));
    }

    private void WriteSession(string path, string timestamp) =>
        _files.WriteText(
            path,
            $$"""{"timestamp":"{{timestamp}}","event":"Fileheader","part":1,"gameversion":"4.2.0","build":"r1"}""" + "\n");
}

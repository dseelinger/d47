using D47.Core.Logbook;
using Xunit;

namespace D47.Core.Tests.Logbook;

public sealed class ADayIsChosenFromTheJournalsOrTypedTests
{
    [Fact]
    public void ThreeDaysOfFileNamesListNewestFirstAndOnceEach()
    {
        string[] names =
        [
            @"C:\j\Journal.2026-10-05T091500.01.log",
            @"C:\j\Journal.2026-10-07T211644.01.log",
            @"C:\j\Journal.2026-10-05T200000.02.log",
            @"C:\j\Journal.2026-09-14T010203.01.log",
        ];

        Assert.Equal(
            [new DateOnly(2026, 10, 7), new DateOnly(2026, 10, 5), new DateOnly(2026, 9, 14)],
            LogDays.FromFileNames(names));
    }

    [Fact]
    public void TheOlderNameFormIsReadAndAnUnreadableNameIsSkipped()
    {
        Assert.Equal(
            [new DateOnly(2017, 6, 15)],
            LogDays.FromFileNames(["Journal.170615153355.01.log", "Journal.notadate.01.log", "Status.json", "Journal.171315153355.01.log"]));
    }

    [Fact]
    public void ATypedDayIsAcceptedAndAShortFormIsRefusedWithTheForm()
    {
        Assert.Equal(new DateOnly(2026, 9, 14), LogDays.Parse("2026-09-14"));
        Assert.Null(LogDays.Parse("14/09"));
        Assert.Contains("2026-09-14", LogDays.NotADay, StringComparison.Ordinal);
    }

    [Fact]
    public void ADayRunsFromMidnightToOneSecondBeforeTheNext()
    {
        var zone = TimeZoneInfo.Utc;
        var day = new DateOnly(2026, 9, 14);

        Assert.Equal(new DateTimeOffset(2026, 9, 14, 0, 0, 0, TimeSpan.Zero), LogDays.Start(day, zone));
        Assert.Equal(new DateTimeOffset(2026, 9, 14, 23, 59, 59, TimeSpan.Zero), LogDays.End(day, zone));
    }
}

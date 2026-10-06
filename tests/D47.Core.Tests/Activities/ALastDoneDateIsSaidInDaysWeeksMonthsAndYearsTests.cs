using D47.Core.Activities;
using Xunit;

namespace D47.Core.Tests.Activities;

public sealed class ALastDoneDateIsSaidInDaysWeeksMonthsAndYearsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("2026-10-06T01:00:00Z", "today")]
    [InlineData("2026-10-05T23:00:00Z", "yesterday")]
    [InlineData("2026-09-23T10:00:00Z", "13 days ago")]
    [InlineData("2026-08-18T10:00:00Z", "7 weeks ago")]
    [InlineData("2026-05-01T10:00:00Z", "5 months ago")]
    [InlineData("2025-02-02T10:00:00Z", "1 year 8 months ago")]
    [InlineData("2024-10-06T10:00:00Z", "2 years ago")]
    public void TheWordsFollowTheGap(string at, string expected) =>
        Assert.Equal(expected, ActivityAge.Say(DateTimeOffset.Parse(at, null, System.Globalization.DateTimeStyles.AssumeUniversal), Now));
}

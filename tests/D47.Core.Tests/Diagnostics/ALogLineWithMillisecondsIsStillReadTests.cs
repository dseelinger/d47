using D47.Core.Diagnostics.Donation;
using Xunit;

namespace D47.Core.Tests.Diagnostics;

public class ALogLineWithMillisecondsIsStillReadTests
{
    private static readonly DateOnly Day = new(2026, 8, 28);

    private static IReadOnlyList<LogEntry> Parse(string log) => LogScrub.Parse(log, Day, TimeZoneInfo.Utc);

    [Fact]
    public void AMillisecondHeadKeepsItsMilliseconds()
    {
        var entries = Parse("[14:36:09.412 INF] D47.App.AppHost: Heard: hello");

        var entry = Assert.Single(entries);
        Assert.Equal(new DateTimeOffset(2026, 8, 28, 14, 36, 9, 412, TimeSpan.Zero), entry.At);
    }

    [Fact]
    public void AFileMixingBothFormsIsReadInOrder()
    {
        var entries = Parse("""
            [23:59:58 INF] D47.App.AppHost: before the upgrade
            [23:59:59.250 INF] D47.App.AppHost: after the upgrade
            continuation of that line
            """);

        Assert.Equal(2, entries.Count);
        Assert.Equal(0, entries[0].At.Millisecond);
        Assert.Equal(250, entries[1].At.Millisecond);
        Assert.True(entries[0].At < entries[1].At);
        Assert.EndsWith("continuation of that line", entries[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnInGameLineWithAMillisecondHeadIsSilenced()
    {
        var entry = Assert.Single(Parse(
            "[12:03:03.101 INF] D47.Core.Audio.SpeechPipeline: Example Delta said: watch where you're going"));

        Assert.Equal(LogVoice.InGame, entry.Voice);

        var redacted = LogScrub.Redact(entry, new Pseudonyms());

        Assert.DoesNotContain("watch where you're going", redacted, StringComparison.Ordinal);
        Assert.Contains("[12:03:03.101 INF]", redacted, StringComparison.Ordinal);
        Assert.Contains("Example Delta said: " + LogScrub.Withheld, redacted, StringComparison.Ordinal);
    }
}

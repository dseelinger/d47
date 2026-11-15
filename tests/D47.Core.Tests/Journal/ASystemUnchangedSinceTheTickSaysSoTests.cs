using System.Globalization;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public sealed class ASystemUnchangedSinceTheTickSaysSoTests
{
    private const string Here = "Col 285 Sector AB-C d1";

    private static readonly DateTimeOffset Tick = new(2026, 10, 11, 0, 30, 0, TimeSpan.Zero);

    private static string Event(string kind, string time, double ours, string system = Here) =>
        "{\"timestamp\":\"" + time + "Z\",\"event\":\"" + kind + "\",\"StarSystem\":\"" + system + "\",\"SystemAddress\":1,"
        + "\"Factions\":[{\"Name\":\"Ours\",\"Influence\":" + ours.ToString(CultureInfo.InvariantCulture)
        + "},{\"Name\":\"Rivals\",\"Influence\":" + (1 - ours).ToString(CultureInfo.InvariantCulture) + "}]}";

    private static SystemStandings Folded(params string[] lines) =>
        lines.Aggregate(SystemStandings.Empty, (standings, line) =>
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            return standings.Apply(parsed!);
        });

    [Fact]
    public void ASystemWithNoReadingGivesNoReading()
    {
        Assert.Equal(SinceTick.NoReading, SystemStandings.Empty.Since(Here, Tick));
    }

    [Fact]
    public void AReadingOnlyBeforeTheTickGivesNoReading()
    {
        var standings = Folded(Event("FSDJump", "2026-10-10T20:00:00", 0.40));

        Assert.Equal(SinceTick.NoReading, standings.Since(Here, Tick));
    }

    [Fact]
    public void AnIdenticalLocationAfterTheTickGivesUnchanged()
    {
        var standings = Folded(
            Event("FSDJump", "2026-10-10T20:00:00", 0.40),
            Event("Location", "2026-10-11T09:00:00", 0.40));

        Assert.Equal(SinceTick.Unchanged, standings.Since(Here, Tick));
    }

    [Fact]
    public void ADifferingJumpAfterTheTickGivesChanged()
    {
        var standings = Folded(
            Event("Location", "2026-10-10T20:00:00", 0.40),
            Event("FSDJump", "2026-10-11T09:00:00", 0.43));

        Assert.Equal(SinceTick.Changed, standings.Since(Here, Tick));
    }

    [Fact]
    public void EveryReadingAfterTheTickGivesNoEarlier()
    {
        var standings = Folded(
            Event("FSDJump", "2026-10-11T09:00:00", 0.40),
            Event("FSDJump", "2026-10-11T10:00:00", 0.43));

        Assert.Equal(SinceTick.NoEarlier, standings.Since(Here, Tick));
    }

    [Fact]
    public void AReadingFirstSeenAWeekBeforeTheTickIsGoneSoItGivesNoEarlier()
    {
        var standings = Folded(
            Event("FSDJump", "2026-10-01T20:00:00", 0.40),
            Event("FSDJump", "2026-10-11T09:00:00", 0.43));

        Assert.Equal(SinceTick.NoEarlier, standings.Since(Here, Tick));
    }
}

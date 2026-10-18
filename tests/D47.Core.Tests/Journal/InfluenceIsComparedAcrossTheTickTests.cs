using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class InfluenceIsComparedAcrossTheTickTests
{
    private static JournalEvent Jump(string at, double influence, double other = 0.2)
    {
        var json = "{\"timestamp\":\"" + at + "\",\"event\":\"FSDJump\",\"StarSystem\":\"Alpha\",\"SystemAddress\":1,"
            + "\"Factions\":[{\"Name\":\"Alpha Mining\",\"Influence\":" + influence.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "},{\"Name\":\"Beta Union\",\"Influence\":" + other.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}]}";

        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));

        return parsed!;
    }

    private static SystemStandings Fold(params JournalEvent[] events) =>
        events.Aggregate(SystemStandings.Empty, (standings, e) => standings.Apply(e));

    private static DateTimeOffset At(string time) => DateTimeOffset.Parse(time, System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void TwoReadingsOnOneUtcDayGiveBothValuesAcrossABoundaryBetweenThem()
    {
        var standings = Fold(Jump("2026-10-10T10:00:00Z", 0.231), Jump("2026-10-10T18:02:00Z", 0.24));

        var across = standings.InfluenceAcross("Alpha", "Alpha Mining", At("2026-10-10T16:00:00Z"));

        Assert.NotNull(across);
        Assert.Equal(0.231, across.Value.Before, 6);
        Assert.Equal(0.24, across.Value.After, 6);
        Assert.Equal(At("2026-10-10T18:02:00Z"), across.Value.LastSeen);
    }

    [Fact]
    public void ReadingsWithTheSameValuesAreOneSpanAndAChangedValueStartsAnother()
    {
        var standings = Fold(
            Jump("2026-10-10T10:00:00Z", 0.2),
            Jump("2026-10-10T11:00:00Z", 0.2),
            Jump("2026-10-10T12:00:00Z", 0.2),
            Jump("2026-10-10T13:00:00Z", 0.25));

        var across = standings.InfluenceAcross("Alpha", "Alpha Mining", At("2026-10-10T12:30:00Z"));

        Assert.Equal(0.2, across!.Value.Before, 6);
        Assert.Equal(0.25, across.Value.After, 6);
        Assert.Null(standings.InfluenceAcross("Alpha", "Alpha Mining", At("2026-10-10T10:00:00Z")));
    }

    [Fact]
    public void ASpanSeenBeforeTheBoundaryAndAfterItIsUnchanged()
    {
        var standings = Fold(Jump("2026-10-10T10:00:00Z", 0.2), Jump("2026-10-10T18:00:00Z", 0.2));

        var across = standings.InfluenceAcross("Alpha", "Alpha Mining", At("2026-10-10T16:00:00Z"));

        Assert.Equal(across!.Value.Before, across.Value.After);
        Assert.Equal(At("2026-10-10T18:00:00Z"), across.Value.LastSeen);
    }

    [Fact]
    public void NothingIsAnsweredWhenTheLatestSpanWasLastSeenBeforeTheBoundary()
    {
        var standings = Fold(Jump("2026-10-10T10:00:00Z", 0.2), Jump("2026-10-10T11:00:00Z", 0.25));

        Assert.Null(standings.InfluenceAcross("Alpha", "Alpha Mining", At("2026-10-10T12:00:00Z")));
    }

    [Fact]
    public void NothingIsAnsweredForAnUnknownFactionOrSystem()
    {
        var standings = Fold(Jump("2026-10-10T10:00:00Z", 0.2), Jump("2026-10-10T18:00:00Z", 0.25));
        var boundary = At("2026-10-10T16:00:00Z");

        Assert.Null(standings.InfluenceAcross("Alpha", "Nobody", boundary));
        Assert.Null(standings.InfluenceAcross("Elsewhere", "Alpha Mining", boundary));
    }

    [Fact]
    public void ASpanOlderThanSevenDaysBeforeTheNewestReadingIsDropped()
    {
        var standings = Fold(
            Jump("2026-10-01T10:00:00Z", 0.2),
            Jump("2026-10-05T10:00:00Z", 0.21),
            Jump("2026-10-10T10:00:00Z", 0.3));

        Assert.Null(standings.InfluenceAcross("Alpha", "Alpha Mining", At("2026-10-03T00:00:00Z")));
        Assert.Equal(0.21, standings.InfluenceAcross("Alpha", "Alpha Mining", At("2026-10-06T00:00:00Z"))!.Value.Before);
    }

    [Fact]
    public void TheLatestAndEarlierReadingsAndTheChangeAreWhatTheyWere()
    {
        var standings = Fold(Jump("2026-10-09T10:00:00Z", 0.2), Jump("2026-10-10T10:00:00Z", 0.25), Jump("2026-10-10T18:00:00Z", 0.3));

        Assert.Equal(0.3, standings.Latest("Alpha")!.InfluenceOf("Alpha Mining"));
        Assert.Equal(0.2, standings.Earlier("Alpha")!.InfluenceOf("Alpha Mining"));
        Assert.Equal(0.1, standings.InfluenceChange("Alpha", "Alpha Mining")!.Value, 6);
    }
}

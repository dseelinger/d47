using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class AShuttleRideIsFoldedIntoGameStateTests
{
    private static string Line(string kind, string fields = "") =>
        "{ \"timestamp\":\"2026-10-20T18:00:00Z\", \"event\":\"" + kind + "\"" + (fields.Length > 0 ? ", " + fields : "") + " }";

    private static readonly string Commander = Line("Commander", "\"FID\":\"F1\", \"Name\":\"Fixture\"");

    private static GameStateStore Fold(params string[] lines)
    {
        var store = new GameStateStore();

        foreach (var json in new[] { Commander }.Concat(lines))
        {
            store.Apply(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed) ? parsed! : throw new InvalidOperationException(json));
        }

        return store;
    }

    private static ShuttleRide? RideAfter(params string[] lines) => Fold(lines).Active!.Shuttle;

    private static readonly string BookApex = Line("BookTaxi", "\"Cost\":100, \"DestinationSystem\":\"Fixture Sys\", \"DestinationLocation\":\"Fixture Port\"");
    private static readonly string BookFrontline = Line("BookDropship", "\"Cost\":100, \"DestinationSystem\":\"Fixture Sys\", \"DestinationLocation\":\"Fixture Settlement\", \"Retreat\":false");
    private static readonly string BookRetreat = Line("BookDropship", "\"Cost\":100, \"DestinationSystem\":\"Fixture Sys\", \"DestinationLocation\":\"Fixture Port\", \"Retreat\":true");
    private static readonly string EmbarkTaxi = Line("Embark", "\"Taxi\":true");

    [Fact]
    public void AnApexRideRunsFromEmbarkToDisembark()
    {
        Assert.Null(RideAfter(BookApex));

        var ride = RideAfter(BookApex, EmbarkTaxi);
        Assert.Equal(new ShuttleRide(ShuttleKind.Apex, "Fixture Sys", "Fixture Port", false), ride);

        Assert.Equal(ride, RideAfter(BookApex, EmbarkTaxi, Line("Docked", "\"StationName\":\"Fixture Port\"")));
        Assert.Null(RideAfter(BookApex, EmbarkTaxi, Line("Disembark", "\"Taxi\":true")));
    }

    [Fact]
    public void AFrontlineRideEndsWithTheDeploy()
    {
        Assert.Equal(new ShuttleRide(ShuttleKind.Frontline, "Fixture Sys", "Fixture Settlement", false), RideAfter(BookFrontline, EmbarkTaxi));
        Assert.Null(RideAfter(BookFrontline, EmbarkTaxi, Line("DropshipDeploy")));
    }

    [Fact]
    public void ARetreatRideRunsFromTheSurfaceToTheStation()
    {
        var ride = RideAfter(BookRetreat, EmbarkTaxi);
        Assert.Equal(new ShuttleRide(ShuttleKind.Frontline, "Fixture Sys", "Fixture Port", true), ride);
        Assert.Null(RideAfter(BookRetreat, EmbarkTaxi, Line("Disembark", "\"Taxi\":true")));
    }

    [Fact]
    public void ACancelledBookingNeverStartsARide()
    {
        Assert.Equal(ShuttleKind.Unknown, RideAfter(BookApex, Line("CancelTaxi"), EmbarkTaxi)!.Kind);
        Assert.Equal(ShuttleKind.Unknown, RideAfter(BookFrontline, Line("CancelDropship"), EmbarkTaxi)!.Kind);
    }

    [Fact]
    public void ARideWithNoBookingIsOfUnknownKind()
    {
        Assert.Equal(new ShuttleRide(ShuttleKind.Unknown, null, null, false), RideAfter(EmbarkTaxi));
    }

    [Fact]
    public void DeathAndANewSessionEndTheRide()
    {
        Assert.Null(RideAfter(BookApex, EmbarkTaxi, Line("Died")));
        Assert.Null(RideAfter(BookApex, EmbarkTaxi, Line("LoadGame", "\"FID\":\"F1\", \"Commander\":\"Fixture\"")));
        Assert.Null(RideAfter(BookApex, EmbarkTaxi, Line("Shutdown")));
    }

    [Fact]
    public void AnEmbarkThatIsNotAShuttleEndsTheRide()
    {
        Assert.Null(RideAfter(BookApex, EmbarkTaxi, Line("Embark", "\"Taxi\":false")));
    }

    [Fact]
    public void ASessionStartedAboardIsARideOfUnknownKind()
    {
        Assert.Equal(ShuttleKind.Unknown, RideAfter(Line("Location", "\"Taxi\":true"))!.Kind);
        Assert.Null(RideAfter(Line("Location", "\"Taxi\":true"), Line("Location", "\"Taxi\":false")));
        Assert.Equal(ShuttleKind.Apex, RideAfter(BookApex, EmbarkTaxi, Line("Location", "\"Taxi\":true"))!.Kind);
    }
}

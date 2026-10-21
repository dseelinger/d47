using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public sealed class AParkedShipIsRememberedTests
{
    private const string Approach = """{ "timestamp":"2026-10-01T11:00:00Z", "event":"ApproachBody", "StarSystem":"Oppi", "SystemAddress":77, "Body":"Oppi 1", "BodyID":4 }""";
    private const string Down = """{ "timestamp":"2026-10-01T11:05:00Z", "event":"Touchdown", "PlayerControlled":true, "Taxi":false, "Multicrew":false, "StarSystem":"Oppi", "SystemAddress":77, "Body":"Oppi 1", "BodyID":4, "OnStation":false, "OnPlanet":true, "Latitude":10.5, "Longitude":-20.25 }""";
    private const string Up = """{ "timestamp":"2026-10-01T11:10:00Z", "event":"Liftoff", "PlayerControlled":true, "Taxi":false, "Multicrew":false, "StarSystem":"Oppi", "SystemAddress":77, "Body":"Oppi 1", "BodyID":4, "OnStation":false, "OnPlanet":true, "Latitude":10.5, "Longitude":-20.25 }""";
    private const string Dismissed = """{ "timestamp":"2026-10-01T11:10:00Z", "event":"Liftoff", "PlayerControlled":false, "Taxi":false, "Multicrew":false, "SystemAddress":77, "BodyID":4, "Latitude":10.5, "Longitude":-20.25 }""";
    private const string Recalled = """{ "timestamp":"2026-10-01T11:20:00Z", "event":"Touchdown", "PlayerControlled":false, "Taxi":false, "Multicrew":false, "SystemAddress":77, "BodyID":4, "Latitude":11.0, "Longitude":-21.0 }""";
    private const string Leave = """{ "timestamp":"2026-10-01T11:30:00Z", "event":"LeaveBody", "StarSystem":"Oppi", "SystemAddress":77, "Body":"Oppi 1", "BodyID":4 }""";

    private static SurfacePoints Fold(params string[] lines)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Tester"));

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed), line);
            state.Apply(parsed!);
        }

        return state.Surface;
    }

    [Theory]
    [InlineData(0, 0, 10, 0, 0)]
    [InlineData(0, 0, 0, 10, 90)]
    [InlineData(10, 0, 0, 0, 180)]
    [InlineData(0, 10, 0, 0, 270)]
    [InlineData(51.5, -0.12, 40.7, -74.0, 288.3)]
    public void BearingFollowsTheCompass(double lat1, double lon1, double lat2, double lon2, double expected)
    {
        var bearing = new SurfaceFix(lat1, lon1, 0).BearingTo(new SurfaceFix(lat2, lon2, 0));

        Assert.InRange(bearing, expected - 0.1, expected + 0.1);
    }

    [Fact]
    public void TheSamePointHasBearingZero() =>
        Assert.Equal(0, new SurfaceFix(12, 34, 0).BearingTo(new SurfaceFix(12, 34, 0)));

    [Fact]
    public void ApproachingSetsTheBody()
    {
        var surface = Fold(Approach);

        Assert.Equal((77L, 4), surface.Body);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 11, 0, 0, TimeSpan.Zero), surface.Since);
        Assert.Null(surface.Ship);
    }

    [Fact]
    public void TouchingDownSetsTheShipPoint()
    {
        var surface = Fold(Approach, Down);

        Assert.Equal(new SurfaceFix(10.5, -20.25, 0), surface.Ship);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 11, 5, 0, TimeSpan.Zero), surface.Since);
    }

    [Fact]
    public void LiftingOffClearsTheShipPointButKeepsTheBody()
    {
        var surface = Fold(Down, Up);

        Assert.Null(surface.Ship);
        Assert.Equal((77L, 4), surface.Body);
    }

    [Fact]
    public void DismissingTheShipClearsItsPoint() => Assert.Null(Fold(Down, Dismissed).Ship);

    [Fact]
    public void RecallingTheShipSetsTheNewPoint() =>
        Assert.Equal(new SurfaceFix(11.0, -21.0, 0), Fold(Down, Dismissed, Recalled).Ship);

    [Theory]
    [InlineData("Taxi")]
    [InlineData("Multicrew")]
    public void ATouchdownInAnotherShipLeavesThePointAlone(string flag)
    {
        var other = Down.Replace($"\"{flag}\":false", $"\"{flag}\":true", StringComparison.Ordinal)
            .Replace("10.5", "99", StringComparison.Ordinal);

        Assert.Equal(new SurfaceFix(10.5, -20.25, 0), Fold(Down, other).Ship);
    }

    [Fact]
    public void ALiftoffInAnotherShipLeavesThePointAlone()
    {
        var taxi = Up.Replace("\"Taxi\":false", "\"Taxi\":true", StringComparison.Ordinal);

        Assert.NotNull(Fold(Down, taxi).Ship);
    }

    [Fact]
    public void LeavingTheBodyClearsBodyAndShip()
    {
        var surface = Fold(Approach, Down, Leave);

        Assert.Null(surface.Body);
        Assert.Null(surface.Ship);
        Assert.Null(surface.Since);
    }
}

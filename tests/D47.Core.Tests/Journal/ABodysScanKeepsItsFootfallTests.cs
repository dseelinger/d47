using System.Text.Json;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>What a body's scan says, and whether it has been footfalled (#202).</summary>
public class ABodysScanKeepsItsFootfallTests
{
    private const long SystemAddress = 1_000_100;
    private const int BodyId = 4;

    private static readonly DateTimeOffset ScannedAt = new(3311, 4, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset DisembarkedAt = ScannedAt.AddMinutes(5);
    private static readonly DateTimeOffset RescannedAt = ScannedAt.AddMinutes(10);

    [Fact]
    public void AScanFillsEveryField()
    {
        var scans = BodyScans.Empty.Apply(Scan(wasFootfalled: false));

        var body = scans.For(SystemAddress, BodyId);

        Assert.NotNull(body);
        Assert.Equal("Smojue EB-O d6-37 AB 1 b", body!.BodyName);
        Assert.Equal("Icy body", body.PlanetClass);
        Assert.Equal("thin", body.Atmosphere);
        Assert.Equal("major", body.Volcanism);
        Assert.Equal(9.81, body.SurfaceGravity);
        Assert.Equal(55.6, body.SurfaceTemperature);
        Assert.Equal(0.012, body.SurfacePressure);
        Assert.True(body.Landable);
        Assert.False(body.WasFootfalled);
        Assert.Equal(ScannedAt, body.SeenAt);
    }

    [Fact]
    public void AScanWithNoWasFootfalledFieldLeavesItNull()
    {
        var scans = BodyScans.Empty.Apply(Scan(wasFootfalled: null));

        Assert.Null(scans.For(SystemAddress, BodyId)?.WasFootfalled);
    }

    [Fact]
    public void ADisembarkOnAnUnfootfalledBodySetsWhenItHappened()
    {
        var scans = BodyScans.Empty
            .Apply(Scan(wasFootfalled: false))
            .Apply(Disembark());

        Assert.Equal(DisembarkedAt, scans.For(SystemAddress, BodyId)?.FootfallTakenAt);
    }

    [Fact]
    public void ASecondDisembarkThereLeavesTheFirstTimeStanding()
    {
        var scans = BodyScans.Empty
            .Apply(Scan(wasFootfalled: false))
            .Apply(Disembark())
            .Apply(Disembark(at: DisembarkedAt.AddMinutes(1)));

        Assert.Equal(DisembarkedAt, scans.For(SystemAddress, BodyId)?.FootfallTakenAt);
    }

    [Fact]
    public void ADisembarkFromAnSrvSetsNothing()
    {
        var scans = BodyScans.Empty
            .Apply(Scan(wasFootfalled: false))
            .Apply(Disembark(srv: true));

        Assert.Null(scans.For(SystemAddress, BodyId)?.FootfallTakenAt);
    }

    [Fact]
    public void ADisembarkOnABodyAlreadyFootfalledSetsNothing()
    {
        var scans = BodyScans.Empty
            .Apply(Scan(wasFootfalled: true))
            .Apply(Disembark());

        Assert.Null(scans.For(SystemAddress, BodyId)?.FootfallTakenAt);
    }

    [Fact]
    public void ADisembarkOnABodyWithNoWasFootfalledFieldSetsNothing()
    {
        var scans = BodyScans.Empty
            .Apply(Scan(wasFootfalled: null))
            .Apply(Disembark());

        Assert.Null(scans.For(SystemAddress, BodyId)?.FootfallTakenAt);
    }

    [Fact]
    public void ADisembarkOnABodyWithNoScanSetsNothing()
    {
        var scans = BodyScans.Empty.Apply(Disembark());

        Assert.Null(scans.For(SystemAddress, BodyId));
    }

    [Fact]
    public void ARescanKeepsTheFootfallAndReplacesEverythingElse()
    {
        var scans = BodyScans.Empty
            .Apply(Scan(wasFootfalled: false))
            .Apply(Disembark())
            .Apply(Scan(wasFootfalled: false, at: RescannedAt, atmosphere: "none"));

        var body = scans.For(SystemAddress, BodyId);

        Assert.Equal(DisembarkedAt, body?.FootfallTakenAt);
        Assert.Equal("none", body?.Atmosphere);
        Assert.Equal(RescannedAt, body?.SeenAt);
    }

    private static JournalEvent Scan(bool? wasFootfalled, DateTimeOffset? at = null, string atmosphere = "thin")
    {
        var timestamp = at ?? ScannedAt;
        var footfallField = wasFootfalled is { } value ? $"\"WasFootfalled\":{(value ? "true" : "false")}," : "";

        var text = $$"""
            {
                "timestamp": "{{timestamp.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}}",
                "event": "Scan",
                "SystemAddress": {{SystemAddress}},
                "BodyID": {{BodyId}},
                "BodyName": "Smojue EB-O d6-37 AB 1 b",
                "PlanetClass": "Icy body",
                "Atmosphere": "{{atmosphere}}",
                "Volcanism": "major",
                "SurfaceGravity": 9.81,
                "SurfaceTemperature": 55.6,
                "SurfacePressure": 0.012,
                "Landable": true,
                {{footfallField}}
                "ScanType": "Detailed"
            }
            """;

        return new JournalEvent(timestamp, "Scan", JsonDocument.Parse(text).RootElement);
    }

    private static JournalEvent Disembark(bool srv = false, DateTimeOffset? at = null)
    {
        var timestamp = at ?? DisembarkedAt;

        var text = $$"""
            {
                "timestamp": "{{timestamp.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}}",
                "event": "Disembark",
                "SRV": {{(srv ? "true" : "false")}},
                "OnPlanet": true,
                "OnStation": false,
                "SystemAddress": {{SystemAddress}},
                "Body": "Smojue EB-O d6-37 AB 1 b",
                "BodyID": {{BodyId}}
            }
            """;

        return new JournalEvent(timestamp, "Disembark", JsonDocument.Parse(text).RootElement);
    }
}

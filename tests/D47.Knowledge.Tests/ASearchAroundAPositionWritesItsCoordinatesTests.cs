using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>A station or body search centred on a point writes its coordinates, and each result carries its system's position (#1846).</summary>
public class ASearchAroundAPositionWritesItsCoordinatesTests
{
    private static readonly StarPosition Point = new(0, 0, 400);

    [Fact]
    public void AStationSearchAroundAPointWritesReferenceCoordsAndNoSystem()
    {
        var body = JsonDocument.Parse(SpanshRequest.Stations(StationQuery.Near(Point, 100, 5))).RootElement;

        var coords = body.GetProperty("reference_coords");
        Assert.Equal(0, coords.GetProperty("x").GetDouble());
        Assert.Equal(0, coords.GetProperty("y").GetDouble());
        Assert.Equal(400, coords.GetProperty("z").GetDouble());
        Assert.False(body.TryGetProperty("reference_system", out _));
    }

    [Fact]
    public void AStationSearchAroundASystemStillWritesReferenceSystem()
    {
        var body = JsonDocument.Parse(SpanshRequest.Stations(StationQuery.Near("Sol", 100, 5))).RootElement;

        Assert.Equal("Sol", body.GetProperty("reference_system").GetString());
        Assert.False(body.TryGetProperty("reference_coords", out _));
    }

    [Fact]
    public void ABodySearchAroundAPointWritesReferenceCoordsAndNoSystem()
    {
        var body = JsonDocument.Parse(SpanshRequest.Bodies(BodyQuery.LandableNear(Point, 100, 5))).RootElement;

        Assert.Equal(400, body.GetProperty("reference_coords").GetProperty("z").GetDouble());
        Assert.False(body.TryGetProperty("reference_system", out _));
    }

    [Fact]
    public void ABodySearchAroundASystemStillWritesReferenceSystem()
    {
        var body = JsonDocument.Parse(SpanshRequest.Bodies(BodyQuery.LandableNear("Sol", 100, 5))).RootElement;

        Assert.Equal("Sol", body.GetProperty("reference_system").GetString());
        Assert.False(body.TryGetProperty("reference_coords", out _));
    }

    [Fact]
    public void ThePointSearchesApplyTheSameClampsAsTheSystemOnes()
    {
        var station = StationQuery.Near(Point, 9999, 99);
        var named = StationQuery.Near("Sol", 9999, 99);
        var bodies = BodyQuery.LandableNear(Point, 0, 0);
        var namedBodies = BodyQuery.LandableNear("Sol", 0, 0);

        Assert.Equal(named.MaxDistance, station.MaxDistance);
        Assert.Equal(named.Size, station.Size);
        Assert.Equal(namedBodies.MaxDistance, bodies.MaxDistance);
        Assert.Equal(namedBodies.Size, bodies.Size);
        Assert.True(station.FixedOnly);
        Assert.True(bodies.Landable);
    }

    [Fact]
    public void AStationResultWithXYAndZHasItsSystemsPosition()
    {
        using var document = JsonDocument.Parse("""
            {"count":2,"results":[
              {"name":"A","system_name":"Col 359 Sector AB-C d1","system_x":1.5,"system_y":-2,"system_z":398.25},
              {"name":"B","system_name":"Nowhere","system_x":1,"system_y":2}]}
            """);

        var stations = SpanshResponse.ReadStations(document).Stations;

        Assert.Equal(new StarPosition(1.5, -2, 398.25), stations[0].Position);
        Assert.Null(stations[1].Position);
    }

    [Fact]
    public void ABodyResultWithXYAndZHasItsSystemsPosition()
    {
        using var document = JsonDocument.Parse("""
            {"count":2,"results":[
              {"name":"A 1","system_name":"Col 359 Sector AB-C d1","system_x":1.5,"system_y":-2,"system_z":398.25},
              {"name":"B 1","system_name":"Nowhere"}]}
            """);

        var bodies = SpanshResponse.ReadBodies(document).Bodies;

        Assert.Equal(new StarPosition(1.5, -2, 398.25), bodies[0].Position);
        Assert.Null(bodies[1].Position);
    }
}

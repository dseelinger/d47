using System.Text.Json;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>A scan keeps the orbit, size, rings and materials it carries, and a system's scanned bodies can be listed (#1388).</summary>
public class ABodysScanKeepsItsOrbitRingsAndMaterialsTests
{
    private const long SystemAddress = 2_000_200;

    [Fact]
    public void APlanetScanKeepsEveryFieldAsWritten()
    {
        var body = BodyScans.Empty.Apply(Scan("""
            "BodyID": 5, "BodyName": "Alpha 1 a", "PlanetClass": "Icy body",
            "Parents": [{"Planet": 4}, {"Star": 2}, {"Null": 1}, {"Null": 0}],
            "SemiMajorAxis": 123456.5, "Eccentricity": 0.25, "OrbitalPeriod": 9000.5,
            "RotationPeriod": -4000.25, "AxialTilt": -0.5, "Radius": 2500000.0, "MassEM": 0.07,
            "TidalLock": true, "TerraformState": "Terraformable",
            "Materials": [{"Name": "iron", "Percent": 19.6}, {"Name": "nickel", "Percent": 14.8}],
            "Rings": [{"Name": "Alpha 1 a A Ring", "RingClass": "eRingClass_Icy", "MassMT": 1.0e9, "InnerRad": 3000000.0, "OuterRad": 5000000.0}]
            """)).For(SystemAddress, 5)!;

        Assert.Equal([("Planet", 4), ("Star", 2), ("Null", 1), ("Null", 0)], body.Parents);
        Assert.Equal(123456.5, body.SemiMajorAxis);
        Assert.Equal(0.25, body.Eccentricity);
        Assert.Equal(9000.5, body.OrbitalPeriod);
        Assert.Equal(-4000.25, body.RotationPeriod);
        Assert.Equal(-0.5, body.AxialTilt);
        Assert.Equal(2500000.0, body.Radius);
        Assert.Equal(0.07, body.MassEM);
        Assert.True(body.TidalLock);
        Assert.Equal("Terraformable", body.TerraformState);
        Assert.Equal([("iron", 19.6), ("nickel", 14.8)], body.Materials);
        Assert.Equal([("Alpha 1 a A Ring", "eRingClass_Icy", 3000000.0, 5000000.0)], body.Rings);
    }

    [Fact]
    public void AStarScanKeepsItsParentsRadiusAndBelts()
    {
        var body = BodyScans.Empty.Apply(Scan("""
            "BodyID": 1, "BodyName": "Alpha B", "StarType": "M",
            "Parents": [{"Null": 0}], "Radius": 400000000.0,
            "Rings": [{"Name": "Alpha B A Belt", "RingClass": "eRingClass_MetalRich", "MassMT": 1.0e12, "InnerRad": 1.0e9, "OuterRad": 2.0e9}]
            """)).For(SystemAddress, 1)!;

        Assert.Equal([("Null", 0)], body.Parents);
        Assert.Equal(400000000.0, body.Radius);
        Assert.Equal("Alpha B A Belt", Assert.Single(body.Rings).Name);
    }

    [Fact]
    public void ABeltClusterScanKeepsItsParents()
    {
        var body = BodyScans.Empty.Apply(Scan("""
            "BodyID": 9, "BodyName": "Alpha A Belt Cluster 1", "Parents": [{"Ring": 8}, {"Star": 0}]
            """)).For(SystemAddress, 9)!;

        Assert.Equal([("Ring", 8), ("Star", 0)], body.Parents);
    }

    [Fact]
    public void AScanWithoutTheFieldsLeavesThemNullOrEmpty()
    {
        var body = BodyScans.Empty.Apply(Scan("""
            "BodyID": 3, "BodyName": "Alpha 2"
            """)).For(SystemAddress, 3)!;

        Assert.Empty(body.Parents);
        Assert.Empty(body.Materials);
        Assert.Empty(body.Rings);
        Assert.Null(body.SemiMajorAxis);
        Assert.Null(body.Eccentricity);
        Assert.Null(body.OrbitalPeriod);
        Assert.Null(body.RotationPeriod);
        Assert.Null(body.AxialTilt);
        Assert.Null(body.Radius);
        Assert.Null(body.MassEM);
        Assert.Null(body.TerraformState);
    }

    [Fact]
    public void InSystemListsOnlyThatSystemsBodies()
    {
        var scans = BodyScans.Empty
            .Apply(Scan("\"BodyID\": 1, \"BodyName\": \"A\""))
            .Apply(Scan("\"BodyID\": 2, \"BodyName\": \"B\""))
            .Apply(Scan("\"BodyID\": 1, \"BodyName\": \"Other\"", system: SystemAddress + 1));

        Assert.Equal(["A", "B"], scans.InSystem(SystemAddress).Select(body => body.BodyName).Order());
        Assert.Equal(["Other"], scans.InSystem(SystemAddress + 1).Select(body => body.BodyName));
        Assert.Empty(scans.InSystem(SystemAddress + 2));
    }

    [Fact]
    public void ARescanReplacesTheseFieldsAndKeepsTheFootfall()
    {
        var scans = BodyScans.Empty
            .Apply(Scan("\"BodyID\": 5, \"BodyName\": \"A\", \"WasFootfalled\": false, \"Radius\": 1.0, \"Materials\": [{\"Name\": \"iron\", \"Percent\": 1.0}]"))
            .Apply(Disembark(5))
            .Apply(Scan("\"BodyID\": 5, \"BodyName\": \"A\", \"WasFootfalled\": false, \"Radius\": 2.0"));

        var body = scans.For(SystemAddress, 5)!;

        Assert.Equal(2.0, body.Radius);
        Assert.Empty(body.Materials);
        Assert.NotNull(body.FootfallTakenAt);
    }

    private static JournalEvent Scan(string fields, long system = SystemAddress) =>
        Event("Scan", $"\"SystemAddress\": {system}, {fields}");

    private static JournalEvent Disembark(int bodyId) =>
        Event("Disembark", $"\"SRV\": false, \"OnPlanet\": true, \"SystemAddress\": {SystemAddress}, \"BodyID\": {bodyId}");

    private static JournalEvent Event(string kind, string fields)
    {
        var timestamp = new DateTimeOffset(3311, 4, 2, 9, 0, 0, TimeSpan.Zero);
        var text = "{\"timestamp\": \"3311-04-02T09:00:00Z\", \"event\": \"" + kind + "\", " + fields + "}";

        return new JournalEvent(timestamp, kind, JsonDocument.Parse(text).RootElement);
    }
}

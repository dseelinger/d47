using System.Globalization;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>Each orbit is placed in its parent's frame, tilted by the parent's axial tilt, and matches journal scans (#2386).</summary>
public class ABodyIsPlacedFromItsOrbitInItsParentsFrameTests
{
    private const double MetresPerAu = 149_597_870_700;
    private const double LightSecondsPerAu = 499.004783836;

    [Fact]
    public void AMoonOfAPlanetIsWhereTheJournalSawIt()
    {
        var system = System(
            Star(0),
            Planet(20, "Star", 0, "2025-07-04T23:44:54Z", 561848568916.3208, 0.005647, 0.264959, 262.407306, -128.453056, 196926832.199097, 48.922651, tilt: 0.149677),
            Planet(21, "Planet", 20, "2025-07-04T23:45:18Z", 1011486470.69931, 0.000043, -0.061074, 198.148446, -128.874581, 297786.444426, 156.335987, tilt: -0.350942, grandparents: [("Star", 0)]));

        AssertDistance(1869.848394, system, 21, from: 0, "2025-07-04T23:45:18Z");
    }

    [Fact]
    public void AMoonOfAMoonIsWhereTheJournalSawIt()
    {
        var system = System(
            Star(0),
            Planet(21, "Star", 0, "2025-08-22T01:42:34Z", 487738579511.64246, 0.006614, -0.230252, 225.521648, 157.298707, 182837986.946106, 306.860048, tilt: 1.106152),
            Planet(23, "Planet", 21, "2025-08-22T01:42:33Z", 1512013316.15448, 0.000278, -0.012965, 56.944971, -169.337561, 502424.716949, 118.934255, tilt: 1.137819, grandparents: [("Star", 0)]),
            Planet(24, "Planet", 23, "2025-08-22T01:42:33Z", 4269924.044609, 0, 71.612235, 359.802693, 128.890246, 28819.146156, 315.293567, tilt: -0.095267, grandparents: [("Planet", 21), ("Star", 0)]));

        AssertDistance(1620.075875, system, 24, from: 0, "2025-08-22T01:42:33Z");
    }

    [Fact]
    public void AMemberOfAPlanetPairIsWhereTheJournalSawIt()
    {
        var system = System(
            Star(0),
            Planet(31, "Null", 24, "2025-07-05T01:27:59Z", 6915036320.68634, 0.192454, 3.400727, 357.388129, -5.075633, 14264655.709267, 296.515488, tilt: null, grandparents: [("Star", 0)]))
            with
            {
                Barycentres =
                [
                    new BarycentreProfile
                    {
                        BodyId = 24,
                        Parents = [("Star", 0)],
                        SemiMajorAxis = 811521780490.8752 / MetresPerAu,
                        OrbitalEccentricity = 0.000031,
                        OrbitalInclination = 0.090271,
                        ArgOfPeriapsis = 202.761812,
                        AscendingNode = 23.316869,
                        OrbitalPeriod = 413825380.802155 / 86400,
                        MeanAnomaly = 218.847092,
                        MeanAnomalyAt = Time("2025-07-05T01:27:59Z"),
                    },
                ],
            };

        AssertDistance(2716.709692, system, 31, from: 0, "2025-07-05T01:27:59Z");
    }

    [Fact]
    public void ANonMemberOfARootBarycentreIsWhereTheJournalSawIt()
    {
        var system = System(
            Planet(1, "Null", 0, "2025-10-08T18:42:53Z", 639260560274.1241, 0.200785, -24.817873, 21.502815, 49.618394, 1812266409.397125, 37.225307, tilt: null, type: "Star"),
            Planet(2, "Null", 0, "2025-10-08T20:17:49Z", 1215848743915.5579, 0.200785, -24.817873, 201.5028, 49.618394, 1812266409.397125, 37.226438, tilt: null, type: "Star"),
            Planet(3, "Null", 0, "2025-10-08T20:37:36Z", 5177484750747.681, 0.272493, -7.783656, 110.905886, -126.540319, 8449730634.689331, 107.944042, tilt: null));

        var nodes = Orrery.Nodes(system);
        var root = nodes.Single(node => node.IsRoot);

        Assert.Equal((OrbitNodeKind.Barycentre, 0), (root.Kind, root.BodyId));
        Assert.Equal([1, 2], root.Members.Select(member => member.BodyId));
        AssertDistance(19958.694101, system, 3, from: 1, "2025-10-08T20:37:36Z");
    }

    [Fact]
    public void ACircularFlatOrbitTurnsAQuarterInAQuarterPeriod()
    {
        var start = Time("2026-01-01T00:00:00Z");
        var system = System(Star(0), Flat(1, "Star", 0, start, au: 2, days: 100, meanAnomaly: 0, periapsis: 0));
        var node = Orrery.Nodes(system).Single(node => node.BodyId == 1);

        AssertNear(new Vector3d(2, 0, 0), Orrery.Offset(node, start));
        AssertNear(new Vector3d(0, 2, 0), Orrery.Offset(node, start.AddDays(25)));
        AssertNear(new Vector3d(2, 0, 0), Orrery.Offset(node, start.AddDays(100)));
    }

    [Fact]
    public void TheMembersOfABarycentreLieOnOppositeSidesOfIt()
    {
        var start = Time("2026-01-01T00:00:00Z");
        var system = System(
            Flat(1, "Null", 0, start, au: 1, days: 40, meanAnomaly: 75, periapsis: 30, eccentricity: 0.3),
            Flat(2, "Null", 0, start, au: 3, days: 40, meanAnomaly: 75, periapsis: 210, eccentricity: 0.3));
        var nodes = Orrery.Nodes(system);

        foreach (var days in new[] { 0.0, 7.5, 19, 33.3 })
        {
            var one = Orrery.Offset(nodes.Single(node => node.BodyId == 1), start.AddDays(days))!.Value;
            var two = Orrery.Offset(nodes.Single(node => node.BodyId == 2), start.AddDays(days))!.Value;
            var cross = new Vector3d((one.Y * two.Z) - (one.Z * two.Y), (one.Z * two.X) - (one.X * two.Z), (one.X * two.Y) - (one.Y * two.X));

            Assert.True(cross.Length < 1e-9 * one.Length * two.Length, $"Not on one line at day {days}");
            Assert.True((one.X * two.X) + (one.Y * two.Y) + (one.Z * two.Z) < 0, $"On the same side at day {days}");
        }
    }

    [Fact]
    public void ABodyUnderABarycentreWithNoRecordAndAParentHasAnOffsetButNoPosition()
    {
        var start = Time("2026-01-01T00:00:00Z");
        var system = System(
            Star(0),
            Flat(5, "Null", 4, start, au: 1, days: 40, meanAnomaly: 10, periapsis: 0, grandparents: [("Star", 0)]));
        var node = Orrery.Nodes(system).Single(node => node.BodyId == 5);

        Assert.NotNull(Orrery.Offset(node, start));
        Assert.Null(Orrery.Position(node, start));
    }

    [Fact]
    public void AMoonOfAPlanetWithNoTiltHasAnOffsetButNoPosition()
    {
        var start = Time("2026-01-01T00:00:00Z");
        var system = System(
            Star(0),
            Flat(1, "Star", 0, start, au: 5, days: 900, meanAnomaly: 10, periapsis: 0),
            Flat(2, "Planet", 1, start, au: 0.01, days: 3, meanAnomaly: 10, periapsis: 0, grandparents: [("Star", 0)]));
        var nodes = Orrery.Nodes(system);

        Assert.NotNull(Orrery.Position(nodes.Single(node => node.BodyId == 1), start));
        Assert.NotNull(Orrery.Offset(nodes.Single(node => node.BodyId == 2), start));
        Assert.Null(Orrery.Position(nodes.Single(node => node.BodyId == 2), start));
    }

    [Fact]
    public void ABodyWithNoParentsChainBesideTheRootIsNotPlaced()
    {
        var start = Time("2026-01-01T00:00:00Z");
        var system = System(
            Star(0),
            Flat(1, "Star", 0, start, au: 5, days: 900, meanAnomaly: 10, periapsis: 0),
            Star(7));
        var nodes = Orrery.Nodes(system);

        Assert.Equal([0], nodes.Where(node => node.IsRoot).Select(node => node.BodyId));
        Assert.Null(Orrery.Position(nodes.Single(node => node.BodyId == 7), start));
    }

    [Fact]
    public void APathRunsOnceRoundTheOrbitInTheOffsetsPlane()
    {
        var when = Time("2025-07-04T23:45:18Z");
        var system = System(
            Star(0),
            Planet(20, "Star", 0, "2025-07-04T23:44:54Z", 561848568916.3208, 0.005647, 0.264959, 262.407306, -128.453056, 196926832.199097, 48.922651, tilt: 0.149677),
            Planet(21, "Planet", 20, "2025-07-04T23:45:18Z", 1011486470.69931, 0.000043, -0.061074, 198.148446, -128.874581, 297786.444426, 156.335987, tilt: -0.350942, grandparents: [("Star", 0)]));
        var moon = Orrery.Nodes(system).Single(node => node.BodyId == 21);

        var path = Orrery.Path(moon, 64);
        var offset = Orrery.Offset(moon, when)!.Value;
        var (p, r) = (path[0], path[16]);
        var normal = new Vector3d((p.Y * r.Z) - (p.Z * r.Y), (p.Z * r.X) - (p.X * r.Z), (p.X * r.Y) - (p.Y * r.X));

        Assert.Equal(64, path.Count);
        Assert.True(Math.Abs((offset.X * normal.X) + (offset.Y * normal.Y) + (offset.Z * normal.Z)) < 1e-9 * offset.Length * normal.Length);
        Assert.Equal(offset.Length, path.Select(point => point.Length).Min(), offset.Length * 1e-3);
    }

    private static void AssertDistance(double lightSeconds, StarSystemProfile system, int bodyId, int from, string when)
    {
        var nodes = Orrery.Nodes(system);
        var body = Orrery.Position(nodes.Single(node => node.BodyId == bodyId), Time(when));
        var origin = Orrery.Position(nodes.Single(node => node.BodyId == from), Time(when));

        Assert.NotNull(body);
        Assert.NotNull(origin);
        Assert.Equal(lightSeconds, (body.Value - origin.Value).Length * LightSecondsPerAu, 0.01);
    }

    private static void AssertNear(Vector3d expected, Vector3d? actual)
    {
        Assert.NotNull(actual);
        Assert.True((expected - actual.Value).Length < 1e-9, $"Expected {expected}, got {actual}");
    }

    private static StarSystemProfile System(params BodyProfile[] bodies) =>
        new() { Name = "Orrery Test", SystemAddress = 1, Bodies = bodies };

    private static BodyProfile Star(int id) => new() { BodyId = id, Name = $"Star {id}", Type = "Star" };

    private static BodyProfile Planet(
        int id,
        string parentKind,
        int parentId,
        string time,
        double metres,
        double eccentricity,
        double inclination,
        double periapsis,
        double ascendingNode,
        double seconds,
        double meanAnomaly,
        double? tilt,
        (string Kind, int BodyId)[]? grandparents = null,
        string type = "Planet") =>
        new()
        {
            BodyId = id,
            Name = $"Body {id}",
            Type = type,
            Parents = [(parentKind, parentId), .. grandparents ?? []],
            SemiMajorAxis = metres / MetresPerAu,
            OrbitalEccentricity = eccentricity,
            OrbitalInclination = inclination,
            ArgOfPeriapsis = periapsis,
            AscendingNode = ascendingNode,
            OrbitalPeriod = seconds / 86400,
            MeanAnomaly = meanAnomaly,
            MeanAnomalyAt = Time(time),
            AxialTilt = tilt * 180 / Math.PI,
        };

    private static BodyProfile Flat(
        int id,
        string parentKind,
        int parentId,
        DateTimeOffset epoch,
        double au,
        double days,
        double meanAnomaly,
        double periapsis,
        double eccentricity = 0,
        (string Kind, int BodyId)[]? grandparents = null) =>
        new()
        {
            BodyId = id,
            Name = $"Body {id}",
            Type = "Planet",
            Parents = [(parentKind, parentId), .. grandparents ?? []],
            SemiMajorAxis = au,
            OrbitalEccentricity = eccentricity,
            OrbitalInclination = 0,
            ArgOfPeriapsis = periapsis,
            AscendingNode = 0,
            OrbitalPeriod = days,
            MeanAnomaly = meanAnomaly,
            MeanAnomalyAt = epoch,
        };

    private static DateTimeOffset Time(string text) => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture);
}

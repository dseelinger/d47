using Avalonia;
using D47.App.Panel;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Orbits plot maps markers and paths through the same compression, so a marker stays on its path; its figures read as designed (#2390).</summary>
public sealed class AnOrbitMarkerStaysOnItsDrawnPathTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    private const double Radius = OrbitPlot.MostWidth / 2 * OrbitPlot.Reach;

    internal sealed class FakeClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public int Reads { get; private set; }

        public override DateTimeOffset GetUtcNow()
        {
            Reads++;
            return _now;
        }

        public void Advance(TimeSpan by) => _now += by;
    }

    [Fact]
    public void OverOneOrbitEveryMarkerStaysOnItsPath()
    {
        var system = new StarSystemProfile
        {
            Name = "Orbit Test",
            SystemAddress = 1,
            Bodies =
            [
                new BodyProfile { BodyId = 0, Name = "Orbit Test", Type = "Star" },
                Planet(1, au: 0.4, eccentricity: 0.3, inclination: 20, days: 60),
                Planet(2, au: 3, eccentricity: 0.1, inclination: 5, days: 400),
                Planet(3, au: 1.2, eccentricity: 0.05, inclination: 80, days: 150),
            ],
        };
        var nodes = Orrery.Nodes(system);
        var level = OrbitLevel.Of(nodes, nodes.Single(node => node.IsRoot), Start);
        var time = new FakeClock(Start);
        var clock = new OrbitClock(time, speed: 4);

        Assert.Equal(3, level.Placed.Count);

        for (var step = 0; step <= 120; step++)
        {
            var now = clock.Now();

            for (var i = 0; i < level.Placed.Count; i++)
            {
                var marker = level.Map(Orrery.Offset(level.Placed[i], now)!.Value, Radius);
                var path = level.Paths[i].Select(point => level.Map(point, Radius)).ToList();

                Assert.True(
                    FromPath(marker, path) < 1,
                    $"Body {level.Placed[i].BodyId} is {FromPath(marker, path):0.00} px off its path at {now:O}.");
            }

            time.Advance(TimeSpan.FromSeconds(400 / 365.25 / 120));
        }
    }

    [Fact]
    public void TheFiguresReadAsDesigned()
    {
        Assert.Equal("0.314 AU", OrbitLevel.DistanceText(0.314247666));
        Assert.Equal("12.436 AU", OrbitLevel.DistanceText(12.435919563));
        Assert.Equal("0.00110 AU", OrbitLevel.DistanceText(0.001097552));
        Assert.Equal("1,094.04 d", OrbitLevel.PeriodText(1094.036639013));
        Assert.Equal("70.71 d", OrbitLevel.PeriodText(70.713534951));
        Assert.Equal("1 AU", OrbitLevel.RingLabel(1));
        Assert.Equal("10 AU", OrbitLevel.RingLabel(10));
        Assert.Equal("0.01 AU", OrbitLevel.RingLabel(0.01));
    }

    [Fact]
    public void ARingIsDrawnAtEachPowerOfTenBetweenTheNearestAndFarthest()
    {
        var system = new StarSystemProfile
        {
            Name = "Orbit Test",
            SystemAddress = 1,
            Bodies =
            [
                new BodyProfile { BodyId = 0, Name = "Orbit Test", Type = "Star" },
                Planet(1, au: 0.05, eccentricity: 0, inclination: 0, days: 5),
                Planet(2, au: 12, eccentricity: 0, inclination: 0, days: 15000),
            ],
        };
        var nodes = Orrery.Nodes(system);
        var level = OrbitLevel.Of(nodes, nodes.Single(node => node.IsRoot), Start);

        Assert.Equal([0.1, 1, 10], level.Rings.Select(ring => Math.Round(ring, 6)));
        Assert.Equal(Radius, level.Scaled(12, Radius), 6);
        Assert.Equal(Radius / (Math.Log(12 / 0.05) + 1), level.Scaled(0.05, Radius), 6);
    }

    [Fact]
    public void PausedTheClockStandsStillAndReadsNoTime()
    {
        var time = new FakeClock(Start);
        var clock = new OrbitClock(time, speed: 1);

        time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(Start.AddDays(2), clock.Now());

        clock.Pause();
        var reads = time.Reads;
        time.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal(Start.AddDays(2), clock.Now());
        Assert.Equal(reads, time.Reads);

        clock.Run();
        clock.SetSpeed(2);
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(Start.AddDays(9), clock.Now());

        clock.Present();
        Assert.Equal(Start.AddSeconds(8), clock.Now());
    }

    private static double FromPath(Point marker, IReadOnlyList<Point> path)
    {
        var nearest = double.MaxValue;

        for (var i = 0; i < path.Count; i++)
        {
            var (a, b) = (path[i], path[(i + 1) % path.Count]);
            var along = b - a;
            var span = (along.X * along.X) + (along.Y * along.Y);
            var t = span == 0 ? 0 : Math.Clamp((((marker.X - a.X) * along.X) + ((marker.Y - a.Y) * along.Y)) / span, 0, 1);
            var foot = new Point(a.X + (t * along.X), a.Y + (t * along.Y));
            var gap = marker - foot;

            nearest = Math.Min(nearest, Math.Sqrt((gap.X * gap.X) + (gap.Y * gap.Y)));
        }

        return nearest;
    }

    private static BodyProfile Planet(int id, double au, double eccentricity, double inclination, double days) =>
        new()
        {
            BodyId = id,
            Name = $"Orbit Test {id}",
            Type = "Planet",
            SubType = "Rocky body",
            Parents = [("Star", 0)],
            SemiMajorAxis = au,
            OrbitalEccentricity = eccentricity,
            OrbitalInclination = inclination,
            ArgOfPeriapsis = 40 * id,
            AscendingNode = 70 * id,
            OrbitalPeriod = days,
            MeanAnomaly = 10 * id,
            MeanAnomalyAt = Start,
        };
}

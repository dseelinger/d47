using System.Globalization;
using Avalonia;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>One level of the System page's Orbits section: a focus and the nodes whose nearest parent it is.</summary>
internal sealed class OrbitLevel
{
    /// <summary>Points sampled round each drawn path.</summary>
    public const int PathSamples = 180;

    /// <summary>The smallest mapped radius, as a fraction of the plot's, so an orbit seen edge-on does not pass through the centre.</summary>
    public const double Floor = 0.03;

    private OrbitLevel(OrbitNode focus, IReadOnlyList<OrbitNode> placed, IReadOnlyList<OrbitNode> unplaced)
    {
        Focus = focus;
        Placed = placed;
        Unplaced = unplaced;
        Paths = [.. placed.Select(node => Orrery.Path(node, PathSamples))];

        if (placed.Count > 0)
        {
            Nearest = placed.Min(node => SemiMajorAxis(node) * (1 - Eccentricity(node)));
            Farthest = Math.Max(Nearest, placed.Max(node => SemiMajorAxis(node) * (1 + Eccentricity(node))));
        }
    }

    public OrbitNode Focus { get; }

    /// <summary>The children <see cref="Orrery.Offset"/> places, nearest semi-major axis first.</summary>
    public IReadOnlyList<OrbitNode> Placed { get; }

    /// <summary>The children <see cref="Orrery.Offset"/> cannot place.</summary>
    public IReadOnlyList<OrbitNode> Unplaced { get; }

    /// <summary>Each placed child's path, in the order of <see cref="Placed"/>.</summary>
    public IReadOnlyList<IReadOnlyList<Vector3d>> Paths { get; }

    /// <summary>The smallest periapsis among the placed children, in AU.</summary>
    public double Nearest { get; }

    /// <summary>The largest apoapsis among the placed children, in AU.</summary>
    public double Farthest { get; }

    /// <summary>The speed step nearest the shortest child period over 20 seconds; 1 day per second with no child placed.</summary>
    public int DefaultSpeed
    {
        get
        {
            if (Placed.Count == 0)
            {
                return 1;
            }

            var wanted = Placed.Min(Period) / 20;
            var best = 0;

            for (var step = 1; step < OrbitClock.Speeds.Count; step++)
            {
                if (Math.Abs(OrbitClock.Speeds[step].DaysPerSecond - wanted) < Math.Abs(OrbitClock.Speeds[best].DaysPerSecond - wanted))
                {
                    best = step;
                }
            }

            return best;
        }
    }

    /// <summary>Each power of ten AU from <see cref="Nearest"/> to <see cref="Farthest"/>.</summary>
    public IReadOnlyList<double> Rings
    {
        get
        {
            if (Placed.Count == 0)
            {
                return [];
            }

            var rings = new List<double>();

            for (var power = (int)Math.Ceiling(Math.Log10(Nearest)); Math.Pow(10, power) <= Farthest; power++)
            {
                rings.Add(Math.Pow(10, power));
            }

            return rings;
        }
    }

    public static OrbitLevel Of(IReadOnlyList<OrbitNode> nodes, OrbitNode focus, DateTimeOffset when)
    {
        var children = nodes.Where(node => node.Parent == focus).ToList();

        return new OrbitLevel(
            focus,
            [.. children.Where(node => Orrery.Offset(node, when) is not null).OrderBy(SemiMajorAxis).ThenBy(node => node.BodyId)],
            [.. children.Where(node => Orrery.Offset(node, when) is null)]);
    }

    /// <summary>The radius <paramref name="au"/> is drawn at on a plot whose outermost apoapsis is at <paramref name="radius"/>.</summary>
    public double Scaled(double au, double radius) =>
        radius * Math.Max(Floor, (Math.Log(au / Nearest) + 1) / (Math.Log(Farthest / Nearest) + 1));

    /// <summary>Where an offset is drawn from the centre: x and y seen from above, z dropped, screen y down.</summary>
    public Point Map(Vector3d offset, double radius)
    {
        var across = Math.Sqrt((offset.X * offset.X) + (offset.Y * offset.Y));

        if (across == 0 || Placed.Count == 0)
        {
            return default;
        }

        var scaled = Scaled(across, radius);

        return new Point(offset.X / across * scaled, -offset.Y / across * scaled);
    }

    /// <summary>"1 AU", "0.01 AU".</summary>
    public static string RingLabel(double au)
    {
        var power = (int)Math.Round(Math.Log10(au));

        return power >= 0
            ? au.ToString("0", CultureInfo.InvariantCulture) + " AU"
            : au.ToString("F" + (-power).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) + " AU";
    }

    /// <summary>"0.314 AU"; five decimals below 0.01 AU.</summary>
    public static string DistanceText(double au) =>
        au.ToString(au < 0.01 ? "N5" : "N3", CultureInfo.InvariantCulture) + " AU";

    /// <summary>"1,094.04 d".</summary>
    public static string PeriodText(double days) => days.ToString("N2", CultureInfo.InvariantCulture) + " d";

    /// <summary>The body's name, or "Barycentre 14".</summary>
    public static string Name(OrbitNode node) =>
        node.Body?.Name ?? $"Barycentre {node.BodyId.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>The body's name with the system's taken off, "9 a"; a barycentre's body ID.</summary>
    public static string ShortName(OrbitNode node, string system)
    {
        if (node.Body is not { } body)
        {
            return node.BodyId.ToString(CultureInfo.InvariantCulture);
        }

        return body.Name.StartsWith(system + " ", StringComparison.OrdinalIgnoreCase) && body.Name.Length > system.Length + 1
            ? body.Name[(system.Length + 1)..]
            : body.Name;
    }

    /// <summary>"Class III gas giant", or "Barycentre".</summary>
    public static string Kind(OrbitNode node) =>
        node.Body is { } body ? body.SubType ?? body.Type : "Barycentre";

    /// <summary>"2 in orbit".</summary>
    public static string InOrbit(int count) => $"{count.ToString(CultureInfo.InvariantCulture)} in orbit";

    public static double SemiMajorAxis(OrbitNode node) =>
        node.Body?.SemiMajorAxis ?? node.Barycentre?.SemiMajorAxis ?? 0;

    public static double Period(OrbitNode node) =>
        node.Body?.OrbitalPeriod ?? node.Barycentre?.OrbitalPeriod ?? 0;

    private static double Eccentricity(OrbitNode node) =>
        node.Body?.OrbitalEccentricity ?? node.Barycentre?.OrbitalEccentricity ?? 0;
}

/// <summary>The Orbits section's simulated time: the time at the last start or NOW, plus the speed times the time since.</summary>
internal sealed class OrbitClock
{
    public static readonly IReadOnlyList<(double DaysPerSecond, string Label)> Speeds =
    [
        (1.0 / 24, "1 hour per second"),
        (1, "1 day per second"),
        (7, "1 week per second"),
        (30, "30 days per second"),
        (365.25, "1 year per second"),
    ];

    private readonly TimeProvider _time;
    private DateTimeOffset _simulated;
    private DateTimeOffset _since;

    /// <summary>Running from the present at <paramref name="speed"/>.</summary>
    public OrbitClock(TimeProvider time, int speed)
    {
        _time = time;
        Speed = speed;
        Running = true;
        _simulated = _since = time.GetUtcNow();
    }

    public bool Running { get; private set; }

    /// <summary>An index into <see cref="Speeds"/>.</summary>
    public int Speed { get; private set; }

    /// <summary>The simulated time; paused, it reads no clock.</summary>
    public DateTimeOffset Now() =>
        Running ? _simulated + TimeSpan.FromDays((_time.GetUtcNow() - _since).TotalSeconds * Speeds[Speed].DaysPerSecond) : _simulated;

    public void Pause()
    {
        if (Running)
        {
            _simulated = Now();
            Running = false;
        }
    }

    public void Run()
    {
        if (!Running)
        {
            _since = _time.GetUtcNow();
            Running = true;
        }
    }

    public void SetSpeed(int speed)
    {
        if (Running)
        {
            _simulated = Now();
            _since = _time.GetUtcNow();
        }

        Speed = Math.Clamp(speed, 0, Speeds.Count - 1);
    }

    /// <summary>Back to the present, running or paused as it was.</summary>
    public void Present() => _simulated = _since = _time.GetUtcNow();
}

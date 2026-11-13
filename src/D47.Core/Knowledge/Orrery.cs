namespace D47.Core.Knowledge;

/// <summary>A point or direction in AU.</summary>
public readonly record struct Vector3d(double X, double Y, double Z)
{
    public double Length => Math.Sqrt((X * X) + (Y * Y) + (Z * Z));

    public static Vector3d operator +(Vector3d a, Vector3d b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vector3d operator -(Vector3d a, Vector3d b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
}

public enum OrbitNodeKind
{
    Star,
    Planet,
    Barycentre,
}

/// <summary>A star, planet or barycentre of one system, linked to its parent and, for a barycentre, its members.</summary>
public sealed class OrbitNode
{
    private readonly List<OrbitNode> members = [];

    internal OrbitNode(int bodyId, OrbitNodeKind kind, BodyProfile? body, BarycentreProfile? barycentre, IReadOnlyList<(string Kind, int BodyId)> parents)
    {
        BodyId = bodyId;
        Kind = kind;
        Body = body;
        Barycentre = barycentre;
        ParentId = parents.Count > 0 ? parents[0].BodyId : null;
        AxialTilt = body?.AxialTilt;
        SemiMajorAxis = body?.SemiMajorAxis ?? barycentre?.SemiMajorAxis;
        Eccentricity = body?.OrbitalEccentricity ?? barycentre?.OrbitalEccentricity;
        Inclination = body?.OrbitalInclination ?? barycentre?.OrbitalInclination;
        ArgOfPeriapsis = body?.ArgOfPeriapsis ?? barycentre?.ArgOfPeriapsis;
        AscendingNode = body?.AscendingNode ?? barycentre?.AscendingNode;
        OrbitalPeriod = body?.OrbitalPeriod ?? barycentre?.OrbitalPeriod;
        MeanAnomaly = body?.MeanAnomaly ?? barycentre?.MeanAnomaly;
        MeanAnomalyAt = body?.MeanAnomalyAt ?? barycentre?.MeanAnomalyAt;
    }

    public int BodyId { get; }

    public OrbitNodeKind Kind { get; }

    /// <summary>The star or planet; null for a barycentre.</summary>
    public BodyProfile? Body { get; }

    /// <summary>The barycentre's record; null for a star, a planet, or a barycentre named only in a parents chain.</summary>
    public BarycentreProfile? Barycentre { get; }

    /// <summary>The first entry of the parents chain, barycentres included; null for a node with no chain.</summary>
    public int? ParentId { get; }

    /// <summary>Null when the parent has no node.</summary>
    public OrbitNode? Parent { get; internal set; }

    /// <summary>Whether this is the node every other is placed from.</summary>
    public bool IsRoot { get; internal set; }

    /// <summary>A barycentre's two members, the lower body ID first; empty for a star or planet.</summary>
    public IReadOnlyList<OrbitNode> Members => members;

    /// <summary>Whether this is one of its barycentre parent's members.</summary>
    public bool IsMember => Parent is { } parent && parent.members.Contains(this);

    /// <summary>In degrees; null for a barycentre.</summary>
    internal double? AxialTilt { get; }

    internal double? SemiMajorAxis { get; }

    internal double? Eccentricity { get; }

    internal double? Inclination { get; }

    internal double? ArgOfPeriapsis { get; }

    internal double? AscendingNode { get; }

    internal double? OrbitalPeriod { get; }

    internal double? MeanAnomaly { get; }

    internal DateTimeOffset? MeanAnomalyAt { get; }

    internal void AddMember(OrbitNode member) => members.Add(member);

    public override string ToString() => $"{Kind} {BodyId}";
}

/// <summary>Places a system's stars, planets and barycentres from their orbits, each orbit in its parent's frame.</summary>
public static class Orrery
{
    private const double Tolerance = 1e-12;
    private const int MostNewtonSteps = 100;

    /// <summary>Every star, planet and barycentre with its parent, members and elements, by body ID.</summary>
    public static IReadOnlyList<OrbitNode> Nodes(StarSystemProfile system)
    {
        var nodes = new Dictionary<int, OrbitNode>();

        foreach (var body in system.Bodies)
        {
            var kind = body.Type == "Star" ? OrbitNodeKind.Star : OrbitNodeKind.Planet;
            nodes.TryAdd(body.BodyId, new OrbitNode(body.BodyId, kind, body, null, body.Parents));
        }

        foreach (var barycentre in system.Barycentres)
        {
            nodes.TryAdd(barycentre.BodyId, new OrbitNode(barycentre.BodyId, OrbitNodeKind.Barycentre, null, barycentre, barycentre.Parents));
        }

        var chains = system.Bodies.Select(body => body.Parents)
            .Concat(system.Barycentres.Select(barycentre => barycentre.Parents))
            .Where(chain => chain.Count > 0)
            .ToList();

        foreach (var chain in chains)
        {
            for (var at = 0; at < chain.Count; at++)
            {
                if (chain[at].Kind == "Null" && !nodes.ContainsKey(chain[at].BodyId))
                {
                    nodes.Add(chain[at].BodyId, new OrbitNode(chain[at].BodyId, OrbitNodeKind.Barycentre, null, null, [.. chain.Skip(at + 1)]));
                }
            }
        }

        foreach (var node in nodes.Values)
        {
            node.Parent = node.ParentId is { } parentId ? nodes.GetValueOrDefault(parentId) : null;
        }

        foreach (var barycentre in nodes.Values.Where(node => node.Kind == OrbitNodeKind.Barycentre))
        {
            foreach (var member in nodes.Values.Where(node => node.Parent == barycentre).OrderBy(node => node.BodyId).Take(2))
            {
                barycentre.AddMember(member);
            }
        }

        if (Root(nodes, chains) is { } root)
        {
            root.IsRoot = true;
        }

        return [.. nodes.Values.OrderBy(node => node.BodyId)];
    }

    /// <summary>The node's offset from its parent, in AU, in the parent's plane; null when it cannot be placed.</summary>
    public static Vector3d? Offset(OrbitNode node, DateTimeOffset when) =>
        node.ParentId is null || node.IsRoot || Perifocal(node, when) is not { } q || PlaneOf(node) is not { } plane
            ? null
            : plane.Times(q);

    /// <summary>The node's position from the root, in AU; null when any ancestor cannot be placed.</summary>
    public static Vector3d? Position(OrbitNode node, DateTimeOffset when) => Place(node, when, depth: 0)?.Position;

    /// <summary>The node's orbit as <paramref name="samples"/> offsets at evenly spaced eccentric anomalies, in the same plane as <see cref="Offset"/>.</summary>
    public static IReadOnlyList<Vector3d> Path(OrbitNode node, int samples)
    {
        if (samples <= 0 || node.ParentId is null || node.IsRoot || PlaneOf(node) is not { } plane)
        {
            return [];
        }

        if (node.SemiMajorAxis is not { } a || node.Eccentricity is not { } e || !IsBound(e))
        {
            return [];
        }

        return [.. Enumerable.Range(0, samples).Select(k => plane.Times(Perifocal(a, e, 2 * Math.PI * k / samples)))];
    }

    private static OrbitNode? Root(Dictionary<int, OrbitNode> nodes, List<IReadOnlyList<(string Kind, int BodyId)>> chains)
    {
        if (chains.Count > 0)
        {
            var tops = chains.Select(chain => chain[^1].BodyId).Distinct().ToList();
            return tops.Count == 1 && nodes.GetValueOrDefault(tops[0]) is { ParentId: null } top ? top : null;
        }

        var parentless = nodes.Values.Where(node => node.ParentId is null).ToList();
        return parentless.Count == 1 ? parentless[0] : null;
    }

    private static (Vector3d Position, Matrix? ChildFrame)? Place(OrbitNode node, DateTimeOffset when, int depth)
    {
        if (node.IsRoot)
        {
            var rootFrame = node.Kind == OrbitNodeKind.Barycentre ? Matrix.Identity : Matrix.Rx(Radians(node.AxialTilt ?? 0));
            return (new Vector3d(0, 0, 0), rootFrame);
        }

        if (depth > 64 || node.Parent is not { } parent || Place(parent, when, depth + 1) is not { ChildFrame: { } parentFrame } placed)
        {
            return null;
        }

        if (PlaneOf(node) is not { } plane || Perifocal(node, when) is not { } q)
        {
            return null;
        }

        var frame = parentFrame.Times(plane);
        var position = placed.Position + frame.Times(q);

        Matrix? childFrame = node.Kind == OrbitNodeKind.Barycentre
            ? frame
            : node.AxialTilt is { } tilt ? frame.Times(Matrix.Rx(Radians(tilt))) : null;

        return (position, childFrame);
    }

    /// <summary>The node's own rotation, preceded by its barycentre's first member's when it is not a member.</summary>
    private static Matrix? PlaneOf(OrbitNode node)
    {
        if (Rotation(node) is not { } own)
        {
            return null;
        }

        if (node.Parent is not { Kind: OrbitNodeKind.Barycentre } barycentre || node.IsMember)
        {
            return own;
        }

        return barycentre.Members.Count > 0 && Rotation(barycentre.Members[0]) is { } first ? first.Times(own) : null;
    }

    private static Matrix? Rotation(OrbitNode node) =>
        node.AscendingNode is { } ascending && node.Inclination is { } inclination && node.ArgOfPeriapsis is { } periapsis
            ? Matrix.Rz(-Radians(ascending)).Times(Matrix.Rx(Radians(inclination))).Times(Matrix.Rz(-Radians(periapsis)))
            : null;

    private static Vector3d? Perifocal(OrbitNode node, DateTimeOffset when)
    {
        if (node.SemiMajorAxis is not { } a || node.Eccentricity is not { } e || !IsBound(e)
            || node.OrbitalPeriod is not { } period || period <= 0
            || node.MeanAnomaly is not { } meanAnomaly || node.MeanAnomalyAt is not { } epoch)
        {
            return null;
        }

        var degrees = meanAnomaly + (360 * (when - epoch).TotalDays / period);
        var m = Radians(((degrees % 360) + 360) % 360);
        return Perifocal(a, e, EccentricAnomaly(m, e));
    }

    private static Vector3d Perifocal(double a, double e, double eccentricAnomaly) =>
        new(a * (Math.Cos(eccentricAnomaly) - e), a * Math.Sqrt(1 - (e * e)) * Math.Sin(eccentricAnomaly), 0);

    private static double EccentricAnomaly(double m, double e)
    {
        var anomaly = e > 0.8 ? Math.PI : m;

        for (var step = 0; step < MostNewtonSteps; step++)
        {
            var change = (anomaly - (e * Math.Sin(anomaly)) - m) / (1 - (e * Math.Cos(anomaly)));
            anomaly -= change;

            if (Math.Abs(change) < Tolerance)
            {
                break;
            }
        }

        return anomaly;
    }

    private static bool IsBound(double e) => e is >= 0 and < 1;

    private static double Radians(double degrees) => degrees * Math.PI / 180;

    private readonly record struct Matrix(double M11, double M12, double M13, double M21, double M22, double M23, double M31, double M32, double M33)
    {
        public static Matrix Identity { get; } = new(1, 0, 0, 0, 1, 0, 0, 0, 1);

        public static Matrix Rz(double a) => new(Math.Cos(a), -Math.Sin(a), 0, Math.Sin(a), Math.Cos(a), 0, 0, 0, 1);

        public static Matrix Rx(double a) => new(1, 0, 0, 0, Math.Cos(a), -Math.Sin(a), 0, Math.Sin(a), Math.Cos(a));

        public Matrix Times(Matrix o) => new(
            (M11 * o.M11) + (M12 * o.M21) + (M13 * o.M31),
            (M11 * o.M12) + (M12 * o.M22) + (M13 * o.M32),
            (M11 * o.M13) + (M12 * o.M23) + (M13 * o.M33),
            (M21 * o.M11) + (M22 * o.M21) + (M23 * o.M31),
            (M21 * o.M12) + (M22 * o.M22) + (M23 * o.M32),
            (M21 * o.M13) + (M22 * o.M23) + (M23 * o.M33),
            (M31 * o.M11) + (M32 * o.M21) + (M33 * o.M31),
            (M31 * o.M12) + (M32 * o.M22) + (M33 * o.M32),
            (M31 * o.M13) + (M32 * o.M23) + (M33 * o.M33));

        public Vector3d Times(Vector3d v) => new(
            (M11 * v.X) + (M12 * v.Y) + (M13 * v.Z),
            (M21 * v.X) + (M22 * v.Y) + (M23 * v.Z),
            (M31 * v.X) + (M32 * v.Y) + (M33 * v.Z));
    }
}

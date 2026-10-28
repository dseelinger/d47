using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>An orthographic window on the galaxy, turned by <see cref="Heading"/> and tilted by <see cref="Tilt"/> about its centre; at 0 and 0 it looks straight down with galactic X across and Z up.</summary>
public sealed record GalaxyViewport(double CentreX, double CentreZ, double LightYearsAcross)
{
    public const double AngleStep = 15;
    public const double DegreesPerPixel = 0.4;

    private static readonly double[] Steps = [100, 400, 1_600, 6_400, 25_600, 102_400];

    private const double PanFraction = 0.25;
    private const double FitMargin = 0.1;
    private const double Degrees = Math.PI / 180;

    public double CentreY { get; init; }

    /// <summary>Degrees in [0, 360): the direction in the galactic plane that screen-up faces, from +Z towards +X.</summary>
    public double Heading { get; init; }

    /// <summary>Degrees in [0, 90]: 0 looks straight down on the plane, 90 looks along it.</summary>
    public double Tilt { get; init; }

    public static GalaxyViewport WholeGalaxy
    {
        get
        {
            var extent = GalacticRegions.Extent;

            return new((extent.X0 + extent.X1) / 2, (extent.Z0 + extent.Z1) / 2, Steps[^1]);
        }
    }

    /// <summary>Centred on the position at 400 ly across.</summary>
    public static GalaxyViewport Around(StarPosition centre) => new GalaxyViewport(0, 0, Steps[1]).Centred(centre.X, centre.Y, centre.Z);

    /// <summary>The smallest step holding every point, as seen at the angles, with a tenth of the width to spare each side; null for none.</summary>
    public static GalaxyViewport? Fitting(IReadOnlyList<StarPosition> points, double heading = 0, double tilt = 0)
    {
        if (points.Count == 0)
        {
            return null;
        }

        var angled = new GalaxyViewport(0, 0, Steps[^1]) { Heading = WrapHeading(heading), Tilt = Math.Clamp(tilt, 0, 90) };
        var faces = points.Select(angled.Face).ToList();
        var minAcross = faces.Min(face => face.Across);
        var maxAcross = faces.Max(face => face.Across);
        var minUp = faces.Min(face => face.Up);
        var maxUp = faces.Max(face => face.Up);
        var span = Math.Max(maxAcross - minAcross, maxUp - minUp);
        var across = Steps.FirstOrDefault(step => span <= step * (1 - 2 * FitMargin), Steps[^1]);

        var boxX = (points.Min(point => point.X) + points.Max(point => point.X)) / 2;
        var boxY = (points.Min(point => point.Y) + points.Max(point => point.Y)) / 2;
        var boxZ = (points.Min(point => point.Z) + points.Max(point => point.Z)) / 2;
        var (r, u) = angled.Axes();
        var shiftAcross = (minAcross + maxAcross) / 2 - (boxX * r.X + boxY * r.Y + boxZ * r.Z);
        var shiftUp = (minUp + maxUp) / 2 - (boxX * u.X + boxY * u.Y + boxZ * u.Z);

        return (angled with { LightYearsAcross = across }).Centred(
            boxX + r.X * shiftAcross + u.X * shiftUp,
            boxY + r.Y * shiftAcross + u.Y * shiftUp,
            boxZ + r.Z * shiftAcross + u.Z * shiftUp);
    }

    /// <summary>Centred on the points' extent with <paramref name="inset"/> px clear each side of a <paramref name="side"/> px view; null for fewer than two points or no room.</summary>
    public static GalaxyViewport? Fitted(IReadOnlyList<StarPosition> points, double side, double inset)
    {
        if (points.Count < 2 || side <= 2 * inset)
        {
            return null;
        }

        var minX = points.Min(point => point.X);
        var maxX = points.Max(point => point.X);
        var minZ = points.Min(point => point.Z);
        var maxZ = points.Max(point => point.Z);
        var span = Math.Max(Math.Max(maxX - minX, maxZ - minZ), 1);

        return new((minX + maxX) / 2, (minZ + maxZ) / 2, span * side / (side - 2 * inset));
    }

    public GalaxyViewport ZoomIn() => this with { LightYearsAcross = Steps[Math.Max(StepIndex() - 1, 0)] };

    public GalaxyViewport ZoomOut() => this with { LightYearsAcross = Steps[Math.Min(StepIndex() + 1, Steps.Length - 1)] };

    /// <summary>Turns the drawing to the next multiple of <see cref="AngleStep"/> clockwise (1) or anticlockwise (-1) on screen.</summary>
    public GalaxyViewport Turned(int clockwise) =>
        Math.Sign(clockwise) switch
        {
            1 => this with { Heading = WrapHeading(StepDown(Heading)) },
            -1 => this with { Heading = WrapHeading(StepUp(Heading)) },
            _ => this,
        };

    /// <summary>Raises (1) or lowers (-1) the tilt to the next multiple of <see cref="AngleStep"/>, within 0 to 90.</summary>
    public GalaxyViewport Tilted(int up) =>
        Math.Sign(up) switch
        {
            1 => this with { Tilt = Math.Min(StepUp(Tilt), 90) },
            -1 => this with { Tilt = Math.Max(StepDown(Tilt), 0) },
            _ => this,
        };

    /// <summary>Turns clockwise and raises the tilt by the degrees given; heading wraps and tilt is clamped.</summary>
    public GalaxyViewport Rotated(double clockwiseDegrees, double tiltDegrees) =>
        this with { Heading = WrapHeading(Heading - clockwiseDegrees), Tilt = Math.Clamp(Tilt + tiltDegrees, 0, 90) };

    /// <summary>Moves the centre against a drag of the pixels given, so the map follows the pointer.</summary>
    public GalaxyViewport Dragged(double dx, double dy, double width, double height)
    {
        var scale = Scale(width, height);
        var (r, u) = Axes();

        return Moved(-dx / scale, dy / scale, r, u);
    }

    /// <summary>Moves a quarter of the width in each direction named: across +1 is right, up +1 is towards the top of the screen.</summary>
    public GalaxyViewport Pan(int across, int up)
    {
        var (r, u) = Axes();

        return Moved(Math.Sign(across) * LightYearsAcross * PanFraction, Math.Sign(up) * LightYearsAcross * PanFraction, r, u);
    }

    /// <summary>The position in the view's frame, in light years, before centring and scale.</summary>
    public (double Across, double Up) Face(StarPosition position)
    {
        var (r, u) = Axes();

        return (position.X * r.X + position.Y * r.Y + position.Z * r.Z, position.X * u.X + position.Y * u.Y + position.Z * u.Z);
    }

    public (double X, double Y) ToScreen(StarPosition position, double width, double height)
    {
        var scale = Scale(width, height);
        var at = Face(position);
        var centre = Face(new StarPosition(CentreX, CentreY, CentreZ));

        return (width / 2 + (at.Across - centre.Across) * scale, height / 2 - (at.Up - centre.Up) * scale);
    }

    private ((double X, double Y, double Z) R, (double X, double Y, double Z) U) Axes()
    {
        var h = Heading * Degrees;
        var t = Tilt * Degrees;

        return ((Math.Cos(h), 0, -Math.Sin(h)), (Math.Sin(h) * Math.Cos(t), Math.Sin(t), Math.Cos(h) * Math.Cos(t)));
    }

    private GalaxyViewport Moved(double across, double up, (double X, double Y, double Z) r, (double X, double Y, double Z) u) =>
        Centred(
            CentreX + r.X * across + u.X * up,
            CentreY + r.Y * across + u.Y * up,
            CentreZ + r.Z * across + u.Z * up);

    private GalaxyViewport Centred(double x, double y, double z)
    {
        var extent = GalacticRegions.Extent;
        var height = (extent.X1 - extent.X0) / 2;

        return this with
        {
            CentreX = Math.Clamp(x, extent.X0, extent.X1),
            CentreY = Math.Clamp(y, -height, height),
            CentreZ = Math.Clamp(z, extent.Z0, extent.Z1),
        };
    }

    private double Scale(double width, double height) => Math.Min(width, height) / LightYearsAcross;

    private static double WrapHeading(double degrees) => ((degrees % 360) + 360) % 360;

    private static double StepUp(double degrees) =>
        degrees % AngleStep == 0 ? degrees + AngleStep : Math.Ceiling(degrees / AngleStep) * AngleStep;

    private static double StepDown(double degrees) =>
        degrees % AngleStep == 0 ? degrees - AngleStep : Math.Floor(degrees / AngleStep) * AngleStep;

    private int StepIndex()
    {
        var best = 0;

        for (var i = 1; i < Steps.Length; i++)
        {
            if (Math.Abs(Steps[i] - LightYearsAcross) < Math.Abs(Steps[best] - LightYearsAcross))
            {
                best = i;
            }
        }

        return best;
    }
}

public static class GalaxyMapHit
{
    /// <summary>The index of the nearest point within radius pixels of the tap, or null; a tie goes to the later point.</summary>
    public static int? Nearest(IReadOnlyList<(double X, double Y)> points, (double X, double Y) tap, double radius)
    {
        int? best = null;
        var bestDistance = double.MaxValue;

        for (var i = 0; i < points.Count; i++)
        {
            var distance = Math.Sqrt(Math.Pow(points[i].X - tap.X, 2) + Math.Pow(points[i].Y - tap.Y, 2));

            if (distance <= radius && distance <= bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        return best;
    }
}

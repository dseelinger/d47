using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>A top-down window on the galaxy: galactic X across, Z up, height dropped, one scale on both axes.</summary>
public sealed record GalaxyViewport(double CentreX, double CentreZ, double LightYearsAcross)
{
    private static readonly double[] Steps = [100, 400, 1_600, 6_400, 25_600, 102_400];

    private const double PanFraction = 0.25;
    private const double FitMargin = 0.1;

    public static GalaxyViewport WholeGalaxy
    {
        get
        {
            var extent = GalacticRegions.Extent;

            return new((extent.X0 + extent.X1) / 2, (extent.Z0 + extent.Z1) / 2, Steps[^1]);
        }
    }

    /// <summary>Centred on the position at 400 ly across.</summary>
    public static GalaxyViewport Around(StarPosition centre) => Clamped(centre.X, centre.Z, Steps[1]);

    /// <summary>The smallest step holding every point with a tenth of the width to spare each side; null for none.</summary>
    public static GalaxyViewport? Fitting(IReadOnlyList<StarPosition> points)
    {
        if (points.Count == 0)
        {
            return null;
        }

        var minX = points.Min(point => point.X);
        var maxX = points.Max(point => point.X);
        var minZ = points.Min(point => point.Z);
        var maxZ = points.Max(point => point.Z);
        var span = Math.Max(maxX - minX, maxZ - minZ);
        var across = Steps.FirstOrDefault(step => span <= step * (1 - 2 * FitMargin), Steps[^1]);

        return Clamped((minX + maxX) / 2, (minZ + maxZ) / 2, across);
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

    /// <summary>Moves a quarter of the width in each direction named: across +1 is right, up +1 is towards higher Z.</summary>
    public GalaxyViewport Pan(int across, int up) =>
        Clamped(
            CentreX + Math.Sign(across) * LightYearsAcross * PanFraction,
            CentreZ + Math.Sign(up) * LightYearsAcross * PanFraction,
            LightYearsAcross);

    public (double X, double Y) ToScreen(StarPosition position, double width, double height)
    {
        var scale = Scale(width, height);

        return (width / 2 + (position.X - CentreX) * scale, height / 2 - (position.Z - CentreZ) * scale);
    }

    public (double X, double Z) ToGalaxy(double x, double y, double width, double height)
    {
        var scale = Scale(width, height);

        return (CentreX + (x - width / 2) / scale, CentreZ - (y - height / 2) / scale);
    }

    private double Scale(double width, double height) => Math.Min(width, height) / LightYearsAcross;

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

    private static GalaxyViewport Clamped(double x, double z, double across)
    {
        var extent = GalacticRegions.Extent;

        return new(Math.Clamp(x, extent.X0, extent.X1), Math.Clamp(z, extent.Z0, extent.Z1), across);
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

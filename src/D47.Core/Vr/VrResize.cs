using System.Numerics;

namespace D47.Core.Vr;

/// <summary>Which edges of a panel a resize drag is holding. A corner is two.</summary>
[Flags]
public enum VrHandle
{
    None = 0,
    Left = 1,
    Right = 2,
    Top = 4,
    Bottom = 8,
}

/// <summary>How a request to enter or leave resize mode went (#107).</summary>
public enum VrResizeOutcome
{
    On,
    Off,

    /// <summary>The motion controllers are switched off, so nothing can take hold of a handle.</summary>
    NoControllers,

    NoHeadset,
}

/// <summary>A panel's pose, its width in the room and the pixels it is rendered at.</summary>
public readonly record struct VrPanelShape(VrPose Pose, float WidthMetres, int PixelsWide, int PixelsTall)
{
    /// <summary>The quad's height, which follows from the width and the texture's aspect.</summary>
    public float HeightMetres => WidthMetres * PixelsTall / Math.Max(1, PixelsWide);
}

/// <summary>A resize drag in progress: the shape it started from, the edges held, and where the ray was.</summary>
public sealed record VrResizeGrab(VrPanelShape Start, VrHandle Handle, Vector2 From);

/// <summary>The arithmetic of resizing a headset panel by its edges (#107).</summary>
public static class VrResize
{
    /// <summary>How deep a handle band is, in metres from the edge.</summary>
    public const float HandleMetres = 0.05f;

    /// <summary>The most of a panel's width or height a band may take, so a small panel keeps a middle to press.</summary>
    public const float MostOfASide = 0.2f;

    /// <summary>Matches the clamp <see cref="SurfacePlacement.Sane"/> applies.</summary>
    public const float NarrowestMetres = 0.15f;

    public const float WidestMetres = 4f;

    public const float ShortestMetres = 0.08f;

    public const float TallestMetres = 3f;

    /// <summary>Width over height, at its most upright.</summary>
    public const float MostUpright = 0.25f;

    /// <summary>Width over height, at its widest.</summary>
    public const float MostLevel = 6f;

    public static readonly (int Width, int Height) FewestPixels = (256, 140);

    public static readonly (int Width, int Height) MostPixels = (2560, 2560);

    /// <summary>Which handle a point on the panel's face is on, in the 0..1 a ray hit answers in.</summary>
    public static VrHandle HandleAt(float u, float v, VrExtent extent)
    {
        var across = Band(extent.WidthMetres);
        var down = Band(extent.HeightMetres);
        var handle = VrHandle.None;

        if (u <= across)
        {
            handle |= VrHandle.Left;
        }
        else if (u >= 1f - across)
        {
            handle |= VrHandle.Right;
        }

        if (v <= down)
        {
            handle |= VrHandle.Top;
        }
        else if (v >= 1f - down)
        {
            handle |= VrHandle.Bottom;
        }

        return handle;
    }

    /// <summary>Starts a drag, or null when the ray does not cross the panel's plane.</summary>
    public static VrResizeGrab? Grab(VrPanelShape shape, VrHandle handle, VrPose ray) =>
        handle == VrHandle.None || OnPlane(shape.Pose, ray) is not { } from
            ? null
            : new VrResizeGrab(shape, handle, from);

    /// <summary>
    /// The shape a drag has reached. The held edges follow the ray across the plane the panel started
    /// on, the opposite edges stay where they were, and the pixels follow the new size at the density
    /// the panel started with — so the content reflows rather than stretching.
    /// </summary>
    public static VrPanelShape Drag(VrResizeGrab grab, VrPose ray)
    {
        if (OnPlane(grab.Start.Pose, ray) is not { } at)
        {
            return grab.Start;
        }

        var start = grab.Start;
        var moved = at - grab.From;
        var handle = grab.Handle;

        var width = start.WidthMetres
                    + (handle.HasFlag(VrHandle.Right) ? moved.X : handle.HasFlag(VrHandle.Left) ? -moved.X : 0f);
        var height = start.HeightMetres
                     + (handle.HasFlag(VrHandle.Top) ? moved.Y : handle.HasFlag(VrHandle.Bottom) ? -moved.Y : 0f);

        width = Math.Clamp(width, NarrowestMetres, WidestMetres);
        height = Math.Clamp(
            height,
            Math.Max(ShortestMetres, width / MostLevel),
            Math.Min(TallestMetres, width / MostUpright));

        var density = start.PixelsWide / Math.Max(start.WidthMetres, 1e-3f);
        var (wide, tall) = Pixels(width * density, height * density);

        var reached = new VrPanelShape(start.Pose, width, wide, tall);

        var shiftX = handle.HasFlag(VrHandle.Right) ? (width - start.WidthMetres) / 2f
            : handle.HasFlag(VrHandle.Left) ? -(width - start.WidthMetres) / 2f
            : 0f;
        var shiftY = handle.HasFlag(VrHandle.Top) ? (reached.HeightMetres - start.HeightMetres) / 2f
            : handle.HasFlag(VrHandle.Bottom) ? -(reached.HeightMetres - start.HeightMetres) / 2f
            : 0f;

        var position = start.Pose.Position + Vector3.Transform(new Vector3(shiftX, shiftY, 0f), start.Pose.Facing);

        return reached with { Pose = start.Pose with { Position = position } };
    }

    /// <summary>
    /// A pixel size at the given width and height, scaled as a whole into <see cref="FewestPixels"/>
    /// and <see cref="MostPixels"/> so its aspect is kept.
    /// </summary>
    public static (int Width, int Height) Pixels(float wide, float tall)
    {
        wide = Math.Max(wide, 1f);
        tall = Math.Max(tall, 1f);

        var down = Math.Min(1f, Math.Min(MostPixels.Width / wide, MostPixels.Height / tall));
        wide *= down;
        tall *= down;

        var up = Math.Max(1f, Math.Max(FewestPixels.Width / wide, FewestPixels.Height / tall));
        wide *= up;
        tall *= up;

        return ((int)MathF.Round(wide), (int)MathF.Round(tall));
    }

    /// <summary>What the Commander is told.</summary>
    public static string Describe(VrResizeOutcome outcome) => outcome switch
    {
        VrResizeOutcome.On =>
            "The panel is in resize mode. Point at an edge or a corner, hold the trigger and pull. "
            + "Say \"stop resizing\" or press the grip when you are done.",
        VrResizeOutcome.Off => "Resize mode is off.",
        VrResizeOutcome.NoControllers =>
            "Resizing by hand needs the motion controllers, and they are switched off. "
            + "The panel's size and resolution settings change the same thing.",
        _ => "There is no headset session to resize a panel in yet.",
    };

    /// <summary>Where a ray crosses the plane of a panel, in metres right and up from its centre.</summary>
    private static Vector2? OnPlane(VrPose surface, VrPose ray)
    {
        if (!Matrix4x4.Invert(surface.ToMatrix(), out var inverse))
        {
            return null;
        }

        var origin = Vector3.Transform(ray.Position, inverse);
        var direction = Vector3.TransformNormal(Vector3.Transform(-Vector3.UnitZ, ray.Facing), inverse);

        if (MathF.Abs(direction.Z) < 1e-6f)
        {
            return null;
        }

        var t = -origin.Z / direction.Z;

        return t <= 0f ? null : new Vector2(origin.X + (direction.X * t), origin.Y + (direction.Y * t));
    }

    private static float Band(float sideMetres) =>
        Math.Min(MostOfASide, HandleMetres / Math.Max(sideMetres, 1e-3f));
}

using System.Numerics;

namespace D47.Core.Vr;

/// <summary>An eye: where it is and the angles of its view's edges from straight ahead, in radians. Left and down are negative.</summary>
public readonly record struct EyeView(VrPose Eye, float AngleLeft, float AngleRight, float AngleUp, float AngleDown);

/// <summary>
/// A quad in the eye's space, as an OpenXR quad or cylinder layer draws it: centred on its pose, facing its +Z, its
/// picture premultiplied BGRA rows from the top left. A curvature from <see cref="EyeComposite.Flat"/> to 1 bends it
/// into a cylinder of that share of a full turn.
/// </summary>
public sealed record EyeQuad(VrPose Pose, float WidthMetres, float HeightMetres, float Curvature, byte[] Pixels, int Width, int Height);

/// <summary>Draws quads over an eye's picture, each ray from the eye through a pixel meeting the quads in order.</summary>
public static class EyeComposite
{
    /// <summary>Below this curvature a quad is drawn flat, as the layer draws it.</summary>
    public const float Flat = 1e-3f;

    /// <summary>Just below a full turn, the largest arc a cylinder layer takes.</summary>
    private static readonly float MostCentralAngle = MathF.BitDecrement(MathF.Tau);

    /// <summary>
    /// Draws <paramref name="quads"/> in order over <paramref name="eye"/>, opaque BGRA rows of
    /// <paramref name="width"/> by <paramref name="height"/>, so later quads cover earlier ones.
    /// </summary>
    public static void Draw(byte[] eye, int width, int height, EyeView view, IReadOnlyList<EyeQuad> quads)
    {
        ArgumentNullException.ThrowIfNull(eye);
        ArgumentNullException.ThrowIfNull(quads);

        if (width <= 0 || height <= 0 || eye.Length < width * height * 4)
        {
            return;
        }

        var left = MathF.Tan(view.AngleLeft);
        var right = MathF.Tan(view.AngleRight);
        var up = MathF.Tan(view.AngleUp);
        var down = MathF.Tan(view.AngleDown);

        foreach (var quad in quads)
        {
            if (quad.Width <= 0 || quad.Height <= 0 || quad.Pixels.Length < quad.Width * quad.Height * 4
                || !(quad.WidthMetres > 0) || !(quad.HeightMetres > 0))
            {
                continue;
            }

            var surface = Surface.Of(quad, view.Eye);

            for (var row = 0; row < height; row++)
            {
                var ty = up - ((row + 0.5f) / height * (up - down));

                for (var column = 0; column < width; column++)
                {
                    var tx = left + ((column + 0.5f) / width * (right - left));
                    var along = (tx * surface.X) + (ty * surface.Y) - surface.Z;

                    if (surface.Hit(along, out var u, out var v))
                    {
                        Over(eye, ((row * width) + column) * 4, quad, u, v);
                    }
                }
            }
        }
    }

    /// <summary>Lays the quad's premultiplied colour at (<paramref name="u"/>, <paramref name="v"/>) over the eye's pixel.</summary>
    private static void Over(byte[] eye, int at, EyeQuad quad, float u, float v)
    {
        var x = Math.Clamp((u * quad.Width) - 0.5f, 0f, quad.Width - 1);
        var y = Math.Clamp((v * quad.Height) - 0.5f, 0f, quad.Height - 1);
        var x0 = (int)x;
        var y0 = (int)y;
        var x1 = Math.Min(x0 + 1, quad.Width - 1);
        var y1 = Math.Min(y0 + 1, quad.Height - 1);
        var fx = x - x0;
        var fy = y - y0;

        Span<float> sample = stackalloc float[4];

        for (var channel = 0; channel < 4; channel++)
        {
            var top = Lerp(Byte(quad, x0, y0, channel), Byte(quad, x1, y0, channel), fx);
            var bottom = Lerp(Byte(quad, x0, y1, channel), Byte(quad, x1, y1, channel), fx);
            sample[channel] = Lerp(top, bottom, fy);
        }

        var keep = 1f - (sample[3] / 255f);

        for (var channel = 0; channel < 3; channel++)
        {
            eye[at + channel] = (byte)Math.Clamp(MathF.Round(sample[channel] + (eye[at + channel] * keep)), 0f, 255f);
        }

        eye[at + 3] = 0xFF;
    }

    private static float Byte(EyeQuad quad, int x, int y, int channel) => quad.Pixels[(((y * quad.Width) + x) * 4) + channel];

    private static float Lerp(float from, float to, float share) => from + ((to - from) * share);

    /// <summary>
    /// A quad in its own frame: the eye's position there, and the columns that turn an eye-space ray
    /// (tx, ty, -1) into a ray there.
    /// </summary>
    private readonly record struct Surface(
        Vector3 Origin,
        Vector3 X,
        Vector3 Y,
        Vector3 Z,
        float HalfWidth,
        float HalfHeight,
        float Radius,
        float HalfAngle,
        float ArcHeight)
    {
        public static Surface Of(EyeQuad quad, VrPose eye)
        {
            var facing = quad.Pose.Facing;
            var curved = quad.Curvature is >= Flat and <= 1f;
            var arc = curved ? quad.Curvature * MathF.Tau : 0f;
            var radius = curved ? quad.WidthMetres / arc : 0f;
            var centre = curved ? quad.Pose.Position + Vector3.Transform(new Vector3(0f, 0f, radius), facing) : quad.Pose.Position;
            var angle = curved ? MathF.Min(arc, MostCentralAngle) : 0f;
            var aspect = (float)quad.Width / quad.Height;

            var inverse = Quaternion.Inverse(facing);
            var turn = inverse * eye.Facing;

            return new Surface(
                Vector3.Transform(eye.Position - centre, inverse),
                Vector3.Transform(Vector3.UnitX, turn),
                Vector3.Transform(Vector3.UnitY, turn),
                Vector3.Transform(Vector3.UnitZ, turn),
                quad.WidthMetres / 2f,
                quad.HeightMetres / 2f,
                radius,
                angle / 2f,
                curved ? radius * angle / aspect : 0f);
        }

        /// <summary>Where the ray along <paramref name="direction"/> first meets the quad, as texture coordinates.</summary>
        public bool Hit(Vector3 direction, out float u, out float v) =>
            Radius > 0f ? HitCylinder(direction, out u, out v) : HitPlane(direction, out u, out v);

        private bool HitPlane(Vector3 direction, out float u, out float v)
        {
            u = 0f;
            v = 0f;

            if (MathF.Abs(direction.Z) < 1e-9f)
            {
                return false;
            }

            var t = -Origin.Z / direction.Z;

            if (t <= 0f)
            {
                return false;
            }

            var x = Origin.X + (t * direction.X);
            var y = Origin.Y + (t * direction.Y);

            if (MathF.Abs(x) > HalfWidth || MathF.Abs(y) > HalfHeight)
            {
                return false;
            }

            u = (x / (2f * HalfWidth)) + 0.5f;
            v = 0.5f - (y / (2f * HalfHeight));
            return true;
        }

        private bool HitCylinder(Vector3 direction, out float u, out float v)
        {
            u = 0f;
            v = 0f;

            var a = (direction.X * direction.X) + (direction.Z * direction.Z);

            if (a < 1e-12f)
            {
                return false;
            }

            var b = 2f * ((Origin.X * direction.X) + (Origin.Z * direction.Z));
            var c = (Origin.X * Origin.X) + (Origin.Z * Origin.Z) - (Radius * Radius);
            var discriminant = (b * b) - (4f * a * c);

            if (discriminant < 0f)
            {
                return false;
            }

            var root = MathF.Sqrt(discriminant);

            foreach (var t in (ReadOnlySpan<float>)[(-b - root) / (2f * a), (-b + root) / (2f * a)])
            {
                if (t <= 0f)
                {
                    continue;
                }

                var point = Origin + (t * direction);
                var theta = MathF.Atan2(point.X, -point.Z);

                if (MathF.Abs(theta) > HalfAngle || MathF.Abs(point.Y) > ArcHeight / 2f)
                {
                    continue;
                }

                u = (theta / (2f * HalfAngle)) + 0.5f;
                v = 0.5f - (point.Y / ArcHeight);
                return true;
            }

            return false;
        }
    }
}

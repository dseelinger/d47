using System.Numerics;

namespace D47.Core.Hulls;

/// <summary>
/// The colour of a fully lit face and of the background, BGRA premultiplied as Avalonia's Bgra8888 bitmap
/// stores them. Less lit faces mix towards the background.
/// </summary>
public readonly record struct HullShade(uint Hull, uint Background);

/// <summary>Draws a hull mesh into a pixel buffer on the CPU, with no window or GPU.</summary>
public static class HullRasteriser
{
    public const float Ambient = 0.15f;

    private const int VertexChunk = 4096;

    /// <summary>Towards the light, in view space.</summary>
    public static readonly Vector3 LightDirection = Vector3.Normalize(new Vector3(-1f, 1f, 1f));

    [ThreadStatic]
    private static Buffers? buffers;

    /// <summary>Clears pixels to the background and draws the mesh; pixels holds width × height rows, top row first.</summary>
    public static void Draw(HullMesh mesh, HullCamera camera, uint[] pixels, int width, int height, HullShade shade)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(pixels);
        if (width <= 0 || height <= 0)
        {
            return;
        }

        if (pixels.Length < (long)width * height)
        {
            throw new ArgumentException("The pixel buffer is smaller than width × height.", nameof(pixels));
        }

        var b = buffers ??= new Buffers();
        b.Fit(mesh.Positions.Length, width * height);

        var view = camera.View();
        var viewProjection = view * camera.Projection((float)width / height);
        var frame = new Frame(mesh, b, pixels, width, height, shade, view, viewProjection, camera.Light);

        var chunks = (mesh.Positions.Length + VertexChunk - 1) / VertexChunk;
        Parallel.For(0, chunks, frame.TransformChunk);

        var bands = Math.Min(Environment.ProcessorCount, height);
        Parallel.For(0, bands, i => frame.DrawBand(height * i / bands, height * (i + 1) / bands));
    }

    /// <summary>How lit a face is, 0 to 1, from the cosine between its normal and the light and the light level.</summary>
    public static float Lighting(float facing, float light) =>
        Math.Clamp((Ambient + ((1f - Ambient) * Math.Max(0f, facing))) * light, 0f, 1f);

    /// <summary>The background at 0, the hull colour at 1, and a per-channel mix between.</summary>
    public static uint Mix(HullShade shade, float lit)
    {
        var t = Math.Clamp(lit, 0f, 1f);
        uint Channel(int shift)
        {
            var from = (shade.Background >> shift) & 0xFF;
            var to = (shade.Hull >> shift) & 0xFF;
            return (uint)MathF.Round(from + ((to - (float)from) * t));
        }

        return (Channel(24) << 24) | (Channel(16) << 16) | (Channel(8) << 8) | Channel(0);
    }

    private sealed class Buffers
    {
        public Vector4[] Clip = [];
        public Vector3[] Screen = [];
        public float[] Intensity = [];
        public float[] Depth = [];

        public void Fit(int vertices, int pixels)
        {
            if (Clip.Length != vertices)
            {
                Clip = new Vector4[vertices];
                Screen = new Vector3[vertices];
                Intensity = new float[vertices];
            }

            if (Depth.Length != pixels)
            {
                Depth = new float[pixels];
            }
        }
    }

    private readonly struct Corner(Vector4 clip, float intensity)
    {
        public Vector4 Clip { get; } = clip;

        public float Intensity { get; } = intensity;

        public static Corner Between(Corner a, Corner b, float t) =>
            new(Vector4.Lerp(a.Clip, b.Clip, t), a.Intensity + ((b.Intensity - a.Intensity) * t));
    }

    private sealed class Frame(
        HullMesh mesh,
        Buffers b,
        uint[] pixels,
        int width,
        int height,
        HullShade shade,
        Matrix4x4 view,
        Matrix4x4 viewProjection,
        float light)
    {
        public void TransformChunk(int chunk)
        {
            var positions = mesh.Positions;
            var normals = mesh.Normals;
            var end = Math.Min(positions.Length, (chunk + 1) * VertexChunk);
            for (var v = chunk * VertexChunk; v < end; v++)
            {
                var p = positions[v];
                var clip = Vector4.Transform(new Vector4(p, 1f), viewProjection);
                b.Clip[v] = clip;
                b.Screen[v] = clip.Z >= 0f ? ToScreen(clip) : default;

                var n = v < normals.Length ? Vector3.TransformNormal(normals[v], view) : Vector3.Zero;
                n = n.LengthSquared() > 1e-12f ? Vector3.Normalize(n) : Vector3.Zero;
                var toViewer = -Vector3.Transform(p, view);
                if (Vector3.Dot(n, toViewer) < 0f)
                {
                    n = -n;
                }

                b.Intensity[v] = Lighting(MathF.Max(0f, Vector3.Dot(n, LightDirection)), light);
            }
        }

        public void DrawBand(int top, int bottom)
        {
            Array.Fill(pixels, shade.Background, top * width, (bottom - top) * width);
            Array.Fill(b.Depth, float.MaxValue, top * width, (bottom - top) * width);

            var indices = mesh.Indices;
            Span<Corner> polygon = stackalloc Corner[4];
            for (var t = 0; t + 2 < indices.Length; t += 3)
            {
                int i0 = indices[t], i1 = indices[t + 1], i2 = indices[t + 2];
                Vector4 c0 = b.Clip[i0], c1 = b.Clip[i1], c2 = b.Clip[i2];
                var inFront = (c0.Z >= 0f ? 1 : 0) + (c1.Z >= 0f ? 1 : 0) + (c2.Z >= 0f ? 1 : 0);
                if (inFront == 3)
                {
                    Triangle(b.Screen[i0], b.Screen[i1], b.Screen[i2], b.Intensity[i0], b.Intensity[i1], b.Intensity[i2], top, bottom);
                }
                else if (inFront > 0)
                {
                    var count = ClipNear(new Corner(c0, b.Intensity[i0]), new Corner(c1, b.Intensity[i1]), new Corner(c2, b.Intensity[i2]), polygon);
                    var s0 = ToScreen(polygon[0].Clip);
                    for (var k = 1; k + 1 < count; k++)
                    {
                        Triangle(s0, ToScreen(polygon[k].Clip), ToScreen(polygon[k + 1].Clip), polygon[0].Intensity, polygon[k].Intensity, polygon[k + 1].Intensity, top, bottom);
                    }
                }
            }
        }

        private static int ClipNear(Corner a, Corner b, Corner c, Span<Corner> output)
        {
            ReadOnlySpan<Corner> input = [a, b, c];
            var count = 0;
            for (var k = 0; k < 3; k++)
            {
                var from = input[k];
                var to = input[(k + 1) % 3];
                if (from.Clip.Z >= 0f)
                {
                    output[count++] = from;
                }

                if ((from.Clip.Z >= 0f) != (to.Clip.Z >= 0f))
                {
                    output[count++] = Corner.Between(from, to, from.Clip.Z / (from.Clip.Z - to.Clip.Z));
                }
            }

            return count;
        }

        private Vector3 ToScreen(Vector4 clip)
        {
            var w = 1f / clip.W;
            return new Vector3(((clip.X * w) + 1f) * 0.5f * width, (1f - (clip.Y * w)) * 0.5f * height, clip.Z * w);
        }

        private void Triangle(Vector3 a, Vector3 b2, Vector3 c, float ia, float ib, float ic, int top, int bottom)
        {
            var area = Edge(a, b2, c.X, c.Y);
            if (!float.IsFinite(area) || MathF.Abs(area) < 1e-12f)
            {
                return;
            }

            if (area < 0f)
            {
                (b2, c) = (c, b2);
                (ib, ic) = (ic, ib);
                area = -area;
            }

            var minY = MathF.Min(a.Y, MathF.Min(b2.Y, c.Y));
            var maxY = MathF.Max(a.Y, MathF.Max(b2.Y, c.Y));
            var minX = MathF.Min(a.X, MathF.Min(b2.X, c.X));
            var maxX = MathF.Max(a.X, MathF.Max(b2.X, c.X));
            if (maxY < top || minY >= bottom || maxX < 0f || minX >= width)
            {
                return;
            }

            var y0 = Math.Max(top, (int)MathF.Ceiling(minY - 0.5f));
            var y1 = Math.Min(bottom - 1, (int)MathF.Floor(maxY - 0.5f));
            var x0 = Math.Max(0, (int)MathF.Ceiling(Math.Max(minX, -1f) - 0.5f));
            var x1 = Math.Min(width - 1, (int)MathF.Floor(Math.Min(maxX, width + 1f) - 0.5f));

            var inverse = 1f / area;
            float dx0 = -(c.Y - b2.Y), dx1 = -(a.Y - c.Y), dx2 = -(b2.Y - a.Y);
            var depth = b.Depth;
            for (var y = y0; y <= y1; y++)
            {
                var py = y + 0.5f;
                var px = x0 + 0.5f;
                var e0 = Edge(b2, c, px, py);
                var e1 = Edge(c, a, px, py);
                var e2 = Edge(a, b2, px, py);
                var row = y * width;
                for (var x = x0; x <= x1; x++, e0 += dx0, e1 += dx1, e2 += dx2)
                {
                    if (e0 < 0f || e1 < 0f || e2 < 0f)
                    {
                        continue;
                    }

                    float w0 = e0 * inverse, w1 = e1 * inverse, w2 = e2 * inverse;
                    var z = (w0 * a.Z) + (w1 * b2.Z) + (w2 * c.Z);
                    if (z >= depth[row + x])
                    {
                        continue;
                    }

                    depth[row + x] = z;
                    pixels[row + x] = Mix(shade, (w0 * ia) + (w1 * ib) + (w2 * ic));
                }
            }
        }

        private static float Edge(Vector3 from, Vector3 to, float x, float y) =>
            ((to.X - from.X) * (y - from.Y)) - ((to.Y - from.Y) * (x - from.X));
    }
}

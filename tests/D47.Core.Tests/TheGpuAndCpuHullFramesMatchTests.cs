using System.Numerics;
using D47.Core.Hulls;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// The CPU rasteriser agrees with a frame the OpenGL path drew, committed beside the mesh it drew. Capture the
/// frame again with <c>d47.exe --capture-hull-gpu tests\fixtures\hulls\sample.mesh tests\fixtures\hulls\sample.gpu.bgra</c>
/// from a Debug build when the shaders or shading constants change (#627).
/// </summary>
public class TheGpuAndCpuHullFramesMatchTests
{
    private const int Width = 640;
    private const int Height = 360;
    private const int EdgeReach = 1;
    private const int ChannelTolerance = 8;
    private const int JumpThreshold = 24;
    private const double RequiredShare = 0.98;

    private static readonly HullShade Shade = new(0xFFC8C8C8, 0xFF101418);

    [Fact]
    public void TheCommittedSampleMeshIsTheOneThisTestBuilds()
    {
        var path = Path.Combine(Fixtures(), "sample.mesh");
        using var built = new MemoryStream();
        SampleMesh().Write(built);

        if (Environment.GetEnvironmentVariable("D47_WRITE_HULL_FIXTURE") == "1")
        {
            File.WriteAllBytes(path, built.ToArray());
        }

        Assert.Equal(built.ToArray(), File.ReadAllBytes(path));
    }

    [Fact]
    public void TheCpuFrameMatchesTheStoredGpuFrameAwayFromEdges()
    {
        using var stream = File.OpenRead(Path.Combine(Fixtures(), "sample.mesh"));
        var mesh = HullMesh.Read(stream)!;

        var gpu = ReadFrame(Path.Combine(Fixtures(), "sample.gpu.bgra"));
        var cpu = new uint[Width * Height];
        HullRasteriser.Draw(mesh, HullCamera.Rest(mesh), cpu, Width, Height, Shade);

        var compared = 0;
        var matched = 0;

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                if (NearEdge(cpu, x, y))
                {
                    continue;
                }

                compared++;

                if (Within(cpu[(y * Width) + x], gpu[(y * Width) + x]))
                {
                    matched++;
                }
            }
        }

        Assert.True(compared > Width * Height / 10, $"only {compared} pixels were compared, so the frame is mostly edge or background");
        Assert.True(matched >= compared * RequiredShare, $"{matched} of {compared} pixels are within {ChannelTolerance}/255 of the GPU frame");
    }

    private static string Fixtures()
    {
        var folder = AppContext.BaseDirectory;

        while (!File.Exists(Path.Combine(folder, "d47.slnx")))
        {
            folder = Path.GetDirectoryName(folder) ?? throw new InvalidOperationException("d47.slnx was not found above the test binary");
        }

        return Path.Combine(folder, "tests", "fixtures", "hulls");
    }

    private static uint[] ReadFrame(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path));

        Assert.Equal(Width, reader.ReadInt32());
        Assert.Equal(Height, reader.ReadInt32());

        var bytes = reader.ReadBytes(Width * Height * 4);
        var pixels = new uint[Width * Height];

        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = BitConverter.ToUInt32(bytes, i * 4);
        }

        return pixels;
    }

    /// <summary>
    /// Whether a pixel within <see cref="EdgeReach"/> of this one is hull where another is background, or
    /// differs from a neighbour by more than <see cref="JumpThreshold"/>, which is how a depth edge shows in a
    /// flat-lit frame.
    /// </summary>
    private static bool NearEdge(uint[] frame, int x, int y)
    {
        var here = frame[(y * Width) + x];

        for (var dy = -EdgeReach; dy <= EdgeReach; dy++)
        {
            for (var dx = -EdgeReach; dx <= EdgeReach; dx++)
            {
                int nx = x + dx, ny = y + dy;

                if (nx < 0 || ny < 0 || nx >= Width || ny >= Height)
                {
                    continue;
                }

                var there = frame[(ny * Width) + nx];

                if ((here == Shade.Background) != (there == Shade.Background) || Furthest(here, there) > JumpThreshold)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool Within(uint a, uint b) => Furthest(a, b) <= ChannelTolerance;

    private static int Furthest(uint a, uint b)
    {
        var furthest = 0;

        for (var shift = 0; shift < 24; shift += 8)
        {
            furthest = Math.Max(furthest, Math.Abs((int)((a >> shift) & 0xFF) - (int)((b >> shift) & 0xFF)));
        }

        return furthest;
    }

    /// <summary>A sphere and a box that overlap, a few thousand triangles, with no Frontier geometry.</summary>
    private static HullMesh SampleMesh()
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var indices = new List<int>();

        const int Rings = 32;
        const int Segments = 48;

        for (var ring = 0; ring <= Rings; ring++)
        {
            var polar = MathF.PI * ring / Rings;

            for (var segment = 0; segment <= Segments; segment++)
            {
                var azimuth = 2f * MathF.PI * segment / Segments;
                var n = new Vector3(MathF.Sin(polar) * MathF.Cos(azimuth), MathF.Cos(polar), MathF.Sin(polar) * MathF.Sin(azimuth));
                positions.Add(n);
                normals.Add(n);
            }
        }

        for (var ring = 0; ring < Rings; ring++)
        {
            for (var segment = 0; segment < Segments; segment++)
            {
                var a = (ring * (Segments + 1)) + segment;
                var b = a + Segments + 1;
                indices.AddRange([a, b, a + 1, a + 1, b, b + 1]);
            }
        }

        var sphereTriangles = indices.Count / 3;
        var centre = new Vector3(0.7f, -0.1f, 0.2f);
        var half = new Vector3(0.9f, 0.35f, 0.5f);
        const int Cells = 8;

        foreach (var n in new[] { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ })
        {
            var u = MathF.Abs(n.Y) > 0.5f ? Vector3.UnitX : Vector3.UnitY;
            var v = Vector3.Cross(n, u);
            var first = positions.Count;

            for (var j = 0; j <= Cells; j++)
            {
                for (var i = 0; i <= Cells; i++)
                {
                    var s = (2f * i / Cells) - 1f;
                    var t = (2f * j / Cells) - 1f;
                    positions.Add(centre + ((n + (u * s) + (v * t)) * half));
                    normals.Add(n);
                }
            }

            for (var j = 0; j < Cells; j++)
            {
                for (var i = 0; i < Cells; i++)
                {
                    var a = first + (j * (Cells + 1)) + i;
                    var b = a + Cells + 1;
                    indices.AddRange([a, a + 1, b, a + 1, b + 1, b]);
                }
            }
        }

        var radius = positions.Max(p => p.Length());
        var total = indices.Count / 3;
        HullPart[] parts = [new("sphere", 0, sphereTriangles, false), new("box", sphereTriangles, total - sphereTriangles, false)];

        return new HullMesh([.. positions], [.. normals], [.. indices], parts, radius, Vector3.Normalize(new Vector3(0.5f, 0.35f, 1f)));
    }
}

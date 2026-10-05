using System.Diagnostics;
using System.Numerics;
using D47.Core.Hulls;
using Xunit;

namespace D47.Core.Tests;

/// <summary>The CPU rasteriser draws a hull into a pixel buffer: lit, depth-tested and safe with the camera inside.</summary>
public class AHullDrawsWithoutAWindowTests(ITestOutputHelper output)
{
    private const int Size = 64;
    private static readonly HullShade Shade = new(0xFFC08040, 0xFF000000);

    private static HullMesh RoundTrip(HullMesh mesh)
    {
        using var stream = new MemoryStream();
        mesh.Write(stream);
        stream.Position = 0;
        return HullMesh.Read(stream)!;
    }

    private static HullMesh Cube()
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var indices = new List<int>();
        foreach (var n in new[] { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ })
        {
            var u = MathF.Abs(n.Y) > 0.5f ? Vector3.UnitX : Vector3.UnitY;
            var v = Vector3.Cross(n, u);
            var first = positions.Count;
            foreach (var (su, sv) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
            {
                positions.Add(n + (u * su) + (v * sv));
                normals.Add(n);
            }

            indices.AddRange([first, first + 1, first + 2, first, first + 2, first + 3]);
        }

        return RoundTrip(new HullMesh([.. positions], [.. normals], [.. indices], [new HullPart("hull", 0, 12, false)], MathF.Sqrt(3), Vector3.UnitZ));
    }

    private static uint[] Draw(HullMesh mesh, HullCamera camera, int width = Size, int height = Size)
    {
        var pixels = new uint[width * height];
        HullRasteriser.Draw(mesh, camera, pixels, width, height, Shade);
        return pixels;
    }

    private static uint Centre(uint[] pixels) => pixels[(Size / 2 * Size) + (Size / 2)];

    [Fact]
    public void ACubeAtRestFillsTheCentreAndLeavesTheCorners()
    {
        var mesh = Cube();
        var pixels = Draw(mesh, HullCamera.Rest(mesh));

        Assert.NotEqual(Shade.Background, Centre(pixels));
        Assert.True((Centre(pixels) & 0xFF) > (HullRasteriser.Lit(Shade.Hull, HullRasteriser.Ambient) & 0xFF));
        foreach (var corner in new[] { 0, Size - 1, Size * (Size - 1), (Size * Size) - 1 })
        {
            Assert.Equal(Shade.Background, pixels[corner]);
        }
    }

    [Fact]
    public void WithTheLightOffEveryHullPixelIsTheAmbientShade()
    {
        var mesh = Cube();
        var pixels = Draw(mesh, HullCamera.Rest(mesh).Turn(40, 30) with { Light = 0f });

        var ambient = HullRasteriser.Lit(Shade.Hull, HullRasteriser.Ambient);
        var hull = pixels.Where(p => p != Shade.Background).ToArray();
        Assert.NotEmpty(hull);
        Assert.All(hull, p => Assert.Equal(ambient, p));
    }

    [Fact]
    public void TheNearerOfTwoOverlappingQuadsWinsWhateverTheOrder()
    {
        Vector3[] positions =
        [
            new(-1, -1, 0.5f), new(0.6f, -1, 0.5f), new(0.6f, 0.6f, 0.5f), new(-1, 0.6f, 0.5f),
            new(-0.6f, -0.6f, -0.5f), new(1, -0.6f, -0.5f), new(1, 1, -0.5f), new(-0.6f, 1, -0.5f),
        ];
        var nearNormal = Vector3.UnitZ;
        var farNormal = Vector3.Normalize(new Vector3(1, -1, 1));
        Vector3[] normals = [nearNormal, nearNormal, nearNormal, nearNormal, farNormal, farNormal, farNormal, farNormal];
        int[] nearFirst = [0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7];
        int[] farFirst = [4, 5, 6, 4, 6, 7, 0, 1, 2, 0, 2, 3];
        var radius = positions.Max(p => p.Length());

        HullMesh Quads(int[] indices) => RoundTrip(new HullMesh(positions, normals, indices, [new HullPart("hull", 0, indices.Length / 3, false)], radius, Vector3.UnitZ));
        var camera = HullCamera.Rest(Quads(nearFirst));
        var nearOnly = Draw(Quads([0, 1, 2, 0, 2, 3]), camera);
        var a = Draw(Quads(nearFirst), camera);
        var b = Draw(Quads(farFirst), camera);

        Assert.Equal(a, b);
        Assert.Equal(nearOnly[(Size / 2 * Size) + (Size / 2)], Centre(a));
        Assert.NotEqual(nearOnly[(Size / 2 * Size) + (Size / 2)], Draw(Quads([4, 5, 6, 4, 6, 7]), camera)[(Size / 2 * Size) + (Size / 2)]);
    }

    [Fact]
    public void ACameraInsideTheMeshDrawsWithoutAnException()
    {
        var mesh = Cube();
        var inside = HullCamera.Rest(mesh).Zoom(100).Turn(25, -15);
        Assert.True(inside.Distance < 1f);

        var pixels = Draw(mesh, inside);

        Assert.NotEqual(Shade.Background, Centre(pixels));
    }

    [Fact]
    public void ALargeMeshAtFourKIsTimed()
    {
        const int across = 275;
        var positions = new Vector3[across * across];
        var normals = new Vector3[positions.Length];
        for (var i = 0; i < across; i++)
        {
            var lat = MathF.PI * (i + 0.5f) / across;
            for (var j = 0; j < across; j++)
            {
                var lon = 2f * MathF.PI * j / across;
                var n = new Vector3(MathF.Sin(lat) * MathF.Cos(lon), MathF.Cos(lat), MathF.Sin(lat) * MathF.Sin(lon));
                positions[(i * across) + j] = n * 10f;
                normals[(i * across) + j] = n;
            }
        }

        var indices = new List<int>(across * across * 6);
        for (var i = 0; i + 1 < across; i++)
        {
            for (var j = 0; j < across; j++)
            {
                int a = (i * across) + j, b = (i * across) + ((j + 1) % across), c = a + across, d = b + across;
                indices.AddRange([a, b, d, a, d, c]);
            }
        }

        var triangles = indices.Count / 3;
        Assert.True(triangles >= 150_000);
        var mesh = new HullMesh(positions, normals, [.. indices], [new HullPart("hull", 0, triangles, false)], 10f, Vector3.UnitZ);
        var camera = HullCamera.Rest(mesh);
        var pixels = new uint[3840 * 2160];

        HullRasteriser.Draw(mesh, camera, pixels, 3840, 2160, Shade);
        var watch = Stopwatch.StartNew();
        HullRasteriser.Draw(mesh, camera, pixels, 3840, 2160, Shade);
        watch.Stop();

        output.WriteLine($"{triangles:N0} triangles at 3840x2160: {watch.Elapsed.TotalMilliseconds:F1} ms");
    }
}

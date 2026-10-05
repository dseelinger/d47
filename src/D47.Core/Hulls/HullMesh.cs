using System.Numerics;
using System.Text;

namespace D47.Core.Hulls;

/// <summary>A named run of triangles in a hull mesh; glass parts are drawn translucent.</summary>
public sealed record HullPart(string Name, int FirstTriangle, int TriangleCount, bool Glass);

/// <summary>A decimated hull in the little-endian D47H version 1 format.</summary>
public sealed class HullMesh
{
    public const int MaxVertices = 2_000_000;
    public const int MaxTriangles = 4_000_000;

    private const uint Version = 1;
    private const int HeaderBytes = 36;
    private const int MinPartBytes = 11;
    private static readonly byte[] Magic = "D47H"u8.ToArray();

    public HullMesh(
        Vector3[] positions,
        Vector3[] normals,
        int[] indices,
        IReadOnlyList<HullPart> parts,
        float radius,
        Vector3 restView)
    {
        Positions = positions;
        Normals = normals;
        Indices = indices;
        Parts = parts;
        Radius = radius;
        RestView = restView;
    }

    public Vector3[] Positions { get; }

    public Vector3[] Normals { get; }

    /// <summary>Three indices per triangle; winding is not guaranteed.</summary>
    public int[] Indices { get; }

    public IReadOnlyList<HullPart> Parts { get; }

    /// <summary>Largest vertex distance from the origin, in metres.</summary>
    public float Radius { get; }

    /// <summary>Unit vector from the origin towards the turntable camera at frame 1.</summary>
    public Vector3 RestView { get; }

    /// <summary>Reads a mesh; returns null for any malformed or oversized input.</summary>
    public static HullMesh? Read(Stream stream)
    {
        try
        {
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return Parse(copy.GetBuffer().AsSpan(0, (int)copy.Length));
        }
        catch (Exception ex) when (ex is IOException or OutOfMemoryException or ArgumentException)
        {
            return null;
        }
    }

    public void Write(Stream stream)
    {
        using var w = new BinaryWriter(stream, new UTF8Encoding(false), leaveOpen: true);
        w.Write(Magic);
        w.Write(Version);
        w.Write((uint)Positions.Length);
        w.Write((uint)(Indices.Length / 3));
        w.Write((uint)Parts.Count);
        w.Write(Radius);
        WriteVector(w, RestView);
        foreach (var p in Positions)
        {
            WriteVector(w, p);
        }

        foreach (var n in Normals)
        {
            WriteVector(w, n);
        }

        foreach (var i in Indices)
        {
            w.Write((uint)i);
        }

        foreach (var part in Parts)
        {
            var name = Encoding.UTF8.GetBytes(part.Name);
            w.Write((ushort)name.Length);
            w.Write(name);
            w.Write((uint)part.FirstTriangle);
            w.Write((uint)part.TriangleCount);
            w.Write((byte)(part.Glass ? 1 : 0));
        }
    }

    private static HullMesh? Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderBytes || !data[..4].SequenceEqual(Magic))
        {
            return null;
        }

        var at = 4;
        if (U32(data, ref at) != Version)
        {
            return null;
        }

        var vertexCount = U32(data, ref at);
        var triangleCount = U32(data, ref at);
        var partCount = U32(data, ref at);
        if (vertexCount > MaxVertices || triangleCount > MaxTriangles)
        {
            return null;
        }

        var radius = F32(data, ref at);
        var restView = Vec(data, ref at);

        var remaining = (long)data.Length - at;
        var needed = (2L * vertexCount * 12) + ((long)triangleCount * 12) + ((long)partCount * MinPartBytes);
        if (needed > remaining)
        {
            return null;
        }

        var positions = new Vector3[vertexCount];
        for (var i = 0; i < positions.Length; i++)
        {
            positions[i] = Vec(data, ref at);
        }

        var normals = new Vector3[vertexCount];
        for (var i = 0; i < normals.Length; i++)
        {
            normals[i] = Vec(data, ref at);
        }

        var indices = new int[(int)triangleCount * 3];
        for (var i = 0; i < indices.Length; i++)
        {
            var index = U32(data, ref at);
            if (index >= vertexCount)
            {
                return null;
            }

            indices[i] = (int)index;
        }

        var parts = new List<HullPart>((int)partCount);
        var strict = new UTF8Encoding(false, throwOnInvalidBytes: true);
        for (var i = 0; i < partCount; i++)
        {
            if (data.Length - at < 2)
            {
                return null;
            }

            var nameLength = BitConverter.ToUInt16(data.Slice(at, 2));
            at += 2;
            if ((long)data.Length - at < (long)nameLength + 9)
            {
                return null;
            }

            var name = strict.GetString(data.Slice(at, nameLength));
            at += nameLength;
            var first = U32(data, ref at);
            var count = U32(data, ref at);
            var flags = data[at++];
            if ((long)first + count > triangleCount)
            {
                return null;
            }

            parts.Add(new HullPart(name, (int)first, (int)count, (flags & 1) != 0));
        }

        return new HullMesh(positions, normals, indices, parts, radius, restView);
    }

    private static uint U32(ReadOnlySpan<byte> data, ref int at)
    {
        var value = BitConverter.ToUInt32(data.Slice(at, 4));
        at += 4;
        return value;
    }

    private static float F32(ReadOnlySpan<byte> data, ref int at)
    {
        var value = BitConverter.ToSingle(data.Slice(at, 4));
        at += 4;
        return value;
    }

    private static Vector3 Vec(ReadOnlySpan<byte> data, ref int at) =>
        new(F32(data, ref at), F32(data, ref at), F32(data, ref at));

    private static void WriteVector(BinaryWriter w, Vector3 v)
    {
        w.Write(v.X);
        w.Write(v.Y);
        w.Write(v.Z);
    }
}

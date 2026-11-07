using System.Numerics;
using System.Text;
using D47.Core.Hulls;

namespace D47.TestSupport;

/// <summary>Writes a <see cref="HullMesh"/> in the D47H version 1 format, for tests that need a hull file.</summary>
public static class HullMeshFiles
{
    public static void Write(this HullMesh mesh, Stream stream)
    {
        using var w = new BinaryWriter(stream, new UTF8Encoding(false), leaveOpen: true);
        w.Write("D47H"u8.ToArray());
        w.Write(1u);
        w.Write((uint)mesh.Positions.Length);
        w.Write((uint)(mesh.Indices.Length / 3));
        w.Write((uint)mesh.Parts.Count);
        w.Write(mesh.Radius);
        WriteVector(w, mesh.RestView);

        foreach (var p in mesh.Positions)
        {
            WriteVector(w, p);
        }

        foreach (var n in mesh.Normals)
        {
            WriteVector(w, n);
        }

        foreach (var i in mesh.Indices)
        {
            w.Write((uint)i);
        }

        foreach (var part in mesh.Parts)
        {
            var name = Encoding.UTF8.GetBytes(part.Name);
            w.Write((ushort)name.Length);
            w.Write(name);
            w.Write((uint)part.FirstTriangle);
            w.Write((uint)part.TriangleCount);
            w.Write((byte)(part.Glass ? 1 : 0));
        }
    }

    private static void WriteVector(BinaryWriter w, Vector3 v)
    {
        w.Write(v.X);
        w.Write(v.Y);
        w.Write(v.Z);
    }
}

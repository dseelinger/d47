using System.Numerics;
using D47.Core.Hulls;
using Xunit;

namespace D47.Core.Tests;

/// <summary>The D47H hull mesh reader accepts what the writer produced and rejects everything else without throwing.</summary>
public class AHullMeshReadsBackTests
{
    private static HullMesh Sample() => new(
        [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 2)],
        [new(0, 0, 1), new(0, 0, 1), new(0, 0, 1), new(0, 1, 0)],
        [0, 1, 2, 1, 2, 3],
        [new HullPart("hull", 0, 1, false), new HullPart("canopy", 1, 1, true)],
        2f,
        new Vector3(0, 0.6f, 0.8f));

    private static HullMesh WithParts(params HullPart[] parts)
    {
        var s = Sample();
        return new HullMesh(s.Positions, s.Normals, s.Indices, parts, s.Radius, s.RestView);
    }

    private static byte[] Bytes(HullMesh mesh)
    {
        using var ms = new MemoryStream();
        mesh.Write(ms);
        return ms.ToArray();
    }

    private static HullMesh? Read(byte[] bytes) => HullMesh.Read(new MemoryStream(bytes));

    private static void PutU32(byte[] bytes, int at, uint value) => BitConverter.GetBytes(value).CopyTo(bytes, at);

    [Fact]
    public void AWrittenMeshReadsBackEqual()
    {
        var original = Sample();

        var read = Read(Bytes(original));

        Assert.NotNull(read);
        Assert.Equal(original.Positions, read.Positions);
        Assert.Equal(original.Normals, read.Normals);
        Assert.Equal(original.Indices, read.Indices);
        Assert.Equal(original.Parts, read.Parts);
        Assert.Equal(original.Radius, read.Radius);
        Assert.Equal(original.RestView, read.RestView);
    }

    [Fact]
    public void TheWrongMagicIsRefused()
    {
        var bytes = Bytes(Sample());
        bytes[0] = (byte)'X';

        Assert.Null(Read(bytes));
    }

    [Fact]
    public void AnotherVersionIsRefused()
    {
        var bytes = Bytes(Sample());
        PutU32(bytes, 4, 2);

        Assert.Null(Read(bytes));
    }

    [Fact]
    public void CountsThatNeedMoreBytesThanTheFileHoldsAreRefused()
    {
        var bytes = Bytes(Sample());
        PutU32(bytes, 8, 1_000_000);

        Assert.Null(Read(bytes));
    }

    [Fact]
    public void MoreThanTwoMillionVerticesIsRefused()
    {
        var bytes = Bytes(Sample());
        PutU32(bytes, 8, 2_000_001);

        Assert.Null(Read(bytes));
    }

    [Fact]
    public void MoreThanFourMillionTrianglesIsRefused()
    {
        var bytes = Bytes(Sample());
        PutU32(bytes, 12, 4_000_001);

        Assert.Null(Read(bytes));
    }

    [Fact]
    public void AnIndexAtTheVertexCountIsRefused()
    {
        var mesh = Sample();
        mesh.Indices[4] = mesh.Positions.Length;

        Assert.Null(Read(Bytes(mesh)));
    }

    [Fact]
    public void APartRangePastTheTriangleListIsRefused()
    {
        Assert.Null(Read(Bytes(WithParts(new HullPart("hull", 1, 2, false)))));
    }

    [Fact]
    public void APartNameThatIsNotUtf8IsRefused()
    {
        var bytes = Bytes(WithParts(new HullPart("ab", 0, 1, false)));
        bytes[bytes.Length - 9 - 2] = 0xFF;

        Assert.Null(Read(bytes));
    }

    [Fact]
    public void AFileCutShortAtAnyLengthIsRefusedWithoutAnException()
    {
        var bytes = Bytes(Sample());

        for (var length = 0; length < bytes.Length; length++)
        {
            Assert.Null(Read(bytes[..length]));
        }
    }
}

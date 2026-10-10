using System.Buffers.Binary;
using System.Text;
using D47.Core.Journal;
using D47.Core.Storage;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary><c>VisitedStarsCache.dat</c> parses in every version seen and is refused when its shape does not match.</summary>
public class VisitedStarsCacheIsReadFromItsBytesTests
{
    internal const long Lave = 2832631632594;

    internal const long Sol = 10477373803;

    internal static readonly DateOnly Second = new(2026, 10, 2);

    internal static readonly DateOnly Fifth = new(2026, 1, 5);

    /// <summary>A file as Elite writes it: header, records, then the trailing markers.</summary>
    internal static byte[] File(
        uint version,
        (long Address, int Count, DateOnly Day)[] records,
        int markers = 2,
        uint? claimed = null,
        uint headerSize = 48,
        uint recordSize = 16,
        string signature = "VisitedStars")
    {
        var bytes = new byte[48 + (records.Length * 16) + (markers * 4)];
        Encoding.ASCII.GetBytes(signature).CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), version);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 200);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20), headerSize);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(24), claimed ?? (uint)records.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(28), recordSize);

        for (var i = 0; i < records.Length; i++)
        {
            var record = bytes.AsSpan(48 + (i * 16));
            BinaryPrimitives.WriteInt64LittleEndian(record, records[i].Address);
            BinaryPrimitives.WriteInt32LittleEndian(record[8..], records[i].Count);
            BinaryPrimitives.WriteUInt32LittleEndian(
                record[12..],
                (uint)(records[i].Day.DayNumber - new DateOnly(1601, 1, 1).DayNumber));
        }

        for (var i = 0; i < markers; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(48 + (records.Length * 16) + (i * 4)), 0x5AFEC0DE);
        }

        return bytes;
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(256u)]
    [InlineData(512u)]
    public void EverySeenVersionGivesEachSystemsCountAndLastDay(uint version)
    {
        var visits = VisitedStarsCache.Parse(File(version, [(Lave, 3, Second), (Sol, 1, Fifth)]));

        Assert.NotNull(visits);
        Assert.Equal(2, visits.Count);
        Assert.Equal(new SystemVisits(3, Second), visits[Lave]);
        Assert.Equal(new SystemVisits(1, Fifth), visits[Sol]);
    }

    [Fact]
    public void OneTrailingMarkerParsesAsWellAsTwo()
    {
        var visits = VisitedStarsCache.Parse(File(512, [(Lave, 3, Second)], markers: 1));

        Assert.Equal(new SystemVisits(3, Second), Assert.Single(visits!).Value);
    }

    [Fact]
    public void AFileShorterThanItsRecordCountIsRefused() =>
        Assert.Null(VisitedStarsCache.Parse(File(512, [(Lave, 3, Second), (Sol, 1, Fifth)], markers: 0, claimed: 3)));

    [Fact]
    public void AFileCutInsideTheHeaderIsRefused() =>
        Assert.Null(VisitedStarsCache.Parse(File(512, [(Lave, 3, Second)])[..40]));

    [Fact]
    public void AWrongSignatureIsRefused() =>
        Assert.Null(VisitedStarsCache.Parse(File(512, [(Lave, 3, Second)], signature: "VisitedStarz")));

    [Fact]
    public void AWrongHeaderSizeIsRefused() =>
        Assert.Null(VisitedStarsCache.Parse(File(512, [(Lave, 3, Second)], headerSize: 64)));

    [Fact]
    public void AWrongRecordSizeIsRefused() =>
        Assert.Null(VisitedStarsCache.Parse(File(512, [(Lave, 3, Second)], recordSize: 24)));

    [Theory]
    [InlineData("F735466", true)]
    [InlineData("735466", false)]
    [InlineData("F", false)]
    [InlineData("F..\\735466", false)]
    [InlineData(null, false)]
    public void OnlyAnFAndDigitsNamesAFile(string? frontierId, bool named) =>
        Assert.Equal(named, VisitedStarsCache.PathFor("root", frontierId) is not null);

    [Fact]
    public void TheFileIsTheCurrentCommandersAndIsReadAgainWhenItChanges()
    {
        const string root = "C:/d47-test/visits";
        var files = new BytesFileSystem();
        var mine = Path.Combine(root, "735466", VisitedStarsCache.FileName);
        files.WriteBytes(mine, File(512, [(Lave, 3, Second)]));
        files.WriteBytes(
            Path.Combine(root, "12484034", VisitedStarsCache.FileName),
            File(256, [(Sol, 9, Fifth)]));

        var book = new VisitedStarsBook(root, files);

        Assert.Equal(new VisitLookup(VisitState.Visited, new SystemVisits(3, Second)), book.Find("F735466", Lave));
        Assert.Equal(VisitState.NotListed, book.Find("F735466", Sol).State);
        Assert.Equal(VisitState.Visited, book.Find("F12484034", Sol).State);
        Assert.Equal(VisitState.Unreadable, book.Find("F14064573", Sol).State);
        Assert.Equal(VisitState.Unreadable, book.Find(null, Sol).State);

        files.WriteBytes(mine, File(512, [(Lave, 4, Second.AddDays(1))]));

        Assert.Equal(new SystemVisits(4, Second.AddDays(1)), book.Find("F735466", Lave).Visits);
    }
}

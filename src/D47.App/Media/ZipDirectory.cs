using System.Buffers.Binary;
using System.Text;

namespace D47.App.Media;

/// <summary>One stored entry of a zip: where its local header starts, its size and its CRC-32.</summary>
internal sealed record ZipTrack(string Name, long Header, long Bytes, uint Crc);

/// <summary>Reads a zip's end record, central directory and local headers from byte ranges. Zip64 is refused.</summary>
internal static class ZipDirectory
{
    /// <summary>The end record's fixed length.</summary>
    internal const int EndLength = 22;

    /// <summary>The end record plus the longest comment it may carry: the tail that holds the end record.</summary>
    internal const int TailLength = EndLength + ushort.MaxValue;

    /// <summary>A local header's fixed length.</summary>
    internal const int HeaderLength = 30;

    /// <summary>The largest central directory read; a library folder's is tens of kilobytes.</summary>
    internal const long MostDirectory = 16L << 20;

    private const uint EndSignature = 0x06054b50;
    private const uint EntrySignature = 0x02014b50;
    private const uint HeaderSignature = 0x04034b50;
    private const int EntryLength = 46;

    /// <summary>The central directory's offset, length and entry count, from the zip's last bytes.</summary>
    internal static (long Offset, long Length, int Count) End(ReadOnlySpan<byte> tail, long zipBytes)
    {
        for (var at = tail.Length - EndLength; at >= 0; at--)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(tail[at..]) != EndSignature)
            {
                continue;
            }

            var count = BinaryPrimitives.ReadUInt16LittleEndian(tail[(at + 10)..]);
            var length = BinaryPrimitives.ReadUInt32LittleEndian(tail[(at + 12)..]);
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(tail[(at + 16)..]);

            if (count == ushort.MaxValue || length == uint.MaxValue || offset == uint.MaxValue)
            {
                throw new InvalidDataException("the zip is a Zip64 archive");
            }

            if (length > MostDirectory || offset + (long)length > zipBytes)
            {
                throw new InvalidDataException("the zip's directory is out of range");
            }

            return (offset, length, count);
        }

        throw new InvalidDataException("the zip has no end record");
    }

    /// <summary>
    /// The directory's stored entries whose names are bare MP3 file names and whose headers lie before the directory.
    /// Compressed entries and other names are left out.
    /// </summary>
    internal static List<ZipTrack> Entries(ReadOnlySpan<byte> directory, int count, long directoryOffset)
    {
        var tracks = new List<ZipTrack>(count);
        var at = 0;

        for (var i = 0; i < count; i++)
        {
            if (at + EntryLength > directory.Length || BinaryPrimitives.ReadUInt32LittleEndian(directory[at..]) != EntrySignature)
            {
                throw new InvalidDataException("the zip's directory is damaged");
            }

            var method = BinaryPrimitives.ReadUInt16LittleEndian(directory[(at + 10)..]);
            var crc = BinaryPrimitives.ReadUInt32LittleEndian(directory[(at + 16)..]);
            var packed = BinaryPrimitives.ReadUInt32LittleEndian(directory[(at + 20)..]);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(directory[(at + 24)..]);
            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(directory[(at + 28)..]);
            var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(directory[(at + 30)..]);
            var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(directory[(at + 32)..]);
            var header = BinaryPrimitives.ReadUInt32LittleEndian(directory[(at + 42)..]);

            var next = at + EntryLength + nameLength + extraLength + commentLength;

            if (next > directory.Length)
            {
                throw new InvalidDataException("the zip's directory is damaged");
            }

            var name = Encoding.UTF8.GetString(directory.Slice(at + EntryLength, nameLength));

            if (method == 0
                && packed == size
                && size != uint.MaxValue
                && header + (long)HeaderLength < directoryOffset
                && IsTrackName(name))
            {
                tracks.Add(new ZipTrack(name, header, size, crc));
            }

            at = next;
        }

        return tracks;
    }

    /// <summary>Where an entry's bytes start, from its local header, whose extra field can differ from the directory's.</summary>
    internal static long DataStart(ReadOnlySpan<byte> header, long headerOffset)
    {
        if (header.Length < HeaderLength || BinaryPrimitives.ReadUInt32LittleEndian(header) != HeaderSignature)
        {
            throw new InvalidDataException("the track's header is damaged");
        }

        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(header[26..]);
        var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(header[28..]);

        return headerOffset + HeaderLength + nameLength + extraLength;
    }

    /// <summary>A bare MP3 file name, safe to use as a path inside a music folder.</summary>
    internal static bool IsTrackName(string name) =>
        name.Length > ".mp3".Length
        && name.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
        && name == Path.GetFileName(name)
        && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
        && name.Trim() == name
        && name[0] != '.';
}

/// <summary>The CRC-32 zip uses (IEEE 802.3, reflected, polynomial 0xEDB88320).</summary>
internal static class Crc32
{
    private static readonly uint[] Table = Build();

    /// <summary>Continues <paramref name="crc"/>, which starts at 0, over <paramref name="bytes"/>.</summary>
    internal static uint Append(uint crc, ReadOnlySpan<byte> bytes)
    {
        var value = ~crc;

        foreach (var b in bytes)
        {
            value = Table[(value ^ b) & 0xFF] ^ (value >> 8);
        }

        return ~value;
    }

    /// <summary>The CRC-32 of a stream read to its end.</summary>
    internal static uint Of(Stream stream)
    {
        var buffer = new byte[81920];
        uint crc = 0;
        int read;

        while ((read = stream.Read(buffer)) > 0)
        {
            crc = Append(crc, buffer.AsSpan(0, read));
        }

        return crc;
    }

    private static uint[] Build()
    {
        var table = new uint[256];

        for (uint i = 0; i < 256; i++)
        {
            var value = i;

            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xEDB88320 ^ (value >> 1) : value >> 1;
            }

            table[i] = value;
        }

        return table;
    }
}

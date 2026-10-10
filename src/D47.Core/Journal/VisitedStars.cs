using D47.Core.Storage;
using System.Buffers.Binary;
using System.Text;

namespace D47.Core.Journal;

/// <summary>One system in Elite's visited-systems list: how many visits, and the day of the last one.</summary>
public readonly record struct SystemVisits(int Count, DateOnly LastVisit);

/// <summary>What the visited-systems list says about one system.</summary>
public enum VisitState
{
    /// <summary>No Commander is known, or their file is missing or refused.</summary>
    Unreadable,

    /// <summary>The file was read and does not list the system.</summary>
    NotListed,

    Visited,
}

public readonly record struct VisitLookup(VisitState State, SystemVisits Visits = default);

/// <summary>Parses <c>VisitedStarsCache.dat</c>, which Elite writes per Frontier account.</summary>
public static class VisitedStarsCache
{
    public const string FileName = "VisitedStarsCache.dat";

    private const int HeaderSize = 48;

    private const int RecordSize = 16;

    private static readonly byte[] Signature = Encoding.ASCII.GetBytes("VisitedStars");

    private static readonly DateOnly Epoch = new(1601, 1, 1);

    /// <summary>The folder holding one subfolder per account, named by the FID without its <c>F</c>.</summary>
    public static string DefaultFolder() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Frontier Developments",
        "Elite Dangerous");

    /// <summary>The file for a journal <c>FID</c>, or null where the FID is not <c>F</c> and digits.</summary>
    public static string? PathFor(string folder, string? frontierId)
    {
        var account = frontierId is { Length: > 1 } && frontierId[0] == 'F' ? frontierId[1..] : null;

        return account is not null && account.All(char.IsAsciiDigit)
            ? Path.Combine(folder, account, FileName)
            : null;
    }

    /// <summary>
    /// System address to visits, or null where the signature, header size or record size does not match,
    /// or the file is shorter than its record count says.
    /// </summary>
    public static IReadOnlyDictionary<long, SystemVisits>? Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < HeaderSize
            || !bytes[..Signature.Length].SequenceEqual(Signature)
            || BinaryPrimitives.ReadUInt32LittleEndian(bytes[20..]) != HeaderSize
            || BinaryPrimitives.ReadUInt32LittleEndian(bytes[28..]) != RecordSize)
        {
            return null;
        }

        var count = BinaryPrimitives.ReadUInt32LittleEndian(bytes[24..]);

        if (bytes.Length < HeaderSize + ((long)count * RecordSize))
        {
            return null;
        }

        var visits = new Dictionary<long, SystemVisits>((int)count);

        for (var i = 0; i < count; i++)
        {
            var record = bytes.Slice(HeaderSize + (i * RecordSize), RecordSize);
            var day = BinaryPrimitives.ReadUInt32LittleEndian(record[12..]);

            if (day > int.MaxValue || Epoch.DayNumber + (long)day > DateOnly.MaxValue.DayNumber)
            {
                return null;
            }

            visits[BinaryPrimitives.ReadInt64LittleEndian(record)] = new SystemVisits(
                BinaryPrimitives.ReadInt32LittleEndian(record[8..]),
                Epoch.AddDays((int)day));
        }

        return visits;
    }
}

/// <summary>
/// Reads the current Commander's visited-systems file on demand, re-reading when its write time or length
/// changes. Blocks on file IO: never call it from the tick.
/// </summary>
public sealed class VisitedStarsBook(string folder, IFileSystem files)
{
    private sealed record Snapshot(string Path, DateTime WrittenAt, long Length, IReadOnlyDictionary<long, SystemVisits> Visits);

    private Snapshot? _last;

    public VisitLookup Find(string? frontierId, long systemAddress)
    {
        if (Load(frontierId) is not { } visits)
        {
            return new VisitLookup(VisitState.Unreadable);
        }

        return visits.TryGetValue(systemAddress, out var found)
            ? new VisitLookup(VisitState.Visited, found)
            : new VisitLookup(VisitState.NotListed);
    }

    private IReadOnlyDictionary<long, SystemVisits>? Load(string? frontierId)
    {
        if (VisitedStarsCache.PathFor(folder, frontierId) is not { } path)
        {
            return null;
        }

        try
        {
            if (files.Stat(path) is not { } file)
            {
                return null;
            }

            if (_last is { } last && last.Path == path && last.WrittenAt == file.Written && last.Length == file.Length)
            {
                return last.Visits;
            }

            byte[] bytes;

            using (var stream = files.OpenRead(path) ?? throw new FileNotFoundException("The file is missing.", path))
            {
                bytes = new byte[stream.Length];
                stream.ReadExactly(bytes);
            }

            if (VisitedStarsCache.Parse(bytes) is not { } visits)
            {
                return null;
            }

            _last = new Snapshot(path, file.Written, file.Length, visits);
            return visits;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

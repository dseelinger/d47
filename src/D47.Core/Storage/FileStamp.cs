namespace D47.Core.Storage;

/// <summary>A file's last write time and length.</summary>
public readonly record struct FileState(DateTime Written, long Length);

/// <summary>Remembers the state of a file when it was last read, so a poll can skip a read of an unchanged file.</summary>
public sealed class FileStamp
{
    private FileState? _read;

    /// <summary>The file's current state, or null when it is missing or cannot be statted.</summary>
    public static FileState? Stat(string path)
    {
        try
        {
            var info = new FileInfo(path);

            return info.Exists ? new FileState(info.LastWriteTimeUtc, info.Length) : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>True when <paramref name="now"/> is the state recorded at the last read.</summary>
    public bool Matches(FileState? now) => now is not null && now == _read;

    /// <summary>Records the state the file was in before it was read; null forgets it.</summary>
    public void Record(FileState? now) => _read = now;
}

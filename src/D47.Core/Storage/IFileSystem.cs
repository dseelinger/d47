namespace D47.Core.Storage;

/// <summary>The file operations a store or reader uses. Paths are full paths.</summary>
public interface IFileSystem
{
    /// <summary>Null when the file is missing or cannot be statted.</summary>
    FileState? Stat(string path);

    /// <summary>The folder's write time, following a junction or link; null when missing.</summary>
    DateTime? FolderWritten(string folder);

    /// <summary>Null when the file is missing. Opened with FileShare.ReadWrite | Delete.</summary>
    string? ReadText(string path);

    /// <summary>Null when the file is missing. Same sharing as ReadText.</summary>
    byte[]? ReadBytes(string path);

    /// <summary>A seekable read-only stream, or null when missing. Same sharing as ReadText.</summary>
    Stream? OpenRead(string path);

    /// <summary>Atomic replace; creates the folder.</summary>
    void WriteText(string path, string contents);

    /// <summary>Atomic replace; creates the folder.</summary>
    void WriteBytes(string path, byte[] contents);

    /// <summary>Creates the file and folder when missing.</summary>
    void AppendText(string path, string contents);

    /// <summary>Creates the folder and its parents; does nothing when it exists.</summary>
    void CreateFolder(string folder);

    /// <summary>Removes the file; does nothing when it is missing.</summary>
    void Delete(string path);

    /// <summary>Overwrites the destination and creates its folder; throws when the source is missing.</summary>
    void Copy(string from, string to);

    /// <summary>Full paths, in ordinal case-insensitive order; empty when the folder is missing.</summary>
    IReadOnlyList<string> Enumerate(string folder, string pattern, bool recursive = false);
}

namespace D47.Core.Storage;

/// <summary>The file system on disk. Reads share the file with writers and deleters, so a file Elite or an editor holds open still reads.</summary>
public sealed class DiskFileSystem : IFileSystem
{
    private const FileShare Shared = FileShare.ReadWrite | FileShare.Delete;

    public FileState? Stat(string path)
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

    public DateTime? FolderWritten(string folder)
    {
        try
        {
            var info = new DirectoryInfo(folder);

            if (!info.Exists)
            {
                return null;
            }

            if (!info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                return info.LastWriteTimeUtc;
            }

            return info.ResolveLinkTarget(returnFinalTarget: true) switch
            {
                null => info.LastWriteTimeUtc,
                { Exists: true } target => target.LastWriteTimeUtc,
                _ => null,
            };
        }
        catch (IOException)
        {
            return null;
        }
    }

    public string? ReadText(string path)
    {
        using var stream = OpenRead(path);

        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    public Stream? OpenRead(string path)
    {
        try
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, Shared);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    public void WriteText(string path, string contents) => AtomicFile.WriteAllText(path, contents);

    public void AppendText(string path, string contents)
    {
        CreateFolderOf(path);
        File.AppendAllText(path, contents);
    }

    public void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    public void Copy(string from, string to)
    {
        CreateFolderOf(to);
        File.Copy(from, to, overwrite: true);
    }

    public IReadOnlyList<string> Enumerate(string folder, string pattern, bool recursive = false)
    {
        var full = Path.GetFullPath(folder);

        try
        {
            return Directory.Exists(full)
                ? [.. Directory.EnumerateFiles(full, pattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                    .Order(StringComparer.OrdinalIgnoreCase)]
                : [];
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }
    }

    private static void CreateFolderOf(string path)
    {
        var folder = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }
    }
}

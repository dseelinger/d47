using System.IO.Enumeration;
using System.Text;

namespace D47.Core.Storage;

/// <summary>
/// A file system held in memory, for tests and the replay harness. Paths compare ordinally and ignoring
/// case. Write times come from a counter, so every write or append moves a file's stamp.
/// </summary>
public sealed class MemoryFileSystem : IFileSystem
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly DateTime Epoch = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly Lock _gate = new();

    private readonly Dictionary<string, Entry> _files = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, DateTime> _folders = new(StringComparer.OrdinalIgnoreCase);

    private long _clock;

    public FileState? Stat(string path)
    {
        lock (_gate)
        {
            return _files.TryGetValue(Full(path), out var entry) ? new FileState(entry.Written, entry.Bytes.Length) : null;
        }
    }

    public DateTime? FolderWritten(string folder)
    {
        lock (_gate)
        {
            return _folders.TryGetValue(Full(folder), out var written) ? written : null;
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
        var bytes = Bytes(path);

        return bytes is null ? null : new MemoryStream(bytes, writable: false);
    }

    public void WriteText(string path, string contents)
    {
        lock (_gate)
        {
            Put(Full(path), Utf8NoBom.GetBytes(contents));
        }
    }

    public void AppendText(string path, string contents)
    {
        var full = Full(path);

        lock (_gate)
        {
            var added = Utf8NoBom.GetBytes(contents);

            Put(full, _files.TryGetValue(full, out var entry) ? [.. entry.Bytes, .. added] : added);
        }
    }

    public void Copy(string from, string to)
    {
        var source = Full(from);

        lock (_gate)
        {
            if (!_files.TryGetValue(source, out var entry))
            {
                throw new FileNotFoundException("The file to copy does not exist.", from);
            }

            Put(Full(to), entry.Bytes, entry.Written);
        }
    }

    public IReadOnlyList<string> Enumerate(string folder, string pattern, bool recursive = false)
    {
        var full = Full(folder);
        var expression = FileSystemName.TranslateWin32Expression(pattern);

        lock (_gate)
        {
            if (!_folders.ContainsKey(full))
            {
                return [];
            }

            return [.. _files.Keys
                .Where(path => recursive ? IsUnder(path, full) : string.Equals(Path.GetDirectoryName(path), full, StringComparison.OrdinalIgnoreCase))
                .Where(path => FileSystemName.MatchesWin32Expression(expression, Path.GetFileName(path), ignoreCase: true))
                .Order(StringComparer.OrdinalIgnoreCase)];
        }
    }

    private static string Full(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static bool IsUnder(string path, string folder) =>
        path.StartsWith(Path.EndsInDirectorySeparator(folder) ? folder : folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private byte[]? Bytes(string path)
    {
        lock (_gate)
        {
            return _files.TryGetValue(Full(path), out var entry) ? entry.Bytes : null;
        }
    }

    /// <summary>Stores the bytes under a fresh stamp unless one is given, creating the folder and moving its stamp when the file is new. Callers hold the lock.</summary>
    private void Put(string full, byte[] bytes, DateTime? written = null)
    {
        var folder = Path.GetDirectoryName(full);

        if (!_files.ContainsKey(full) && folder is not null)
        {
            CreateFolder(folder);
            _folders[folder] = Tick();
        }

        _files[full] = new Entry(bytes, written ?? Tick());
    }

    private void CreateFolder(string folder)
    {
        if (_folders.ContainsKey(folder))
        {
            return;
        }

        var parent = Path.GetDirectoryName(folder);

        if (parent is not null)
        {
            CreateFolder(parent);
            _folders[parent] = Tick();
        }

        _folders[folder] = Tick();
    }

    private DateTime Tick() => Epoch.AddSeconds(++_clock);

    private sealed record Entry(byte[] Bytes, DateTime Written);
}

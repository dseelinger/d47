using D47.Core.Storage;

namespace D47.Replay;

/// <summary>Passes reads through and throws on every write, so a replay cannot change a journal folder.</summary>
internal sealed class ReadOnlyFileSystem(IFileSystem inner) : IFileSystem
{
    public FileState? Stat(string path) => inner.Stat(path);

    public DateTime? FolderWritten(string folder) => inner.FolderWritten(folder);

    public string? ReadText(string path) => inner.ReadText(path);

    public byte[]? ReadBytes(string path) => inner.ReadBytes(path);

    public Stream? OpenRead(string path) => inner.OpenRead(path);

    public IReadOnlyList<string> Enumerate(string folder, string pattern, bool recursive = false) =>
        inner.Enumerate(folder, pattern, recursive);

    public void WriteText(string path, string contents) => throw Refused(path);

    public void WriteBytes(string path, byte[] contents) => throw Refused(path);

    public void AppendText(string path, string contents) => throw Refused(path);

    public void CreateFolder(string folder) => throw Refused(folder);

    public void Delete(string path) => throw Refused(path);

    public void Copy(string from, string to) => throw Refused(to);

    private static InvalidOperationException Refused(string path) =>
        new($"The replay writes nothing; a write to {Path.GetFileName(path)} was refused.");
}

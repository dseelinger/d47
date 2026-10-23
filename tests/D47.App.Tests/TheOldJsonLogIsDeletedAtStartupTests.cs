using D47.App.Logging;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

public class TheOldJsonLogIsDeletedAtStartupTests
{
    private const string Logs = @"C:\d47\data\logs";

    [Fact]
    public void EveryJsonlFileGoesAndTheLogsStay()
    {
        var files = new MemoryFileSystem();
        files.WriteText($@"{Logs}\d47-20260901.jsonl", "{}");
        files.WriteText($@"{Logs}\d47-20260902.jsonl", "{}");
        files.WriteText($@"{Logs}\d47-20260902.log", "line");
        files.WriteText($@"{Logs}\notes.jsonl", "{}");
        files.WriteText($@"{Logs}\other.txt", "x");

        var removed = LegacyLogSweep.Run(files, Logs, NullLogger.Instance);

        Assert.Equal(2, removed);
        Assert.Empty(files.Enumerate(Logs, "d47-*.jsonl"));
        Assert.Single(files.Enumerate(Logs, "d47-*.log"));
        Assert.NotNull(files.Stat($@"{Logs}\notes.jsonl"));
        Assert.NotNull(files.Stat($@"{Logs}\other.txt"));
    }

    [Fact]
    public void AFileThatCannotBeDeletedIsSkippedAndTheRestGo()
    {
        var inner = new MemoryFileSystem();
        inner.WriteText($@"{Logs}\d47-20260901.jsonl", "{}");
        inner.WriteText($@"{Logs}\d47-20260902.jsonl", "{}");
        var files = new LockedFirstFile(inner);

        var removed = LegacyLogSweep.Run(files, Logs, NullLogger.Instance);

        Assert.Equal(1, removed);
        Assert.Single(inner.Enumerate(Logs, "d47-*.jsonl"));
    }

    private sealed class LockedFirstFile(MemoryFileSystem inner) : IFileSystem
    {
        public FileState? Stat(string path) => inner.Stat(path);

        public DateTime? FolderWritten(string folder) => inner.FolderWritten(folder);

        public string? ReadText(string path) => inner.ReadText(path);

        public byte[]? ReadBytes(string path) => inner.ReadBytes(path);

        public Stream? OpenRead(string path) => inner.OpenRead(path);

        public void WriteText(string path, string contents) => inner.WriteText(path, contents);

        public void WriteBytes(string path, byte[] contents) => inner.WriteBytes(path, contents);

        public void AppendText(string path, string contents) => inner.AppendText(path, contents);

        public void CreateFolder(string folder) => inner.CreateFolder(folder);

        public void Delete(string path)
        {
            if (path.EndsWith("20260901.jsonl", StringComparison.Ordinal))
            {
                throw new IOException("in use");
            }

            inner.Delete(path);
        }

        public void Copy(string from, string to) => inner.Copy(from, to);

        public IReadOnlyList<string> Enumerate(string folder, string pattern, bool recursive = false) =>
            inner.Enumerate(folder, pattern, recursive);
    }
}

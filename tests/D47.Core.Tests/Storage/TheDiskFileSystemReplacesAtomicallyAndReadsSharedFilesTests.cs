using D47.Core.Storage;
using Xunit;

namespace D47.Core.Tests.Storage;

[Trait("Category", "Integration")]
public sealed class TheDiskFileSystemReplacesAtomicallyAndReadsSharedFilesTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-disk-file-system-tests", Guid.NewGuid().ToString("N"));

    private readonly DiskFileSystem _files = new();

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void AWriteReplacesTheFileThroughARenameAndLeavesNoPendingFile()
    {
        var path = Path.Combine(_folder, "nested", "settings.json");

        _files.WriteText(path, "old");
        _files.WriteText(path, "new");

        Assert.Equal("new", _files.ReadText(path));
        Assert.False(File.Exists(path + AtomicFile.PendingSuffix));
        Assert.Equal([path], _files.Enumerate(Path.Combine(_folder, "nested"), "*"));
    }

    [Fact]
    public void AFileHeldOpenForWritingStillReads()
    {
        var path = Path.Combine(_folder, "Journal.log");
        _files.WriteText(path, "line one");

        using var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read);

        Assert.Equal("line one", _files.ReadText(path));

        using var stream = _files.OpenRead(path)!;

        Assert.True(stream.CanSeek);
        Assert.Equal(8, stream.Length);
    }

    [Fact]
    public void AMissingFileOrFolderReadsAsNullAndEnumeratesEmpty()
    {
        var path = Path.Combine(_folder, "nowhere", "a.json");

        Assert.Null(_files.ReadText(path));
        Assert.Null(_files.OpenRead(path));
        Assert.Null(_files.Stat(path));
        Assert.Null(_files.FolderWritten(Path.Combine(_folder, "nowhere")));
        Assert.Empty(_files.Enumerate(Path.Combine(_folder, "nowhere"), "*.json"));
    }

    [Fact]
    public void AnAppendCreatesTheFileAndItsFolder()
    {
        var path = Path.Combine(_folder, "log", "spend.jsonl");

        _files.AppendText(path, "one\n");
        _files.AppendText(path, "two\n");

        Assert.Equal("one\ntwo\n", _files.ReadText(path));
        Assert.Equal(8, _files.Stat(path)?.Length);
        Assert.NotNull(_files.FolderWritten(Path.Combine(_folder, "log")));
    }

    [Fact]
    public void ACopyOverwritesTheDestinationAndCreatesItsFolder()
    {
        var from = Path.Combine(_folder, "settings.json");
        var to = Path.Combine(_folder, "backup", "settings.json");

        _files.WriteText(from, "new");
        _files.Copy(from, to);
        _files.WriteText(from, "newer");
        _files.Copy(from, to);

        Assert.Equal("newer", _files.ReadText(to));
        Assert.Equal(_files.Stat(from), _files.Stat(to));
    }

    [Fact]
    public void ALeadingByteOrderMarkIsNotPartOfTheText()
    {
        var path = Path.Combine(_folder, "a.json");

        _files.WriteText(path, "\uFEFF{}");

        Assert.Equal("{}", _files.ReadText(path));
    }
}

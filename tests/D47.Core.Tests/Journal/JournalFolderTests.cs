using D47.Core.Journal;
using D47.Core.Storage;
using Xunit;

namespace D47.Core.Tests.Journal;

public class JournalFolderTests
{
    private const string Root = @"C:\d47-test\journals";

    private readonly MemoryFileSystem _files = new();

    [Fact]
    public void MissingDirectoryYieldsNoFileRatherThanThrowing()
    {
        Assert.Null(JournalFolder.LatestFile(_files, Root));
    }

    [Fact]
    public void EmptyDirectoryYieldsNoFile()
    {
        _files.WriteText(Path.Combine(Root, "Status.json"), "");
        _files.Delete(Path.Combine(Root, "Status.json"));

        Assert.Null(JournalFolder.LatestFile(_files, Root));
    }

    [Fact]
    public void PicksTheLatestFileByFilenameNotByFilesystemTimestamp()
    {
        // Written out of chronological order, with the earlier-named file written last, so a write-time picker
        // would get this wrong; only a filename sort gets it right.
        var older = Path.Combine(Root, "Journal.2026-02-10T090000.01.log");
        var newer = Path.Combine(Root, "Journal.2026-02-10T113000.01.log");
        _files.WriteText(newer, "");
        _files.WriteText(older, "");

        Assert.Equal(newer, JournalFolder.LatestFile(_files, Root));
    }

    [Fact]
    public void IgnoresFilesThatDoNotMatchTheJournalPattern()
    {
        _files.WriteText(Path.Combine(Root, "Journal.2026-02-10T090000.01.log"), "");
        _files.WriteText(Path.Combine(Root, "Status.json"), "");

        Assert.EndsWith("Journal.2026-02-10T090000.01.log", JournalFolder.LatestFile(_files, Root));
    }
}

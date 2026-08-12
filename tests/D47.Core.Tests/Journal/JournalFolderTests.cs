using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Journal;

public class JournalFolderTests
{
    [Fact]
    public void MissingDirectoryYieldsNoFileRatherThanThrowing()
    {
        var missing = Path.Combine(Path.GetTempPath(), "d47-tests", "does-not-exist-" + Guid.NewGuid().ToString("N"));

        Assert.Null(JournalFolder.LatestFile(missing));
    }

    [Fact]
    public void EmptyDirectoryYieldsNoFile()
    {
        using var install = new TempInstall();

        Assert.Null(JournalFolder.LatestFile(install.Root));
    }

    [Fact]
    public void PicksTheLatestFileByFilenameNotByFilesystemTimestamp()
    {
        using var install = new TempInstall();

        // Written out of chronological order, with the earlier-named file touched last, so a
        // filesystem-mtime-based picker would get this wrong; only a filename sort gets it right.
        var older = Path.Combine(install.Root, "Journal.2026-02-10T090000.01.log");
        var newer = Path.Combine(install.Root, "Journal.2026-02-10T113000.01.log");
        File.WriteAllText(newer, "");
        File.WriteAllText(older, "");
        File.SetLastWriteTimeUtc(older, DateTime.UtcNow);

        Assert.Equal(newer, JournalFolder.LatestFile(install.Root));
    }

    [Fact]
    public void IgnoresFilesThatDoNotMatchTheJournalPattern()
    {
        using var install = new TempInstall();
        File.WriteAllText(Path.Combine(install.Root, "Journal.2026-02-10T090000.01.log"), "");
        File.WriteAllText(Path.Combine(install.Root, "Status.json"), "");

        Assert.EndsWith("Journal.2026-02-10T090000.01.log", JournalFolder.LatestFile(install.Root));
    }
}

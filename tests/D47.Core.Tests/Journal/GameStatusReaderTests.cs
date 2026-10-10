using System.Text;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// The one field of Status.json read for the galaxy-map macro (2026-08-21): <c>GuiFocus</c>, which is
/// how d47 knows the map is showing before it types into it, and that it has closed again afterwards.
/// </summary>
public class GameStatusReaderTests
{
    private const string Root = @"C:\d47-test\journals";

    private static GameStatus Read(string json)
    {
        var files = new MemoryFileSystem();
        files.WriteText(Path.Combine(Root, GameStatusReader.FileName), json);

        var reader = new GameStatusReader(Root, files, NullLogger.Instance);
        Assert.True(reader.Poll());

        return reader.Current;
    }

    [Fact]
    public void AnUnchangedFileIsNotReadAgain()
    {
        var files = new MemoryFileSystem();
        var path = Path.Combine(Root, GameStatusReader.FileName);
        files.WriteText(path, """{ "timestamp":"2026-08-21T21:11:02Z", "event":"Status", "Flags":16777224, "GuiFocus":6 }""");

        var reader = new GameStatusReader(Root, files, NullLogger.Instance);

        Assert.True(reader.Poll());
        Assert.False(reader.Poll());

        files.WriteText(path, """{ "timestamp":"2026-08-21T21:11:04Z", "event":"Status", "Flags":16777224, "GuiFocus":0 }""");

        Assert.True(reader.Poll());
        Assert.Equal(GuiFocus.None, reader.Current.GuiFocus);
    }

    [Fact]
    public void NoFileIsNotAChange()
    {
        var reader = new GameStatusReader(Root, new MemoryFileSystem(), NullLogger.Instance);

        Assert.False(reader.Poll());
        Assert.False(reader.Current.IsKnown);
    }

    [Fact]
    public void TheGalaxyMapIsReadOffGuiFocus()
    {
        var status = Read("""{ "timestamp":"2026-08-21T21:11:02Z", "event":"Status", "Flags":16777224, "Flags2":0, "GuiFocus":6 }""");

        Assert.Equal(GuiFocus.GalaxyMap, status.GuiFocus);
        Assert.True(status.InShip);
    }

    /// <summary>Absent is the cockpit, which is what Elite writes as zero as well.</summary>
    [Fact]
    public void NoGuiFocusIsTheCockpit()
    {
        var status = Read("""{ "timestamp":"2026-08-21T21:11:02Z", "event":"Status", "Flags":16777224, "Flags2":0 }""");

        Assert.Equal(GuiFocus.None, status.GuiFocus);
    }

    [Fact]
    public void TheLegalStateIsKeptAsEliteWritesIt()
    {
        var status = Read("""{ "timestamp":"2026-10-05T10:00:00Z", "event":"Status", "Flags":16777224, "Flags2":0, "LegalState":"Hostile" }""");

        Assert.Equal("Hostile", status.LegalState);
    }

    [Fact]
    public void NoLegalStateReadsAsNull()
    {
        var status = Read("""{ "timestamp":"2026-10-05T10:00:00Z", "event":"Status", "Flags":16777224, "Flags2":0 }""");

        Assert.Null(status.LegalState);
    }
}

[Trait("Category", "Integration")]
public class AStatusFileEliteHoldsOpenStillReadsTests
{
    [Fact]
    public void ReadingWorksWhileAnotherHandleHasTheFileOpenForWriting()
    {
        using var install = new TempInstall();
        var path = Path.Combine(install.Root, GameStatusReader.FileName);
        File.WriteAllText(path, "");

        using (var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
        {
            writer.Write(Encoding.UTF8.GetBytes("""{ "timestamp":"2026-08-21T21:11:02Z", "event":"Status", "Flags":16777224, "GuiFocus":6 }"""));
            writer.Flush();

            var reader = new GameStatusReader(install.Root, new DiskFileSystem(), NullLogger.Instance);

            Assert.True(reader.Poll());
            Assert.Equal(GuiFocus.GalaxyMap, reader.Current.GuiFocus);
        }
    }
}

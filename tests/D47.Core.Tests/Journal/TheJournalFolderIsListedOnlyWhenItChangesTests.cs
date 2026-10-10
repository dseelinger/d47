using System.Diagnostics;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

[Trait("Category", "Integration")]
public class TheJournalFolderIsListedOnlyWhenItChangesTests
{
    private const string Older = "Journal.2026-02-10T090000.01.log";
    private const string Newer = "Journal.2026-02-10T113000.01.log";

    private static readonly DateTime Earlier = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AFolderWhoseWriteTimeHasNotChangedIsNotListedAgain()
    {
        using var install = new TempInstall();
        WriteJournal(install.Root, Older);
        Directory.SetLastWriteTimeUtc(install.Root, Earlier);

        var spine = new JournalSpine(install.Root, new DiskFileSystem(), new GameStateStore(), NullLoggerFactory.Instance);
        spine.Poll();
        spine.Poll();

        // A newer file whose creation leaves the folder's write time where it was is only found by a listing.
        WriteJournal(install.Root, Newer);
        Directory.SetLastWriteTimeUtc(install.Root, Earlier);
        spine.Poll();

        Assert.EndsWith(Older, spine.CurrentFile);
    }

    [Fact]
    public void ANewJournalFileIsTailedOnTheNextPoll()
    {
        using var install = new TempInstall();
        WriteJournal(install.Root, Older);
        Directory.SetLastWriteTimeUtc(install.Root, Earlier);

        var spine = new JournalSpine(install.Root, new DiskFileSystem(), new GameStateStore(), NullLoggerFactory.Instance);
        spine.Poll();
        spine.Poll();
        spine.Poll();
        Assert.EndsWith(Older, spine.CurrentFile);

        WriteJournal(install.Root, Newer);
        spine.Poll();

        Assert.EndsWith(Newer, spine.CurrentFile);
    }

    [Fact]
    public void ANewJournalFileIsTailedThroughAJunctionedFolder()
    {
        using var install = new TempInstall();
        var target = Path.Combine(install.Root, "target");
        var junction = Path.Combine(install.Root, "junction");
        Directory.CreateDirectory(target);
        CreateJunction(junction, target);

        try
        {
            WriteJournal(target, Older);
            Directory.SetLastWriteTimeUtc(target, Earlier);

            var spine = new JournalSpine(junction, new DiskFileSystem(), new GameStateStore(), NullLoggerFactory.Instance);
            spine.Poll();
            spine.Poll();
            spine.Poll();
            Assert.EndsWith(Older, spine.CurrentFile);

            // Creating a file in the target leaves the junction's own write time where it was.
            WriteJournal(target, Newer);
            spine.Poll();

            Assert.EndsWith(Newer, spine.CurrentFile);
        }
        finally
        {
            // A recursive delete refuses a junction, so it goes first, on its own.
            Directory.Delete(junction);
        }
    }

    [Fact]
    public void TheFirstPollTailsTheNewestFile()
    {
        using var install = new TempInstall();
        WriteJournal(install.Root, Older);
        WriteJournal(install.Root, Newer);

        var spine = new JournalSpine(install.Root, new DiskFileSystem(), new GameStateStore(), NullLoggerFactory.Instance);
        spine.Poll(priming: true);

        Assert.EndsWith(Newer, spine.CurrentFile);
    }

    private static void CreateJunction(string junction, string target)
    {
        using var mklink = Process.Start(new ProcessStartInfo("cmd.exe", ["/c", "mklink", "/J", junction, target])
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        })!;
        mklink.StandardOutput.ReadToEnd();
        mklink.WaitForExit();
        Assert.True(Directory.Exists(junction), $"mklink /J exited {mklink.ExitCode} without creating {junction}.");
    }

    private static void WriteJournal(string directory, string fileName) =>
        File.WriteAllText(
            Path.Combine(directory, fileName),
            """{ "timestamp":"2026-02-10T09:00:00Z", "event":"Fileheader", "part":1 }""" + "\n");
}

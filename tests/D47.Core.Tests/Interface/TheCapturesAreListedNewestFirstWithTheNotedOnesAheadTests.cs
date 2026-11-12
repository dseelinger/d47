using D47.Core.Interface;
using D47.Core.Storage;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>The screenshots Choose a picture lists: newest first, those noted with the entry and region ahead.</summary>
public class TheCapturesAreListedNewestFirstWithTheNotedOnesAheadTests
{
    private const string Folder = @"C:\Pictures\Frontier Developments\Elite Dangerous\D47";

    private static void Capture(MemoryFileSystem files, string stem, string at, params (long Entry, int? Region)[] entries)
    {
        files.WriteBytes(Path.Combine(Folder, stem + ".jpg"), [1]);
        files.WriteText(
            Path.Combine(Folder, stem + ".json"),
            new PictureNote(
                DateTimeOffset.Parse(at, System.Globalization.CultureInfo.InvariantCulture),
                ScreenPictures.FromScreenshot,
                1,
                1,
                "Sol",
                1,
                "B 3",
                18,
                [.. entries.Select(entry => new PictureNoteEntry(entry.Entry, "x", entry.Region))]).Write());
    }

    [Fact]
    public void TheNotedOnesComeFirstAndEachGroupIsNewestFirst()
    {
        var files = new MemoryFileSystem();
        Capture(files, "2026-10-18 21-08-00 Sol B 3", "2026-10-18T21:08:00Z", (101, 18));
        Capture(files, "2026-10-18 21-09-00 Sol B 3", "2026-10-18T21:09:00Z", (101, 18));
        Capture(files, "2026-10-18 22-00-00 Sol B 3", "2026-10-18T22:00:00Z", (101, 3));
        Capture(files, "2026-10-18 19-00-00 Sol B 3", "2026-10-18T19:00:00Z", (102, 18));
        files.WriteBytes(Path.Combine(Folder, "Taken by hand.jpg"), [1]);

        var listed = CodexCaptures.List(files, Folder, new CodexPictureKey(101, 18));

        Assert.Equal(
            ["2026-10-18 21-09-00 Sol B 3", "2026-10-18 21-08-00 Sol B 3", "2026-10-18 22-00-00 Sol B 3", "2026-10-18 19-00-00 Sol B 3", "Taken by hand"],
            listed.Select(capture => Path.GetFileNameWithoutExtension(capture.Path)));
        Assert.Equal([true, true, false, false, false], listed.Select(capture => capture.Noted));
        Assert.Equal("Sol", listed[0].System);
        Assert.Equal("B 3", listed[0].Body);
        Assert.Null(listed[4].System);
    }

    [Fact]
    public void AnEmptyFolderListsNothing()
    {
        Assert.Empty(CodexCaptures.List(new MemoryFileSystem(), Folder, new CodexPictureKey(101, 18)));
    }
}

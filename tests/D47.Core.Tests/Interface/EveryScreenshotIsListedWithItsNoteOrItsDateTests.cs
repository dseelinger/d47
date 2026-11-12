using D47.Core.Interface;
using D47.Core.Storage;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>
/// The screenshots folder is listed whole: a noted JPEG is dated from its note, an un-noted one from the time its
/// name starts with, and any other from its file time.
/// </summary>
public class EveryScreenshotIsListedWithItsNoteOrItsDateTests
{
    private const string Folder = @"C:\Pictures\Frontier Developments\Elite Dangerous\D47";

    private static readonly DateTimeOffset NotedAt = new(2026, 11, 12, 20, 0, 0, TimeSpan.Zero);

    private static PictureNote Note(DateTimeOffset at) =>
        new(at, ScreenPictures.FromScreenshot, 3840, 2160, "Sol", 10477373803, "Earth", 18, [new PictureNoteEntry(101, "Entry", 18)], "Screenshot_0001.bmp");

    [Fact]
    public void ANotedScreenshotIsDatedFromItsNoteAndCarriesIt()
    {
        var files = new MemoryFileSystem();
        files.WriteBytes(Path.Combine(Folder, "2026-11-12 21-00-00 Sol Earth.jpg"), [1]);
        files.WriteText(Path.Combine(Folder, "2026-11-12 21-00-00 Sol Earth.json"), Note(NotedAt).Write());

        var shot = Assert.Single(ScreenshotFolder.List(files, Folder));

        Assert.Equal(NotedAt, shot.At);
        Assert.Equal(("Sol", "Earth", "Screenshot_0001.bmp"), (shot.Note?.System, shot.Note?.Body, shot.Note?.Bmp));
        Assert.Equal(101, Assert.Single(shot.Note!.CodexEntries).EntryId);
    }

    [Fact]
    public void AnUnNotedScreenshotWithADatedNameIsDatedFromItsNameInLocalTime()
    {
        var files = new MemoryFileSystem();
        files.WriteBytes(Path.Combine(Folder, "2026-10-29 14-05-09 Sol.jpg"), [1]);

        var shot = Assert.Single(ScreenshotFolder.List(files, Folder));

        Assert.Null(shot.Note);
        Assert.Equal(new DateTimeOffset(new DateTime(2026, 10, 29, 14, 5, 9, DateTimeKind.Local)), shot.At);
    }

    [Fact]
    public void AnUnNotedScreenshotWithAnyOtherNameIsDatedFromItsFileTime()
    {
        var files = new MemoryFileSystem();
        var path = Path.Combine(Folder, "Renamed by hand.jpg");
        files.WriteBytes(path, [1]);

        var shot = Assert.Single(ScreenshotFolder.List(files, Folder));

        Assert.Null(shot.Note);
        Assert.Equal(new DateTimeOffset(DateTime.SpecifyKind(files.Stat(path)!.Value.Written, DateTimeKind.Utc)), shot.At);
    }

    [Fact]
    public void TheyAreListedNewestFirstWhateverTheirDateCameFrom()
    {
        var files = new MemoryFileSystem();
        files.WriteBytes(Path.Combine(Folder, "Renamed by hand.jpg"), [1]);
        files.WriteBytes(Path.Combine(Folder, "2026-10-29 14-05-09 Sol.jpg"), [1]);
        files.WriteBytes(Path.Combine(Folder, "2026-11-12 21-00-00 Sol Earth.jpg"), [1]);
        files.WriteText(Path.Combine(Folder, "2026-11-12 21-00-00 Sol Earth.json"), Note(NotedAt).Write());

        Assert.Equal(
            ["2026-11-12 21-00-00 Sol Earth.jpg", "2026-10-29 14-05-09 Sol.jpg", "Renamed by hand.jpg"],
            ScreenshotFolder.List(files, Folder).Select(shot => Path.GetFileName(shot.Path)));
    }

    [Fact]
    public void ANoteWrittenBeforeTheBmpWasRecordedStillParses()
    {
        const string Older = """{"at":"2026-08-16T10:30:00+00:00","source":"Elite's window","width":3840,"height":2160,"system":"Sol","systemAddress":1,"body":"Earth","regionId":18,"codexEntries":[]}""";

        var note = PictureNote.Parse(Older);

        Assert.NotNull(note);
        Assert.Null(note.Bmp);
        Assert.Equal("Sol", note.System);
    }
}

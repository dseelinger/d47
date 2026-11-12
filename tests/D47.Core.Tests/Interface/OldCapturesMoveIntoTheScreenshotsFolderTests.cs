using D47.Core.Interface;
using D47.Core.Storage;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>
/// The pictures "take a picture" kept in <c>data\captures</c> move into the screenshots folder with their notes,
/// named as a screenshot of the same time and place is, and are then listed with their original time and place.
/// </summary>
public class OldCapturesMoveIntoTheScreenshotsFolderTests
{
    private const string Captures = @"C:\d47\data\captures";

    private const string Target = @"C:\Pictures\Frontier Developments\Elite Dangerous\D47";

    private static readonly DateTimeOffset Taken = new(2026, 8, 16, 10, 30, 0, TimeSpan.Zero);

    private static string Stamp(DateTimeOffset at) => at.LocalDateTime.ToString("yyyy-MM-dd HH-mm-ss", System.Globalization.CultureInfo.InvariantCulture);

    private static PictureNote Note(string source = ScreenPictures.FromHeadset) =>
        new(Taken, source, 2000, 2000, "Fixture", 1000300, "Fixture A 1", 18, [new PictureNoteEntry(1, "Entry 1", 18)]);

    [Fact]
    public void ANotedCaptureMovesWithItsNoteAndKeepsItsSource()
    {
        var files = new MemoryFileSystem();
        files.WriteBytes(Path.Combine(Captures, "capture-20260816-103000.jpg"), [7, 7]);
        files.WriteText(Path.Combine(Captures, "capture-20260816-103000.json"), Note().Write());

        Assert.Equal(1, ScreenshotFolder.MoveCaptures(files, Captures, Target));

        Assert.Empty(files.Enumerate(Captures, "capture-*.*"));

        var moved = Path.Combine(Target, $"{Stamp(Taken)} Fixture A 1.jpg");
        Assert.Equal([7, 7], files.ReadBytes(moved));

        var shot = Assert.Single(ScreenshotFolder.List(files, Target));
        Assert.Equal(moved, shot.Path);
        Assert.Equal(Taken, shot.At);
        Assert.Equal(("Fixture", "Fixture A 1", ScreenPictures.FromHeadset), (shot.Note?.System, shot.Note?.Body, shot.Note?.Source));
        Assert.Equal(PictureLift.HeadsetGamma, PictureLift.For(shot.Note?.Source));
    }

    [Fact]
    public void ACaptureWithNoNoteIsNamedFromTheTimeInItsName()
    {
        var files = new MemoryFileSystem();
        files.WriteBytes(Path.Combine(Captures, "capture-20260816-103000.jpg"), [7]);

        ScreenshotFolder.MoveCaptures(files, Captures, Target);

        var local = new DateTime(2026, 8, 16, 10, 30, 0, DateTimeKind.Local);
        var shot = Assert.Single(ScreenshotFolder.List(files, Target));

        Assert.Equal("2026-08-16 10-30-00.jpg", Path.GetFileName(shot.Path));
        Assert.Equal(new DateTimeOffset(local), shot.At);
        Assert.Null(shot.Note);
    }

    [Fact]
    public void AScreenshotAlreadyThereOfTheSameTimeAndPlaceKeepsItsNameAndTheCaptureTakesTheNextCopy()
    {
        var files = new MemoryFileSystem();
        var existing = Path.Combine(Target, $"{Stamp(Taken)} Fixture A 1.jpg");
        files.WriteBytes(existing, [1]);
        files.WriteBytes(Path.Combine(Captures, "capture-20260816-103000.jpg"), [7]);
        files.WriteText(Path.Combine(Captures, "capture-20260816-103000.json"), Note().Write());

        ScreenshotFolder.MoveCaptures(files, Captures, Target);

        Assert.Equal([1], files.ReadBytes(existing));
        Assert.Equal([7], files.ReadBytes(Path.Combine(Target, $"{Stamp(Taken)} Fixture A 1 (2).jpg")));
        Assert.NotNull(PictureNote.Parse(files.ReadText(Path.Combine(Target, $"{Stamp(Taken)} Fixture A 1 (2).json"))));
    }

    [Fact]
    public void ACaptureThatCannotBeWrittenStaysAndTheRestStillMove()
    {
        var files = new RefusingFileSystem("Fixture A 1");
        files.WriteBytes(Path.Combine(Captures, "capture-20260816-103000.jpg"), [7]);
        files.WriteText(Path.Combine(Captures, "capture-20260816-103000.json"), Note().Write());
        files.WriteBytes(Path.Combine(Captures, "capture-20260816-103100.jpg"), [8]);

        Assert.Equal(1, ScreenshotFolder.MoveCaptures(files, Captures, Target));

        Assert.Equal(["capture-20260816-103000.jpg"], files.Enumerate(Captures, "capture-*.jpg").Select(Path.GetFileName));
        Assert.Single(files.Enumerate(Target, "*.jpg"));
    }

    /// <summary>A memory file system that refuses to write any file whose name contains <paramref name="refused"/>.</summary>
    private sealed class RefusingFileSystem(string refused) : IFileSystem
    {
        private readonly MemoryFileSystem _inner = new();

        public FileState? Stat(string path) => _inner.Stat(path);

        public DateTime? FolderWritten(string folder) => _inner.FolderWritten(folder);

        public string? ReadText(string path) => _inner.ReadText(path);

        public byte[]? ReadBytes(string path) => _inner.ReadBytes(path);

        public Stream? OpenRead(string path) => _inner.OpenRead(path);

        public void WriteText(string path, string contents) => _inner.WriteText(path, contents);

        public void WriteBytes(string path, byte[] contents)
        {
            if (Path.GetFileName(path).Contains(refused, StringComparison.Ordinal))
            {
                throw new IOException("refused");
            }

            _inner.WriteBytes(path, contents);
        }

        public void AppendText(string path, string contents) => _inner.AppendText(path, contents);

        public void CreateFolder(string folder) => _inner.CreateFolder(folder);

        public void Delete(string path) => _inner.Delete(path);

        public void DeleteFolder(string folder) => _inner.DeleteFolder(folder);

        public void Copy(string from, string to) => _inner.Copy(from, to);

        public IReadOnlyList<string> Enumerate(string folder, string pattern, bool recursive = false) =>
            _inner.Enumerate(folder, pattern, recursive);
    }

    [Fact]
    public void ASecondRunMovesNothing()
    {
        var files = new MemoryFileSystem();
        files.WriteBytes(Path.Combine(Captures, "capture-20260816-103000.jpg"), [7]);
        files.WriteText(Path.Combine(Captures, "capture-20260816-103000.json"), Note().Write());

        ScreenshotFolder.MoveCaptures(files, Captures, Target);

        Assert.Equal(0, ScreenshotFolder.MoveCaptures(files, Captures, Target));
        Assert.Single(files.Enumerate(Target, "*.jpg"));
    }
}

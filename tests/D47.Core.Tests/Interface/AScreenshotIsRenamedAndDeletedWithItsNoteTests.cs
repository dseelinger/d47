using D47.Core.Interface;
using D47.Core.Storage;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>Renaming a screenshot moves its JPEG and note together; deleting recycles the JPEG, and Elite's BMP when asked.</summary>
public class AScreenshotIsRenamedAndDeletedWithItsNoteTests
{
    private const string Folder = @"C:\Pictures\Frontier Developments\Elite Dangerous\D47";

    private const string Elite = @"C:\Pictures\Frontier Developments\Elite Dangerous";

    private const string Name = "2026-11-12 21-00-00 Sol Earth";

    private static readonly DateTimeOffset At = new(2026, 11, 12, 20, 0, 0, TimeSpan.Zero);

    private static string Jpeg(string stem) => Path.Combine(Folder, stem + ".jpg");

    private static string Note(string stem) => Path.Combine(Folder, stem + ".json");

    private static PictureNote Noted(string? bmp = "Screenshot_0001.bmp", MemoryFileSystem? files = null)
    {
        var note = new PictureNote(At, ScreenPictures.FromScreenshot, 3840, 2160, "Sol", 10477373803, "Earth", 18, [], bmp);

        return files?.Stat(Path.Combine(Elite, bmp ?? "")) is { } state
            ? note with { BmpLength = state.Length, BmpWritten = new DateTimeOffset(state.Written.ToUniversalTime()) }
            : note;
    }

    private static MemoryFileSystem Stocked()
    {
        var files = new MemoryFileSystem();
        files.WriteBytes(Jpeg(Name), [1, 2, 3]);
        files.WriteText(Note(Name), Noted().Write());

        return files;
    }

    [Fact]
    public void RenamingMovesTheJpegAndItsNoteAndKeepsTheDateTaken()
    {
        var files = Stocked();

        var result = ScreenshotEdits.Rename(files, Jpeg(Name), "  Earth at dawn ");

        Assert.Equal(Jpeg("Earth at dawn"), result.Path);
        Assert.Null(files.Stat(Jpeg(Name)));
        Assert.Null(files.Stat(Note(Name)));
        Assert.Equal([1, 2, 3], files.ReadBytes(Jpeg("Earth at dawn")));

        var shot = Assert.Single(ScreenshotFolder.List(files, Folder));

        Assert.Equal(At, shot.At);
        Assert.Equal("Screenshot_0001.bmp", shot.Note?.Bmp);
    }

    [Theory]
    [InlineData("", "Type a name.")]
    [InlineData("   ", "Type a name.")]
    [InlineData("a/b", "A name can't hold")]
    [InlineData("what?", "A name can't hold")]
    [InlineData("dawn.", "A name can't end with a dot.")]
    [InlineData("con", "Windows doesn't allow that name.")]
    [InlineData("Taken", "A picture with that name already exists.")]
    public void ANameWindowsRefusesOrAnotherPictureHasRenamesNothing(string typed, string problem)
    {
        var files = Stocked();
        files.WriteBytes(Jpeg("Taken"), [9]);

        var result = ScreenshotEdits.Rename(files, Jpeg(Name), typed);

        Assert.Null(result.Path);
        Assert.True(result.IsName);
        Assert.StartsWith(problem, result.Error, StringComparison.Ordinal);
        Assert.NotNull(files.Stat(Jpeg(Name)));
        Assert.NotNull(files.Stat(Note(Name)));
        Assert.Equal([9], files.ReadBytes(Jpeg("Taken")));
    }

    [Fact]
    public void ANameThatDiffersOnlyInCaseIsRenamed()
    {
        var files = Stocked();

        var result = ScreenshotEdits.Rename(files, Jpeg(Name), Name.ToUpperInvariant());

        Assert.Equal(Jpeg(Name.ToUpperInvariant()), result.Path);
        Assert.Equal(["sol earth".ToUpperInvariant()], files.Enumerate(Folder, "*.jpg").Select(p => Path.GetFileNameWithoutExtension(p)[20..]));
        Assert.Equal(2, files.Enumerate(Folder, "*").Count);
    }

    [Fact]
    public void TheSameNameChangesNothing()
    {
        var files = Stocked();

        var result = ScreenshotEdits.Rename(files, Jpeg(Name), Name);

        Assert.Equal(Jpeg(Name), result.Path);
        Assert.Equal(2, files.Enumerate(Folder, "*").Count);
    }

    [Fact]
    public void AJpegThatCannotBeRemovedLeavesThePictureAsItWas()
    {
        var inner = Stocked();
        var files = new Refusing(inner, Jpeg(Name));

        var result = ScreenshotEdits.Rename(files, Jpeg(Name), "Earth at dawn");

        Assert.Null(result.Path);
        Assert.False(result.IsName);
        Assert.Contains("refused", result.Error, StringComparison.Ordinal);
        Assert.Equal([Jpeg(Name), Note(Name)], inner.Enumerate(Folder, "*").Order());
    }

    [Fact]
    public void ABmpIsOfferedOnlyWhileTheNoteNamesOneThatStillExists()
    {
        var files = Stocked();

        Assert.Null(ScreenshotEdits.BmpToDelete(files, Noted(), Elite));

        var bmp = Path.Combine(Elite, "Screenshot_0001.bmp");
        files.WriteBytes(bmp, [7]);
        var noted = Noted(files: files);

        Assert.Equal(bmp, ScreenshotEdits.BmpToDelete(files, noted, Elite));
        Assert.Null(ScreenshotEdits.BmpToDelete(files, noted with { Bmp = null }, Elite));
        Assert.Null(ScreenshotEdits.BmpToDelete(files, null, Elite));
        Assert.Null(ScreenshotEdits.BmpToDelete(files, noted with { Bmp = @"..\Screenshot_0001.bmp" }, Elite));
        Assert.Null(ScreenshotEdits.BmpToDelete(files, noted with { Bmp = "Screenshot_0001.png" }, Elite));
    }

    [Fact]
    public void ABmpOfAnotherLengthOrWriteTimeThanTheNoteRecordedIsNotOffered()
    {
        var files = Stocked();
        var bmp = Path.Combine(Elite, "Screenshot_0001.bmp");
        files.WriteBytes(bmp, [7]);
        var noted = Noted(files: files);

        Assert.NotNull(noted.BmpLength);
        Assert.Null(ScreenshotEdits.BmpToDelete(files, noted with { BmpLength = noted.BmpLength + 1 }, Elite));
        Assert.Null(ScreenshotEdits.BmpToDelete(files, noted with { BmpWritten = noted.BmpWritten + TimeSpan.FromSeconds(1) }, Elite));

        files.WriteBytes(bmp, [7]);

        Assert.Null(ScreenshotEdits.BmpToDelete(files, noted, Elite));
    }

    [Fact]
    public void ANoteWrittenBeforeTheBmpWasRecordedOffersNoBmp()
    {
        var files = Stocked();
        files.WriteBytes(Path.Combine(Elite, "Screenshot_0001.bmp"), [7]);

        Assert.Null(ScreenshotEdits.BmpToDelete(files, Noted(), Elite));
    }

    [Fact]
    public void DeletingRecyclesTheJpegAndDeletesItsNoteAndLeavesTheBmp()
    {
        var files = Stocked();
        var bmp = Path.Combine(Elite, "Screenshot_0001.bmp");
        files.WriteBytes(bmp, [7]);
        var recycled = new List<string>();

        var reason = ScreenshotEdits.Delete(files, Jpeg(Name), null, path => Recycle(files, recycled, path));

        Assert.Null(reason);
        Assert.Equal([Jpeg(Name)], recycled);
        Assert.Null(files.Stat(Note(Name)));
        Assert.NotNull(files.Stat(bmp));
    }

    [Fact]
    public void DeletingWithTheBmpTickedRecyclesItToo()
    {
        var files = Stocked();
        var bmp = Path.Combine(Elite, "Screenshot_0001.bmp");
        files.WriteBytes(bmp, [7]);
        var recycled = new List<string>();

        var reason = ScreenshotEdits.Delete(files, Jpeg(Name), bmp, path => Recycle(files, recycled, path));

        Assert.Null(reason);
        Assert.Equal([bmp, Jpeg(Name)], recycled);
        Assert.Null(files.Stat(bmp));
    }

    [Fact]
    public void AFileTheRecycleBinRefusesLeavesTheNoteAndSaysWhy()
    {
        var files = Stocked();

        var reason = ScreenshotEdits.Delete(files, Jpeg(Name), null, _ => throw new IOException("The file is in use"));

        Assert.Equal("The JPEG could not be moved to the Recycle Bin: The file is in use.", reason);
        Assert.NotNull(files.Stat(Jpeg(Name)));
        Assert.NotNull(files.Stat(Note(Name)));
    }

    [Fact]
    public void ABmpTheRecycleBinRefusesLeavesTheJpegWhereItIs()
    {
        var files = Stocked();
        var bmp = Path.Combine(Elite, "Screenshot_0001.bmp");
        files.WriteBytes(bmp, [7]);
        var recycled = new List<string>();

        var reason = ScreenshotEdits.Delete(files, Jpeg(Name), bmp, path => path == bmp ? false : Recycle(files, recycled, path));

        Assert.StartsWith("Elite's BMP could not be moved to the Recycle Bin", reason, StringComparison.Ordinal);
        Assert.Empty(recycled);
        Assert.NotNull(files.Stat(Jpeg(Name)));
        Assert.NotNull(files.Stat(Note(Name)));
    }

    private static bool Recycle(MemoryFileSystem files, List<string> recycled, string path)
    {
        recycled.Add(path);
        files.Delete(path);

        return true;
    }

    private sealed class Refusing(MemoryFileSystem inner, string refused) : IFileSystem
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
            if (string.Equals(path, refused, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Delete refused.");
            }

            inner.Delete(path);
        }

        public void DeleteFolder(string folder) => inner.DeleteFolder(folder);

        public void Copy(string from, string to) => inner.Copy(from, to);

        public IReadOnlyList<string> Enumerate(string folder, string pattern, bool recursive = false) => inner.Enumerate(folder, pattern, recursive);
    }
}

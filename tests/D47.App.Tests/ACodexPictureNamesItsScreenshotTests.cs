using System.Text;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

public sealed class ACodexPictureNamesItsScreenshotTests
{
    private const string Folder = @"C:\d47\data\codex-pictures";
    private const string Shots = @"C:\d47\data\captures";

    private static readonly CodexPictureKey First = new(101, 18);
    private static readonly CodexPictureKey Second = new(102, 18);

    private static byte[] Bytes(string text) => Encoding.ASCII.GetBytes(text);

    private static (CodexPictureStore Store, MemoryFileSystem Files) Open(string commander = "F1")
    {
        var files = new MemoryFileSystem();
        var store = new CodexPictureStore(files, Folder, () => false, NullLogger.Instance, commander: () => commander, screenshots: Shots);

        return (store, files);
    }

    [Fact]
    public void SavingWritesTheScreenshotNameAndNoSourceCopy()
    {
        var (store, files) = Open();
        files.WriteBytes(Path.Combine(Shots, "a.jpg"), Bytes("shot"));

        store.SaveLocal(First, Bytes("cut"), "a.jpg", new CropSquare(0, 0, 600), 0.5);

        var local = Path.Combine(Folder, "local", "F1");
        Assert.Equal(Bytes("cut"), files.ReadBytes(Path.Combine(local, First.FileName)));
        Assert.Null(files.ReadBytes(Path.Combine(local, First.SourceFileName)));
        Assert.Equal("a.jpg", store.LocalScreenshot(First));
        Assert.Equal(new CropSquare(0, 0, 600), store.LocalCrop(First));
        Assert.Equal(0.5, store.LocalGamma(First));
        Assert.Equal(Bytes("shot"), store.LocalSource(First));
        Assert.True(store.HasLocalSource(First));
    }

    [Fact]
    public void ASourceIsOfferedOnlyWhileTheScreenshotExists()
    {
        var (store, files) = Open();
        files.WriteBytes(Path.Combine(Shots, "a.jpg"), Bytes("shot"));
        store.SaveLocal(First, Bytes("cut"), "a.jpg", new CropSquare(0, 0, 600));

        files.Delete(Path.Combine(Shots, "a.jpg"));

        Assert.False(store.HasLocalSource(First));
        Assert.Null(store.LocalSource(First));
        Assert.True(store.HasLocal(First));
    }

    [Fact]
    public void ACropFileWithNoScreenshotNameReadsItsOlderSourceCopy()
    {
        var (store, files) = Open();
        var local = Path.Combine(Folder, "local", "F1");
        files.WriteBytes(Path.Combine(local, First.FileName), Bytes("cut"));
        files.WriteBytes(Path.Combine(local, First.SourceFileName), Bytes("old"));
        files.WriteText(Path.Combine(local, First.CropFileName), PictureLift.Write(new CropSquare(1, 2, 500), null));

        Assert.Null(store.LocalScreenshot(First));
        Assert.True(store.HasLocalSource(First));
        Assert.Equal(Bytes("old"), store.LocalSource(First));

        store.SaveLocal(First, Bytes("cut2"), null, new CropSquare(3, 4, 500));

        Assert.Equal(Bytes("old"), store.LocalSource(First));
    }

    [Fact]
    public void SavingFromAScreenshotDropsAnOlderSourceCopy()
    {
        var (store, files) = Open();
        var local = Path.Combine(Folder, "local", "F1");
        files.WriteBytes(Path.Combine(local, First.SourceFileName), Bytes("old"));

        store.SaveLocal(First, Bytes("cut"), "a.jpg", new CropSquare(0, 0, 600));

        Assert.Null(files.ReadBytes(Path.Combine(local, First.SourceFileName)));
    }

    [Fact]
    public void TheStoreListsThePicturesCutFromAScreenshotAcrossCommanders()
    {
        var (store, _) = Open("F1");
        store.SaveLocal(First, Bytes("1"), "a.jpg", new CropSquare(0, 0, 600));
        store.SaveLocal(Second, Bytes("2"), "b.jpg", new CropSquare(0, 0, 600));

        Assert.Equal([First], store.PicturesCutFrom("a.jpg"));
        Assert.Equal([Second], store.PicturesCutFrom("B.JPG"));
        Assert.Empty(store.PicturesCutFrom("c.jpg"));
    }

    [Fact]
    public void RenamingAScreenshotRewritesOnlyTheCropFilesThatNameIt()
    {
        var (store, files) = Open();
        files.WriteBytes(Path.Combine(Shots, "new.jpg"), Bytes("shot"));
        store.SaveLocal(First, Bytes("1"), "a.jpg", new CropSquare(7, 8, 600), 0.5);
        store.SaveLocal(Second, Bytes("2"), "b.jpg", new CropSquare(0, 0, 600));

        Assert.Equal(1, store.ScreenshotRenamed("a.jpg", "new.jpg"));

        Assert.Equal("new.jpg", store.LocalScreenshot(First));
        Assert.Equal("b.jpg", store.LocalScreenshot(Second));
        Assert.Equal(new CropSquare(7, 8, 600), store.LocalCrop(First));
        Assert.Equal(0.5, store.LocalGamma(First));
        Assert.Equal(Bytes("shot"), store.LocalSource(First));
        Assert.Empty(store.PicturesCutFrom("a.jpg"));
    }
}

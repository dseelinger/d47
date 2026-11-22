using D47.Core.Books;
using D47.Core.Storage;
using Xunit;

namespace D47.App.Tests.Books;

/// <summary>A stopped book plays on at the paragraph saved in <c>data\books\</c> when the shelf is read again.</summary>
[Trait("Category", "Integration")]
public sealed class ABookResumesAtItsSavedParagraphTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "d47-book-player-" + Guid.NewGuid().ToString("N"), "books");

    public void Dispose()
    {
        var root = Path.GetDirectoryName(_folder)!;

        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PlayingAfterAStopAndANewShelfStartsAtTheSavedParagraph()
    {
        var first = BookRig.OnShelf(new DiskFileSystem(), _folder, add: true);
        first.Play();
        first.Finish(BookRig.First);
        first.Finish(BookRig.Second);
        first.Player.Stop();

        var second = BookRig.OnShelf(new DiskFileSystem(), _folder);
        second.Play();

        Assert.Equal(new BookPlace(0, 1), second.Player.Now!.Place);
        Assert.Equal(BookRig.Third, second.Player.Now.Text);
        Assert.Equal([BookRig.Third], second.Sink.Names);
    }
}

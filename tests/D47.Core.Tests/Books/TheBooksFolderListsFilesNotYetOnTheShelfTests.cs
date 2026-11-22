using System.IO.Compression;
using System.Text;
using D47.Core.Books;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Books;

public sealed class TheBooksFolderListsFilesNotYetOnTheShelfTests
{
    private const string Folder = @"C:\d47\data\books";

    private const string Container = """
        <?xml version="1.0"?>
        <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
          <rootfiles><rootfile full-path="content.opf" media-type="application/oebps-package+xml"/></rootfiles>
        </container>
        """;

    private const string Package = """
        <?xml version="1.0" encoding="utf-8"?>
        <package xmlns="http://www.idpf.org/2007/opf" version="2.0" unique-identifier="id">
          <metadata xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:title>The Night Watch</dc:title><dc:creator>H. G. Wellstone</dc:creator></metadata>
          <manifest><item id="one" href="one.xhtml" media-type="application/xhtml+xml"/></manifest>
          <spine><itemref idref="one"/></spine>
        </package>
        """;

    private const string Encryption = """
        <encryption xmlns="urn:oasis:names:tc:opendocument:xmlns:container" xmlns:enc="http://www.w3.org/2001/04/xmlenc#">
          <enc:EncryptedData><enc:EncryptionMethod Algorithm="http://www.w3.org/2001/04/xmlenc#aes128-cbc"/>
          <enc:CipherData><enc:CipherReference URI="one.xhtml"/></enc:CipherData></enc:EncryptedData>
        </encryption>
        """;

    private static byte[] Epub(bool protectedBook)
    {
        var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entries = new List<(string, string)>
            {
                ("mimetype", "application/epub+zip"),
                ("META-INF/container.xml", Container),
                ("content.opf", Package),
                ("one.xhtml", "<html xmlns=\"http://www.w3.org/1999/xhtml\"><body><p>The lamp was lit.</p></body></html>"),
            };

            if (protectedBook)
            {
                entries.Add(("META-INF/encryption.xml", Encryption));
            }

            foreach (var (path, text) in entries)
            {
                using var writer = new StreamWriter(archive.CreateEntry(path).Open(), new UTF8Encoding(false));
                writer.Write(text);
            }
        }

        return stream.ToArray();
    }

    private static (MemoryFileSystem Files, BookShelf Shelf, BookFolder Folder) Rig()
    {
        var files = new MemoryFileSystem();
        var shelf = new BookShelf(files, Folder, NullLogger.Instance);

        return (files, shelf, new BookFolder(files, Folder, shelf, NullLogger.Instance));
    }

    [Fact]
    public void ATextAndAnEpubAreListedWithTheirChapters()
    {
        var (files, _, folder) = Rig();
        files.WriteBytes(Path.Combine(Folder, "night.epub"), Epub(protectedBook: false));
        files.WriteText(Path.Combine(Folder, "the-raven.txt"), "Once upon a midnight dreary.\n\nWhile I pondered.");
        files.WriteText(Path.Combine(Folder, "notes.docx"), "not a book");

        var listing = folder.List();

        Assert.Equal(["night.epub", "the-raven.txt"], listing.Books.Select(book => book.FileName));
        Assert.Equal("The Night Watch", listing.Books[0].FileTitle);
        Assert.Equal("H. G. Wellstone", listing.Books[0].Book.Author);
        Assert.Null(listing.Books[1].FileTitle);
        Assert.Equal(1, listing.Books[1].Chapters);
        Assert.Empty(listing.Refused);
    }

    [Fact]
    public void AFileOnTheShelfIsNotListed()
    {
        var (files, _, folder) = Rig();
        files.WriteBytes(Path.Combine(Folder, "night.epub"), Epub(protectedBook: false));

        folder.AddToShelf(folder.List().Books.Single());

        Assert.Empty(folder.List().Books);
    }

    [Fact]
    public void AFileCopiedInAfterTheFirstListingAppearsInTheNext()
    {
        var (files, _, folder) = Rig();
        Assert.Empty(folder.List().Books);

        files.WriteText(@"D:\Downloads\the-raven.txt", "Once upon a midnight dreary.");

        Assert.Equal("the-raven.txt", folder.CopyIn(@"D:\Downloads\the-raven.txt"));
        Assert.Equal(["the-raven.txt"], folder.List().Books.Select(book => book.FileName));
    }

    [Fact]
    public void AProtectedEpubIsRefusedWithTheDrmNoticeAndStaysOffTheShelf()
    {
        var (files, shelf, folder) = Rig();
        files.WriteBytes(Path.Combine(Folder, "notes.epub"), Epub(protectedBook: true));

        var listing = folder.List();

        Assert.Empty(listing.Books);
        Assert.Equal(new BookFileRefusal("notes.epub", "notes.epub has no text", BookFolder.ProtectedText), listing.Refused.Single());
        Assert.Empty(shelf.Books);
    }

    [Fact]
    public void AnEmptyFileIsRefusedAsHavingNoText()
    {
        var (files, _, folder) = Rig();
        files.WriteText(Path.Combine(Folder, "empty.txt"), string.Empty);

        var refusal = folder.List().Refused.Single();

        Assert.Equal("empty.txt has no text", refusal.Label);
        Assert.Equal(BookFolder.EmptyText, refusal.Text);
    }

    [Fact]
    public void AFileThatIsNotATextOrEpubIsNotCopiedIn()
    {
        var (files, _, folder) = Rig();
        files.WriteText(@"D:\Downloads\notes.docx", "not a book");

        Assert.Null(folder.CopyIn(@"D:\Downloads\notes.docx"));
        Assert.Null(files.Stat(Path.Combine(Folder, "notes.docx")));
    }

    [Fact]
    public void AChangedFileIsReadAgain()
    {
        var (files, _, folder) = Rig();
        var path = Path.Combine(Folder, "draft.txt");
        files.WriteText(path, string.Empty);
        Assert.Single(folder.List().Refused);

        files.WriteText(path, "Now there is text.");

        Assert.Equal(["draft.txt"], folder.List().Books.Select(book => book.FileName));
    }
}

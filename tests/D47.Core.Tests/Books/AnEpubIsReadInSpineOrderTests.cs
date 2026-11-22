using System.IO.Compression;
using System.Text;
using D47.Core.Books;
using Xunit;

namespace D47.Core.Tests.Books;

public sealed class AnEpubIsReadInSpineOrderTests
{
    private static readonly BookSource Source = BookSource.FromFile("night-watch.epub");

    private const string Container = """
        <?xml version="1.0"?>
        <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
          <rootfiles><rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/></rootfiles>
        </container>
        """;

    private static string Package(string manifest, string spine) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <package xmlns="http://www.idpf.org/2007/opf" version="2.0" unique-identifier="id">
          <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
            <dc:title>The Night Watch</dc:title>
            <dc:creator>H. G. Wellstone</dc:creator>
          </metadata>
          <manifest>{manifest}</manifest>
          <spine>{spine}</spine>
        </package>
        """;

    private static string Page(string body) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.1//EN" "http://www.w3.org/TR/xhtml11/DTD/xhtml11.dtd">
        <html xmlns="http://www.w3.org/1999/xhtml"><head><title>Page</title><style>p {"{ margin: 0 }"}</style></head>
        <body>{body}</body></html>
        """;

    private static MemoryStream Epub(params (string Path, string Text)[] entries)
    {
        var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, text) in (IEnumerable<(string, string)>)[("mimetype", "application/epub+zip"), ("META-INF/container.xml", Container), .. entries])
            {
                using var writer = new StreamWriter(archive.CreateEntry(path).Open(), new UTF8Encoding(false));
                writer.Write(text);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static string Item(string id, string href) => $"""<item id="{id}" href="{href}" media-type="application/xhtml+xml"/>""";

    [Fact]
    public void ThreeSpineItemsAreThreeChaptersInSpineOrder()
    {
        var epub = Epub(
            ("OEBPS/content.opf", Package(
                Item("one", "text/one.xhtml") + Item("two", "text/two%20page.xhtml") + Item("three", "text/three.xhtml"),
                """<itemref idref="three"/><itemref idref="one"/><itemref idref="two"/>""")),
            ("OEBPS/text/one.xhtml", Page("<h1>Chapter One</h1><h2>The Lamp</h2><p>The lamp was lit&nbsp;at dusk.</p><p>Mr. Hale <em>climbed</em>\n the stair.</p>")),
            ("OEBPS/text/two page.xhtml", Page("<div><p>The tide was out.</p><h3>Later</h3><p>Night fell.</p></div>")),
            ("OEBPS/text/three.xhtml", Page("<h2>Preface</h2><p>Written at sea &amp; ashore.</p>")));

        var read = EpubBookReader.Read(epub, Source);
        var book = read.Book!;

        Assert.Null(read.Problem);
        Assert.Equal("The Night Watch", book.Title);
        Assert.Equal("H. G. Wellstone", book.Author);
        Assert.Equal(["Preface", "Chapter One. The Lamp", "Chapter 3"], book.Chapters.Select(chapter => chapter.Title));
        Assert.Equal(["Written at sea & ashore."], book.Chapters[0].Paragraphs.Select(p => p.Text));
        Assert.Equal(["The lamp was lit at dusk.", "Mr. Hale climbed the stair."], book.Chapters[1].Paragraphs.Select(p => p.Text));
        Assert.Equal(["The tide was out.", "Later", "Night fell."], book.Chapters[2].Paragraphs.Select(p => p.Text));
    }

    [Fact]
    public void ASpineWithNoTextGivesAReasonNotAnEmptyBook()
    {
        var epub = Epub(
            ("OEBPS/content.opf", Package(Item("cover", "cover.xhtml"), """<itemref idref="cover"/>""")),
            ("OEBPS/cover.xhtml", Page("""<div><img src="cover.jpg" alt=""/></div>""")));

        var read = EpubBookReader.Read(epub, Source);

        Assert.Null(read.Book);
        Assert.Contains("no text", read.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEncryptedEpubIsRefusedAsProtected()
    {
        var epub = Epub(
            ("OEBPS/content.opf", Package(Item("one", "one.xhtml"), """<itemref idref="one"/>""")),
            ("OEBPS/one.xhtml", Page("<p>Secret.</p>")),
            ("META-INF/encryption.xml", """
                <encryption xmlns="urn:oasis:names:tc:opendocument:xmlns:container" xmlns:enc="http://www.w3.org/2001/04/xmlenc#">
                  <enc:EncryptedData><enc:EncryptionMethod Algorithm="http://www.w3.org/2001/04/xmlenc#aes128-cbc"/>
                  <enc:CipherData><enc:CipherReference URI="OEBPS/one.xhtml"/></enc:CipherData></enc:EncryptedData>
                </encryption>
                """));

        var read = EpubBookReader.Read(epub, Source);

        Assert.Null(read.Book);
        Assert.Contains("protected", read.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void AFileThatIsNotAZipGivesAReason()
    {
        var read = EpubBookReader.Read(new MemoryStream(Encoding.UTF8.GetBytes("plain text")), Source);

        Assert.Null(read.Book);
        Assert.Contains("not a zip", read.Problem, StringComparison.Ordinal);
    }
}

using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace D47.Core.Books;

/// <summary>
/// Reads an unprotected EPUB: the package file named by <c>META-INF/container.xml</c>, then each spine item in order,
/// its markup reduced to paragraphs. A spine item with no text, such as a cover, gives no chapter.
/// </summary>
public static partial class EpubBookReader
{
    /// <summary>The largest uncompressed entry read; a larger one refuses the book.</summary>
    public const long MostEntryBytes = 32L * 1024 * 1024;

    private const string ContainerPath = "META-INF/container.xml";

    private const string EncryptionPath = "META-INF/encryption.xml";

    private static readonly HashSet<string> FontObfuscation = new(StringComparer.Ordinal)
    {
        "http://www.idpf.org/2008/embedding",
        "http://ns.adobe.com/pdf/enc#RC",
    };

    /// <summary>Never throws on a bad file; gives the reason instead. The stream is left open.</summary>
    public static BookRead Read(Stream stream, BookSource source)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(source);

        ZipArchive archive;

        try
        {
            archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            return BookRead.Refused("The file is not an EPUB: it is not a zip archive.");
        }

        try
        {
            using (archive)
            {
                return Read(archive, source);
            }
        }
        catch (InvalidDataException ex)
        {
            return BookRead.Refused($"The EPUB could not be read: {ex.Message}");
        }
        catch (XmlException ex)
        {
            return BookRead.Refused($"The EPUB's package could not be read: {ex.Message}");
        }
        catch (IOException ex)
        {
            return BookRead.Refused($"The EPUB could not be read: {ex.Message}");
        }
    }

    private static BookRead Read(ZipArchive archive, BookSource source)
    {
        if (IsProtected(archive))
        {
            return BookRead.Refused("The EPUB is protected, so its text cannot be read.");
        }

        if (Entry(archive, ContainerPath) is not { } container
            || Xml(container).Descendants().FirstOrDefault(e => e.Name.LocalName == "rootfile")?.Attribute("full-path")?.Value
                is not { Length: > 0 } packagePath
            || Entry(archive, packagePath) is not { } packageEntry)
        {
            return BookRead.Refused("The file is not an EPUB: it names no package file.");
        }

        var package = Xml(packageEntry);
        var folder = packagePath.Contains('/') ? packagePath[..(packagePath.LastIndexOf('/') + 1)] : "";

        var manifest = Elements(package, "manifest", "item")
            .Where(item => item.Attribute("id") is not null && item.Attribute("href") is not null)
            .GroupBy(item => item.Attribute("id")!.Value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var chapters = new List<BookChapter>();

        foreach (var itemref in Elements(package, "spine", "itemref"))
        {
            if (string.Equals(itemref.Attribute("linear")?.Value, "no", StringComparison.OrdinalIgnoreCase)
                || itemref.Attribute("idref")?.Value is not { } idref
                || !manifest.TryGetValue(idref, out var item)
                || !IsMarkup(item.Attribute("media-type")?.Value)
                || Entry(archive, Resolve(folder, item.Attribute("href")!.Value)) is not { } entry)
            {
                continue;
            }

            if (Chapter(Text(entry), chapters.Count + 1) is { } chapter)
            {
                chapters.Add(chapter);
            }
        }

        if (chapters.Count == 0)
        {
            return BookRead.Refused("The EPUB's spine has no text to read.");
        }

        var title = Metadata(package, "title") ?? source.DefaultTitle;
        var author = Metadata(package, "creator") ?? "";

        return BookRead.Read(new Book(title, author, source, chapters));
    }

    /// <summary>A chapter from one spine item's markup; leading headings are its title. Null when it has no paragraphs.</summary>
    private static BookChapter? Chapter(string markup, int number)
    {
        var headings = new List<string>();
        var paragraphs = new List<BookParagraph>();

        foreach (var (heading, text) in Blocks(markup))
        {
            if (heading && paragraphs.Count == 0)
            {
                headings.Add(text);
            }
            else
            {
                paragraphs.Add(new BookParagraph(text));
            }
        }

        if (paragraphs.Count == 0)
        {
            return null;
        }

        var title = headings.Count == 0
            ? $"Chapter {number}"
            : headings.Aggregate((joined, next) => joined[^1] is '.' or ':' or '!' or '?' ? $"{joined} {next}" : $"{joined}. {next}");

        return new BookChapter(title, paragraphs);
    }

    /// <summary>The markup's block elements as text, each marked when it is a heading.</summary>
    private static IEnumerable<(bool Heading, string Text)> Blocks(string markup)
    {
        var body = Body().Match(markup) is { Success: true } match ? match.Groups[1].Value : markup;

        body = Hidden().Replace(body, " ");
        body = Whitespace().Replace(body, " ");
        body = HeadingOpen().Replace(body, "\u0002\u0001");
        body = BlockTag().Replace(body, "\u0002");
        body = Tag().Replace(body, "");

        foreach (var block in body.Split('\u0002'))
        {
            var heading = block.Contains('\u0001');
            var text = Whitespace().Replace(WebUtility.HtmlDecode(block.Replace("\u0001", "", StringComparison.Ordinal)), " ").Trim();

            if (text.Length > 0)
            {
                yield return (heading, text);
            }
        }
    }

    private static bool IsProtected(ZipArchive archive)
    {
        if (Entry(archive, EncryptionPath) is not { } encryption)
        {
            return false;
        }

        return Xml(encryption)
            .Descendants()
            .Where(e => e.Name.LocalName == "EncryptionMethod")
            .Any(method => !FontObfuscation.Contains(method.Attribute("Algorithm")?.Value ?? ""));
    }

    private static bool IsMarkup(string? mediaType) =>
        mediaType is "application/xhtml+xml" or "text/html" or "application/x-dtbook+xml";

    private static string? Metadata(XDocument package, string name) =>
        Elements(package, "metadata", name).Select(e => Whitespace().Replace(e.Value, " ").Trim()).FirstOrDefault(value => value.Length > 0);

    private static IEnumerable<XElement> Elements(XDocument package, string parent, string child) =>
        package.Descendants()
            .Where(e => e.Name.LocalName == parent)
            .Take(1)
            .SelectMany(e => e.Elements())
            .Where(e => e.Name.LocalName == child);

    /// <summary>The archive path of <paramref name="href"/>, which is relative to the package's folder.</summary>
    private static string Resolve(string folder, string href)
    {
        var path = Uri.UnescapeDataString(href.Split('#')[0]).Replace('\\', '/');
        var parts = new List<string>();

        foreach (var part in (path.StartsWith('/') ? path[1..] : folder + path).Split('/'))
        {
            if (part == "..")
            {
                if (parts.Count > 0)
                {
                    parts.RemoveAt(parts.Count - 1);
                }
            }
            else if (part is not ("." or ""))
            {
                parts.Add(part);
            }
        }

        return string.Join('/', parts);
    }

    private static ZipArchiveEntry? Entry(ZipArchive archive, string path) =>
        archive.GetEntry(path)
        ?? archive.Entries.FirstOrDefault(entry => string.Equals(entry.FullName, path, StringComparison.OrdinalIgnoreCase));

    private static string Text(ZipArchiveEntry entry)
    {
        if (entry.Length > MostEntryBytes)
        {
            throw new InvalidDataException($"{entry.FullName} is larger than {MostEntryBytes} bytes.");
        }

        using var reader = new StreamReader(entry.Open(), detectEncodingFromByteOrderMarks: true);

        return reader.ReadToEnd();
    }

    private static XDocument Xml(ZipArchiveEntry entry)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };

        using var reader = XmlReader.Create(new StringReader(Text(entry)), settings);

        return XDocument.Load(reader);
    }

    [GeneratedRegex(@"<body\b[^>]*>(.*)</body\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Body();

    [GeneratedRegex(@"<(script|style|head)\b[^>]*>.*?</\1\s*>|<!--.*?-->", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Hidden();

    [GeneratedRegex(@"<h[1-6]\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex HeadingOpen();

    [GeneratedRegex(
        @"</?(p|div|li|ul|ol|dl|dt|dd|blockquote|section|article|header|footer|aside|nav|figure|figcaption|table|tr|td|th|pre|h[1-6])\b[^>]*>|<(br|hr)\b[^>]*>",
        RegexOptions.IgnoreCase)]
    private static partial Regex BlockTag();

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}

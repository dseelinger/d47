using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace D47.Core.Podcasts;

/// <summary>Parses an RSS 2.0 podcast feed with the <c>itunes:</c> namespace. Feeds are written by strangers.</summary>
public static class PodcastFeed
{
    private static readonly XNamespace Itunes = "http://www.itunes.com/dtds/podcast-1.0.dtd";

    private static readonly string[] AudioExtensions = [".mp3", ".m4a", ".aac", ".ogg", ".oga", ".opus", ".wav", ".flac"];

    private static readonly XmlReaderSettings Settings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersFromEntities = 0,
    };

    public static PodcastShow Parse(string xml, Uri feedUrl)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(feedUrl);

        var root = Load(xml).Root;

        if (root is null || root.Name.LocalName != "rss" || root.Element("channel") is not { } channel)
        {
            throw new PodcastFeedException("This is not an RSS feed.");
        }

        var episodes = channel.Elements("item")
            .Select(item => ReadEpisode(item, feedUrl))
            .OfType<PodcastEpisode>()
            .OrderByDescending(episode => episode.Published)
            .ToList();

        var title = Text(channel.Element("title"));

        return new PodcastShow(title.Length > 0 ? title : feedUrl.Host, episodes);
    }

    private static XDocument Load(string xml)
    {
        try
        {
            using var text = new StringReader(xml);
            using var reader = XmlReader.Create(text, Settings);

            return XDocument.Load(reader);
        }
        catch (XmlException)
        {
            throw new PodcastFeedException(
                "This feed could not be read. It is not valid XML, or it carries a DOCTYPE declaration, which D47 does not accept.");
        }
    }

    private static PodcastEpisode? ReadEpisode(XElement item, Uri feedUrl)
    {
        var enclosure = item.Element("enclosure");
        var url = enclosure?.Attribute("url")?.Value.Trim();

        if (enclosure is null
            || string.IsNullOrEmpty(url)
            || !Uri.TryCreate(feedUrl, url, out var address)
            || address.Scheme is not ("http" or "https"))
        {
            return null;
        }

        var type = enclosure.Attribute("type")?.Value.Trim() ?? "";

        if (!IsAudio(type, address))
        {
            return null;
        }

        var guid = Text(item.Element("guid"));
        var title = Text(item.Element("title"));
        long? length = long.TryParse(
            enclosure.Attribute("length")?.Value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var bytes) && bytes > 0
            ? bytes
            : null;

        return new PodcastEpisode(
            guid.Length > 0 ? guid : address.AbsoluteUri,
            title.Length > 0 ? title : "Untitled episode",
            ReadDate(Text(item.Element("pubDate"))),
            address,
            type,
            length,
            ReadDuration(Text(item.Element(Itunes + "duration"))));
    }

    private static bool IsAudio(string type, Uri address)
    {
        if (type.Length > 0)
        {
            return type.StartsWith("audio/", StringComparison.OrdinalIgnoreCase);
        }

        return AudioExtensions.Any(extension => address.AbsolutePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }

    private static DateTimeOffset ReadDate(string text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? date
            : DateTimeOffset.MinValue;

    /// <summary>Seconds, <c>mm:ss</c> or <c>hh:mm:ss</c>; null when the text is none of these.</summary>
    private static TimeSpan? ReadDuration(string text)
    {
        if (text.Length == 0)
        {
            return null;
        }

        var parts = text.Split(':');

        if (parts.Length > 3)
        {
            return null;
        }

        double total = 0;

        for (var i = 0; i < parts.Length; i++)
        {
            var style = i == parts.Length - 1 ? NumberStyles.AllowDecimalPoint : NumberStyles.None;

            if (!double.TryParse(parts[i], style, CultureInfo.InvariantCulture, out var value))
            {
                return null;
            }

            total = (total * 60) + value;
        }

        return TimeSpan.FromSeconds(total);
    }

    private static string Text(XElement? element) => element?.Value.Trim() ?? "";
}

using System.Net;
using System.Security.Cryptography;
using System.Text;
using D47.Core.Configuration;
using D47.Core.Podcasts;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.App.Media;

/// <summary>
/// The podcast feeds as last fetched, one file per feed in <c>data\podcasts\feeds\</c> named by the SHA-256 of its URL.
/// Reads that cache when built and requests a feed only when <see cref="RefreshAsync"/> or <see cref="RefreshAllAsync"/>
/// is called.
/// </summary>
internal sealed class PodcastStore
{
    /// <summary>The largest feed read; a longer one is refused.</summary>
    internal const long MostBytes = 10L * 1024 * 1024;

    /// <summary>The longest one feed's request and body may take.</summary>
    private static readonly TimeSpan Answer = TimeSpan.FromMinutes(2);

    private readonly string _feedsFolder;
    private readonly Func<IReadOnlyList<Uri>> _feeds;
    private readonly IFileSystem _files;
    private readonly HttpClient _http;
    private readonly ILogger _logger;
    private readonly Lock _gate = new();

    /// <summary>Replaced whole under <see cref="_gate"/>, never changed in place.</summary>
    private Dictionary<string, PodcastShow> _shows = new(StringComparer.Ordinal);

    /// <summary>The feeds requested since the app started, under <see cref="_gate"/>.</summary>
    private readonly HashSet<string> _requested = new(StringComparer.Ordinal);

    /// <param name="folder"><c>data\podcasts</c>.</param>
    /// <param name="feeds">The feeds added, as <c>PodcastSettings.FeedUris</c> gives them.</param>
    internal PodcastStore(string folder, Func<IReadOnlyList<Uri>> feeds, IFileSystem files, HttpClient http, ILogger logger)
    {
        _feedsFolder = Path.Combine(folder, "feeds");
        _feeds = feeds;
        _files = files;
        _http = http;
        _logger = logger;

        foreach (var feed in feeds())
        {
            if (ReadCache(feed) is { } show)
            {
                _shows[feed.AbsoluteUri] = show;
            }
        }
    }

    /// <summary>Raised on a background thread when a refresh has replaced a show.</summary>
    internal event Action? Changed;

    /// <summary>The cached show of each feed added, in the order added; a feed never read is left out.</summary>
    internal IReadOnlyList<CachedShow> Shows
    {
        get
        {
            var feeds = _feeds();
            Dictionary<string, PodcastShow> shows;

            lock (_gate)
            {
                shows = _shows;
            }

            return [.. feeds
                .Select(feed => shows.TryGetValue(feed.AbsoluteUri, out var show) ? new CachedShow(feed, show) : null)
                .OfType<CachedShow>()];
        }
    }

    /// <summary>The distinct hosts the cached shows' episodes are streamed from.</summary>
    internal IReadOnlyList<string> EpisodeHosts =>
        [.. Shows
            .SelectMany(cached => cached.Show.Episodes)
            .Select(episode => episode.Enclosure.Host)
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Fetches one feed on the pool and caches it once it parses.</summary>
    internal Task<PodcastRefresh> RefreshAsync(Uri feed) => Task.Run(() => FetchAsync(feed));

    /// <summary>
    /// Adds a feed to <see cref="PodcastSettings.Feeds"/>, once, and fetches it on the pool. Adding a feed adds a
    /// destination, so this is reached from the panel and the keyword router, never from a model tool.
    /// </summary>
    internal Task<PodcastRefresh> AddAsync(Uri feed, SettingsService settings)
    {
        settings.Replace("a podcast feed was added", current => current with { Podcasts = current.Podcasts.WithFeed(feed) });

        return RefreshAsync(feed);
    }

    /// <summary>Fetches each feed added that has no cached show and has not been requested since the app started.</summary>
    internal Task<IReadOnlyList<PodcastRefresh>> RefreshNewAsync() => Task.Run(async () =>
    {
        var results = new List<PodcastRefresh>();

        foreach (var feed in _feeds())
        {
            bool isNew;

            lock (_gate)
            {
                isNew = !_shows.ContainsKey(feed.AbsoluteUri) && !_requested.Contains(feed.AbsoluteUri);
            }

            if (isNew)
            {
                results.Add(await FetchAsync(feed).ConfigureAwait(false));
            }
        }

        return (IReadOnlyList<PodcastRefresh>)results;
    });

    /// <summary>Fetches every feed added, one after another, on the pool.</summary>
    internal Task<IReadOnlyList<PodcastRefresh>> RefreshAllAsync() => Task.Run(async () =>
    {
        var results = new List<PodcastRefresh>();

        foreach (var feed in _feeds())
        {
            results.Add(await FetchAsync(feed).ConfigureAwait(false));
        }

        return (IReadOnlyList<PodcastRefresh>)results;
    });

    private async Task<PodcastRefresh> FetchAsync(Uri feed)
    {
        lock (_gate)
        {
            _requested.Add(feed.AbsoluteUri);
        }

        if (!feed.IsAbsoluteUri || feed.Scheme is not ("http" or "https"))
        {
            return new PodcastRefresh(feed, null, "A podcast feed has to be an http or https address.");
        }

        string text;

        try
        {
            using var answer = new CancellationTokenSource(Answer);
            using var response = await _http.GetAsync(feed, HttpCompletionOption.ResponseHeadersRead, answer.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return new PodcastRefresh(feed, null, $"The feed could not be fetched: {feed.Host} returned {(int)response.StatusCode}.");
            }

            if (response.Content.Headers.ContentLength > MostBytes
                || await ReadAtMostAsync(response.Content, MostBytes, answer.Token).ConfigureAwait(false) is not { } bytes)
            {
                return new PodcastRefresh(feed, null, "The feed is larger than 10 MB, so D47 does not read it.");
            }

            text = Decode(bytes, response.Content.Headers.ContentType?.CharSet);
        }
        catch (OperationCanceledException exception)
        {
            _logger.LogInformation(exception, "The podcast feed at {Host} did not answer in time", feed.Host);
            return new PodcastRefresh(feed, null, $"The feed could not be fetched: {feed.Host} did not answer within two minutes.");
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            _logger.LogInformation(exception, "The podcast feed at {Host} could not be fetched", feed.Host);
            return new PodcastRefresh(feed, null, $"The feed could not be fetched from {feed.Host}.");
        }

        PodcastShow show;

        try
        {
            show = PodcastFeed.Parse(text, feed);
        }
        catch (PodcastFeedException exception)
        {
            _logger.LogInformation("The podcast feed at {Host} did not parse: {Reason}", feed.Host, exception.Message);
            return new PodcastRefresh(feed, null, exception.Message);
        }

        try
        {
            _files.WriteText(CachePath(feed), text);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "The podcast feed at {Host} could not be saved", feed.Host);
        }

        lock (_gate)
        {
            _shows = new Dictionary<string, PodcastShow>(_shows, StringComparer.Ordinal) { [feed.AbsoluteUri] = show };
        }

        Raise();

        return new PodcastRefresh(feed, show, null);
    }

    /// <summary>The body, or null once it passes <paramref name="most"/> bytes.</summary>
    internal static async Task<byte[]?> ReadAtMostAsync(HttpContent content, long most, CancellationToken token)
    {
        await using var body = await content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var kept = new MemoryStream();
        var buffer = new byte[81920];
        int read;

        while ((read = await body.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
        {
            if (kept.Length + read > most)
            {
                return null;
            }

            kept.Write(buffer, 0, read);
        }

        return kept.ToArray();
    }

    /// <summary>The response's charset when it names one D47 knows, otherwise UTF-8; a byte order mark wins over both.</summary>
    private static string Decode(byte[] bytes, string? charset)
    {
        var encoding = Encoding.UTF8;

        if (!string.IsNullOrWhiteSpace(charset))
        {
            try
            {
                encoding = Encoding.GetEncoding(charset.Trim('"', ' '));
            }
            catch (ArgumentException)
            {
                // A charset this runtime does not know is read as UTF-8.
            }
        }

        using var reader = new StreamReader(new MemoryStream(bytes), encoding, detectEncodingFromByteOrderMarks: true);

        return reader.ReadToEnd();
    }

    private PodcastShow? ReadCache(Uri feed)
    {
        try
        {
            return _files.ReadText(CachePath(feed)) is { } text ? PodcastFeed.Parse(text, feed) : null;
        }
        catch (Exception exception) when (exception is PodcastFeedException or IOException or UnauthorizedAccessException)
        {
            _logger.LogInformation(exception, "The cached podcast feed for {Host} could not be read", feed.Host);
            return null;
        }
    }

    private string CachePath(Uri feed) =>
        Path.Combine(_feedsFolder, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(feed.AbsoluteUri))) + ".xml");

    private void Raise()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "A podcast subscriber failed");
        }
    }

    /// <summary>A client that keeps no cookies and has no overall timeout; each request is bounded instead.</summary>
    internal static HttpClient CreateClient()
    {
        var client = new HttpClient(new SocketsHttpHandler
        {
            UseCookies = false,
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All,
        })
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("d47-podcasts");

        return client;
    }
}

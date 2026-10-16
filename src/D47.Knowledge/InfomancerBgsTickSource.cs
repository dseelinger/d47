using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Knowledge;

/// <summary><see cref="IBgsTickSource"/> against the community tick detector, over plain HTTP.</summary>
public sealed class InfomancerBgsTickSource : IBgsTickSource, IDisposable
{
    /// <summary>Named here and in <see cref="D47.Core.Configuration.EgressDisclosure"/>.</summary>
    public const string Host = "tick.infomancer.uk";

    public const string Address = $"http://{Host}/galtick.json";

    /// <summary>The largest answer read.</summary>
    public const int MostBytes = 4 * 1024;

    private const string Field = "lastGalaxyTick";

    private static readonly string[] Formats =
    [
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz",
    ];

    private readonly HttpClient _http;

    private readonly ILogger<InfomancerBgsTickSource> _logger;

    private EntityTagHeaderValue? _etag;

    private DateTimeOffset? _held;

    public InfomancerBgsTickSource(ILogger<InfomancerBgsTickSource> logger, HttpMessageHandler? handler = null)
    {
        _logger = logger;
        _http = new HttpClient(handler ?? Handler());
        _http.Timeout = TimeSpan.FromSeconds(5);
        _http.MaxResponseContentBufferSize = MostBytes;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("d47");
    }

    /// <summary>Follows no redirect and keeps no cookie, so the request goes only to <see cref="Host"/> and carries nothing it set.</summary>
    internal static SocketsHttpHandler Handler() => new() { AllowAutoRedirect = false, UseCookies = false };

    /// <summary>Not safe to call concurrently: the conditional request reads and writes the last ETag.</summary>
    public async Task<DateTimeOffset?> LastTickAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Address);

        if (_etag is { } etag && _held is not null)
        {
            request.Headers.IfNoneMatch.Add(etag);
        }

        try
        {
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotModified && _held is { } held)
            {
                return held;
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                _logger.LogInformation("BGS tick fetch skipped: the host answered {Status}", (int)response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

            if (Read(body) is not { } time)
            {
                _logger.LogInformation("BGS tick fetch ignored: the answer was not a {Field} time", Field);
                return null;
            }

            _etag = response.Headers.ETag;
            _held = time;
            return time;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("BGS tick fetch failed: {Reason}", ex.GetType().Name);
            return null;
        }
    }

    /// <summary>The tick time in an answer, or null when it is too long, not JSON or has no UTC time in it.</summary>
    internal static DateTimeOffset? Read(byte[] body)
    {
        if (body.Length > MostBytes)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);

            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(Field, out var value)
                && value.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParseExact(
                    value.GetString(),
                    Formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var parsed)
                && parsed.Offset == TimeSpan.Zero
                    ? parsed
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Dispose() => _http.Dispose();
}

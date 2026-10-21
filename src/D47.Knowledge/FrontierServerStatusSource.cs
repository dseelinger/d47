using System.Net;
using System.Text.Json;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Knowledge;

/// <summary><see cref="IServerStatusSource"/> against Frontier's server status page.</summary>
public sealed class FrontierServerStatusSource : IServerStatusSource, IDisposable
{
    /// <summary>Named here and in <see cref="D47.Core.Configuration.EgressDisclosure"/>.</summary>
    public const string Host = "ed-server-status.orerve.net";

    /// <summary>The largest answer read.</summary>
    public const int MostBytes = 4 * 1024;

    /// <summary>The <c>status</c> value of a healthy answer.</summary>
    public const string OnlineStatus = "Good";

    public const string MaintenanceStatus = "Maintenance";

    private readonly HttpClient _http;
    private readonly ILogger<FrontierServerStatusSource> _logger;

    public FrontierServerStatusSource(ILogger<FrontierServerStatusSource> logger, HttpMessageHandler? handler = null)
    {
        _logger = logger;
        _http = new HttpClient(handler ?? InfomancerBgsTickSource.Handler());
        _http.Timeout = TimeSpan.FromSeconds(5);
        _http.MaxResponseContentBufferSize = MostBytes;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("d47");
    }

    public static string Address => $"https://{Host}/";

    public async Task<GameServerState> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(Address, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                _logger.LogInformation("Server status skipped: the host answered {Status}", (int)response.StatusCode);
                return GameServerState.Unknown;
            }

            var body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

            return body.Length > MostBytes ? GameServerState.Unknown : Parse(System.Text.Encoding.UTF8.GetString(body));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Server status failed: {Reason}", ex.GetType().Name);
            return GameServerState.Unknown;
        }
    }

    /// <summary>Online for <see cref="OnlineStatus"/>, Maintenance for <see cref="MaintenanceStatus"/>, Offline for any other string status, Unknown when there is none.</summary>
    public static GameServerState Parse(string response)
    {
        try
        {
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("status", out var status)
                || status.ValueKind != JsonValueKind.String)
            {
                return GameServerState.Unknown;
            }

            return status.GetString() switch
            {
                OnlineStatus => GameServerState.Online,
                MaintenanceStatus => GameServerState.Maintenance,
                _ => GameServerState.Offline,
            };
        }
        catch (JsonException)
        {
            return GameServerState.Unknown;
        }
    }

    public void Dispose() => _http.Dispose();
}

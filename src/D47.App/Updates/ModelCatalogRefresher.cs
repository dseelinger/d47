using D47.Core.Capabilities.Builtin;
using D47.Core.Catalog;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace D47.App.Updates;

/// <summary>
/// Fetches the published model catalog at startup and every 24 hours while
/// <see cref="ModelSettings.RefreshCatalog"/> is on, on the thread pool.
/// </summary>
public sealed class ModelCatalogRefresher : IDisposable
{
    public const string Address = "https://raw.githubusercontent.com/dseelinger/d47/models/model-catalog.json";

    public static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private const int MostBytes = 1024 * 1024;

    private readonly ModelCatalogCache _cache;

    private readonly SettingsService _settings;

    private readonly ILogger<ModelCatalogRefresher> _logger;

    private readonly HttpClient _http;

    private readonly CancellationTokenSource _stopping = new();

    public ModelCatalogRefresher(
        ModelCatalogCache cache,
        SettingsService settings,
        ILogger<ModelCatalogRefresher> logger,
        HttpMessageHandler? handler = null)
    {
        _cache = cache;
        _settings = settings;
        _logger = logger;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(5);
        _http.MaxResponseContentBufferSize = MostBytes;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("d47");
        _settings.Changed += OnSettingsChanged;
    }

    /// <summary>Fetches now, then every <see cref="Interval"/> until disposed.</summary>
    public void Start() => _ = Task.Run(() => RunAsync(_stopping.Token));

    /// <summary>One fetch, if the setting is on. A failure leaves the catalog in use unchanged.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (!_settings.Current.Models.RefreshCatalog)
        {
            return;
        }

        try
        {
            using var response = await _http.GetAsync(Address, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Model catalog fetch skipped: GitHub returned {Status}", response.StatusCode);
                return;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            _cache.Offer(json);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Model catalog fetch failed: {Reason}", ex.Message);
        }
    }

    public void Dispose()
    {
        _settings.Changed -= OnSettingsChanged;
        _stopping.Cancel();
        _stopping.Dispose();
        _http.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(Interval);

            do
            {
                await RefreshAsync(cancellationToken).ConfigureAwait(false);
            }
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnSettingsChanged(SettingsChanged change)
    {
        if (change.Key != ConversationCapability.RefreshCatalogKey)
        {
            return;
        }

        if (change.Settings.Models.RefreshCatalog)
        {
            _ = Task.Run(() => RefreshAsync(_stopping.Token));
        }
        else
        {
            _cache.Load();
        }
    }
}

using System.Globalization;
using System.Text.Json;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Knowledge;

/// <summary><see cref="ICommunityBiology"/> against Canonn's per-system codex record.</summary>
public sealed class CanonnBiologyService : ICommunityBiology, IDisposable
{
    public const string Host = "us-central1-canonn-api-236217.cloudfunctions.net";

    public const string Path = "query/getSystemPoi";

    private readonly HttpClient _http;
    private readonly ILogger<CanonnBiologyService> _logger;
    private readonly bool _ownsClient;

    public CanonnBiologyService(ILogger<CanonnBiologyService> logger, HttpClient? http = null)
    {
        _logger = logger;
        _ownsClient = http is null;

        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.BaseAddress ??= new Uri($"https://{Host}/");

        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("d47/0.1 (+https://github.com/dseelinger/d47)");
        }
    }

    public async Task<IReadOnlyList<EarlierFind>> SystemFindsAsync(string systemName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(systemName))
        {
            return [];
        }

        try
        {
            using var response = await _http
                .GetAsync($"{Path}?system={Uri.EscapeDataString(systemName)}", cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Canonn answered {Status} for a system's biology", (int)response.StatusCode);
                return [];
            }

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            return Read(systemName, document);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Canonn took too long to answer");
            return [];
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Canonn could not be reached");
            return [];
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Canonn answered with something that was not JSON");
            return [];
        }
    }

    internal static IReadOnlyList<EarlierFind> Read(string systemName, JsonDocument document)
    {
        var finds = new List<EarlierFind>();

        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("codex", out var codex)
            || codex.ValueKind != JsonValueKind.Array)
        {
            return finds;
        }

        foreach (var row in codex.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object
                || Text(row, "hud_category") != "Biology"
                || Text(row, "body") is not { } body
                || Text(row, "english_name") is not { } name
                || Number(row, "entryid") is not { } entryId
                || Number(row, "latitude") is not { } latitude
                || Number(row, "longitude") is not { } longitude)
            {
                continue;
            }

            var full = body.StartsWith(systemName, StringComparison.OrdinalIgnoreCase) ? body : $"{systemName} {body}";
            finds.Add(new EarlierFind(full, (long)entryId, name, latitude, longitude));
        }

        return finds;
    }

    private static string? Text(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
        && value.GetString()?.Trim() is { Length: > 0 } text
            ? text
            : null;

    private static double? Number(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) switch
        {
            true when value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) => number,
            true when value.ValueKind == JsonValueKind.String
                      && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };

    public void Dispose()
    {
        if (_ownsClient)
        {
            _http.Dispose();
        }
    }
}

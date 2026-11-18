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

        _http = KnowledgeHttp.Client(http, Host, TimeSpan.FromSeconds(20));
    }

    public async Task<IReadOnlyList<EarlierFind>> SystemFindsAsync(string systemName, CancellationToken cancellationToken) =>
        await FetchAsync(systemName, document => Read(systemName, document), cancellationToken).ConfigureAwait(false) ?? [];

    public Task<CommunitySystemRecord?> SystemRecordAsync(string systemName, CancellationToken cancellationToken) =>
        FetchAsync(systemName, ReadRecord, cancellationToken);

    private async Task<T?> FetchAsync<T>(string systemName, Func<JsonDocument, T> read, CancellationToken cancellationToken)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(systemName))
        {
            return null;
        }

        try
        {
            using var response = await _http
                .GetAsync($"{Path}?system={Uri.EscapeDataString(systemName)}", cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Canonn answered {Status} for a system's biology", (int)response.StatusCode);
                return null;
            }

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            return read(document);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Canonn took too long to answer");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Canonn could not be reached");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Canonn answered with something that was not JSON");
            return null;
        }
    }

    internal static CommunitySystemRecord ReadRecord(JsonDocument document) =>
        new(
            Rows(document, "codex", CodexRow),
            Rows(document, "ScanOrganic", CodexRow),
            Rows(document, "SAAsignals", SignalRow));

    private static List<T> Rows<T>(JsonDocument document, string section, Func<JsonElement, T> read) =>
        document.RootElement.ValueKind == JsonValueKind.Object
        && document.RootElement.TryGetProperty(section, out var rows)
        && rows.ValueKind == JsonValueKind.Array
            ? [.. rows.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object).Select(read)]
            : [];

    private static CodexReport CodexRow(JsonElement row) =>
        new(
            Text(row, "body"),
            Text(row, "english_name"),
            Whole(row, "entryid"),
            Text(row, "hud_category"),
            Whole(row, "index_id"),
            JsonNumbers.Number(row, "latitude"),
            JsonNumbers.Number(row, "longitude"),
            Text(row, "scanned"));

    private static SignalReport SignalRow(JsonElement row) =>
        new(
            Text(row, "body"),
            Text(row, "english_name"),
            Text(row, "hud_category"),
            JsonNumbers.Number(row, "count") is { } count ? (int)count : null);

    private static long? Whole(JsonElement row, string property) =>
        JsonNumbers.Number(row, property) is { } number ? (long)number : null;

    internal static IReadOnlyList<EarlierFind> Read(string systemName, JsonDocument document)
    {
        var finds = new List<EarlierFind>();

        foreach (var row in ReadRecord(document).Codex)
        {
            if (row is not { Category: "Biology", Body: { } body, Name: { } name, EntryId: { } entryId, Latitude: { } latitude, Longitude: { } longitude })
            {
                continue;
            }

            var full = body.StartsWith(systemName, StringComparison.OrdinalIgnoreCase) ? body : $"{systemName} {body}";
            finds.Add(new EarlierFind(full, entryId, name, latitude, longitude));
        }

        return finds;
    }

    private static string? Text(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
        && value.GetString()?.Trim() is { Length: > 0 } text
            ? text
            : null;

    public void Dispose()
    {
        if (_ownsClient)
        {
            _http.Dispose();
        }
    }
}

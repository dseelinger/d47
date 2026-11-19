using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Knowledge;

/// <summary>
/// <see cref="ICommunityBiology"/> against Canonn's per-system codex record, and <see cref="IRegionFirstReport"/>
/// to Canonn's event intake.
/// </summary>
public sealed class CanonnBiologyService : ICommunityBiology, IRegionFirstReport, IDisposable
{
    public const string Host = "us-central1-canonn-api-236217.cloudfunctions.net";

    public const string Path = "query/getSystemPoi";

    public const string ReportPath = "postEvent";

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

    public async Task<bool> SendAsync(RegionFirstReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (ReportBody(report) is not { } body)
        {
            _logger.LogWarning("A region first had no journal line or position to send");
            return false;
        }

        try
        {
            using var content = new ByteArrayContent(body);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using var response = await _http.PostAsync(ReportPath, content, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                return true;
            }

            _logger.LogWarning("Canonn answered {Status} to a region first report", (int)response.StatusCode);
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Canonn took too long to answer a region first report");
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Canonn could not be reached to report a region first");
            return false;
        }
    }

    /// <summary>The postEvent body for <paramref name="report"/>, or null when its journal line is not a JSON object or it has no position.</summary>
    internal static byte[]? ReportBody(RegionFirstReport report)
    {
        var record = report.Record;

        if (record is not { Journal: { Length: > 0 } line, StarPos: { } position })
        {
            return null;
        }

        JsonDocument raw;

        try
        {
            raw = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return null;
        }

        using (raw)
        {
            if (raw.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            using var buffer = new MemoryStream();

            using (var json = new Utf8JsonWriter(buffer))
            {
                json.WriteStartObject();

                json.WriteStartObject("gameState");
                json.WriteString("systemName", record.System);
                json.WriteStartArray("systemCoordinates");
                json.WriteNumberValue(position.X);
                json.WriteNumberValue(position.Y);
                json.WriteNumberValue(position.Z);
                json.WriteEndArray();
                json.WriteString("bodyName", record.Body);
                WriteNumber(json, "latitude", record.Latitude);
                WriteNumber(json, "longitude", record.Longitude);
                json.WriteString("clientVersion", report.ClientVersion);
                json.WriteBoolean("isBeta", false);
                json.WriteString("platform", "PC");
                json.WriteBoolean("odyssey", report.Odyssey);
                json.WriteEndObject();

                json.WritePropertyName("rawEvent");
                raw.RootElement.WriteTo(json);
                json.WriteString("eventType", "CodexEntry");
                json.WriteString("cmdrName", report.CommanderName);

                json.WriteEndObject();
            }

            return buffer.ToArray();
        }
    }

    private static void WriteNumber(Utf8JsonWriter json, string name, double? value)
    {
        if (value is { } number)
        {
            json.WriteNumber(name, number);
        }
        else
        {
            json.WriteNull(name);
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

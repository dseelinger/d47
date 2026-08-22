using System.Globalization;
using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Knowledge;

/// <summary>
/// <see cref="INotablePlacesService"/> against EDAstro's Galactic Exploration Catalog — the
/// community catalogue the Galactic Mapping Project was migrated into (list.md Phase 47).
/// <para>
/// <b>One document, fetched whole and filtered here.</b> The catalogue is served as a single JSON
/// array of every point of interest — 633 entries and about two megabytes on 2026-08-22 — and there
/// is no endpoint that takes a position. So the request carries nothing about the Commander at all:
/// where they are never leaves this machine, and the radius is applied to the downloaded list. The
/// document is kept in memory for a while so that a draft and its revisions do not fetch it three
/// times in five minutes; it is never written to disk.
/// </para>
/// <para>
/// EDSM's own Galactic Mapping endpoint answers any non-browser client with a bot challenge (measured
/// 2026-08-22), which is why the source is this one. Its content is CC BY-NC-SA 3.0, which d47's
/// non-commercial use and the attribution in <c>NOTICE</c> satisfy; nothing from it is ever stored in
/// a table of d47's own.
/// </para>
/// </summary>
public sealed class GecNotablePlacesService : INotablePlacesService, IDisposable
{
    /// <summary>Named here and in <see cref="D47.Core.Configuration.EgressDisclosure"/>, like the spansh host.</summary>
    public const string Host = "edastro.com";

    public const string Path = "gec/json/all";

    private static readonly TimeSpan KeepFor = TimeSpan.FromHours(6);

    private readonly HttpClient _http;
    private readonly ILogger<GecNotablePlacesService> _logger;
    private readonly bool _ownsClient;
    private readonly Func<DateTimeOffset> _now;
    private readonly SemaphoreSlim _fetching = new(1, 1);

    private IReadOnlyList<NotablePlace>? _places;
    private DateTimeOffset _fetchedAt;

    public GecNotablePlacesService(
        ILogger<GecNotablePlacesService> logger,
        HttpClient? http = null,
        Func<DateTimeOffset>? now = null)
    {
        _logger = logger;
        _ownsClient = http is null;
        _now = now ?? (() => DateTimeOffset.UtcNow);

        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _http.BaseAddress ??= new Uri($"https://{Host}/");

        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("d47/0.1 (+https://github.com/dseelinger/d47)");
        }
    }

    public async Task<IReadOnlyList<NotablePlace>> NearAsync(
        StarPosition from,
        double radiusLightYears,
        int limit,
        CancellationToken cancellationToken)
    {
        var places = await CatalogueAsync(cancellationToken).ConfigureAwait(false);

        return
        [
            .. places
                .Select(place => (Place: place, Distance: place.DistanceFrom(from)))
                .Where(pair => pair.Distance <= radiusLightYears)
                .OrderByDescending(pair => pair.Place.Rating ?? 0)
                .ThenBy(pair => pair.Distance)
                .Take(Math.Max(1, limit))
                .Select(pair => pair.Place),
        ];
    }

    private async Task<IReadOnlyList<NotablePlace>> CatalogueAsync(CancellationToken cancellationToken)
    {
        if (_places is { } kept && _now() - _fetchedAt < KeepFor)
        {
            return kept;
        }

        await _fetching.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_places is { } again && _now() - _fetchedAt < KeepFor)
            {
                return again;
            }

            JsonDocument document;

            try
            {
                using var response = await _http.GetAsync(Path, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("The notable-places catalogue answered {Status}", (int)response.StatusCode);
                    throw new GalaxyUnavailableException("The catalogue of notable places refused the request.");
                }

                var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw new GalaxyUnavailableException("The catalogue of notable places took too long to answer.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "The notable-places catalogue could not be reached");
                throw new GalaxyUnavailableException("I couldn't reach the catalogue of notable places — check the network connection.");
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "The notable-places catalogue answered with something that was not JSON");
                throw new GalaxyUnavailableException("The catalogue of notable places answered with something I couldn't read.");
            }

            using (document)
            {
                _places = Read(document);
                _fetchedAt = _now();
            }

            _logger.LogInformation("Read {Count} notable places from {Host}", _places.Count, Host);
            return _places;
        }
        finally
        {
            _fetching.Release();
        }
    }

    /// <summary>
    /// The document's shape, as measured on 2026-08-22: every field a string, coordinates as a
    /// bracketed list inside one, ids as decimal text. Read tolerantly — an entry missing a system
    /// or a position is skipped rather than failing the lot.
    /// </summary>
    internal static IReadOnlyList<NotablePlace> Read(JsonDocument document)
    {
        var places = new List<NotablePlace>();

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return places;
        }

        foreach (var entry in document.RootElement.EnumerateArray())
        {
            var name = Text(entry, "name");
            var system = Text(entry, "galMapSearch");

            if (name is null || system is null || Position(entry) is not { } position)
            {
                continue;
            }

            places.Add(new NotablePlace(name, Text(entry, "type") ?? "Point of interest", system, Long(entry, "id64"), position)
            {
                Region = Text(entry, "region"),
                Summary = Text(entry, "summary"),
                Rating = Number(entry, "rating"),
            });
        }

        return places;
    }

    private static string? Text(JsonElement entry, string property) =>
        entry.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
        && value.GetString() is { Length: > 0 } text && text != "None"
            ? text.Trim()
            : null;

    private static long? Long(JsonElement entry, string property) =>
        entry.TryGetProperty(property, out var value) switch
        {
            true when value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) => number,
            true when value.ValueKind == JsonValueKind.String
                      && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };

    private static double? Number(JsonElement entry, string property) =>
        entry.TryGetProperty(property, out var value) switch
        {
            true when value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) => number,
            true when value.ValueKind == JsonValueKind.String
                      && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };

    private static StarPosition? Position(JsonElement entry)
    {
        if (!entry.TryGetProperty("coordinates", out var value))
        {
            return null;
        }

        string[] parts;

        if (value.ValueKind == JsonValueKind.Array)
        {
            parts = [.. value.EnumerateArray().Select(axis => axis.ToString())];
        }
        else if (value.ValueKind == JsonValueKind.String)
        {
            parts = value.GetString()!.Trim('[', ']', ' ').Split(',');
        }
        else
        {
            return null;
        }

        if (parts.Length != 3
            || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            || !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
        {
            return null;
        }

        return new StarPosition(x, y, z);
    }

    public void Dispose()
    {
        _fetching.Dispose();

        if (_ownsClient)
        {
            _http.Dispose();
        }
    }
}

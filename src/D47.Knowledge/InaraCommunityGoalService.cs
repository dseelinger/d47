using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Knowledge;

/// <summary><see cref="ICommunityGoalService"/> against inara.cz.</summary>
public sealed class InaraCommunityGoalService : ICommunityGoalService, IDisposable
{
    /// <summary>
    /// The one host this capability reaches, named here and in <see
    /// cref="D47.Core.Configuration.EgressDisclosure"/> — a destination that exists in code and not in
    /// the disclosure is the failure that section exists to prevent.
    /// </summary>
    public const string Host = "inara.cz";

    public const string Endpoint = "https://inara.cz/inapi/v1/";

    private const string Event = "getCommunityGoalsRecent";

    private readonly Func<string?> _key;

    private readonly string _version;

    private readonly HttpClient _http;

    private readonly ILogger<InaraCommunityGoalService> _logger;

    private readonly bool _ownsClient;

    /// <summary>
    /// <param name="key"> Read on every call rather than captured once: a Commander who pastes a key in
    /// mid-session should not have to restart d47 to use it, and one who clears it should stop reaching
    /// the site immediately.
    /// </summary>
    /// <param name="key">
    /// Read on every call rather than captured once: a Commander who pastes a key in mid-session should
    /// not have to restart d47 to use it, and one who clears it should stop reaching the site
    /// immediately.
    /// </param>
    public InaraCommunityGoalService(
        Func<string?> key,
        string version,
        ILogger<InaraCommunityGoalService> logger,
        HttpClient? http = null)
    {
        _key = key;
        _version = version;
        _logger = logger;
        _ownsClient = http is null;

        _http = http ?? new HttpClient
        {
            // The same reasoning as the galaxy search: this runs while a Commander waits for an answer, so a
            // lookup that takes half a minute has already failed at being useful.
            Timeout = TimeSpan.FromSeconds(15),
        };

        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("d47/0.1 (+https://github.com/dseelinger/d47)");
        }
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_key());

    public async Task<CommunityGoalReport> RecentAsync(CancellationToken cancellationToken)
    {
        if (_key() is not { } key || string.IsNullOrWhiteSpace(key))
        {
            throw new CommunityGoalsUnavailableException(
                "There is no Inara API key stored, so I can only see the goals your own journal has reported.");
        }

        using var content = new StringContent(
            Request(key, _version, DateTimeOffset.UtcNow),
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response;

        try
        {
            response = await _http.PostAsync(Endpoint, content, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Inara did not answer within the timeout");
            throw new CommunityGoalsUnavailableException("Inara took too long to answer.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Inara could not be reached");
            throw new CommunityGoalsUnavailableException("I couldn't reach Inara — check the network connection.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Inara answered {Status}", (int)response.StatusCode);

                throw new CommunityGoalsUnavailableException(response.StatusCode switch
                {
                    HttpStatusCode.TooManyRequests => "Inara is rate limiting me. It should clear shortly.",
                    >= HttpStatusCode.InternalServerError =>
                        "Inara reported a server error. It should clear shortly.",
                    _ => "Inara refused that request.",
                });
            }

            try
            {
                var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

                using var document = await JsonDocument
                    .ParseAsync(stream, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                return Read(document.RootElement);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Inara answered with something that was not JSON");
                throw new CommunityGoalsUnavailableException("Inara answered with something I couldn't read.");
            }
        }
    }

    /// <summary>The request body.</summary>
    internal static string Request(string key, string version, DateTimeOffset now) =>
        JsonSerializer.Serialize(new
        {
            header = new
            {
                appName = "d47",
                appVersion = version,
                isBeingDeveloped = false,
                APIkey = key,
            },
            events = new[]
            {
                new
                {
                    eventName = Event,

                    // "Always use a correct date and time related to the event ...
                    eventTimestamp = now.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),

                    // Documented as "no properties to set (but eventData is still required)".
                    eventData = Array.Empty<object>(),
                },
            },
        });

    /// <summary>Reads the envelope.</summary>
    private static CommunityGoalReport Read(JsonElement root)
    {
        var header = Object(root, "header");

        if (header is { } envelope && Int(envelope, "eventStatus") is { } headerStatus and >= 400)
        {
            // The site's own explanation where it gave one: an invalid key and a malformed request are
            // different problems and only the Commander can tell them apart.
            throw new CommunityGoalsUnavailableException(
                Text(envelope, "eventStatusText") is { Length: > 0 } why
                    ? $"Inara rejected the request: {why} Check the API key."
                    : "Inara rejected the request. Check the API key.");
        }

        var events = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("events", out var array) &&
            array.ValueKind == JsonValueKind.Array
                ? array
                : default;

        if (events.ValueKind != JsonValueKind.Array)
        {
            throw new CommunityGoalsUnavailableException("Inara answered with something I couldn't read.");
        }

        foreach (var entry in events.EnumerateArray())
        {
            var status = Int(entry, "eventStatus");

            if (status is >= 400)
            {
                throw new CommunityGoalsUnavailableException(
                    Text(entry, "eventStatusText") is { Length: > 0 } why
                        ? $"Inara could not list the community goals: {why}"
                        : "Inara could not list the community goals.");
            }

            // 204 is a soft error — formally fine, nothing found — and reads as an empty board, which is a
            // true answer rather than a fault.
            if (!entry.TryGetProperty("eventData", out var data) || data.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            return new CommunityGoalReport([.. data.EnumerateArray().Select(ReadGoal).Where(goal => goal is not null).Select(goal => goal!)]);
        }

        return new CommunityGoalReport([]);
    }

    private static CommunityGoalListing? ReadGoal(JsonElement entry)
    {
        if (Text(entry, "communitygoalName") is not { Length: > 0 } name)
        {
            return null;
        }

        return new CommunityGoalListing
        {
            Name = name,
            SystemName = Text(entry, "starsystemName"),
            StationName = Text(entry, "stationName"),
            Expiry = Time(entry, "goalExpiry"),
            TierReached = Positive(Int(entry, "tierReached")),

            // Zero is the site saying it does not know: the journals carry no maximum tier, so an entry built
            // from a journal upload has tierMax 0.
            TopTier = Positive(Int(entry, "tierMax")),
            Contributors = Int(entry, "contributorsNum"),
            ContributionsTotal = Long(entry, "contributionsTotal"),
            IsComplete = entry.TryGetProperty("isCompleted", out var complete) &&
                complete.ValueKind == JsonValueKind.True,
            LastUpdate = Time(entry, "lastUpdate"),
            Objective = Clip(Text(entry, "goalObjectiveText")),
            Reward = Clip(Text(entry, "goalRewardText")),
        };
    }

    /// <summary>How much third-party free text is allowed through, per field.</summary>
    private const int FreeTextLimit = 300;

    private static string? Clip(string? text) => text switch
    {
        null or "" => null,
        { Length: <= FreeTextLimit } => text,
        _ => text[..FreeTextLimit] + "…",
    };

    private static int? Positive(int? value) => value is > 0 ? value : null;

    private static JsonElement? Object(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Object
            ? value
            : null;

    private static string? Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? Int(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : null;

    private static long? Long(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)
            ? number
            : null;

    private static DateTimeOffset? Time(JsonElement element, string property) =>
        DateTimeOffset.TryParse(
            Text(element, property),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : null;

    public void Dispose()
    {
        if (_ownsClient)
        {
            _http.Dispose();
        }
    }
}

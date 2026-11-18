using System.Globalization;
using System.Net;
using System.Text.Json;
using D47.Core.Conversation;

namespace D47.Tts;

/// <summary>Reads the character quota from ElevenLabs' subscription endpoint.</summary>
public sealed class ElevenLabsAccountReader : IAccountReader
{
    private const string Url = "https://api.elevenlabs.io/v1/user/subscription";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly Func<string?> _key;
    private readonly HttpClient _http;

    /// <param name="key">Asked for at each read, so a key added or replaced later is used.</param>
    public ElevenLabsAccountReader(Func<string?> key, HttpClient? http = null)
    {
        _key = key;
        _http = http ?? new HttpClient();
    }

    public AccountProvider Provider => AccountProvider.ElevenLabs;

    public async Task<AccountReading> ReadAsync(CancellationToken cancellationToken)
    {
        if (_key() is not { Length: > 0 } key)
        {
            return new AccountReading.NoKey();
        }

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(Timeout);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Url);
            request.Headers.Add("xi-api-key", key);

            using var response = await _http.SendAsync(request, limit.Token).ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                var said = await ElevenLabsTtsProvider.MessageFromBodyAsync(response, limit.Token).ConfigureAwait(false);
                return new AccountReading.Refused((int)response.StatusCode, said);
            }

            if (!response.IsSuccessStatusCode)
            {
                return new AccountReading.Unreachable($"ElevenLabs answered {(int)response.StatusCode}");
            }

            var body = await response.Content.ReadAsStringAsync(limit.Token).ConfigureAwait(false);
            return Parse(body) ?? (AccountReading)new AccountReading.Unreachable("the reply could not be read");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new AccountReading.Unreachable("ElevenLabs did not answer in time");
        }
        catch (HttpRequestException exception)
        {
            return new AccountReading.Unreachable(exception.Message);
        }
    }

    private static ElevenLabsFigures? Parse(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !Number(root, "character_count", out var used)
                || !Number(root, "character_limit", out var limit))
            {
                return null;
            }

            DateTimeOffset? reset = Number(root, "next_character_count_reset_unix", out var unix)
                ? DateTimeOffset.FromUnixTimeSeconds(unix)
                : null;

            return new ElevenLabsFigures(used, limit, reset, Period(root), Extension(root), Overage(root));
        }
        catch (Exception exception) when (exception is JsonException or ArgumentOutOfRangeException or FormatException or OverflowException)
        {
            return null;
        }
    }

    private static bool Number(JsonElement parent, string name, out long value)
    {
        value = 0;
        return parent.TryGetProperty(name, out var element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetInt64(out value);
    }

    private static RefreshPeriod? Period(JsonElement root)
    {
        if (!root.TryGetProperty("character_refresh_period", out var element) || element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return element.GetString() switch
        {
            "monthly_period" => RefreshPeriod.Monthly,
            "3_month_period" => RefreshPeriod.Quarterly,
            "6_month_period" => RefreshPeriod.HalfYearly,
            "annual_period" => RefreshPeriod.Annual,
            _ => null,
        };
    }

    private static LimitExtension Extension(JsonElement root)
    {
        if (!root.TryGetProperty("max_credit_limit_extension", out var element))
        {
            return LimitExtension.Stops;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt64(out var characters) && characters > 0 => LimitExtension.UpTo(characters),
            JsonValueKind.String when string.Equals(element.GetString(), "unlimited", StringComparison.OrdinalIgnoreCase) => LimitExtension.Unlimited,
            _ => LimitExtension.Stops,
        };
    }

    private static Money Overage(JsonElement root)
    {
        if (root.TryGetProperty("current_overage", out var overage)
            && overage.ValueKind == JsonValueKind.Object
            && overage.TryGetProperty("amount", out var amount)
            && amount.ValueKind == JsonValueKind.String
            && decimal.TryParse(amount.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            var currency = overage.TryGetProperty("currency", out var code) && code.ValueKind == JsonValueKind.String
                ? code.GetString() ?? "USD"
                : "USD";
            return new Money(parsed, currency);
        }

        return new Money(0m, "USD");
    }
}

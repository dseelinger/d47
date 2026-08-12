using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>
/// One parsed journal line. <see cref="Raw"/> is a cloned <see cref="JsonElement"/> — safe to
/// keep after the parsing <see cref="JsonDocument"/> is disposed — so fields the current
/// schema does not model yet stay reachable instead of being discarded.
/// </summary>
public sealed record JournalEvent(DateTimeOffset Timestamp, string Kind, JsonElement Raw)
{
    /// <summary>
    /// Parses one line. A missing "event" field or invalid JSON is the only thing that fails
    /// here — an unrecognised event *name* still parses fine, because surviving a schema
    /// change means new event types are inert, not unreadable (list.md Phase 2).
    /// </summary>
    public static bool TryParse(string line, ILogger logger, out JournalEvent? result)
    {
        result = null;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Malformed journal line skipped: {Line}", Truncate(line));
            return false;
        }

        using (document)
        {
            var root = document.RootElement;

            if (!root.TryGetProperty("event", out var eventProperty) ||
                eventProperty.ValueKind != JsonValueKind.String)
            {
                logger.LogWarning("Journal line has no 'event' field; skipped: {Line}", Truncate(line));
                return false;
            }

            var timestamp = DateTimeOffset.MinValue;
            if (root.TryGetProperty("timestamp", out var timestampProperty) &&
                timestampProperty.ValueKind == JsonValueKind.String)
            {
                DateTimeOffset.TryParse(
                    timestampProperty.GetString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out timestamp);
            }

            result = new JournalEvent(timestamp, eventProperty.GetString()!, root.Clone());
            return true;
        }
    }

    public bool TryGetString(string property, out string? value)
    {
        value = Raw.ValueKind == JsonValueKind.Object &&
                Raw.TryGetProperty(property, out var element) &&
                element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

        return value is not null;
    }

    public bool TryGetBoolean(string property, out bool value)
    {
        if (Raw.ValueKind == JsonValueKind.Object &&
            Raw.TryGetProperty(property, out var element) &&
            element.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = element.GetBoolean();
            return true;
        }

        value = false;
        return false;
    }

    private static string Truncate(string line) => line.Length <= 200 ? line : line[..200] + "…";
}

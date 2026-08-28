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
    /// change means new event types are inert, not unreadable (Phase 2).
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

    /// <summary>
    /// Field access, forwarded to <see cref="JournalJson"/> so an event and a nested object are
    /// read exactly the same way. All of these answer null for a field that is missing or is
    /// the wrong type; see that class for why that matters.
    /// </summary>
    public string? String(string property) => Raw.String(property);

    public bool Bool(string property) => Raw.Bool(property);

    public int? Int(string property) => Raw.Int(property);

    public long? Long(string property) => Raw.Long(property);

    public double? Double(string property) => Raw.Double(property);

    public JsonElement? Object(string property) => Raw.Object(property);

    public IEnumerable<JsonElement> Items(string property) => Raw.Items(property);

    /// <summary>The player-facing name where Elite supplies one, else the internal symbol.</summary>
    public string? Named(string property) => Raw.Named(property);

    private static string Truncate(string line) => line.Length <= 200 ? line : line[..200] + "…";
}

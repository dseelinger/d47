using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>One parsed journal line.</summary>
public sealed record JournalEvent(DateTimeOffset Timestamp, string Kind, JsonElement Raw)
{
    /// <summary>Whether Elite wrote this line for an Operation, as <see cref="OperationMarker"/> marks it.</summary>
    public bool InOperation { get; init; }

    /// <summary>A <c>Location</c> in an Operation's instance, which names a place no record outlives the run to keep.</summary>
    public bool IsOperationLocation => Kind == "Location" && InOperation;

    /// <summary>A <c>LoadGame</c> that starts a play session rather than entering or leaving an Operation.</summary>
    public bool StartsSession => Kind == "LoadGame" && !InOperation;

    /// <summary>Parses one line.</summary>
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
    /// Field access, forwarded to <see cref="JournalJson"/> so an event and a nested object are read
    /// exactly the same way.
    /// </summary>
    public string? String(string property) => Raw.String(property);

    public bool Bool(string property) => Raw.Bool(property);

    /// <summary><c>LaunchSRV</c>, or a <c>LaunchVessel</c> the Commander flies, such as the Nomad.</summary>
    public bool LaunchesSrv => Kind is "LaunchSRV" || (Kind is "LaunchVessel" && Bool("PlayerControlled"));

    /// <summary>Whether the Commander is in an SRV after this event, or null when the event does not say.</summary>
    public bool? InSrvAfter => Kind switch
    {
        "LaunchSRV" => true,
        "LaunchVessel" => LaunchesSrv ? true : null,
        "Embark" => Bool("SRV"),
        "Location" => Bool("InSRV"),
        "LoadGame" or "DockSRV" or "SRVDestroyed" or "Died" or "Disembark" => false,
        _ => null,
    };

    public int? Int(string property) => Raw.Int(property);

    public long? Long(string property) => Raw.Long(property);

    public double? Double(string property) => Raw.Double(property);

    public JsonElement? Object(string property) => Raw.Object(property);

    public IEnumerable<JsonElement> Items(string property) => Raw.Items(property);

    /// <summary>The player-facing name where Elite supplies one, else the internal symbol.</summary>
    public string? Named(string property) => Raw.Named(property);

    private static string Truncate(string line) => line.Length <= 200 ? line : line[..200] + "…";
}

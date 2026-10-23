using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using D47.Core.Capabilities;

namespace D47.Core.Usage;

/// <summary>Counts of tool calls per UTC day, tool and caller. Reads no clock and holds no identifier.</summary>
public sealed class UsageLedger
{
    /// <summary>Days older than this many days before the given day are dropped.</summary>
    public const int KeepDays = 30;

    private const string DayFormat = "yyyy-MM-dd";

    private readonly Dictionary<DateOnly, Dictionary<(string Tool, ToolCaller Caller), int>> _days = [];
    private readonly object _gate = new();

    public static DateOnly DayOf(DateTimeOffset at) => DateOnly.FromDateTime(at.UtcDateTime);

    public void Record(string tool, ToolCaller caller, DateTimeOffset at)
    {
        lock (_gate)
        {
            var day = DayOf(at);
            if (!_days.TryGetValue(day, out var counts))
            {
                counts = [];
                _days[day] = counts;
            }

            counts[(tool, caller)] = counts.GetValueOrDefault((tool, caller)) + 1;
        }
    }

    /// <summary>The days before <paramref name="today"/> that hold counts, oldest first.</summary>
    public IReadOnlyList<DateOnly> CompletedDays(DateOnly today)
    {
        lock (_gate)
        {
            return [.. _days.Keys.Where(day => day < today).Order()];
        }
    }

    /// <summary>The body the usage Worker accepts for one day, or null when the day holds no counts.</summary>
    public string? Report(DateOnly day)
    {
        lock (_gate)
        {
            if (!_days.TryGetValue(day, out var counts) || counts.Count == 0)
            {
                return null;
            }

            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                writer.WriteString("day", day.ToString(DayFormat, CultureInfo.InvariantCulture));
                writer.WriteStartArray("uses");
                WriteUses(writer, counts);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }
    }

    public void Remove(DateOnly day)
    {
        lock (_gate)
        {
            _days.Remove(day);
        }
    }

    /// <summary>Drops days more than <see cref="KeepDays"/> days before <paramref name="today"/>.</summary>
    public void DropOld(DateOnly today)
    {
        lock (_gate)
        {
            foreach (var day in _days.Keys.Where(day => today.DayNumber - day.DayNumber > KeepDays).ToList())
            {
                _days.Remove(day);
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _days.Clear();
        }
    }

    public string ToJson()
    {
        lock (_gate)
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                writer.WriteStartObject("days");
                foreach (var (day, counts) in _days.OrderBy(pair => pair.Key))
                {
                    writer.WriteStartArray(day.ToString(DayFormat, CultureInfo.InvariantCulture));
                    WriteUses(writer, counts);
                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }
    }

    /// <summary>Parses <see cref="ToJson"/> output; anything malformed gives an empty ledger.</summary>
    public static UsageLedger Parse(string json)
    {
        var ledger = new UsageLedger();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("days", out var days)
                || days.ValueKind != JsonValueKind.Object)
            {
                return ledger;
            }

            var parsed = new UsageLedger();
            foreach (var entry in days.EnumerateObject())
            {
                if (!DateOnly.TryParseExact(entry.Name, DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                    || entry.Value.ValueKind != JsonValueKind.Array)
                {
                    return ledger;
                }

                var counts = new Dictionary<(string Tool, ToolCaller Caller), int>();
                foreach (var use in entry.Value.EnumerateArray())
                {
                    var tool = use.GetProperty("tool").GetString();
                    var caller = use.GetProperty("caller").GetString() switch
                    {
                        "commander" => ToolCaller.Commander,
                        "model" => ToolCaller.Model,
                        _ => (ToolCaller?)null,
                    };
                    var count = use.GetProperty("count").GetInt32();
                    if (string.IsNullOrEmpty(tool) || caller is null || count <= 0)
                    {
                        return ledger;
                    }

                    counts[(tool, caller.Value)] = counts.GetValueOrDefault((tool, caller.Value)) + count;
                }

                if (counts.Count > 0)
                {
                    parsed._days[day] = counts;
                }
            }

            return parsed;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or KeyNotFoundException)
        {
            return ledger;
        }
    }

    private static void WriteUses(Utf8JsonWriter writer, Dictionary<(string Tool, ToolCaller Caller), int> counts)
    {
        foreach (var ((tool, caller), count) in counts
            .OrderBy(pair => pair.Key.Tool, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.Caller))
        {
            writer.WriteStartObject();
            writer.WriteString("tool", tool);
            writer.WriteString("caller", caller == ToolCaller.Commander ? "commander" : "model");
            writer.WriteNumber("count", count);
            writer.WriteEndObject();
        }
    }
}

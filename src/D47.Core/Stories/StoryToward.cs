using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Journal;

namespace D47.Core.Stories;

/// <summary>One stop of a span's <c>"toward"</c>: a <see cref="StoryDestinations"/> name, or a system with its own position.</summary>
public sealed record StoryStop(
    [property: JsonPropertyName("system")] string? System,
    [property: JsonPropertyName("address"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? Address = null,
    [property: JsonPropertyName("position"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<double>? Position = null)
{
    /// <summary>The stop as a place, or null where it has no known position.</summary>
    public StoryTowardPlace? Place()
    {
        if (string.IsNullOrWhiteSpace(System))
        {
            return null;
        }

        if (Position is { Count: 3 } xyz)
        {
            return new(System, Address, new StarPosition(xyz[0], xyz[1], xyz[2]));
        }

        return Position is null && StoryDestinations.PositionOf(System) is { } at ? new(System, Address, at) : null;
    }
}

/// <summary>A stop of a span's travel with its position.</summary>
public sealed record StoryTowardPlace(string System, long? Address, StarPosition Position);

/// <summary>The stops a span travels to, in order: one name, one object, or a list of either.</summary>
[JsonConverter(typeof(StoryTowardConverter))]
public sealed record StoryToward(IReadOnlyList<StoryStop> Stops)
{
    /// <summary>Whether it was written as a list.</summary>
    public bool IsList { get; init; }

    public static implicit operator StoryToward(string system) => new([new StoryStop(system)]);
}

internal sealed class StoryTowardConverter : JsonConverter<StoryToward>
{
    public override StoryToward? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var stops = new List<StoryStop>();

            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                stops.Add(Stop(ref reader, options));
            }

            return new StoryToward(stops) { IsList = true };
        }

        return new StoryToward([Stop(ref reader, options)]);
    }

    public override void Write(Utf8JsonWriter writer, StoryToward value, JsonSerializerOptions options)
    {
        if (value.IsList)
        {
            writer.WriteStartArray();

            foreach (var stop in value.Stops)
            {
                Write(writer, stop, options);
            }

            writer.WriteEndArray();
        }
        else
        {
            Write(writer, value.Stops.FirstOrDefault() ?? new StoryStop(null), options);
        }
    }

    private static void Write(Utf8JsonWriter writer, StoryStop stop, JsonSerializerOptions options)
    {
        if (stop is { Address: null, Position: null, System: not null })
        {
            writer.WriteStringValue(stop.System);
        }
        else
        {
            JsonSerializer.Serialize(writer, stop, options);
        }
    }

    private static StoryStop Stop(ref Utf8JsonReader reader, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
            ? new StoryStop(reader.GetString())
            : JsonSerializer.Deserialize<StoryStop>(ref reader, options) ?? new StoryStop(null);
}

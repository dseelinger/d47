using System.Buffers;
using System.Text;
using System.Text.Json;

namespace D47.Core.Capabilities;

/// <summary>
/// Emits a tool's JSON Schema deterministically. Byte-identity across turns is what makes
/// prompt caching work, and tool definitions serialize first in the request, so a single
/// changed byte here invalidates the entire cached prefix rather than a tail of it
/// (architecture.md §6).
/// </summary>
public static class ToolSchemaWriter
{
    /// <summary>
    /// Compact, with a fixed key order and property names sorted ordinally. Sorting means
    /// reordering a parameter declaration in source cannot change the bytes. Whitespace is
    /// omitted because formatting is one more thing that would have to stay stable.
    /// Enum values keep their declared order, which is semantic.
    /// </summary>
    public static string Canonical(ToolDefinition tool)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "object");

            writer.WriteStartObject("properties");
            foreach (var parameter in tool.Parameters.OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                writer.WriteStartObject(parameter.Name);
                writer.WriteString("type", JsonTypeOf(parameter.Type));
                writer.WriteString("description", parameter.Description);

                if (parameter.AllowedValues.Count > 0)
                {
                    writer.WriteStartArray("enum");
                    foreach (var allowed in parameter.AllowedValues)
                    {
                        writer.WriteStringValue(allowed);
                    }

                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();

            writer.WriteStartArray("required");
            foreach (var name in tool.Parameters
                         .Where(p => p.Required)
                         .Select(p => p.Name)
                         .OrderBy(n => n, StringComparer.Ordinal))
            {
                writer.WriteStringValue(name);
            }

            writer.WriteEndArray();

            writer.WriteBoolean("additionalProperties", false);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static string JsonTypeOf(ToolParameterType type) => type switch
    {
        ToolParameterType.String => "string",
        ToolParameterType.Integer => "integer",
        ToolParameterType.Number => "number",
        ToolParameterType.Boolean => "boolean",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unmapped tool parameter type"),
    };
}

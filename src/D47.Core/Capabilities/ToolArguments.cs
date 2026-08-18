using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

namespace D47.Core.Capabilities;

/// <summary>
/// Arguments for one tool call. Flat string values, matching the flat scalar schema in
/// <see cref="ToolParameter"/>; the provider seam is responsible for flattening whatever
/// the model actually sent.
/// </summary>
public sealed class ToolArguments(IReadOnlyDictionary<string, string> values)
{
    public static ToolArguments Empty { get; } = new(new Dictionary<string, string>());

    public IReadOnlyDictionary<string, string> Values { get; } = values;

    /// <summary>
    /// The flattening the provider seam owes the registry: a model's JSON object becomes the flat
    /// string map every handler already reads.
    /// <para>
    /// Anything that is not a scalar is dropped rather than stringified. The schema
    /// (<see cref="ToolParameter"/>) only declares flat scalars, so an object or an array is
    /// something the model invented — and a handler receiving <c>"[object Object]"</c> for a
    /// parameter it believes is a string is worse than receiving nothing, because the missing
    /// case is one it already handles.
    /// </para>
    /// <para>
    /// Malformed JSON yields <see cref="Empty"/> rather than throwing. A tool call the model
    /// truncated is a bad call, and the handler's own "no value was given" answer is a better
    /// thing to send back than an exception crossing the turn loop.
    /// </para>
    /// </summary>
    public static ToolArguments FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Empty;
        }

        Dictionary<string, string> values = new(StringComparer.Ordinal);

        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Empty;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                var scalar = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.GetRawText(),

                    // Lowercased because TryGetBoolean parses with bool.TryParse, which is
                    // case-insensitive, and because the raw text is already what JSON says.
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => null,
                };

                if (scalar is not null)
                {
                    values[property.Name] = scalar;
                }
            }
        }
        catch (JsonException)
        {
            return Empty;
        }

        return new ToolArguments(values);
    }

    public bool TryGetString(string name, [NotNullWhen(true)] out string? value) =>
        Values.TryGetValue(name, out value) && value is not null;

    public bool TryGetInt32(string name, out int value)
    {
        value = 0;
        return Values.TryGetValue(name, out var raw)
               && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    public bool TryGetBoolean(string name, out bool value)
    {
        value = false;
        return Values.TryGetValue(name, out var raw) && bool.TryParse(raw, out value);
    }

    /// <summary>
    /// A fractional number, for the parameters that genuinely are one — a length of time in
    /// minutes, where "an hour and a half" is ninety and "a minute and a half" is one and a half
    /// (list.md Phase 24).
    /// <para>
    /// Invariant culture, like every other reader here: what arrives is a model's JSON rather than
    /// something a Commander typed, and JSON numbers use a point wherever the Commander lives.
    /// </para>
    /// </summary>
    public bool TryGetDouble(string name, out double value)
    {
        value = 0;
        return Values.TryGetValue(name, out var raw)
               && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}

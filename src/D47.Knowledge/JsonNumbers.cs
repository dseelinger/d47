using System.Globalization;
using System.Text.Json;

namespace D47.Knowledge;

internal static class JsonNumbers
{
    /// <summary>The property as a number, accepting a numeric string; null when absent or not numeric.</summary>
    public static double? Number(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) switch
        {
            true when value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) => number,
            true when value.ValueKind == JsonValueKind.String
                      && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };
}

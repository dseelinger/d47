using System.Text.Json;
using System.Text.Json.Serialization;

namespace D47.Core.Storage;

/// <summary>JSON options shared by the per-Commander stores. Instances are shared; never change one.</summary>
public static class StoreJson
{
    /// <summary>Indented camelCase, string enums, null properties left out.</summary>
    public static readonly JsonSerializerOptions OmittingNulls = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>As <see cref="OmittingNulls"/>, but null properties are written.</summary>
    public static readonly JsonSerializerOptions KeepingNulls = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}

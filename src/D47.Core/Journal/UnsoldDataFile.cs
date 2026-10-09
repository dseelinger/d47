using System.Text.Json;
using System.Text.Json.Nodes;
using D47.Core.Storage;

namespace D47.Core.Journal;

/// <summary>
/// <c>data\unsold-data.json</c>, where each unsold-data ledger keeps its reset times under its own property.
/// A write replaces that property alone and keeps the others.
/// </summary>
internal static class UnsoldDataFile
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private static readonly Lock Gate = new();

    /// <summary>The reset times under <paramref name="property"/>; throws what reading or parsing the file throws.</summary>
    public static Dictionary<string, DateTimeOffset> Read(IFileSystem files, string path, string property)
    {
        lock (Gate)
        {
            if (files.ReadText(path) is not { } text
                || JsonNode.Parse(text) is not JsonObject document
                || document[property] is not { } resets)
            {
                return new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
            }

            return new Dictionary<string, DateTimeOffset>(
                resets.Deserialize<Dictionary<string, DateTimeOffset>>() ?? [],
                StringComparer.Ordinal);
        }
    }

    /// <summary>Replaces <paramref name="property"/>; throws what writing the file throws.</summary>
    public static void Write(IFileSystem files, string path, string property, IReadOnlyDictionary<string, DateTimeOffset> resets)
    {
        lock (Gate)
        {
            JsonObject document;

            try
            {
                document = files.ReadText(path) is { } text && JsonNode.Parse(text) is JsonObject existing
                    ? existing
                    : [];
            }
            catch (JsonException)
            {
                document = [];
            }

            document[property] = JsonSerializer.SerializeToNode(resets);

            files.WriteText(path, document.ToJsonString(Json));
        }
    }
}

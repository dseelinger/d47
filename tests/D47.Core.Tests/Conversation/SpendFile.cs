using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Conversation;
using D47.Core.Storage;

namespace D47.Core.Tests.Conversation;

/// <summary>The rows the spend ledger has written to its file, oldest first.</summary>
internal static class SpendFile
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static IReadOnlyList<SpendEntry> Entries(IFileSystem files, string path) =>
        [.. (files.ReadText(path) ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonSerializer.Deserialize<SpendEntry>(line, Json)!)];
}

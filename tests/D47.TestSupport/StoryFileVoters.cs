using System.Text.Json.Nodes;
using D47.Core.Storage;

namespace D47.Core.Stories;

public static class StoryFileVoters
{
    /// <summary>The voter token a Commander has on disk in the story file, or null when none is written.</summary>
    public static string? Of(IFileSystem files, string path, string frontierId) =>
        files.ReadText(path) is { } text
        && JsonNode.Parse(text)?["commanders"] is JsonArray commanders
        && commanders.FirstOrDefault(node => node?["frontierId"]?.GetValue<string>() == frontierId) is { } commander
            ? commander["voter"]?.GetValue<string>()
            : null;
}

using System.Text.Json;

namespace D47.Core.Interface;

/// <summary>A codex entry logged shortly before a picture was taken.</summary>
public sealed record PictureNoteEntry(long EntryId, string? Name, int? RegionId);

/// <summary>Where and when a kept picture was taken, and the BMP of Elite's it was converted from. Stored beside the picture as JSON.</summary>
public sealed record PictureNote(
    DateTimeOffset At,
    string Source,
    int Width,
    int Height,
    string? System,
    long? SystemAddress,
    string? Body,
    int? RegionId,
    IReadOnlyList<PictureNoteEntry> CodexEntries,
    string? Bmp = null)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string Write() => JsonSerializer.Serialize(this, Options);

    /// <summary>Reads a note <see cref="Write"/> produced; null for text that is not one.</summary>
    public static PictureNote? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var note = JsonSerializer.Deserialize<PictureNote>(json, Options);

            return note is { Source: not null, CodexEntries: not null } ? note : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

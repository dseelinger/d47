using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;

namespace D47.Donations.Store;

/// <summary>
/// What the utility remembers between openings: when it was last opened, and the zip each downloaded
/// donation was written to.
/// </summary>
public sealed class UtilityState
{
    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Null until the utility has completed one listing.</summary>
    public DateTimeOffset? LastOpened { get; set; }

    /// <summary>Object key to the name of the zip holding it.</summary>
    public Dictionary<string, string> Downloaded { get; init; } = new(StringComparer.Ordinal);

    public static UtilityState Read(string path)
    {
        if (!File.Exists(path))
        {
            return new UtilityState();
        }

        try
        {
            return JsonSerializer.Deserialize<UtilityState>(File.ReadAllText(path)) ?? new UtilityState();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // An unreadable state file costs the "new since" count and the record of what was
            // downloaded; it is not worth refusing to open over.
            return new UtilityState();
        }
    }

    public void Write(string path) =>
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(this, Format));
}

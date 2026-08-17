using System.Text.Json;

namespace MirrorProbe;

/// <summary>
/// The cheap signal, read from the channel Frontier publishes.
/// <para>
/// <c>GuiFocus</c> in <c>Status.json</c> says which panel is open. list.md Phase 22 makes it the
/// gate — nothing captures unless it says a panel is up — so the spike records it beside every
/// frame. That turns "which panel was this?" from something the person taking the measurement has
/// to remember into something the capture already knows, and it exercises the gate at the same
/// time.
/// </para>
/// <para>
/// d47 itself does not parse this field yet: <c>GameStatus</c> in Core reads Flags, Flags2, fuel,
/// cargo, heat, body, balance and position, and stops. Adding it is product work for whichever
/// Phase 22 item survives the measurement, so it is written out here rather than imported.
/// </para>
/// </summary>
internal static class GameStatus
{
    private static readonly string[] FocusNames =
    [
        "no panel",
        "internal panel (right)",
        "external panel (left)",
        "comms panel",
        "role panel",
        "station services",
        "galaxy map",
        "system map",
        "orrery",
        "FSS",
        "SAA",
        "codex",
    ];

    internal sealed record Snapshot(int? GuiFocus, string Focus, string? Timestamp, string Path, string? Problem);

    internal static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Saved Games",
        "Frontier Developments",
        "Elite Dangerous",
        "Status.json");

    internal static Snapshot Read(string? path = null)
    {
        path ??= DefaultPath;

        if (!File.Exists(path))
        {
            return new Snapshot(null, "unknown", null, path, "Status.json is not there.");
        }

        try
        {
            // Elite rewrites this file in place while the game is running, so a read can land
            // mid-write and see a truncated document. That is a normal event rather than a fault.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var document = JsonDocument.Parse(stream);

            var root = document.RootElement;

            var focus = root.TryGetProperty("GuiFocus", out var value) && value.TryGetInt32(out var number)
                ? number
                : (int?)null;

            var timestamp = root.TryGetProperty("timestamp", out var stamp) ? stamp.GetString() : null;

            return new Snapshot(focus, Name(focus), timestamp, path, null);
        }
        catch (JsonException error)
        {
            return new Snapshot(null, "unknown", null, path, $"Status.json did not parse: {error.Message}");
        }
        catch (IOException error)
        {
            return new Snapshot(null, "unknown", null, path, $"Status.json could not be read: {error.Message}");
        }
    }

    private static string Name(int? focus) => focus is { } value && value >= 0 && value < FocusNames.Length
        ? FocusNames[value]
        : "unknown";
}

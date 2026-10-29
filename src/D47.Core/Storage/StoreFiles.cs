using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace D47.Core.Storage;

/// <summary>Helpers shared by the per-Commander JSON stores.</summary>
public static class StoreFiles
{
    /// <summary>Writes <paramref name="text"/>; false, after logging, when the write fails.</summary>
    public static bool TryWriteText(IFileSystem files, string path, string text, ILogger logger)
    {
        try
        {
            files.WriteText(path, text);
            return true;
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            logger.LogWarning(ex, "Could not write {Path}", path);
            return false;
        }
    }

    /// <summary>A copy of <paramref name="byCommander"/> in which the Commander's entry with <paramref name="entry"/>'s key is replaced, or added at the end.</summary>
    public static Dictionary<string, IReadOnlyList<T>> ReplaceKeyed<T>(
        IReadOnlyDictionary<string, IReadOnlyList<T>> byCommander, string commander, T entry, Func<T, string> key)
    {
        var kept = byCommander
            .GetValueOrDefault(commander, [])
            .Where(existing => !string.Equals(key(existing), key(entry), StringComparison.Ordinal));

        return new Dictionary<string, IReadOnlyList<T>>(byCommander, StringComparer.Ordinal)
        {
            [commander] = [.. kept, entry],
        };
    }
}

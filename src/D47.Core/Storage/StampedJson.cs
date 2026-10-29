using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace D47.Core.Storage;

/// <summary>Reads a JSON file Elite rewrites in place, only when its write time has changed.</summary>
public static class StampedJson
{
    /// <summary>
    /// The value <paramref name="read"/> builds from the file, or null when the file is missing, unchanged since
    /// <paramref name="stamp"/> or unreadable. <paramref name="stamp"/> moves only after a successful parse, so a
    /// file caught mid-write is read again on the next poll.
    /// </summary>
    public static T? Read<T>(
        IFileSystem files,
        string path,
        ref DateTime stamp,
        ILogger logger,
        Func<JsonElement, DateTime, T> read)
        where T : class
    {
        if (files.Stat(path) is not { } state || state.Written == stamp)
        {
            return null;
        }

        var written = state.Written;

        try
        {
            using var stream = files.OpenRead(path);

            if (stream is null)
            {
                return null;
            }

            using var document = JsonDocument.Parse(stream);
            var value = read(document.RootElement, written);
            stamp = written;
            return value;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            logger.LogDebug(ex, "Could not read {File}; will retry", Path.GetFileName(path));
            return null;
        }
    }
}

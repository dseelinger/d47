using System.Text.Json;
using D47.Core.Storage;

namespace D47.Core.Tests.Stories;

internal static class BarCounts
{
    /// <summary>How many times the Commander's replacement of <paramref name="key"/> has been written to the bars file.</summary>
    public static int Of(IFileSystem files, string path, string commander, string key)
    {
        if (files.ReadText(path) is not { } text)
        {
            return 0;
        }

        using var document = JsonDocument.Parse(text);

        return document.RootElement.TryGetProperty("Changed", out var changed)
            && changed.TryGetProperty(commander, out var counts)
            && counts.TryGetProperty(key, out var count)
                ? count.GetInt32()
                : 0;
    }
}

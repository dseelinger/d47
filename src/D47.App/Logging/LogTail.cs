using System.Text;

namespace D47.App.Logging;

/// <summary>The end of today's log, for the panel's log page.</summary>
public static class LogTail
{
    /// <summary>How much of the end to read.</summary>
    private const long MaxBytes = 256 * 1024;

    /// <summary>Lines kept after the byte window, so the page is a screenful and not a book.</summary>
    private const int MaxLines = 500;

    /// <summary>The newest <c>d47-*.log</c> in <paramref name="folder"/>, tail first-trimmed.</summary>
    /// <paramref name="folder"/>, tail first-trimmed.</paramref>
    /// <param name="maxLines">The same, for the line trim that follows the byte window.</param>
    public static string Read(string folder, long maxBytes = MaxBytes, int maxLines = MaxLines)
    {
        if (!Directory.Exists(folder))
        {
            return "No log folder yet.";
        }

        var newest = Directory
            .EnumerateFiles(folder, "d47-*.log")
            .OrderBy(path => path, StringComparer.Ordinal)
            .LastOrDefault();

        if (newest is null)
        {
            return "No log file has been written yet.";
        }

        // Shared with the sink that is still writing it, deletion included: a roll at midnight renames out
        // from under this, and a reader that forbade it would take the panel down with a sharing violation
        // once a day.
        using var file = new FileStream(
            newest,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        if (file.Length > maxBytes)
        {
            file.Seek(-maxBytes, SeekOrigin.End);
        }

        using var reader = new StreamReader(file, Encoding.UTF8);
        var text = reader.ReadToEnd();

        var lines = text.Split('\n');

        // The first line is a fragment whenever the window started mid-file.
        var from = file.Length > maxBytes && lines.Length > 1 ? 1 : 0;
        var kept = lines.Length - from > maxLines ? lines.Length - maxLines : from;

        return string.Join('\n', lines[kept..]).TrimEnd();
    }
}

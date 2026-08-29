using System.Text;

namespace D47.App.Logging;

/// <summary>
/// The end of today's log, for the panel's log page.
/// <para>
/// Reads the file Serilog is currently writing, which is why the share flags are not the
/// defaults: the sink holds it open with <c>shared: true</c>, and opening it any other way
/// fails with a sharing violation rather than with anything that explains itself.
/// </para>
/// </summary>
public static class LogTail
{
    /// <summary>
    /// How much of the end to read. A day's log is usually well under this; a day with a
    /// runaway loop in it is not, and reading a hundred megabytes to show the last screenful
    /// would turn a diagnostic into a second fault.
    /// </summary>
    private const long MaxBytes = 256 * 1024;

    /// <summary>Lines kept after the byte window, so the page is a screenful and not a book.</summary>
    private const int MaxLines = 500;

    /// <summary>
    /// The newest <c>d47-*.log</c> in <paramref name="folder"/>, tail first-trimmed.
    /// <para>
    /// Newest by ordinal filename sort rather than by computing today's date: Serilog names
    /// these <c>d47-yyyyMMdd.log</c> and rolls on its own clock, so around midnight - and on a
    /// machine whose clock disagrees with the sink's - asking the folder is right where
    /// arithmetic is a guess. The same rule the journal reader already follows.
    /// </para>
    /// </summary>
    /// <param name="maxBytes">
    /// How much of the end to read, defaulting to the page's window. An incident excerpt asks for
    /// more (<a href="https://github.com/dseelinger/d47/issues/160">#160</a>): the page shows a
    /// screenful and an excerpt cuts a stretch of minutes out of it, and a busy minute of d47 runs
    /// to hundreds of lines. A parameter rather than a second reader, so there is one thing that
    /// knows how to open a file the sink is still writing.
    /// </param>
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

        // Shared with the sink that is still writing it, deletion included: a roll at midnight
        // renames out from under this, and a reader that forbade it would take the panel down
        // with a sharing violation once a day.
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

        // The first line is a fragment whenever the window started mid-file. Dropped rather
        // than shown, because half a timestamp reads as corruption.
        var from = file.Length > maxBytes && lines.Length > 1 ? 1 : 0;
        var kept = lines.Length - from > maxLines ? lines.Length - maxLines : from;

        return string.Join('\n', lines[kept..]).TrimEnd();
    }
}

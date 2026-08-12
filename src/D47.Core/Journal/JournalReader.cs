using System.Text;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>
/// Pull-based tail of one journal file. Owns no thread and reads no clock — it exposes
/// <see cref="Poll"/>, and it is the caller's cadence, not this type's, that decides how often
/// new lines are picked up (architecture.md §4). Production calls it from the tick loop; a
/// test can call it as fast as it likes and get the same result.
/// </summary>
public sealed class JournalReader(string path, ILogger logger)
{
    private long _position;

    public string Path { get; } = path;

    /// <summary>
    /// Returns events completed since the last call. A line still being written by Elite is
    /// left unconsumed: the read position only advances past bytes up to and including the
    /// last newline seen, so a partial line is picked up whole on the next call rather than
    /// parsed half-written.
    /// </summary>
    public IReadOnlyList<JournalEvent> Poll()
    {
        byte[] bytes;

        // FileShare.ReadWrite | Delete: Elite holds this file open for writing for the whole
        // session, and a plain File.OpenRead fails intermittently against that (architecture.md §4).
        using (var stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            if (_position > stream.Length)
            {
                // The file was truncated or replaced under us; start over rather than seek past the end.
                _position = 0;
            }

            stream.Seek(_position, SeekOrigin.Begin);
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            bytes = buffer.ToArray();
        }

        if (bytes.Length == 0)
        {
            return [];
        }

        var text = Encoding.UTF8.GetString(bytes);
        var lastNewline = text.LastIndexOf('\n');
        if (lastNewline < 0)
        {
            // No complete line yet; leave it for the next poll rather than parsing a fragment.
            return [];
        }

        var completeText = text[..(lastNewline + 1)];
        _position += Encoding.UTF8.GetByteCount(completeText);

        var events = new List<JournalEvent>();
        foreach (var rawLine in completeText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            // A parse failure here is exactly the "survive a schema change" requirement: it is
            // logged and skipped, and it never stops the rest of the file from being read.
            if (JournalEvent.TryParse(line, logger, out var parsed))
            {
                events.Add(parsed!);
            }
        }

        return events;
    }
}

using System.Buffers;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>Pull-based tail of one journal file.</summary>
public sealed class JournalReader(string path, IFileSystem files, ILogger logger, long? until = null)
{
    /// <summary>The most read at once; a longer line grows the buffer.</summary>
    private const int Chunk = 64 * 1024;

    private long _position;

    public string Path { get; } = path;

    /// <summary>Byte offset of the next unread line.</summary>
    public long Position => _position;

    /// <summary>Returns events completed since the last call, none ending after <c>until</c> when one was given.</summary>
    public IReadOnlyList<JournalEvent> Poll()
    {
        using var stream = files.OpenRead(Path) ?? throw new FileNotFoundException("The journal file is missing.", Path);

        if (_position > stream.Length)
        {
            // The file was truncated or replaced under us; start over rather than seek past the end.
            _position = 0;
        }

        var end = until is { } limit ? Math.Min(stream.Length, limit) : stream.Length;
        var remaining = end - _position;

        if (remaining <= 0)
        {
            return [];
        }

        stream.Seek(_position, SeekOrigin.Begin);

        var events = new List<JournalEvent>();
        var buffer = ArrayPool<byte>.Shared.Rent((int)Math.Min(remaining, Chunk));
        var filled = 0;

        try
        {
            while (remaining > 0)
            {
                if (filled == buffer.Length)
                {
                    // One line longer than the buffer.
                    var larger = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
                    Buffer.BlockCopy(buffer, 0, larger, 0, filled);
                    ArrayPool<byte>.Shared.Return(buffer);
                    buffer = larger;
                }

                var read = stream.Read(buffer, filled, (int)Math.Min(buffer.Length - filled, remaining));

                if (read == 0)
                {
                    break;
                }

                var searchFrom = filled;
                filled += read;
                remaining -= read;

                // A '\n' byte never occurs inside a multi-byte UTF-8 character, so a line is split on bytes.
                var lineStart = 0;
                int newline;

                while ((newline = Array.IndexOf(buffer, (byte)'\n', searchFrom, filled - searchFrom)) >= 0)
                {
                    var lineEnd = newline;

                    while (lineEnd > lineStart && buffer[lineEnd - 1] == (byte)'\r')
                    {
                        lineEnd--;
                    }

                    // A parse failure here is exactly the "survive a schema change" requirement: it is logged and
                    // skipped, and it never stops the rest of the file from being read.
                    if (lineEnd > lineStart
                        && JournalEvent.TryParse(buffer.AsMemory(lineStart, lineEnd - lineStart), logger, out var parsed))
                    {
                        events.Add(parsed!);
                    }

                    _position += newline + 1 - lineStart;
                    lineStart = newline + 1;
                    searchFrom = lineStart;
                }

                // A partial line moves to the front and waits for the rest, or for the next poll.
                if (lineStart > 0)
                {
                    Buffer.BlockCopy(buffer, lineStart, buffer, 0, filled - lineStart);
                    filled -= lineStart;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return events;
    }
}

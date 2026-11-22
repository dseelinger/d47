using System.Text;
using System.Text.Json;

namespace D47.EliteVad;

/// <summary>
/// Polls the journal folder every 50 ms and logs each new journal event and each change to Status.json,
/// stamped with the moment it was seen. Logs event names and changed Status keys, not whole lines.
/// </summary>
internal sealed class GameWatcher(string folder, RunLog log)
{
    private readonly Dictionary<string, string> _status = [];
    private DateTime _statusWritten;
    private string? _journal;
    private long _journalOffset;
    private readonly StringBuilder _partial = new();
    private DateTime _nextJournalScan;

    public void Run(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                PollStatus();
                PollJournal();
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            stop.WaitHandle.WaitOne(50);
        }
    }

    private void PollStatus()
    {
        var path = Path.Combine(folder, "Status.json");
        if (!File.Exists(path))
        {
            return;
        }

        var written = File.GetLastWriteTimeUtc(path);
        if (written == _statusWritten)
        {
            return;
        }

        var seen = log.Now;
        string text;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var reader = new StreamReader(stream))
        {
            text = reader.ReadToEnd();
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _statusWritten = written;
        var changes = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(text);
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Name is "timestamp" or "event")
                {
                    continue;
                }

                var value = property.Value.GetRawText();
                if (!_status.TryGetValue(property.Name, out var old) || old != value)
                {
                    _status[property.Name] = value;
                    changes.Add($"{property.Name}={Shorten(value)}");
                }
            }
        }
        catch (JsonException)
        {
            _statusWritten = default;
            return;
        }

        if (changes.Count > 0)
        {
            log.Line("status", seen, string.Join(' ', changes));
        }
    }

    private void PollJournal()
    {
        if (DateTime.UtcNow >= _nextJournalScan)
        {
            _nextJournalScan = DateTime.UtcNow.AddSeconds(1);
            var newest = new DirectoryInfo(folder).EnumerateFiles("Journal.*.log")
                .OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
            if (newest is not null && newest.FullName != _journal)
            {
                var first = _journal is null;
                _journal = newest.FullName;
                _journalOffset = first ? newest.Length : 0;
                _partial.Clear();
                log.Line("journal-file", log.Now, newest.Name);
            }
        }

        if (_journal is null)
        {
            return;
        }

        using var stream = new FileStream(_journal, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length <= _journalOffset)
        {
            return;
        }

        var seen = log.Now;
        stream.Position = _journalOffset;
        var buffer = new byte[stream.Length - _journalOffset];
        stream.ReadExactly(buffer);
        _journalOffset += buffer.Length;
        _partial.Append(Encoding.UTF8.GetString(buffer));

        var text = _partial.ToString();
        var end = text.LastIndexOf('\n');
        if (end < 0)
        {
            return;
        }

        _partial.Clear().Append(text[(end + 1)..]);
        foreach (var line in text[..end].Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            LogJournalLine(line, seen);
        }
    }

    private void LogJournalLine(string line, double seen)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var name = root.TryGetProperty("event", out var e) ? e.GetString() : "?";
            var stamp = root.TryGetProperty("timestamp", out var t) ? t.GetString() : "?";
            var extra = root.TryGetProperty("Channel", out var c) ? $"\tChannel={c.GetString()}" : "";
            log.Line("journal", seen, $"{name}\t{stamp}{extra}");
        }
        catch (JsonException)
        {
        }
    }

    private static string Shorten(string value) => value.Length <= 80 ? value : value[..80] + "…";
}

using System.Globalization;
using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>
/// The journal events Elite also sends as inbox mail and d47 has not already said, per Commander, with an
/// unread watermark kept in <see cref="FileName"/> (#618).
/// </summary>
public sealed class MailLedger
{
    /// <summary>The watermark file's name in the data folder.</summary>
    public const string FileName = "mail-read.json";

    private enum Group
    {
        Completed,
        Failed,
        Promotion,
        Squadron,
        CommunityGoal,
    }

    private sealed record Entry(DateTimeOffset At, Group Group, string? Ladder, int Rank, string? Named);

    private sealed record Found(int File, string Commander, string Key, Entry Entry);

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private static readonly IReadOnlyList<string> Ladders = [.. RankState.Careers, "Empire", "Federation"];

    private readonly string _path;
    private readonly ILogger _logger;
    private readonly Lock _gate = new();

    private readonly Dictionary<string, Dictionary<string, Entry>> _entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _readThrough;

    /// <summary>Keys a live callout has already spoken, which history never counts.</summary>
    private readonly HashSet<string> _said = new(StringComparer.Ordinal);

    private string? _liveCommander;
    private bool _historyFolded;

    public MailLedger(string path, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(logger);

        _path = path;
        _logger = logger;
        _readThrough = Load();
    }

    /// <summary>Whether the journals older than this session have been folded in.</summary>
    public bool HistoryFolded
    {
        get
        {
            lock (_gate)
            {
                return _historyFolded;
            }
        }
    }

    /// <summary>The moment <paramref name="commander"/> last heard their mail, or null when never.</summary>
    public DateTimeOffset? ReadThrough(string commander)
    {
        lock (_gate)
        {
            return _readThrough.TryGetValue(commander, out var through) ? through : null;
        }
    }

    /// <summary>
    /// Folds events in order, attributed to <paramref name="commander"/> until one names another. With
    /// <paramref name="live"/> true, promotions are skipped because the callout speaks them. Callers do not
    /// pass the priming tick.
    /// </summary>
    public void Fold(IEnumerable<JournalEvent> events, bool live, string? commander = null)
    {
        ArgumentNullException.ThrowIfNull(events);

        lock (_gate)
        {
            var current = live ? (_liveCommander ??= commander) : commander;

            foreach (var journalEvent in events)
            {
                if (CommanderOf(journalEvent) is { } named)
                {
                    current = named;

                    if (live)
                    {
                        _liveCommander = named;
                    }

                    continue;
                }

                if (current is null)
                {
                    continue;
                }

                if (live && journalEvent.Kind is "Promotion" or "PowerplayRank")
                {
                    var spoken = Key(journalEvent);
                    _said.Add(spoken);
                    _entries.GetValueOrDefault(current)?.Remove(spoken);
                    continue;
                }

                if (EntryOf(journalEvent) is { } entry)
                {
                    Add(current, Key(journalEvent), entry);
                }
            }
        }
    }

    /// <summary>
    /// Folds journal files oldest first, on the calling thread, reading only those written since the oldest
    /// watermark. A Commander with no watermark counts only the newest file they appear in.
    /// </summary>
    public void FoldHistory(IReadOnlyList<string> files, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        DateTimeOffset? since;

        lock (_gate)
        {
            since = _readThrough.Count == 0 ? null : _readThrough.Values.Min();
        }

        var read = since is { } oldest
            ? files.Where(file => File.GetLastWriteTimeUtc(file) >= oldest.UtcDateTime).ToList()
            : files.TakeLast(1).ToList();

        var found = new List<Found>();
        var newest = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var index = 0; index < read.Count; index++)
        {
            cancellation.ThrowIfCancellationRequested();

            string? commander = null;

            foreach (var line in Lines(read[index]))
            {
                if (!Relevant(line) || !JournalEvent.TryParse(line, _logger, out var parsed) || parsed is null)
                {
                    continue;
                }

                if (CommanderOf(parsed) is { } named)
                {
                    commander = named;
                    newest[named] = index;
                }
                else if (commander is not null && EntryOf(parsed) is { } entry)
                {
                    found.Add(new Found(index, commander, Key(parsed), entry));
                }
            }
        }

        lock (_gate)
        {
            foreach (var item in found)
            {
                if (!_readThrough.ContainsKey(item.Commander) && item.File != newest[item.Commander])
                {
                    continue;
                }

                if (!_said.Contains(item.Key))
                {
                    Add(item.Commander, item.Key, item.Entry);
                }
            }

            _historyFolded = true;
        }
    }

    /// <summary>The spoken answer to "any mail?" for <paramref name="commander"/>.</summary>
    public string Compose(string commander)
    {
        List<Entry> unread;

        lock (_gate)
        {
            unread = _entries.TryGetValue(commander, out var book) ? [.. book.Values] : [];
        }

        if (unread.Count == 0)
        {
            return "Nothing I know of since you last asked. Frontier's own notices never reach the journal.";
        }

        var groups = unread
            .GroupBy(entry => entry.Group)
            .OrderBy(group => group.Key)
            .ToList();

        var named = groups.Take(3).ToList();
        var rest = groups.Skip(3).Sum(group => group.Count());

        var phrases = new List<string>();

        for (var i = 0; i < named.Count; i++)
        {
            var group = named[i];

            if (group.Key == Group.Completed && i + 1 < named.Count && named[i + 1].Key == Group.Failed)
            {
                phrases.Add($"{Missions(group.Count())} completed and {Words(named[i + 1].Count())} failed");
                i++;
                continue;
            }

            phrases.Add(Phrase(group.Key, [.. group]));
        }

        if (rest > 0)
        {
            phrases.Add($"{Words(rest)} more {(rest == 1 ? "notice" : "notices")}");
        }

        return $"I can't read the inbox itself, but since you last asked: {Joined(phrases)}. The comms panel has the true count.";
    }

    /// <summary>Moves <paramref name="commander"/>'s watermark to <paramref name="through"/> and saves it.</summary>
    public void MarkRead(string commander, DateTimeOffset through)
    {
        ArgumentNullException.ThrowIfNull(commander);

        Dictionary<string, DateTimeOffset> snapshot;

        lock (_gate)
        {
            if (_readThrough.TryGetValue(commander, out var held) && held >= through)
            {
                return;
            }

            _readThrough[commander] = through;

            if (_entries.TryGetValue(commander, out var book))
            {
                foreach (var key in book.Where(pair => pair.Value.At <= through).Select(pair => pair.Key).ToList())
                {
                    book.Remove(key);
                }
            }

            snapshot = new Dictionary<string, DateTimeOffset>(_readThrough, StringComparer.Ordinal);
        }

        try
        {
            AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(snapshot, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not write {Path}", _path);
        }
    }

    private void Add(string commander, string key, Entry entry)
    {
        if (_readThrough.TryGetValue(commander, out var through) && entry.At <= through)
        {
            return;
        }

        if (!_entries.TryGetValue(commander, out var book))
        {
            book = new Dictionary<string, Entry>(StringComparer.Ordinal);
            _entries[commander] = book;
        }

        book.TryAdd(key, entry);
    }

    private static string? CommanderOf(JournalEvent journalEvent) =>
        journalEvent.Kind is "Commander" or "LoadGame" && journalEvent.String("FID") is { Length: > 0 } fid ? fid : null;

    /// <summary>The same event folded by history and by a live tick has the same key.</summary>
    private static string Key(JournalEvent journalEvent) =>
        journalEvent.Timestamp.UtcTicks.ToString(CultureInfo.InvariantCulture) + "|" + journalEvent.Kind + "|"
        + (journalEvent.Long("MissionID") is { } mission
            ? mission.ToString(CultureInfo.InvariantCulture)
            : journalEvent.Raw.GetRawText());

    private static Entry? EntryOf(JournalEvent journalEvent)
    {
        var at = journalEvent.Timestamp;

        switch (journalEvent.Kind)
        {
            case "MissionCompleted":
                return new Entry(at, Group.Completed, null, 0, null);

            case "MissionFailed":
                return new Entry(at, Group.Failed, null, 0, null);

            case "SquadronPromotion":
                return new Entry(at, Group.Squadron, null, 0, null);

            case "CommunityGoalReward":
                return new Entry(at, Group.CommunityGoal, null, 0, null);

            case "PowerplayRank" when journalEvent.Int("Rank") is { } rank && rank > 0
                && journalEvent.String("Power") is { Length: > 0 } power:
                return new Entry(at, Group.Promotion, "Powerplay:" + power, rank, $"Powerplay rank {Words(rank)} with {power}");

            case "Promotion":
                foreach (var ladder in Ladders)
                {
                    if (journalEvent.Int(ladder) is { } promoted)
                    {
                        return new Entry(at, Group.Promotion, ladder, promoted, $"{new RankStanding(ladder, promoted).Describe()} {LadderWords(ladder)}");
                    }
                }

                return null;

            default:
                return null;
        }
    }

    private static string Phrase(Group group, IReadOnlyList<Entry> entries) => group switch
    {
        Group.Completed => $"{Missions(entries.Count)} completed",
        Group.Failed => $"{Missions(entries.Count)} failed",
        Group.Squadron => entries.Count == 1 ? "a squadron promotion" : $"{Words(entries.Count)} squadron promotions",
        Group.CommunityGoal => entries.Count == 1 ? "a community goal reward" : $"{Words(entries.Count)} community goal rewards",
        _ => Promotions(entries),
    };

    /// <summary>The highest promotion on each ladder, newest first, two by name and the rest as a count.</summary>
    private static string Promotions(IReadOnlyList<Entry> entries)
    {
        var highest = entries
            .GroupBy(entry => entry.Ladder, StringComparer.Ordinal)
            .Select(ladder => ladder.MaxBy(entry => entry.Rank)!)
            .OrderByDescending(entry => entry.At)
            .ToList();

        if (highest.Count == 1)
        {
            return $"a promotion to {highest[0].Named}";
        }

        var names = highest.Take(2).Select(entry => entry.Named!).ToList();
        var more = highest.Count - names.Count;

        return more == 0
            ? $"promotions to {names[0]} and {names[1]}"
            : $"promotions to {names[0]}, {names[1]} and {Words(more)} more";
    }

    private static string LadderWords(string ladder) => ladder switch
    {
        "Empire" => "in the Imperial Navy",
        "Federation" => "in the Federal Navy",
        "Explore" => "in exploration",
        "Soldier" => "as a mercenary",
        "Exobiologist" => "in exobiology",
        "CQC" => "in CQC",
        _ => "in " + ladder.ToLowerInvariant(),
    };

    private static string Missions(int count) => count == 1 ? "one mission" : $"{Words(count)} missions";

    private static string Words(int count) => SpokenNumbers.Expand(count.ToString(CultureInfo.InvariantCulture));

    private static string Joined(IReadOnlyList<string> phrases) => phrases.Count switch
    {
        1 => phrases[0],
        _ => string.Join(", ", phrases.Take(phrases.Count - 1)) + ", and " + phrases[^1],
    };

    /// <summary>The text test, before any JSON is touched.</summary>
    private static bool Relevant(string line) =>
        line.Contains("\"event\":\"Commander\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"LoadGame\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"MissionCompleted\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"MissionFailed\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"SquadronPromotion\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"CommunityGoalReward\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"Promotion\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"PowerplayRank\"", StringComparison.Ordinal);

    private Dictionary<string, DateTimeOffset> Load()
    {
        try
        {
            if (File.Exists(_path)
                && JsonSerializer.Deserialize<Dictionary<string, DateTimeOffset>>(File.ReadAllText(_path)) is { } stored)
            {
                return new Dictionary<string, DateTimeOffset>(stored, StringComparer.Ordinal);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "Could not read {Path}; every Commander starts unread", _path);
        }

        return new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
    }

    private IEnumerable<string> Lines(string file)
    {
        FileStream stream;

        try
        {
            stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not read {File} for mail", file);
            yield break;
        }

        using (stream)
        {
            using var reader = new StreamReader(stream);

            while (reader.ReadLine() is { } line)
            {
                yield return line;
            }
        }
    }
}

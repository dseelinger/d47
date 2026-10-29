using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace D47.Core.Storage;

/// <summary>
/// A per-Commander file derived from the journals and stamped with the journal time it has been folded
/// through. A file that cannot be read is discarded, since the journals rebuild it.
/// </summary>
public sealed class FoldedCommanderFile<TKept, TRecord>(
    string path,
    IFileSystem files,
    ILogger logger,
    JsonSerializerOptions json,
    Func<TRecord, string?> frontierId,
    Func<TRecord, TKept> rehydrate,
    Func<string, TKept, TRecord> dehydrate,
    Func<TKept, TKept, TKept> combine,
    Action<IReadOnlyDictionary<string, TKept>, DateTimeOffset?> loaded,
    Action<Exception> readFailed)
{
    private readonly Lock _gate = new();
    private Dictionary<string, TKept> _byCommander = new(StringComparer.Ordinal);
    private DateTimeOffset? _foldedThrough;

    /// <summary>The journal time this file has been folded through, or null for a file never written.</summary>
    public DateTimeOffset? FoldedThrough
    {
        get
        {
            lock (_gate)
            {
                return _foldedThrough;
            }
        }
    }

    public bool TryGet(string frontierId, out TKept? kept)
    {
        lock (_gate)
        {
            return _byCommander.TryGetValue(frontierId, out kept);
        }
    }

    public IReadOnlyDictionary<string, TKept> All
    {
        get
        {
            lock (_gate)
            {
                return new Dictionary<string, TKept>(_byCommander, StringComparer.Ordinal);
            }
        }
    }

    public void Load()
    {
        try
        {
            if (files.ReadText(path) is not { } text)
            {
                return;
            }

            var document = JsonSerializer.Deserialize<Document>(text, json);
            var read = new Dictionary<string, TKept>(StringComparer.Ordinal);

            foreach (var commander in document?.Commanders ?? [])
            {
                if (string.IsNullOrWhiteSpace(frontierId(commander)))
                {
                    continue;
                }

                read[frontierId(commander)!] = rehydrate(commander);
            }

            lock (_gate)
            {
                _byCommander = read;
                _foldedThrough = document?.FoldedThrough;
            }

            loaded(read, document?.FoldedThrough);
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            readFailed(ex);
        }
    }

    /// <summary>Merges <paramref name="updates"/> into what the file held and writes it; the stamp never moves backwards.</summary>
    public void Save(IEnumerable<(string FrontierId, TKept Kept)> updates, DateTimeOffset foldedThrough)
    {
        var states = updates.ToList();

        if (states.Count == 0)
        {
            return;
        }

        Dictionary<string, TKept> merged;

        lock (_gate)
        {
            merged = new Dictionary<string, TKept>(_byCommander, StringComparer.Ordinal);
        }

        foreach (var (fid, kept) in states)
        {
            merged[fid] = merged.TryGetValue(fid, out var onFile) ? combine(onFile, kept) : kept;
        }

        var stamp = _foldedThrough is { } held && held > foldedThrough ? held : foldedThrough;

        var document = new Document
        {
            FoldedThrough = stamp,
            Commanders = [.. merged.Select(entry => dehydrate(entry.Key, entry.Value))],
        };

        if (!StoreFiles.TryWriteText(files, path, JsonSerializer.Serialize(document, json), logger))
        {
            return;
        }

        lock (_gate)
        {
            _byCommander = merged;
            _foldedThrough = stamp;
        }
    }

    private sealed class Document
    {
        public DateTimeOffset? FoldedThrough { get; set; }

        public IReadOnlyList<TRecord> Commanders { get; set; } = [];
    }
}

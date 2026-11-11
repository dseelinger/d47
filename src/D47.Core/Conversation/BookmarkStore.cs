using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Conversation;

/// <summary>What a bookmark points at.</summary>
public enum BookmarkKind
{
    System,
    Body,
    Station,
}

/// <summary>
/// A system, body or station a Commander named, so they can plot a course to its system by saying the name
/// (#488). <see cref="System"/> is the system to plot to; <see cref="Kind"/> is null when not known.
/// </summary>
public sealed record Bookmark(
    string Name,
    string System,
    DateTimeOffset MadeAt,
    BookmarkKind? Kind = BookmarkKind.System,
    string? Place = null,
    long? SystemAddress = null,
    string? Note = null);

/// <summary>
/// Per Commander Frontier id, the systems they have named. Kept between sessions at
/// <c>data/bookmarks.json</c>, beside <see cref="LearnedPhrasesStore"/>.
/// </summary>
public sealed class BookmarkStore(string path, IFileSystem files, ILogger<BookmarkStore> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Lock _gate = new();

    /// <summary>
    /// Serialises <see cref="Add"/>, <see cref="Rename"/> and <see cref="Delete"/> against each other, across
    /// the read that decides what changes and the write that applies it — <see cref="_gate"/> alone only
    /// protects one snapshot at a time, and two callers each starting from the same snapshot would otherwise
    /// silently undo one another's change.
    /// </summary>
    private readonly Lock _writeGate = new();

    private Dictionary<string, Dictionary<string, Bookmark>> _byCommander = new(StringComparer.Ordinal);

    /// <summary>Raised when a bookmark is added, renamed or deleted, whoever changed it.</summary>
    public event Action? Changed;

    public void Load()
    {
        try
        {
            if (files.ReadText(path) is not { } text)
            {
                return;
            }

            var document = JsonSerializer.Deserialize<Document>(text, Json);

            var loaded = new Dictionary<string, Dictionary<string, Bookmark>>(StringComparer.Ordinal);

            foreach (var commander in document?.Commanders ?? [])
            {
                if (string.IsNullOrWhiteSpace(commander.FrontierId))
                {
                    continue;
                }

                var forCommander = new Dictionary<string, Bookmark>(StringComparer.OrdinalIgnoreCase);

                foreach (var bookmark in commander.Bookmarks ?? [])
                {
                    if (bookmark is { Name.Length: > 0, System.Length: > 0 })
                    {
                        forCommander[bookmark.Name.Trim()] =
                            new Bookmark(
                                bookmark.Name,
                                bookmark.System,
                                bookmark.MadeAt,
                                bookmark.Kind ?? (string.IsNullOrEmpty(bookmark.Place) ? BookmarkKind.System : null),
                                bookmark.Place,
                                bookmark.SystemAddress,
                                bookmark.Note);
                    }
                }

                loaded[commander.FrontierId] = forCommander;
            }

            lock (_gate)
            {
                _byCommander = loaded;
            }

            logger.LogInformation(
                "Loaded {Bookmarks} bookmark(s) for {Count} Commanders",
                loaded.Values.Sum(commander => commander.Count),
                loaded.Count);
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            logger.LogWarning(ex, "Could not read {Path}; bookmarks made before now are lost", path);
        }
    }

    /// <summary>This Commander's bookmarks, newest first.</summary>
    public IReadOnlyList<Bookmark> For(string frontierId)
    {
        lock (_gate)
        {
            return _byCommander.TryGetValue(frontierId, out var bookmarks)
                ? [.. bookmarks.Values.OrderByDescending(bookmark => bookmark.MadeAt)]
                : [];
        }
    }

    /// <summary>Adds a bookmark. False, and no change, when this Commander already has that name.</summary>
    public bool Add(string frontierId, string name, string system, DateTimeOffset at) =>
        Add(frontierId, new Bookmark(name, system, at));

    /// <summary>Adds a bookmark. False, and no change, when this Commander already has its name.</summary>
    public bool Add(string frontierId, Bookmark bookmark)
    {
        var name = bookmark.Name.Trim();
        var system = bookmark.System;

        lock (_writeGate)
        {
            var merged = CloneReplacing(frontierId, out var forCommander);

            if (forCommander.ContainsKey(name))
            {
                return false;
            }

            forCommander[name] = bookmark with { Name = name };

            Write(merged);
        }

        logger.LogInformation("Bookmarked \"{System}\" as \"{Name}\"", system, name);

        return true;
    }

    /// <summary>
    /// Renames a bookmark. False, and no change, when there is no such bookmark or another bookmark already
    /// has <paramref name="newName"/> — a case-only change of its own name is allowed.
    /// </summary>
    public bool Rename(string frontierId, string name, string newName)
    {
        Bookmark? existing;

        lock (_gate)
        {
            existing = _byCommander.TryGetValue(frontierId, out var bookmarks)
                && bookmarks.TryGetValue(name.Trim(), out var found)
                ? found
                : null;
        }

        return existing is not null && Update(frontierId, name, existing with { Name = newName.Trim() });
    }

    /// <summary>
    /// Replaces one bookmark whole, including its name. False, and no change, when there is no such bookmark
    /// or another bookmark already has the new name — a case-only change of its own name is allowed.
    /// </summary>
    public bool Update(string frontierId, string name, Bookmark replacement)
    {
        var trimmedNewName = replacement.Name.Trim();

        lock (_writeGate)
        {
            var merged = CloneReplacing(frontierId, out var forCommander);

            if (!forCommander.TryGetValue(name.Trim(), out var existing))
            {
                return false;
            }

            if (!string.Equals(existing.Name, trimmedNewName, StringComparison.OrdinalIgnoreCase)
                && forCommander.ContainsKey(trimmedNewName))
            {
                return false;
            }

            forCommander.Remove(existing.Name);
            forCommander[trimmedNewName] = replacement with { Name = trimmedNewName };

            Write(merged);
        }

        logger.LogInformation("Updated the bookmark \"{Name}\"", name);

        return true;
    }

    /// <summary>Deletes a bookmark. False when there is no such bookmark.</summary>
    public bool Delete(string frontierId, string name)
    {
        lock (_writeGate)
        {
            if (!_byCommander.TryGetValue(frontierId, out var bookmarks) || !bookmarks.ContainsKey(name.Trim()))
            {
                return false;
            }

            var merged = CloneReplacing(frontierId, out var forCommander);

            forCommander.Remove(name.Trim());
            Write(merged);
        }

        logger.LogInformation("Deleted the bookmark \"{Name}\"", name);

        return true;
    }

    /// <summary>
    /// The outer dictionary, shallow-copied so every other Commander's map is reused rather than cloned,
    /// with <paramref name="frontierId"/>'s own map deep-copied so it can be changed without touching what
    /// <see cref="_byCommander"/> still points at. Called only while holding <see cref="_writeGate"/>, so
    /// it always starts from the latest write rather than a snapshot an earlier caller has since replaced.
    /// </summary>
    private Dictionary<string, Dictionary<string, Bookmark>> CloneReplacing(
        string frontierId, out Dictionary<string, Bookmark> forCommander)
    {
        var merged = new Dictionary<string, Dictionary<string, Bookmark>>(_byCommander, StringComparer.Ordinal);

        forCommander = merged.TryGetValue(frontierId, out var existing)
            ? new Dictionary<string, Bookmark>(existing, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, Bookmark>(StringComparer.OrdinalIgnoreCase);

        merged[frontierId] = forCommander;

        return merged;
    }

    private void Write(Dictionary<string, Dictionary<string, Bookmark>> merged)
    {
        var document = new Document
        {
            Commanders =
            [
                .. merged.Select(entry => new CommanderRecord
                {
                    FrontierId = entry.Key,
                    Bookmarks =
                    [
                        .. entry.Value.Values.Select(bookmark => new BookmarkRecord
                        {
                            Name = bookmark.Name,
                            System = bookmark.System,
                            MadeAt = bookmark.MadeAt,
                            Kind = bookmark.Kind,
                            Place = bookmark.Place,
                            SystemAddress = bookmark.SystemAddress,
                            Note = bookmark.Note,
                        }),
                    ],
                }),
            ],
        };

        if (!StoreFiles.TryWriteText(files, path, JsonSerializer.Serialize(document, Json), logger))
        {
            return;
        }

        lock (_gate)
        {
            _byCommander = merged;
        }

        Changed?.Invoke();
    }

    private sealed class Document
    {
        public IReadOnlyList<CommanderRecord> Commanders { get; set; } = [];
    }

    private sealed class CommanderRecord
    {
        public string FrontierId { get; set; } = string.Empty;

        public IReadOnlyList<BookmarkRecord>? Bookmarks { get; set; }
    }

    private sealed class BookmarkRecord
    {
        public string Name { get; set; } = string.Empty;

        public string System { get; set; } = string.Empty;

        public DateTimeOffset MadeAt { get; set; }

        public BookmarkKind? Kind { get; set; }

        public string? Place { get; set; }

        public long? SystemAddress { get; set; }

        public string? Note { get; set; }
    }
}

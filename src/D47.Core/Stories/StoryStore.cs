using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Adventures;
using D47.Core.Diagnostics.Donation;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.Core.Stories;

/// <summary>The stories each Commander picked — <c>data/story.json</c>. At most one per Commander is current.</summary>
public sealed class StoryStore
{
    private static readonly JsonSerializerOptions Json = StoreJson.OmittingNulls;

    private string? _path;
    private readonly ILogger? _logger;
    private readonly Lock _gate = new();

    /// <summary>Held across each change and its file write, so writes reach the file in the order they were made.</summary>
    private readonly Lock _writeGate = new();
    private readonly IFileSystem _files;
    private Dictionary<string, IReadOnlyList<Story>> _byCommander;
    private Dictionary<string, string> _voters;
    private HashSet<string> _forgotten;

    private StoryStore(
        string? path,
        ILogger? logger,
        Dictionary<string, IReadOnlyList<Story>> byCommander,
        Dictionary<string, string> voters,
        HashSet<string> forgotten,
        IFileSystem files)
    {
        _path = path;
        _logger = logger;
        _files = files;
        _byCommander = byCommander;
        _voters = voters;
        _forgotten = forgotten;
    }

    /// <summary>Held in memory only.</summary>
    public static StoryStore InMemory() =>
        new(null, null, new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal), new MemoryFileSystem());

    public static StoryStore Open(string path, IFileSystem files, ILogger<StoryStore> logger)
    {
        var (byCommander, voters, forgotten) = Read(path, files, logger);

        return new(path, logger, byCommander, voters, forgotten, files);
    }

    public event Action? Changed;

    /// <summary>Reads <paramref name="path"/> in place of the file it held, writes there from now on, and raises <see cref="Changed"/>.</summary>
    public void Reopen(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        lock (_writeGate)
        {
            var (byCommander, voters, forgotten) = Read(path, _files, _logger ?? NullLogger.Instance);

            lock (_gate)
            {
                _path = path;
                _byCommander = byCommander;
                _voters = voters;
                _forgotten = forgotten;
            }
        }

        Changed?.Invoke();
    }

    /// <summary>This Commander's stories, in the order they were first picked.</summary>
    public IReadOnlyList<Story> For(string? frontierId)
    {
        lock (_gate)
        {
            return _byCommander.GetValueOrDefault(frontierId ?? AdventureStore.NoCommander, []);
        }
    }

    /// <summary>The running or paused story, or null.</summary>
    public Story? Current(string? frontierId) => For(frontierId).FirstOrDefault(story => story.IsCurrent);

    public Story? Find(string? frontierId, string id) =>
        For(frontierId).FirstOrDefault(story => string.Equals(story.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Every Commander's stories, in any state.</summary>
    public IReadOnlyList<(string FrontierId, Story Story)> All()
    {
        lock (_gate)
        {
            return [.. _byCommander.SelectMany(pair => pair.Value.Select(story => (pair.Key, story)))];
        }
    }

    /// <summary>The running or paused stories of every Commander.</summary>
    public IReadOnlyList<Story> AllCurrent()
    {
        lock (_gate)
        {
            return [.. _byCommander.Values.SelectMany(stories => stories).Where(story => story.IsCurrent)];
        }
    }

    /// <summary>The Commander's voter token, or null until they first rate a story.</summary>
    public string? Voter(string? frontierId)
    {
        lock (_gate)
        {
            return _voters.GetValueOrDefault(frontierId ?? AdventureStore.NoCommander);
        }
    }

    /// <summary>Whether the Commander asked to fly to a Guardian beacon again, so no scan counts as on record.</summary>
    public bool BeaconForgotten(string? frontierId)
    {
        lock (_gate)
        {
            return _forgotten.Contains(frontierId ?? AdventureStore.NoCommander);
        }
    }

    public void SetBeaconForgotten(string? frontierId, bool forgotten)
    {
        var commander = frontierId ?? AdventureStore.NoCommander;

        lock (_writeGate)
        {
            lock (_gate)
            {
                if (_forgotten.Contains(commander) == forgotten)
                {
                    return;
                }

                _forgotten = new HashSet<string>(_forgotten, StringComparer.Ordinal);

                if (forgotten)
                {
                    _forgotten.Add(commander);
                }
                else
                {
                    _forgotten.Remove(commander);
                }
            }

            Write();
        }

        Changed?.Invoke();
    }

    /// <summary>The Commander's voter token, minted and saved the first time it is asked for. It is not the donor token.</summary>
    public string EnsureVoter(string? frontierId)
    {
        var commander = frontierId ?? AdventureStore.NoCommander;

        lock (_writeGate)
        {
            string minted;

            lock (_gate)
            {
                if (_voters.TryGetValue(commander, out var existing))
                {
                    return existing;
                }

                minted = DonorToken.NewToken();

                _voters = new Dictionary<string, string>(_voters, StringComparer.Ordinal) { [commander] = minted };
            }

            Write();

            return minted;
        }
    }

    /// <summary>Adds the story or replaces the one with its id.</summary>
    public void Save(string? frontierId, Story story)
    {
        ArgumentNullException.ThrowIfNull(story);

        var commander = frontierId ?? AdventureStore.NoCommander;

        lock (_writeGate)
        {
            lock (_gate)
            {
                var existing = _byCommander.GetValueOrDefault(commander, []);
                var replacing = existing.Any(other => string.Equals(other.Id, story.Id, StringComparison.OrdinalIgnoreCase));

                _byCommander = new Dictionary<string, IReadOnlyList<Story>>(_byCommander, StringComparer.Ordinal)
                {
                    [commander] = replacing
                        ? [.. existing.Select(other => string.Equals(other.Id, story.Id, StringComparison.OrdinalIgnoreCase) ? story : other)]
                        : [.. existing, story],
                };
            }

            Write();
        }

        Changed?.Invoke();
    }

    /// <summary>Replaces the story with <paramref name="id"/> by what <paramref name="change"/> makes of it. No other write runs between the two.</summary>
    public void Update(string? frontierId, string id, Func<Story, Story> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        var commander = frontierId ?? AdventureStore.NoCommander;

        lock (_writeGate)
        {
            var existing = For(commander);

            if (existing.FirstOrDefault(story => string.Equals(story.Id, id, StringComparison.OrdinalIgnoreCase)) is not { } before)
            {
                return;
            }

            var after = change(before);

            if (after == before)
            {
                return;
            }

            lock (_gate)
            {
                _byCommander = new Dictionary<string, IReadOnlyList<Story>>(_byCommander, StringComparer.Ordinal)
                {
                    [commander] = [.. existing.Select(story => ReferenceEquals(story, before) ? after : story)],
                };
            }

            Write();
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Deletes the Commander's stories picked at or before <paramref name="at"/>, and with them the Commander's voter and
    /// forgotten beacon, which exist only beside a story. Returns the stories deleted.
    /// </summary>
    public IReadOnlyList<Story> RemovePickedBy(string? frontierId, DateTimeOffset at)
    {
        var commander = frontierId ?? AdventureStore.NoCommander;
        List<Story> removed;

        lock (_writeGate)
        {
            var existing = For(commander);

            removed = [.. existing.Where(story => story.PickedAt <= at)];

            if (removed.Count == 0)
            {
                return [];
            }

            lock (_gate)
            {
                _byCommander = new Dictionary<string, IReadOnlyList<Story>>(_byCommander, StringComparer.Ordinal)
                {
                    [commander] = [.. existing.Where(story => story.PickedAt > at)],
                };

                _voters = new Dictionary<string, string>(_voters, StringComparer.Ordinal);
                _voters.Remove(commander);
                _forgotten = new HashSet<string>(_forgotten, StringComparer.Ordinal);
                _forgotten.Remove(commander);
            }

            Write();
        }

        Changed?.Invoke();
        return removed;
    }

    /// <summary>Called holding <see cref="_writeGate"/> and not <see cref="_gate"/>; only a holder of <see cref="_writeGate"/> replaces the fields it reads.</summary>
    private void Write()
    {
        if (_path is null)
        {
            return;
        }

        var document = new Document
        {
            Commanders =
            [
                .. _byCommander.Keys
                    .Concat(_voters.Keys)
                    .Concat(_forgotten)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .Where(key => _byCommander.GetValueOrDefault(key)?.Count > 0 || _voters.ContainsKey(key) || _forgotten.Contains(key))
                    .Select(key => new CommanderRecord
                    {
                        FrontierId = key,
                        Voter = _voters.GetValueOrDefault(key),
                        BeaconForgotten = _forgotten.Contains(key),
                        Stories = _byCommander.GetValueOrDefault(key) ?? [],
                    }),
            ],
        };

        try
        {
            _files.WriteText(_path, JsonSerializer.Serialize(document, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.LogError(ex, "Could not write {Path}", _path);
        }
    }

    private static (Dictionary<string, IReadOnlyList<Story>> Stories, Dictionary<string, string> Voters, HashSet<string> Forgotten) Read(string path, IFileSystem files, ILogger logger)
    {
        var loaded = new Dictionary<string, IReadOnlyList<Story>>(StringComparer.Ordinal);
        var voters = new Dictionary<string, string>(StringComparer.Ordinal);
        var forgotten = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            if (files.ReadText(path) is not { } text)
            {
                return (loaded, voters, forgotten);
            }

            foreach (var commander in JsonSerializer.Deserialize<Document>(text, Json)?.Commanders ?? [])
            {
                var key = commander.FrontierId ?? AdventureStore.NoCommander;

                loaded[key] = commander.Stories ?? [];

                if (DonorToken.IsWellFormed(commander.Voter))
                {
                    voters[key] = commander.Voter!;
                }

                if (commander.BeaconForgotten)
                {
                    forgotten.Add(key);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogError(ex, "Could not read {Path}; no story is running", path);
        }

        return (loaded, voters, forgotten);
    }

    private sealed class Document
    {
        public IReadOnlyList<CommanderRecord> Commanders { get; set; } = [];
    }

    private sealed class CommanderRecord
    {
        public string? FrontierId { get; set; }

        public string? Voter { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool BeaconForgotten { get; set; }

        public IReadOnlyList<Story>? Stories { get; set; }
    }
}

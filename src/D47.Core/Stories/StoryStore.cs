using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Adventures;
using D47.Core.Diagnostics.Donation;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.Core.Stories;

/// <summary>The stories each Commander picked — <c>data/story.json</c>. At most one per Commander is current; any number are shelved.</summary>
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
    private Dictionary<string, string> _defaults;

    private StoryStore(
        string? path,
        ILogger? logger,
        Dictionary<string, IReadOnlyList<Story>> byCommander,
        Dictionary<string, string> voters,
        HashSet<string> forgotten,
        Dictionary<string, string> defaults,
        IFileSystem files)
    {
        _path = path;
        _logger = logger;
        _files = files;
        _byCommander = byCommander;
        _voters = voters;
        _forgotten = forgotten;
        _defaults = defaults;
    }

    /// <summary>Held in memory only.</summary>
    public static StoryStore InMemory() =>
        new(null, null, new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal), new MemoryFileSystem());

    public static StoryStore Open(string path, IFileSystem files, ILogger<StoryStore> logger)
    {
        var (byCommander, voters, forgotten, defaults) = Read(path, files, logger);

        return new(path, logger, byCommander, voters, forgotten, defaults, files);
    }

    public event Action? Changed;

    /// <summary>Reads <paramref name="path"/> in place of the file it held, writes there from now on, and raises <see cref="Changed"/>.</summary>
    public void Reopen(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        lock (_writeGate)
        {
            var (byCommander, voters, forgotten, defaults) = Read(path, _files, _logger ?? NullLogger.Instance);

            lock (_gate)
            {
                _path = path;
                _byCommander = byCommander;
                _voters = voters;
                _forgotten = forgotten;
                _defaults = defaults;
            }
        }

        Changed?.Invoke();
    }

    /// <summary>This Commander's story runs, in the order they were first picked.</summary>
    public IReadOnlyList<Story> For(string? frontierId)
    {
        lock (_gate)
        {
            return _byCommander.GetValueOrDefault(frontierId ?? AdventureStore.NoCommander, []);
        }
    }

    /// <summary>This Commander's story runs in any state, the most recently picked first.</summary>
    public IReadOnlyList<Story> Runs(string? frontierId) => [.. For(frontierId).OrderByDescending(story => story.PickedAt)];

    /// <summary>The running or paused story, or null.</summary>
    public Story? Current(string? frontierId) => For(frontierId).FirstOrDefault(story => story.IsCurrent);

    /// <summary>The newest run of the story with <paramref name="id"/>, by <see cref="Story.PickedAt"/>.</summary>
    public Story? Find(string? frontierId, string id) => Newest(For(frontierId), id);

    private static Story? Newest(IReadOnlyList<Story> stories, string id) => stories
        .Where(story => string.Equals(story.Id, id, StringComparison.OrdinalIgnoreCase))
        .MaxBy(story => story.PickedAt);

    private static bool SameRun(Story a, Story b) =>
        string.Equals(a.Id, b.Id, StringComparison.OrdinalIgnoreCase) && a.PickedAt == b.PickedAt;

    /// <summary>Every Commander's stories, in any state.</summary>
    public IReadOnlyList<(string FrontierId, Story Story)> All()
    {
        lock (_gate)
        {
            return [.. _byCommander.SelectMany(pair => pair.Value.Select(story => (pair.Key, story)))];
        }
    }

    /// <summary>The running, paused or shelved stories of every Commander.</summary>
    public IReadOnlyList<Story> AllCurrentOrShelved()
    {
        lock (_gate)
        {
            return [.. _byCommander.Values.SelectMany(stories => stories).Where(story => story.IsCurrentOrShelved)];
        }
    }

    /// <summary>The Backstory the Commander had before their first story, or null when none is kept.</summary>
    public string? DefaultBackstory(string? frontierId)
    {
        lock (_gate)
        {
            return _defaults.GetValueOrDefault(frontierId ?? AdventureStore.NoCommander);
        }
    }

    public void SetDefaultBackstory(string? frontierId, string backstory)
    {
        ArgumentNullException.ThrowIfNull(backstory);

        var commander = frontierId ?? AdventureStore.NoCommander;

        lock (_writeGate)
        {
            lock (_gate)
            {
                if (_defaults.TryGetValue(commander, out var existing) && existing == backstory)
                {
                    return;
                }

                _defaults = new Dictionary<string, string>(_defaults, StringComparer.Ordinal) { [commander] = backstory };
            }

            Write();
        }

        Changed?.Invoke();
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

    /// <summary>Adds the run or replaces the one with its id and <see cref="Story.PickedAt"/>.</summary>
    public void Save(string? frontierId, Story story)
    {
        ArgumentNullException.ThrowIfNull(story);

        var commander = frontierId ?? AdventureStore.NoCommander;

        lock (_writeGate)
        {
            lock (_gate)
            {
                var existing = _byCommander.GetValueOrDefault(commander, []);
                var replacing = existing.Any(other => SameRun(other, story));

                _byCommander = new Dictionary<string, IReadOnlyList<Story>>(_byCommander, StringComparer.Ordinal)
                {
                    [commander] = replacing
                        ? [.. existing.Select(other => SameRun(other, story) ? story : other)]
                        : [.. existing, story],
                };
            }

            Write();
        }

        Changed?.Invoke();
    }

    /// <summary>Replaces the newest run of the story with <paramref name="id"/> by what <paramref name="change"/> makes of it. No other write runs between the two.</summary>
    public void Update(string? frontierId, string id, Func<Story, Story> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        var commander = frontierId ?? AdventureStore.NoCommander;

        lock (_writeGate)
        {
            var existing = For(commander);

            if (Newest(existing, id) is not { } before)
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
    /// forgotten beacon, which exist only beside a story, and the default Backstory when no story is left. Returns the stories deleted.
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

                if (_byCommander[commander].Count == 0)
                {
                    _defaults = new Dictionary<string, string>(_defaults, StringComparer.Ordinal);
                    _defaults.Remove(commander);
                }
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
                    .Concat(_defaults.Keys)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .Where(key => _byCommander.GetValueOrDefault(key)?.Count > 0 || _voters.ContainsKey(key) || _forgotten.Contains(key) || _defaults.ContainsKey(key))
                    .Select(key => new CommanderRecord
                    {
                        FrontierId = key,
                        Voter = _voters.GetValueOrDefault(key),
                        BeaconForgotten = _forgotten.Contains(key),
                        DefaultBackstory = _defaults.GetValueOrDefault(key),
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

    private static (Dictionary<string, IReadOnlyList<Story>> Stories, Dictionary<string, string> Voters, HashSet<string> Forgotten, Dictionary<string, string> Defaults) Read(string path, IFileSystem files, ILogger logger)
    {
        var loaded = new Dictionary<string, IReadOnlyList<Story>>(StringComparer.Ordinal);
        var voters = new Dictionary<string, string>(StringComparer.Ordinal);
        var forgotten = new HashSet<string>(StringComparer.Ordinal);
        var defaults = new Dictionary<string, string>(StringComparer.Ordinal);

        try
        {
            if (files.ReadText(path) is not { } text)
            {
                return (loaded, voters, forgotten, defaults);
            }

            foreach (var commander in JsonSerializer.Deserialize<Document>(text, Json)?.Commanders ?? [])
            {
                var key = commander.FrontierId ?? AdventureStore.NoCommander;

                var stories = commander.Stories ?? [];

                if (commander.DefaultBackstory is { } kept)
                {
                    defaults[key] = kept;
                }
                else if (stories.Where(story => story.BackstoryBefore is not null).OrderByDescending(story => story.IsCurrent).ThenByDescending(story => story.PickedAt).FirstOrDefault() is { } older)
                {
                    defaults[key] = older.BackstoryBefore!;
                }

                loaded[key] = [.. stories.Select(story => story.BackstoryBefore is null ? story : story with { BackstoryBefore = null })];

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

        return (loaded, voters, forgotten, defaults);
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

        public string? DefaultBackstory { get; set; }

        public IReadOnlyList<Story>? Stories { get; set; }
    }
}

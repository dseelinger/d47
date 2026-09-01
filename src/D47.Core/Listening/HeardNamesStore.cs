using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Listening;

/// <summary>What one Commander's listening surface has learned.</summary>
/// <param name="Names">Every place they have met, to match a misheard one against.</param>
/// <param name="Aliases">What their transcriber gets wrong, and what they meant.</param>
public sealed record HeardNames(SpokenNames Names, SoundsLike Aliases)
{
    public static readonly HeardNames Empty = new(SpokenNames.Empty, SoundsLike.Empty);
}

/// <summary>
/// The two halves of hearing a proper noun right, kept between sessions
/// (<a href="https://github.com/dseelinger/d47/issues/134">#134</a>): the names this Commander has
/// met, and the corrections they have confirmed.
/// <para>
/// <b>One file for two things because they are one behaviour</b> — <i>hear it wrong, ask, retry,
/// remember</i> — and because the alias half is meaningless without the name half: the rule that
/// stops a real word being aliased is <i>does this Commander already know it as a name</i>, so a
/// correction and the catalogue it was checked against must not be able to drift apart across two
/// files.
/// </para>
/// <para>
/// <b>Half a cache and half not, and the difference is worth stating.</b> The names are derived —
/// delete them and replaying the journals puts them back. The <em>aliases are not</em>: they came
/// from the Commander correcting d47 out loud, and nothing on disk restates them. So a file that
/// will not parse loses names that rebuild themselves and corrections that do not, which is why
/// this logs a warning rather than shrugging.
/// </para>
/// <para>
/// <b>Keyed per Commander with the key inside the document</b>, like every other store here: the
/// Frontier id comes out of the journal, journal content is untrusted, and turning it into a
/// filename buys a path-traversal surface for an organisational convenience.
/// </para>
/// </summary>
public sealed class HeardNamesStore(string path, ILogger<HeardNamesStore> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Lock _gate = new();

    private Dictionary<string, HeardNames> _byCommander = new(StringComparer.Ordinal);

    private DateTimeOffset? _foldedThrough;

    public string Path => path;

    /// <summary>
    /// How far through the journals the names have been read, or null for a file that was never
    /// written. The same watermark <see cref="LoadoutStore"/> keeps, and for the same reason: the
    /// catch-up should be the size of the gap rather than a fixed guess at one.
    /// </summary>
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

    /// <summary>What was on disk for this Commander, or null if nothing was.</summary>
    public HeardNames? For(string frontierId)
    {
        lock (_gate)
        {
            return _byCommander.GetValueOrDefault(frontierId);
        }
    }

    /// <summary>Everything the file held, for the miner to start from rather than rebuild over.</summary>
    public IReadOnlyDictionary<string, HeardNames> All
    {
        get
        {
            lock (_gate)
            {
                return new Dictionary<string, HeardNames>(_byCommander, StringComparer.Ordinal);
            }
        }
    }

    public void Load()
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(path), Json);

            var loaded = new Dictionary<string, HeardNames>(StringComparer.Ordinal);

            foreach (var commander in document?.Commanders ?? [])
            {
                if (string.IsNullOrWhiteSpace(commander.FrontierId))
                {
                    continue;
                }

                loaded[commander.FrontierId] = new HeardNames(
                    SpokenNames.Empty with { Names = commander.Names ?? [] },
                    SoundsLike.Empty with
                    {
                        Entries =
                        [
                            .. (commander.Aliases ?? [])
                                .Where(alias => alias is { Heard.Length: > 0, Meant.Length: > 0 })
                                .Select(alias => new SoundsLikeEntry(
                                    alias.Heard, alias.Meant, alias.LearnedAt)),
                        ],
                    });
            }

            lock (_gate)
            {
                _byCommander = loaded;
                _foldedThrough = document?.FoldedThrough;
            }

            logger.LogInformation(
                "Loaded {Names} heard name(s) and {Aliases} correction(s) for {Count} Commanders",
                loaded.Values.Sum(heard => heard.Names.Names.Count),
                loaded.Values.Sum(heard => heard.Aliases.Entries.Count),
                loaded.Count);
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            // Warned rather than shrugged at: half of what is in here cannot be rebuilt from
            // anything on disk. See the note on this class.
            logger.LogWarning(
                ex, "Could not read {Path}; corrections learned before now are lost", path);
        }
    }

    /// <summary>
    /// Writes every Commander's names and corrections, merged over what was loaded so a Commander
    /// who has not flown this session is not erased.
    /// </summary>
    public void Save(IReadOnlyDictionary<string, HeardNames> heard, DateTimeOffset foldedThrough)
    {
        ArgumentNullException.ThrowIfNull(heard);

        if (heard.Count == 0)
        {
            return;
        }

        Dictionary<string, HeardNames> merged;

        lock (_gate)
        {
            merged = new Dictionary<string, HeardNames>(_byCommander, StringComparer.Ordinal);
        }

        foreach (var (fid, one) in heard)
        {
            merged[fid] = one;
        }

        var stamp = _foldedThrough is { } held && held > foldedThrough ? held : foldedThrough;

        var document = new Document
        {
            FoldedThrough = stamp,
            Commanders =
            [
                .. merged.Select(entry => new CommanderRecord
                {
                    FrontierId = entry.Key,
                    Names = entry.Value.Names.Names,
                    Aliases =
                    [
                        .. entry.Value.Aliases.Entries.Select(alias => new AliasRecord
                        {
                            Heard = alias.Heard,
                            Meant = alias.Meant,
                            LearnedAt = alias.LearnedAt,
                        }),
                    ],
                }),
            ],
        };

        try
        {
            AtomicFile.WriteAllText(path, JsonSerializer.Serialize(document, Json));
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            logger.LogWarning(ex, "Could not write {Path}", path);
            return;
        }

        lock (_gate)
        {
            _byCommander = merged;
            _foldedThrough = stamp;
        }
    }

    /// <summary>The names this Commander has met, or an empty catalogue.</summary>
    public SpokenNames NamesFor(string frontierId) => For(frontierId)?.Names ?? SpokenNames.Empty;

    /// <summary>What their transcriber gets wrong, or nothing.</summary>
    public SoundsLike AliasesFor(string frontierId) => For(frontierId)?.Aliases ?? SoundsLike.Empty;

    /// <summary>
    /// Records a correction and writes it, <b>if every rule allows it</b>. Answers whether it was
    /// kept, so a caller can say so — and says nothing when it was refused, because the refusal is
    /// not the Commander's problem to solve.
    /// <para>
    /// The guards live in <see cref="SoundsLike.MayLearn"/>; what is supplied here is the two
    /// questions only this store can answer — whether the token already names something this
    /// Commander has met, and whether d47's own routing answers to it.
    /// </para>
    /// </summary>
    public bool Learn(
        string frontierId,
        string heard,
        string meant,
        DateTimeOffset at,
        Func<string, bool> reserved)
    {
        ArgumentNullException.ThrowIfNull(reserved);

        var known = NamesFor(frontierId);

        if (!SoundsLike.MayLearn(heard, meant, known.Knows, reserved))
        {
            return false;
        }

        var held = For(frontierId) ?? HeardNames.Empty;

        Save(
            new Dictionary<string, HeardNames>(StringComparer.Ordinal)
            {
                [frontierId] = held with { Aliases = held.Aliases.Learn(heard, meant, at) },
            },
            at);

        logger.LogInformation(
            "Learned that \"{Heard}\" is how this transcriber says {Meant}", heard, meant);

        return true;
    }

    /// <summary>
    /// Drops every correction for one Commander. The clearable half of "readable and clearable" —
    /// and it leaves the names alone, because those are not a claim about anything.
    /// </summary>
    public void ForgetCorrections(string frontierId, DateTimeOffset at)
    {
        if (For(frontierId) is not { } held || !held.Aliases.IsKnown)
        {
            return;
        }

        Save(
            new Dictionary<string, HeardNames>(StringComparer.Ordinal)
            {
                [frontierId] = held with { Aliases = SoundsLike.Empty },
            },
            at);

        logger.LogInformation("Forgot every learned correction for {Commander}", frontierId);
    }

    /// <summary>
    /// Writes what a mining run or a session of play found, keeping whatever corrections are
    /// already held against each Commander.
    /// </summary>
    public void RememberNames(
        IReadOnlyDictionary<string, SpokenNames> names,
        DateTimeOffset foldedThrough)
    {
        ArgumentNullException.ThrowIfNull(names);

        Save(
            names.ToDictionary(
                entry => entry.Key,
                entry => (For(entry.Key) ?? HeardNames.Empty) with { Names = entry.Value },
                StringComparer.Ordinal),
            foldedThrough);
    }

    private sealed class Document
    {
        public DateTimeOffset? FoldedThrough { get; set; }

        public IReadOnlyList<CommanderRecord> Commanders { get; set; } = [];
    }

    private sealed class CommanderRecord
    {
        public string FrontierId { get; set; } = string.Empty;

        public IReadOnlyList<string>? Names { get; set; }

        public IReadOnlyList<AliasRecord>? Aliases { get; set; }
    }

    private sealed class AliasRecord
    {
        public string Heard { get; set; } = string.Empty;

        public string Meant { get; set; } = string.Empty;

        public DateTimeOffset LearnedAt { get; set; }
    }
}

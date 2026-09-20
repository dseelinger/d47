using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>What every suit and weapon the Commander owns was last seen as, kept between sessions (#293).</summary>
public sealed class KitStore(string path, ILogger<KitStore> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Lock _gate = new();

    private Dictionary<string, OwnedKit> _byCommander = new(StringComparer.Ordinal);

    private DateTimeOffset? _foldedThrough;

    public string Path => path;

    /// <summary>The journal timestamp this file has already been folded through, or null for a file never written.</summary>
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
    public OwnedKit? For(string frontierId)
    {
        lock (_gate)
        {
            return _byCommander.GetValueOrDefault(frontierId);
        }
    }

    /// <summary>Everything the file held, for <see cref="KitBackfill"/> to start from rather than rebuild over.</summary>
    public IReadOnlyDictionary<string, OwnedKit> All
    {
        get
        {
            lock (_gate)
            {
                return new Dictionary<string, OwnedKit>(_byCommander, StringComparer.Ordinal);
            }
        }
    }

    /// <summary>Reads the file.</summary>
    public void Load()
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(path), Json);

            var loaded = new Dictionary<string, OwnedKit>(StringComparer.Ordinal);

            foreach (var commander in document?.Commanders ?? [])
            {
                if (string.IsNullOrWhiteSpace(commander.FrontierId))
                {
                    continue;
                }

                loaded[commander.FrontierId] = Rehydrate(commander);
            }

            lock (_gate)
            {
                _byCommander = loaded;
                _foldedThrough = document?.FoldedThrough;
            }

            logger.LogInformation(
                "Loaded stored kit for {Count} Commanders, folded through {Through:u}",
                loaded.Count,
                document?.FoldedThrough);
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            // Discarded rather than refused, which is the licence a derived file has and an authored one does
            // not: the worst this costs is a rebuild from the journals.
            logger.LogWarning(ex, "Could not read {Path}; starting with no stored kit", path);
        }
    }

    /// <summary>
    /// Writes every Commander's kit through <see cref="AtomicFile"/>, and keeps what it wrote so <see
    /// cref="For"/> answers with it afterwards.
    /// </summary>
    public void Save(IEnumerable<CommanderGameState> commanders, DateTimeOffset foldedThrough)
    {
        ArgumentNullException.ThrowIfNull(commanders);

        var states = commanders.ToList();

        if (states.Count == 0)
        {
            return;
        }

        Dictionary<string, OwnedKit> merged;

        lock (_gate)
        {
            merged = new Dictionary<string, OwnedKit>(_byCommander, StringComparer.Ordinal);
        }

        foreach (var commander in states)
        {
            merged[commander.Identity.FrontierId] = commander.Kit;
        }

        // Never backwards.
        var stamp = _foldedThrough is { } held && held > foldedThrough ? held : foldedThrough;

        var document = new Document
        {
            FoldedThrough = stamp,
            Commanders = [.. merged.Select(entry => Dehydrate(entry.Key, entry.Value))],
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

    private static OwnedKit Rehydrate(CommanderRecord record) => new()
    {
        Suits = (record.Suits ?? [])
            .ToDictionary(suit => suit.SuitId, suit => new OwnedSuit(
                suit.SuitId, suit.Symbol, suit.Grade, Rehydrate(suit.Modifications), suit.SeenAt)),
        Weapons = (record.Weapons ?? [])
            .ToDictionary(weapon => weapon.ModuleId, weapon => new OwnedWeapon(
                weapon.ModuleId, weapon.Symbol, weapon.Grade, Rehydrate(weapon.Modifications), weapon.SeenAt)),
    };

    private static IReadOnlyList<FittedModification> Rehydrate(IReadOnlyList<string>? modifications) =>
        [.. (modifications ?? []).Select(symbol => new FittedModification(symbol))];

    private static CommanderRecord Dehydrate(string frontierId, OwnedKit kit) => new()
    {
        FrontierId = frontierId,
        Suits =
        [
            .. kit.Suits.OrderBy(entry => entry.Key).Select(entry => new SuitRecord
            {
                SuitId = entry.Key,
                Symbol = entry.Value.Symbol,
                Grade = entry.Value.Grade,
                SeenAt = entry.Value.SeenAt,
                Modifications = [.. entry.Value.Modifications.Select(modification => modification.Symbol)],
            }),
        ],
        Weapons =
        [
            .. kit.Weapons.OrderBy(entry => entry.Key).Select(entry => new WeaponRecord
            {
                ModuleId = entry.Key,
                Symbol = entry.Value.Symbol,
                Grade = entry.Value.Grade,
                SeenAt = entry.Value.SeenAt,
                Modifications = [.. entry.Value.Modifications.Select(modification => modification.Symbol)],
            }),
        ],
    };

    private sealed class Document
    {
        /// <summary>See <see cref="FoldedThrough"/>.</summary>
        public DateTimeOffset? FoldedThrough { get; set; }

        public IReadOnlyList<CommanderRecord> Commanders { get; set; } = [];
    }

    private sealed class CommanderRecord
    {
        public string FrontierId { get; set; } = string.Empty;

        public IReadOnlyList<SuitRecord>? Suits { get; set; }

        public IReadOnlyList<WeaponRecord>? Weapons { get; set; }
    }

    private sealed class SuitRecord
    {
        public long SuitId { get; set; }

        public string Symbol { get; set; } = string.Empty;

        public int? Grade { get; set; }

        public DateTimeOffset SeenAt { get; set; }

        public IReadOnlyList<string>? Modifications { get; set; }
    }

    private sealed class WeaponRecord
    {
        public long ModuleId { get; set; }

        public string Symbol { get; set; } = string.Empty;

        public int? Grade { get; set; }

        public DateTimeOffset SeenAt { get; set; }

        public IReadOnlyList<string>? Modifications { get; set; }
    }
}

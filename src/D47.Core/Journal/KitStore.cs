using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>What every suit and weapon the Commander owns was last seen as, kept between sessions (#293).</summary>
public sealed class KitStore(string path, IFileSystem files, ILogger<KitStore> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly FoldedCommanderFile<OwnedKit, CommanderRecord> _file = new(
        path,
        files,
        logger,
        Json,
        record => record.FrontierId,
        Rehydrate,
        Dehydrate,
        (_, incoming) => incoming,
        (read, through) => logger.LogInformation( "Loaded stored kit for {Count} Commanders, folded through {Through:u}", read.Count, through),
        ex => logger.LogWarning(ex, "Could not read {Path}; starting with no stored kit", path));


    /// <summary>The journal timestamp this file has already been folded through, or null for a file never written.</summary>
    public DateTimeOffset? FoldedThrough => _file.FoldedThrough;

    /// <summary>What was on disk for this Commander, or null if nothing was.</summary>
    public OwnedKit? For(string frontierId) => _file.TryGet(frontierId, out var kept) ? kept : null;

    /// <summary>Everything the file held, for the backfill to start from rather than rebuild over.</summary>
    public IReadOnlyDictionary<string, OwnedKit> All => _file.All;

    /// <summary>Reads the file.</summary>
    public void Load() => _file.Load();

    /// <summary>Writes every Commander's state, and keeps what it wrote so <see cref="For"/> answers with it afterwards.</summary>
    public void Save(IEnumerable<CommanderGameState> commanders, DateTimeOffset foldedThrough)
    {
        ArgumentNullException.ThrowIfNull(commanders);

        _file.Save(commanders.Select(commander => (commander.Identity.FrontierId, commander.Kit)), foldedThrough);
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

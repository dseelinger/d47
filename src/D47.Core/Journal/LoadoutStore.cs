using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>What every ship the Commander has flown was last seen holding, kept between sessions (#128).</summary>
public sealed class LoadoutStore(string path, ILogger<LoadoutStore> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Lock _gate = new();

    private Dictionary<string, ShipLoadouts> _byCommander = new(StringComparer.Ordinal);

    private DateTimeOffset? _foldedThrough;

    public string Path => path;

    /// <summary>
    /// The journal timestamp this file has already been folded through, or null for a file that
    /// predates the stamp or was never written (#128).
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
    public ShipLoadouts? For(string frontierId)
    {
        lock (_gate)
        {
            return _byCommander.GetValueOrDefault(frontierId);
        }
    }

    /// <summary>
    /// Everything the file held, for <see cref="LoadoutBackfill"/> to start from rather than rebuild
    /// over.
    /// </summary>
    public IReadOnlyDictionary<string, ShipLoadouts> All
    {
        get
        {
            lock (_gate)
            {
                return new Dictionary<string, ShipLoadouts>(_byCommander, StringComparer.Ordinal);
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

            var loaded = new Dictionary<string, ShipLoadouts>(StringComparer.Ordinal);

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
                "Loaded stored loadouts for {Count} Commanders, {Ships} ship(s) in all, folded through {Through:u}",
                loaded.Count,
                loaded.Values.Sum(ships => ships.Ships.Count),
                document?.FoldedThrough);
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            // Discarded rather than refused, which is the licence a derived file has and an authored one does
            // not: the worst this costs is a rebuild from the journals.
            logger.LogWarning(ex, "Could not read {Path}; starting with no stored loadouts", path);
        }
    }

    /// <summary>
    /// Writes every Commander's ships through <see cref="AtomicFile"/>, and keeps what it wrote so <see
    /// cref="For"/> and <see cref="All"/> answer with it afterwards.
    /// </summary>
    /// <paramref name="commanders"/>
    /// at all, and writing only what this session has seen would delete their ships from the file —
    /// quietly, and permanently once the journals scrolled past the catch-up window.
    /// </paramref>
    public void Save(IEnumerable<CommanderGameState> commanders, DateTimeOffset foldedThrough)
    {
        ArgumentNullException.ThrowIfNull(commanders);

        var states = commanders.ToList();

        if (states.Count == 0)
        {
            return;
        }

        Dictionary<string, ShipLoadouts> merged;

        lock (_gate)
        {
            merged = new Dictionary<string, ShipLoadouts>(_byCommander, StringComparer.Ordinal);
        }

        foreach (var commander in states)
        {
            merged[commander.Identity.FrontierId] = commander.Loadouts;
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

    private static ShipLoadouts Rehydrate(CommanderRecord record)
    {
        var ships = new Dictionary<int, RememberedShip>();

        foreach (var ship in record.Ships ?? [])
        {
            var loadout = new ShipLoadout
            {
                Type = ship.Type,
                TypeName = ship.TypeName,
                Name = ship.Name,
                Ident = ship.Ident,
                ShipId = ship.ShipId,
                HullValue = ship.HullValue,
                ModulesValue = ship.ModulesValue,
                Rebuy = ship.Rebuy,
                HullHealth = ship.HullHealth,
                UnladenMass = ship.UnladenMass,
                CargoCapacity = ship.CargoCapacity,
                FuelCapacity = ship.FuelCapacity,
                ReserveCapacity = ship.ReserveCapacity,
                MaxJumpRange = ship.MaxJumpRange,
                Modules = [.. (ship.Modules ?? []).Select(Rehydrate)],
            };

            // A row that never carried a hull is not a ship, it is a gap — and IsKnown reads off Type, so
            // letting one in would put an entry in the fleet that renders as nothing.
            if (loadout.ShipId is { } id && loadout.IsKnown)
            {
                ships[id] = new RememberedShip(loadout, ship.SeenAt);
            }
        }

        return ShipLoadouts.Empty with { Ships = ships };
    }

    private static ShipModule Rehydrate(ModuleRecord record) =>
        new(record.Slot, record.Item, record.Powered, record.Health, record.Value)
        {
            Blueprint = record.Blueprint,
            BlueprintLevel = record.BlueprintLevel,
            Experimental = record.Experimental,
            Quality = record.Quality,
            Engineer = record.Engineer,
            EngineerId = record.EngineerId,
            Modifiers =
            [
                .. (record.Modifiers ?? []).Select(modifier => new ShipModifier(modifier.Label)
                {
                    Value = modifier.Value,
                    OriginalValue = modifier.OriginalValue,
                    Text = modifier.Text,
                    LessIsGood = modifier.LessIsGood,
                }),
            ],
        };

    private static CommanderRecord Dehydrate(string frontierId, ShipLoadouts loadouts) => new()
    {
        FrontierId = frontierId,
        Ships =
        [
            .. loadouts.Ships.OrderBy(entry => entry.Key).Select(entry => new ShipRecord
            {
                ShipId = entry.Key,
                SeenAt = entry.Value.SeenAt,
                Type = entry.Value.Loadout.Type,
                TypeName = entry.Value.Loadout.TypeName,
                Name = entry.Value.Loadout.Name,
                Ident = entry.Value.Loadout.Ident,
                HullValue = entry.Value.Loadout.HullValue,
                ModulesValue = entry.Value.Loadout.ModulesValue,
                Rebuy = entry.Value.Loadout.Rebuy,
                HullHealth = entry.Value.Loadout.HullHealth,
                UnladenMass = entry.Value.Loadout.UnladenMass,
                CargoCapacity = entry.Value.Loadout.CargoCapacity,
                FuelCapacity = entry.Value.Loadout.FuelCapacity,
                ReserveCapacity = entry.Value.Loadout.ReserveCapacity,
                MaxJumpRange = entry.Value.Loadout.MaxJumpRange,
                Modules = [.. entry.Value.Loadout.Modules.Select(Dehydrate)],
            }),
        ],
    };

    private static ModuleRecord Dehydrate(ShipModule module) => new()
    {
        Slot = module.Slot,
        Item = module.Item,
        Powered = module.Powered,
        Health = module.Health,
        Value = module.Value,
        Blueprint = module.Blueprint,
        BlueprintLevel = module.BlueprintLevel,
        Experimental = module.Experimental,
        Quality = module.Quality,
        Engineer = module.Engineer,
        EngineerId = module.EngineerId,
        Modifiers = module.Modifiers.Count == 0
            ? null
            : [
                .. module.Modifiers.Select(modifier => new ModifierRecord
                {
                    Label = modifier.Label,
                    Value = modifier.Value,
                    OriginalValue = modifier.OriginalValue,
                    Text = modifier.Text,
                    LessIsGood = modifier.LessIsGood,
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

        public IReadOnlyList<ShipRecord>? Ships { get; set; }
    }

    /// <summary>One remembered ship.</summary>
    private sealed class ShipRecord
    {
        public int ShipId { get; set; }

        public DateTimeOffset SeenAt { get; set; }

        public string? Type { get; set; }

        public string? TypeName { get; set; }

        public string? Name { get; set; }

        public string? Ident { get; set; }

        public long? HullValue { get; set; }

        public long? ModulesValue { get; set; }

        public long? Rebuy { get; set; }

        public int? HullHealth { get; set; }

        public double? UnladenMass { get; set; }

        public int? CargoCapacity { get; set; }

        public double? FuelCapacity { get; set; }

        public double? ReserveCapacity { get; set; }

        public double? MaxJumpRange { get; set; }

        public IReadOnlyList<ModuleRecord>? Modules { get; set; }
    }

    private sealed class ModuleRecord
    {
        public string Slot { get; set; } = string.Empty;

        public string Item { get; set; } = string.Empty;

        public bool Powered { get; set; }

        public int? Health { get; set; }

        public long? Value { get; set; }

        public string? Blueprint { get; set; }

        public int? BlueprintLevel { get; set; }

        public string? Experimental { get; set; }

        public double? Quality { get; set; }

        public string? Engineer { get; set; }

        public long? EngineerId { get; set; }

        public IReadOnlyList<ModifierRecord>? Modifiers { get; set; }
    }

    private sealed class ModifierRecord
    {
        public string Label { get; set; } = string.Empty;

        public double? Value { get; set; }

        public double? OriginalValue { get; set; }

        public string? Text { get; set; }

        public bool LessIsGood { get; set; }
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>What every ship the Commander has flown was last seen holding, kept between sessions (#128).</summary>
public sealed class LoadoutStore(string path, IFileSystem files, ILogger<LoadoutStore> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly FoldedCommanderFile<ShipLoadouts, CommanderRecord> _file = new(
        path,
        files,
        logger,
        Json,
        record => record.FrontierId,
        Rehydrate,
        Dehydrate,
        (onFile, incoming) => onFile.With(incoming),
        (read, through) => logger.LogInformation( "Loaded stored loadouts for {Count} Commanders, {Ships} ship(s) in all, folded through {Through:u}", read.Count, read.Values.Sum(ships => ships.Ships.Count), through),
        ex => logger.LogWarning(ex, "Could not read {Path}; starting with no stored loadouts", path));


    /// <summary>The journal timestamp this file has already been folded through, or null for a file never written.</summary>
    public DateTimeOffset? FoldedThrough => _file.FoldedThrough;

    /// <summary>What was on disk for this Commander, or null if nothing was.</summary>
    public ShipLoadouts? For(string frontierId) => _file.TryGet(frontierId, out var kept) ? kept : null;

    /// <summary>Everything the file held, for the backfill to start from rather than rebuild over.</summary>
    public IReadOnlyDictionary<string, ShipLoadouts> All => _file.All;

    /// <summary>Reads the file.</summary>
    public void Load() => _file.Load();

    /// <summary>Writes every Commander's state, and keeps what it wrote so <see cref="For"/> answers with it afterwards.</summary>
    public void Save(IEnumerable<CommanderGameState> commanders, DateTimeOffset foldedThrough)
    {
        ArgumentNullException.ThrowIfNull(commanders);

        _file.Save(commanders.Select(commander => (commander.Identity.FrontierId, commander.Loadouts)), foldedThrough);
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
            Priority = record.Priority,
            Blueprint = record.Blueprint,
            BlueprintLevel = record.BlueprintLevel,
            Experimental = record.Experimental,
            ExperimentalSymbol = record.ExperimentalSymbol,
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
        Priority = module.Priority,
        Blueprint = module.Blueprint,
        BlueprintLevel = module.BlueprintLevel,
        Experimental = module.Experimental,
        ExperimentalSymbol = module.ExperimentalSymbol,
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

        public int? Priority { get; set; }

        public string? Blueprint { get; set; }

        public int? BlueprintLevel { get; set; }

        public string? Experimental { get; set; }

        public string? ExperimentalSymbol { get; set; }

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

using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>
/// What every ship the Commander has flown was last seen holding, kept between sessions
/// (<a href="https://github.com/dseelinger/d47/issues/128">#128</a>).
/// <para>
/// <b>Half of this was already built and the durable half was missing.</b>
/// <see cref="ShipLoadouts"/> has remembered every ship the Commander has sat in since v0.41.1,
/// and the panel already draws another ship's modules with <i>"as you left it, N ago"</i> — but
/// it was rebuilt at every start by <see cref="LoadoutBackfill"/> over the newest 25 journals, so
/// a ship not flown inside that window was forgotten on the next launch and re-forgotten on every
/// launch after it. That is the amnesia the report was about, one level below where it was fixed.
/// </para>
/// <para>
/// <b>It is a cache, not a source of truth</b>, and that is worth saying where somebody will read
/// it before treating this like <c>settings.json</c>. Every entry came out of a journal event and
/// can be re-derived by replaying those journals, so there is no append-only obligation, no
/// migration story, and a schema change is a file that is discarded and rebuilt. Deleting it
/// loses nothing the journals can still supply.
/// </para>
/// <para>
/// <b>Not <c>ships.json</c>, and the two must stay apart.</b> That file holds the <em>intended</em>
/// build per slot — authored by the Commander, hand-editable, and reported on rather than
/// discarded when it goes wrong. This is what is actually fitted, derived from the game. Intent is
/// authored and a loadout is derived, which is the distinction the checklist already draws.
/// </para>
/// <para>
/// <b>Keyed per Commander with the key inside the document</b>, following the rule
/// <see cref="Checklists.ChecklistStore"/> established and <see cref="SamplingStore"/> repeats: the
/// Frontier id comes out of the journal, journal content is untrusted input, and turning it into a
/// filename buys a path-traversal surface for an organisational convenience. Two Commanders share
/// one journal folder and neither may be handed the other's modules.
/// </para>
/// <para>
/// <b>Size is a non-issue, measured rather than assumed.</b> 44 distinct ships have ever been
/// flown on the corpus this was built against; one <c>Loadout</c> each is 452 KB and the largest
/// single ship is 20 KB.
/// </para>
/// </summary>
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

    public string Path => path;

    /// <summary>What was on disk for this Commander, or null if nothing was.</summary>
    public ShipLoadouts? For(string frontierId)
    {
        lock (_gate)
        {
            return _byCommander.GetValueOrDefault(frontierId);
        }
    }

    /// <summary>
    /// Everything the file held, for <see cref="LoadoutBackfill"/> to start from rather than
    /// rebuild over. <b>That seeding is what makes a sale stick:</b> the catch-up replays
    /// <c>ShipyardSell</c> and <c>ShipyardNew</c> through the same fold the live path uses, so a
    /// ship sold while d47 was closed is removed from the long memory instead of surviving in it.
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

    /// <summary>
    /// Reads the file. A missing file is the normal first run; an unreadable one is logged and
    /// treated as empty, because this is derived state and the journals rebuild it.
    /// </summary>
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
            }

            logger.LogInformation(
                "Loaded stored loadouts for {Count} Commanders, {Ships} ship(s) in all",
                loaded.Count,
                loaded.Values.Sum(ships => ships.Ships.Count));
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            // Discarded rather than refused, which is the licence a derived file has and an
            // authored one does not: the worst this costs is a rebuild from the journals.
            logger.LogWarning(ex, "Could not read {Path}; starting with no stored loadouts", path);
        }
    }

    /// <summary>
    /// Writes every Commander's ships through <see cref="AtomicFile"/>, and keeps what it wrote
    /// so <see cref="For"/> and <see cref="All"/> answer with it afterwards.
    /// <para>
    /// <b>An empty picture is written rather than skipped</b>, which is the one place this
    /// deliberately differs from <see cref="SamplingStore"/>: a Commander whose last remembered
    /// ship was just sold has an empty set, and refusing to write it would leave the sold ship in
    /// the file to be loaded back tomorrow. A file with no Commanders in it at all is still
    /// skipped — that is nothing to say rather than something to say.
    /// </para>
    /// <para>
    /// <b>Merged over what was loaded rather than replacing it.</b> A second Commander who has not
    /// flown this session is not in <paramref name="commanders"/> at all, and writing only what
    /// this session has seen would delete their ships from the file — quietly, and permanently
    /// once the journals scrolled past the catch-up window. Everyone in the file stays in the
    /// file; whoever flew today is updated.
    /// </para>
    /// </summary>
    public void Save(IEnumerable<CommanderGameState> commanders)
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

        var document = new Document
        {
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

            // A row that never carried a hull is not a ship, it is a gap — and IsKnown reads off
            // Type, so letting one in would put an entry in the fleet that renders as nothing.
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
        public IReadOnlyList<CommanderRecord> Commanders { get; set; } = [];
    }

    private sealed class CommanderRecord
    {
        public string FrontierId { get; set; } = string.Empty;

        public IReadOnlyList<ShipRecord>? Ships { get; set; }
    }

    /// <summary>
    /// One remembered ship. <b>Every field of <see cref="ShipLoadout"/> that is set rather than
    /// computed</b> — a persisted field that was quietly dropped would read as a ship that came
    /// back from the file with less in it than went in, which no surface would report.
    /// <c>ALoadoutOutlivesTheBackfillWindowTests</c> holds that by reflection rather than by
    /// anybody remembering.
    /// </summary>
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

using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Ships;

/// <summary>
/// The Commander's ship builds, in one file beside the executable (Phase 26, "The fleet, and the fleet
/// you intend").
/// </summary>
public sealed class ShipBuildStore(string path, IFileSystem files, ILogger<ShipBuildStore> logger)
{
    private static readonly JsonSerializerOptions Json = StoreJson.KeepingNulls;

    /// <summary>The most builds one file may hold.</summary>
    public const int MaxBuilds = 64;

    /// <summary>The most slots one build may plan.</summary>
    public const int MaxSlots = 64;

    private readonly Lock _gate = new();

    private IReadOnlyList<ShipBuild> _builds = [];

    /// <summary>The file's contents as last read.</summary>
    private readonly WatchedTextFile _file = new(files, path, logger, "Could not read the ship build file");

    /// <summary>Raised when the set changed, whoever wrote it — the panel, a phrase, an editor.</summary>
    public event Action? Changed;

    public string Path => path;

    public IReadOnlyList<ShipBuild> Builds
    {
        get
        {
            lock (_gate)
            {
                return _builds;
            }
        }
    }

    /// <summary>The build with this identity, or null.</summary>
    public ShipBuild? Find(string id) =>
        Builds.FirstOrDefault(build => string.Equals(build.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>One Commander's build for a journal ship id, or null when nothing is planned for it.</summary>
    public ShipBuild? ForShip(string? fid, int shipId) =>
        Builds.FirstOrDefault(build =>
            build.ShipId == shipId
            && string.Equals(build.CommanderFid, fid ?? string.Empty, StringComparison.Ordinal));

    /// <summary>Everything one Commander has planned, owned hulls and intended ones alike.</summary>
    public IReadOnlyList<ShipBuild> BuildsFor(string? fid) =>
        [.. Builds.Where(build =>
            string.Equals(build.CommanderFid, fid ?? string.Empty, StringComparison.Ordinal))];

    /// <summary>Stamps this Commander's id onto every build from before the file carried one.</summary>
    public bool Adopt(string fid, string? name = null)
    {
        if (fid.Length == 0 || !Builds.Any(build => build.CommanderFid.Length == 0))
        {
            return false;
        }

        Save([.. Builds.Select(build => build.CommanderFid.Length == 0
            ? build with { CommanderFid = fid, CommanderName = name }
            : build)]);

        return true;
    }

    /// <summary>Re-reads if the file changed.</summary>
    public bool Poll() => _file.Poll(
        () =>
        {
            lock (_gate)
            {
                _builds = [];
            }

            Changed?.Invoke();
        },
        Reload);

    /// <summary>
    /// Writes a new set, and re-reads what landed so the store never believes something the file does
    /// not say.
    /// </summary>
    public void Save(IReadOnlyList<ShipBuild> builds)
    {
        var file = new BuildFile
        {
            Ships = [.. builds.Take(MaxBuilds).Select(build => new BuildLine
            {
                CommanderFid = build.CommanderFid.Length > 0 ? build.CommanderFid : null,
                CommanderName = build.CommanderName,
                Id = build.Id,
                Hull = build.Hull,
                ShipId = build.ShipId,
                Name = build.Name,
                Slots = [.. build.Slots.Take(MaxSlots).Select(plan => new SlotLine
                {
                    Slot = plan.Slot,
                    Blueprint = plan.Blueprint,
                    Grade = plan.Grade,
                    Experimental = plan.Experimental,
                    Module = plan.Module,
                    Variant = plan.Variant,
                    Priority = plan.Priority,
                })],
                Moves = build.PriorityMoves.Count > 0
                    ? build.PriorityMoves.ToDictionary(move => move.Key, move => move.Value)
                    : null,
            })],
        };

        var text = JsonSerializer.Serialize(file, Json);

        try
        {
            files.WriteText(path, text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not write the ship build file");
            return;
        }

        Reload(text);
    }

    private bool Reload(string text)
    {
        BuildFile? file;

        try
        {
            file = JsonSerializer.Deserialize<BuildFile>(text, Json);
        }
        catch (JsonException ex)
        {
            lock (_gate)
            {
                _file.Seen(text);
            }

            logger.LogWarning(ex, "The ship build file could not be read");
            Changed?.Invoke();
            return true;
        }

        var builds = new List<ShipBuild>();

        foreach (var line in file?.Ships ?? [])
        {
            var written = (line.Hull ?? string.Empty).Trim();

            if (written.Length == 0)
            {
                continue;
            }

            // Normalised on the way in, not refused: `ShipBuild.Hull` is the journal's own symbol — the slot
            // layout is keyed on it and the checklist compares against it — and files already on disk hold
            // display names, because the fleet's `StoredShip.Type` is `ShipType_Localised` where Frontier
            // supplies one.
            var hull = Knowledge.EliteSpecifications.Ship(written)?.Symbol ?? written;

            var id = (line.Id ?? string.Empty).Trim();

            if (id.Length == 0)
            {
                continue;
            }

            if (builds.Any(existing => string.Equals(existing.Id, id, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            // One build per ship, enforced where the file is read rather than only where it is written: a
            // hand edit is exactly the route that would otherwise produce two.
            var fid = (line.CommanderFid ?? string.Empty).Trim();

            if (line.ShipId is { } shipId
                && builds.Any(existing => existing.ShipId == shipId
                                          && string.Equals(existing.CommanderFid, fid, StringComparison.Ordinal)))
            {
                continue;
            }

            if (builds.Count >= MaxBuilds)
            {
                continue;
            }

            var slots = new List<SlotPlan>();

            foreach (var slot in line.Slots ?? [])
            {
                var name = (slot.Slot ?? string.Empty).Trim();

                if (name.Length == 0)
                {
                    continue;
                }

                if (slots.Any(existing => string.Equals(existing.Slot, name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (slots.Count >= MaxSlots)
                {
                    continue;
                }

                slots.Add(new SlotPlan(
                    name,
                    Blank(slot.Blueprint),

                    // A stored plan from before grades stopped being nullable reads as none rather than
                    // failing the load, the same way a retired persona id does.
                    slot.Grade ?? 0,
                    Blank(slot.Experimental),
                    Blank(slot.Module))
                {
                    Variant = Blank(slot.Variant),

                    // Absent on a build saved before this existed, which reads as group 1 rather than as
                    // unplanned (#253).
                    Priority = slot.Priority ?? 1,
                });
            }

            var moves = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var (slot, priority) in line.Moves ?? [])
            {
                var name = slot.Trim();

                if (name.Length == 0)
                {
                    continue;
                }

                if (priority is < 1 or > 5)
                {
                    continue;
                }

                moves[name] = priority;
            }

            builds.Add(new ShipBuild(fid, id, hull, line.ShipId, Blank(line.Name), slots)
            {
                CommanderName = Blank(line.CommanderName),
                PriorityMoves = moves,
            });
        }

        lock (_gate)
        {
            _builds = builds;
            _file.Seen(text);
        }

        Changed?.Invoke();
        return true;
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record BuildFile
    {
        public IReadOnlyList<BuildLine> Ships { get; init; } = [];
    }

    private sealed record BuildLine
    {
        public string? CommanderFid { get; init; }

        public string? CommanderName { get; init; }

        public string? Id { get; init; }

        public string? Hull { get; init; }

        public int? ShipId { get; init; }

        public string? Name { get; init; }

        public IReadOnlyList<SlotLine> Slots { get; init; } = [];

        /// <summary>Priority groups slots have been moved to, keyed by slot; absent when there are none.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, int>? Moves { get; init; }
    }

    private sealed record SlotLine
    {
        public string? Slot { get; init; }

        public string? Blueprint { get; init; }

        public int? Grade { get; init; }

        public string? Experimental { get; init; }

        public string? Module { get; init; }

        /// <summary>The exact module by symbol, where one was chosen.</summary>
        public string? Variant { get; init; }

        public int? Priority { get; init; }
    }
}

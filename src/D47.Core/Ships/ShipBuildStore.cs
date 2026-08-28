using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace D47.Core.Ships;

/// <summary>One build the file was asked to hold, and why it was refused.</summary>
public sealed record ShipBuildProblem(string Where, string Reason);

/// <summary>
/// The Commander's ship builds, in one file beside the executable (Phase 26, "The fleet,
/// and the fleet you intend").
/// <para>
/// <b>Ships owns its own store, and nothing crosses into the checklist unasked.</b> The plan owns
/// <em>what</em> and the checklist owns <em>when</em> — so a build changing does not reorder
/// anybody's work, and work reaching the list is an act the Commander performs. Promotion goes
/// through <c>ChecklistProposals</c>, which is the third use of that mechanism and a good sign the
/// trust boundary is in the right place.
/// </para>
/// <para>
/// <b>Change is detected by comparing content rather than a last-write time</b>, which is Phase 21
/// 's correction: Windows updates the file-system clock about every 15.6 ms, so two writes
/// inside one tick carry the same stamp and the second is invisible.
/// </para>
/// <para>
/// <b>A bad build is reported, never silently dropped</b>, and the rest of the file still loads —
/// one typo must not cost somebody the other five ships they had planned.
/// </para>
/// </summary>
public sealed class ShipBuildStore(string path, ILogger<ShipBuildStore> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>The most builds one file may hold. A guard on a hand-editable file.</summary>
    public const int MaxBuilds = 64;

    /// <summary>The most slots one build may plan. A hull has around twenty.</summary>
    public const int MaxSlots = 64;

    private readonly Lock _gate = new();

    private IReadOnlyList<ShipBuild> _builds = [];
    private IReadOnlyList<ShipBuildProblem> _problems = [];

    /// <summary>The file's contents as last read. See the class summary for why not a stamp.</summary>
    private string? _seen;

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

    /// <summary>Builds that were refused, and why. Empty in the ordinary case.</summary>
    public IReadOnlyList<ShipBuildProblem> Problems
    {
        get
        {
            lock (_gate)
            {
                return _problems;
            }
        }
    }

    /// <summary>The build with this identity, or null.</summary>
    public ShipBuild? Find(string id) =>
        Builds.FirstOrDefault(build => string.Equals(build.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// One Commander's build for a journal ship id, or null when nothing is planned for it. The
    /// Commander is half the key (the Phase 44 defect item): ship ids are per Commander,
    /// so two Commanders' ship 7s are two ships.
    /// </summary>
    public ShipBuild? ForShip(string? fid, int shipId) =>
        Builds.FirstOrDefault(build =>
            build.ShipId == shipId
            && string.Equals(build.CommanderFid, fid ?? string.Empty, StringComparison.Ordinal));

    /// <summary>Everything one Commander has planned, owned hulls and intended ones alike.</summary>
    public IReadOnlyList<ShipBuild> BuildsFor(string? fid) =>
        [.. Builds.Where(build =>
            string.Equals(build.CommanderFid, fid ?? string.Empty, StringComparison.Ordinal))];

    /// <summary>
    /// Stamps this Commander's id onto every build from before the file carried one. A pre-existing
    /// file was written by the installation's one Commander, so the first one seen claims it — the
    /// same reasoning as <see cref="Checklists.ChecklistService"/> adopting unowned notes. True when
    /// anything was claimed.
    /// </summary>
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

    /// <summary>
    /// Re-reads if the file changed. Pull-based and clock-free like every other reader in Core, so
    /// a build edited by hand is live without a restart.
    /// </summary>
    public bool Poll()
    {
        string text;

        try
        {
            if (!File.Exists(path))
            {
                // Not an error: no builds is the normal state, and will be until somebody plans one.
                if (_seen is null)
                {
                    return false;
                }

                lock (_gate)
                {
                    _builds = [];
                    _problems = [];
                    _seen = null;
                }

                Changed?.Invoke();
                return true;
            }

            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            using var reader = new StreamReader(stream);
            text = reader.ReadToEnd();
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, "Could not read the ship build file");
            return false;
        }

        return !string.Equals(text, _seen, StringComparison.Ordinal) && Reload(text);
    }

    /// <summary>
    /// Writes a new set, and re-reads what landed so the store never believes something the file
    /// does not say.
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
                Settled = build.Settled,
                Slots = [.. build.Slots.Take(MaxSlots).Select(plan => new SlotLine
                {
                    Slot = plan.Slot,
                    Blueprint = plan.Blueprint,
                    Grade = plan.Grade,
                    Engineer = plan.Engineer,
                    Experimental = plan.Experimental,
                    Module = plan.Module,
                    Variant = plan.Variant,
                })],
            })],
        };

        var text = JsonSerializer.Serialize(file, Json);

        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
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
                _problems = [new ShipBuildProblem(System.IO.Path.GetFileName(path), ex.Message)];
                _seen = text;
            }

            logger.LogWarning(ex, "The ship build file could not be read");
            Changed?.Invoke();
            return true;
        }

        var builds = new List<ShipBuild>();
        var problems = new List<ShipBuildProblem>();

        foreach (var line in file?.Ships ?? [])
        {
            var written = (line.Hull ?? string.Empty).Trim();

            if (written.Length == 0)
            {
                problems.Add(new ShipBuildProblem("a build", "it names no hull."));
                continue;
            }

            // Normalised on the way in, not refused: `ShipBuild.Hull` is the journal's own symbol
            // — the slot layout is keyed on it and the checklist compares against it — and files
            // already on disk hold display names, because the fleet's `StoredShip.Type` is
            // `ShipType_Localised` where Frontier supplies one. This is also what a Commander
            // hand-editing the file will write, since "Panther Clipper Mk II" is what they call
            // it. An unknown spelling stands as given and is reported by the layout, not here.
            var hull = Knowledge.EliteSpecifications.Ship(written)?.Symbol ?? written;

            var id = (line.Id ?? string.Empty).Trim();

            if (id.Length == 0)
            {
                problems.Add(new ShipBuildProblem(hull, "it has no id, so nothing can point at it."));
                continue;
            }

            if (builds.Any(existing => string.Equals(existing.Id, id, StringComparison.OrdinalIgnoreCase)))
            {
                problems.Add(new ShipBuildProblem(hull, $"a second build is using the id \"{id}\"."));
                continue;
            }

            // One build per ship, enforced where the file is read rather than only where it is
            // written: a hand edit is exactly the route that would otherwise produce two. Per
            // Commander, because a ship id alone only names a ship within one Commander's journal.
            var fid = (line.CommanderFid ?? string.Empty).Trim();

            if (line.ShipId is { } shipId
                && builds.Any(existing => existing.ShipId == shipId
                                          && string.Equals(existing.CommanderFid, fid, StringComparison.Ordinal)))
            {
                problems.Add(new ShipBuildProblem(
                    hull, $"ship {shipId} already has a build, and a ship has one."));

                continue;
            }

            if (builds.Count >= MaxBuilds)
            {
                problems.Add(new ShipBuildProblem(hull, $"the file already holds {MaxBuilds} builds."));
                continue;
            }

            var slots = new List<SlotPlan>();

            foreach (var slot in line.Slots ?? [])
            {
                var name = (slot.Slot ?? string.Empty).Trim();

                if (name.Length == 0)
                {
                    problems.Add(new ShipBuildProblem(hull, "a slot plan names no slot."));
                    continue;
                }

                if (slots.Any(existing => string.Equals(existing.Slot, name, StringComparison.OrdinalIgnoreCase)))
                {
                    problems.Add(new ShipBuildProblem(hull, $"{name} is planned twice, and a slot holds one plan."));
                    continue;
                }

                if (slots.Count >= MaxSlots)
                {
                    problems.Add(new ShipBuildProblem(hull, $"that build already plans {MaxSlots} slots."));
                    continue;
                }

                slots.Add(new SlotPlan(
                    name,
                    Blank(slot.Blueprint),

                    // A stored plan from before grades stopped being nullable reads as none rather
                    // than failing the load, the same way a retired persona id does. The stepper
                    // then lands it on the blueprint's highest offered grade, which is where a new
                    // plan would land anyway (remediation.md 15, item 4).
                    slot.Grade ?? 0,
                    Blank(slot.Engineer),
                    Blank(slot.Experimental),
                    Blank(slot.Module))
                {
                    Variant = Blank(slot.Variant),
                });
            }

            builds.Add(new ShipBuild(fid, id, hull, line.ShipId, Blank(line.Name), slots)
            {
                CommanderName = Blank(line.CommanderName),
                Settled = Blank(line.Settled),
            });
        }

        lock (_gate)
        {
            _builds = builds;
            _problems = problems;
            _seen = text;
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

        /// <summary>
        /// The disagreement with the checklist the Commander has already said no to
        /// (Phase 38). See <see cref="ShipBuild.Settled"/>. Never removed or renamed — a
        /// property dropped from this file is a Commander being asked a settled question again.
        /// </summary>
        public string? Settled { get; init; }

        public IReadOnlyList<SlotLine> Slots { get; init; } = [];
    }

    private sealed record SlotLine
    {
        public string? Slot { get; init; }

        public string? Blueprint { get; init; }

        public int? Grade { get; init; }

        public string? Engineer { get; init; }

        public string? Experimental { get; init; }

        public string? Module { get; init; }

        /// <summary>The exact module by symbol, where one was chosen. Never removed or renamed.</summary>
        public string? Variant { get; init; }
    }
}

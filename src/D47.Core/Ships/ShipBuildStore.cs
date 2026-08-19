using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace D47.Core.Ships;

/// <summary>One build the file was asked to hold, and why it was refused.</summary>
public sealed record ShipBuildProblem(string Where, string Reason);

/// <summary>
/// The Commander's ship builds, in one file beside the executable (list.md Phase 26, "The fleet,
/// and the fleet you intend").
/// <para>
/// <b>Ships owns its own store, and nothing crosses into the checklist unasked.</b> The plan owns
/// <em>what</em> and the checklist owns <em>when</em> — so a build changing does not reorder
/// anybody's work, and work reaching the list is an act the Commander performs. Promotion goes
/// through <c>ChecklistProposals</c>, which is the third use of that mechanism and a good sign the
/// trust boundary is in the right place.
/// </para>
/// <para>
/// <b>Change is detected by comparing content rather than a last-write time</b>, which is Phase
/// 21's correction: Windows updates the file-system clock about every 15.6 ms, so two writes
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

    /// <summary>The build bound to a journal ship id, or null when nothing is planned for it.</summary>
    public ShipBuild? ForShip(int shipId) =>
        Builds.FirstOrDefault(build => build.ShipId == shipId);

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
                Id = build.Id,
                Hull = build.Hull,
                ShipId = build.ShipId,
                Name = build.Name,
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
            var hull = (line.Hull ?? string.Empty).Trim();

            if (hull.Length == 0)
            {
                problems.Add(new ShipBuildProblem("a build", "it names no hull."));
                continue;
            }

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
            // written: a hand edit is exactly the route that would otherwise produce two.
            if (line.ShipId is { } shipId && builds.Any(existing => existing.ShipId == shipId))
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
                    slot.Grade,
                    Blank(slot.Engineer),
                    Blank(slot.Experimental),
                    Blank(slot.Module))
                {
                    Variant = Blank(slot.Variant),
                });
            }

            builds.Add(new ShipBuild(id, hull, line.ShipId, Blank(line.Name), slots));
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
        public string? Id { get; init; }

        public string? Hull { get; init; }

        public int? ShipId { get; init; }

        public string? Name { get; init; }

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

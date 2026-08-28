using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Core.Loadout;

/// <summary>One build the file was asked to hold, and why it was refused.</summary>
public sealed record OnFootBuildProblem(string Where, string Reason);

/// <summary>
/// The Commander's suit and weapon builds, in one file beside the executable (Phase 27,
/// "The same page, on foot").
/// <para>
/// <b>Its own file rather than a second array in the ship one</b>, and the reason is the reason
/// the page is a second mode rather than a second tab: the game separates ship and on-foot hard,
/// so a Commander opening a file to hand-edit a suit should not be reading past twenty hardpoints
/// to find it. Everything else here is <see cref="Ships.ShipBuildStore"/>'s, deliberately —
/// content comparison rather than a write time, a bad build reported rather than silently
/// dropped, and the rest of the file still loading around it.
/// </para>
/// </summary>
public sealed class OnFootBuildStore(string path, ILogger<OnFootBuildStore> logger)
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

    /// <summary>The most slots one build may plan: the grade, and four modifications.</summary>
    public const int MaxSlots = OnFootRules.MaxSlots + 1;

    private readonly Lock _gate = new();

    private IReadOnlyList<OnFootBuild> _builds = [];
    private IReadOnlyList<OnFootBuildProblem> _problems = [];

    /// <summary>
    /// The file's contents as last read. Content rather than a stamp, which is Phase 21's
    /// correction: two writes inside one file-system tick carry the same time and the second is
    /// invisible.
    /// </summary>
    private string? _seen;

    /// <summary>Raised when the set changed, whoever wrote it — the panel, a phrase, an editor.</summary>
    public event Action? Changed;

    public string Path => path;

    public IReadOnlyList<OnFootBuild> Builds
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
    public IReadOnlyList<OnFootBuildProblem> Problems
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
    public OnFootBuild? Find(string id) =>
        Builds.FirstOrDefault(build => string.Equals(build.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The build bound to a journal id, or null when nothing is planned for it.
    /// <para>
    /// The kind is asked for as well as the id: a <c>SuitID</c> and a <c>SuitModuleID</c> are
    /// drawn from the same counter, so an id on its own is not an identity.
    /// </para>
    /// </summary>
    public OnFootBuild? ForItem(OnFootKind kind, long itemId) =>
        Builds.FirstOrDefault(build => build.ItemId == itemId && build.Kind == kind);

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
            logger.LogDebug(ex, "Could not read the on-foot build file");
            return false;
        }

        return !string.Equals(text, _seen, StringComparison.Ordinal) && Reload(text);
    }

    /// <summary>
    /// Writes a new set, and re-reads what landed so the store never believes something the file
    /// does not say.
    /// </summary>
    public void Save(IReadOnlyList<OnFootBuild> builds)
    {
        var file = new BuildFile
        {
            Kit = [.. builds.Take(MaxBuilds).Select(build => new BuildLine
            {
                Id = build.Id,
                Equipment = build.Equipment,
                Kind = build.Kind,
                ItemId = build.ItemId,
                Slots = [.. build.Slots.Take(MaxSlots).Select(plan => new SlotLine
                {
                    Slot = plan.Slot,
                    Grade = plan.Grade,
                    Modification = plan.Modification,
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
            logger.LogWarning(ex, "Could not write the on-foot build file");
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
                _problems = [new OnFootBuildProblem(System.IO.Path.GetFileName(path), ex.Message)];
                _seen = text;
            }

            logger.LogWarning(ex, "The on-foot build file could not be read");
            Changed?.Invoke();
            return true;
        }

        var builds = new List<OnFootBuild>();
        var problems = new List<OnFootBuildProblem>();

        foreach (var line in file?.Kit ?? [])
        {
            var equipment = (line.Equipment ?? string.Empty).Trim();

            if (equipment.Length == 0)
            {
                problems.Add(new OnFootBuildProblem("a build", "it names no suit or weapon."));
                continue;
            }

            var id = (line.Id ?? string.Empty).Trim();

            if (id.Length == 0)
            {
                problems.Add(new OnFootBuildProblem(equipment, "it has no id, so nothing can point at it."));
                continue;
            }

            if (builds.Any(existing => string.Equals(existing.Id, id, StringComparison.OrdinalIgnoreCase)))
            {
                problems.Add(new OnFootBuildProblem(equipment, $"a second build is using the id \"{id}\"."));
                continue;
            }

            if (line.Kind is not (OnFootKind.Suit or OnFootKind.Weapon))
            {
                problems.Add(new OnFootBuildProblem(
                    equipment, "a build is for a suit or a weapon, and that is neither."));

                continue;
            }

            // One build per item, enforced where the file is read rather than only where it is
            // written: a hand edit is exactly the route that would otherwise produce two.
            if (line.ItemId is { } itemId
                && builds.Any(existing => existing.ItemId == itemId && existing.Kind == line.Kind))
            {
                problems.Add(new OnFootBuildProblem(
                    equipment, $"item {itemId} already has a build, and an item has one."));

                continue;
            }

            if (builds.Count >= MaxBuilds)
            {
                problems.Add(new OnFootBuildProblem(equipment, $"the file already holds {MaxBuilds} builds."));
                continue;
            }

            var slots = new List<KitPlan>();

            foreach (var slot in line.Slots ?? [])
            {
                var name = (slot.Slot ?? string.Empty).Trim();

                if (name.Length == 0)
                {
                    problems.Add(new OnFootBuildProblem(equipment, "a slot plan names no slot."));
                    continue;
                }

                // The mod slot read as the mod slot it is. An item on foot has a grade and four
                // modification slots and nothing else, so a hand edit naming a hardpoint is
                // refused here rather than stored and then shown as something that can never
                // promote.
                if (!OnFootBuild.IsSlot(name))
                {
                    problems.Add(new OnFootBuildProblem(
                        equipment,
                        $"\"{name}\" is not a slot on foot — it is {OnFootBuild.GradeSlot}, or "
                        + $"{OnFootBuild.ModSlot(1)} to {OnFootBuild.ModSlot(OnFootRules.MaxSlots)}."));

                    continue;
                }

                if (slots.Any(existing => string.Equals(existing.Slot, name, StringComparison.OrdinalIgnoreCase)))
                {
                    problems.Add(new OnFootBuildProblem(
                        equipment, $"{name} is planned twice, and a slot holds one plan."));

                    continue;
                }

                if (slots.Count >= MaxSlots)
                {
                    problems.Add(new OnFootBuildProblem(equipment, $"that build already plans {MaxSlots} slots."));
                    continue;
                }

                slots.Add(new KitPlan(name, slot.Grade, Blank(slot.Modification)));
            }

            builds.Add(new OnFootBuild(id, equipment, line.Kind, line.ItemId, slots));
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
        public IReadOnlyList<BuildLine> Kit { get; init; } = [];
    }

    private sealed record BuildLine
    {
        public string? Id { get; init; }

        public string? Equipment { get; init; }

        public OnFootKind Kind { get; init; } = OnFootKind.Suit;

        public long? ItemId { get; init; }

        public IReadOnlyList<SlotLine> Slots { get; init; } = [];
    }

    private sealed record SlotLine
    {
        public string? Slot { get; init; }

        public int? Grade { get; init; }

        public string? Modification { get; init; }
    }
}

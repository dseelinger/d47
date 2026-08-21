using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace D47.Core.Persona;

/// <summary>
/// One ship, and the core that flies it (list.md Phase 35, "A ship remembers the core that flew
/// it").
/// <para>
/// <b>Keyed on the journal's <c>ShipID</c> rather than on the hull</b>, which is the whole of the
/// requirement: two Kraits are two ships, and a Commander who kitted one for mining and one for a
/// fight means different things by them. It is also what survives a rename, and it is what
/// <see cref="Checklists.ChecklistScope.Ship(int)"/> is already keyed on.
/// </para>
/// </summary>
/// <param name="CommanderFid">
/// Whose binding this is — the Frontier id, <b>inside the document rather than in a path</b>, which
/// is <see cref="Checklists.ChecklistDocument"/>'s rule for the same untrusted input. It is half
/// the key: Elite's <c>ShipID</c> is per Commander and starts small, so without it one Commander's
/// ship 7 answered for another's (the list.md Phase 44 defect item). Empty for a line written
/// before this file carried a Commander; the first Commander seen adopts those.
/// </param>
/// <param name="ShipId">The journal's id for this ship.</param>
/// <param name="Core">A core id from <see cref="PersonaCatalog"/>.</param>
/// <param name="Hull">
/// The hull symbol as the journal writes it, and <paramref name="Name"/> what the Commander
/// christened it. Neither is read back for anything: they are written so that a Commander opening
/// the file sees "python / Bad Idea" beside the number rather than a bare id they would have to go
/// and look up. A hand edit that gets them wrong changes nothing.
/// </param>
public sealed record ShipCoreBinding(
    string CommanderFid, int ShipId, string Core, string? Hull = null, string? Name = null)
{
    /// <summary>The Commander's name at the time of writing, for a person reading a file two
    /// Commanders now share. Never the key.</summary>
    public string? CommanderName { get; init; }
}

/// <summary>One binding the file was asked to hold, and why it was refused.</summary>
public sealed record ShipCoreProblem(string Where, string Reason);

/// <summary>
/// The Commander's ship-to-core bindings, in one file beside the executable (list.md Phase 35,
/// "Nothing is bound until it is asked for").
/// <para>
/// <b>Its own file rather than a field on <c>ships.json</c></b>, which the phase called the
/// obvious home for it. A record there is a <em>build</em> — a plan, which exists for hulls the
/// Commander does not own yet and does not exist for most of the ships they fly. Hanging a core
/// off one would mean a plan being created as a side effect of stating a preference, and a plan
/// being deleted silently forgetting which core flies the ship. Two files, two lifetimes.
/// </para>
/// <para>
/// <b>Written at the Commander's command, never by watching.</b> Nothing in Core calls
/// <see cref="Bind"/> off a journal event; the callers are a panel button, a phrase, a hotkey —
/// and the model is not among them, because a tool that could bind a core to a ship is a tool that
/// can change who is speaking one ship swap later.
/// </para>
/// <para>
/// Same discipline as <see cref="Ships.ShipBuildStore"/>, deliberately: hand-editable, polled by
/// content rather than by a file-system stamp — Windows moves that clock about every 15.6 ms, so
/// two writes inside one tick are indistinguishable — and a bad line is reported rather than
/// silently dropped, with the rest of the file still loading.
/// </para>
/// </summary>
public sealed class ShipCoreStore(string path, ILogger<ShipCoreStore> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// The most bindings one file may hold. A guard on a hand-editable file rather than a limit
    /// anybody will meet: the largest fleets in the game are a few dozen ships.
    /// </summary>
    public const int MaxBindings = 128;

    private readonly Lock _gate = new();

    private IReadOnlyList<ShipCoreBinding> _bindings = [];
    private IReadOnlyList<ShipCoreProblem> _problems = [];

    /// <summary>The file's contents as last read. See the class summary for why not a stamp.</summary>
    private string? _seen;

    /// <summary>Raised when the set changed, whoever wrote it — the panel, a phrase, an editor.</summary>
    public event Action? Changed;

    public string Path => path;

    public IReadOnlyList<ShipCoreBinding> Bindings
    {
        get
        {
            lock (_gate)
            {
                return _bindings;
            }
        }
    }

    /// <summary>Bindings that were refused, and why. Empty in the ordinary case.</summary>
    public IReadOnlyList<ShipCoreProblem> Problems
    {
        get
        {
            lock (_gate)
            {
                return _problems;
            }
        }
    }

    /// <summary>
    /// The core this Commander bound to this ship, or null when nothing is. The Commander is half
    /// the key: two Commanders' ship 7s are two ships.
    /// </summary>
    public ShipCoreBinding? For(string? fid, int shipId) =>
        Bindings.FirstOrDefault(binding =>
            binding.ShipId == shipId
            && string.Equals(binding.CommanderFid, fid ?? string.Empty, StringComparison.Ordinal));

    /// <summary>
    /// Stamps this Commander's id onto every binding from before the file carried one. A pre-existing
    /// file was written by the installation's one Commander, so the first one seen claims it — the
    /// same reasoning as <see cref="Checklists.ChecklistService"/> adopting unowned notes. True when
    /// anything was claimed.
    /// </summary>
    public bool Adopt(string fid, string? name = null)
    {
        if (fid.Length == 0 || !Bindings.Any(binding => binding.CommanderFid.Length == 0))
        {
            return false;
        }

        Save([.. Bindings.Select(binding => binding.CommanderFid.Length == 0
            ? binding with { CommanderFid = fid, CommanderName = name }
            : binding)]);

        return true;
    }

    /// <summary>
    /// Re-reads if the file changed. Pull-based and clock-free like every other reader in Core, so
    /// a binding edited by hand is live without a restart.
    /// </summary>
    public bool Poll()
    {
        string text;

        try
        {
            if (!File.Exists(path))
            {
                // Not an error: nothing bound is the normal state, and stays it until the
                // Commander says otherwise.
                if (_seen is null)
                {
                    return false;
                }

                lock (_gate)
                {
                    _bindings = [];
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
            logger.LogDebug(ex, "Could not read the ship core file");
            return false;
        }

        return !string.Equals(text, _seen, StringComparison.Ordinal) && Reload(text);
    }

    /// <summary>
    /// Binds one ship, replacing whatever it was bound to. Returns the binding as stored.
    /// <para>
    /// One core per ship, because a ship has one AI aboard. Rebinding is therefore the same act
    /// as binding and there is nothing to undo first.
    /// </para>
    /// </summary>
    public ShipCoreBinding Bind(ShipCoreBinding binding)
    {
        var kept = Bindings
            .Where(existing => !Same(existing, binding.CommanderFid, binding.ShipId))
            .Take(MaxBindings - 1)
            .Append(binding)
            .OrderBy(existing => existing.CommanderFid, StringComparer.Ordinal)
            .ThenBy(existing => existing.ShipId)
            .ToArray();

        Save(kept);

        return binding;
    }

    /// <summary>Unbinds one Commander's ship. True when there was something to unbind.</summary>
    public bool Forget(string? fid, int shipId)
    {
        if (For(fid, shipId) is null)
        {
            return false;
        }

        Save([.. Bindings.Where(existing => !Same(existing, fid ?? string.Empty, shipId))]);

        return true;
    }

    private static bool Same(ShipCoreBinding binding, string fid, int shipId) =>
        binding.ShipId == shipId
        && string.Equals(binding.CommanderFid, fid, StringComparison.Ordinal);

    /// <summary>
    /// Writes a new set, and re-reads what landed so the store never believes something the file
    /// does not say.
    /// </summary>
    public void Save(IReadOnlyList<ShipCoreBinding> bindings)
    {
        var file = new BindingFile
        {
            Ships = [.. bindings.Take(MaxBindings).Select(binding => new BindingLine
            {
                CommanderFid = binding.CommanderFid.Length > 0 ? binding.CommanderFid : null,
                CommanderName = binding.CommanderName,
                ShipId = binding.ShipId,
                Core = binding.Core,
                Hull = binding.Hull,
                Name = binding.Name,
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
            logger.LogWarning(ex, "Could not write the ship core file");
            return;
        }

        Reload(text);
    }

    private bool Reload(string text)
    {
        BindingFile? file;

        try
        {
            file = JsonSerializer.Deserialize<BindingFile>(text, Json);
        }
        catch (JsonException ex)
        {
            lock (_gate)
            {
                _problems = [new ShipCoreProblem(System.IO.Path.GetFileName(path), ex.Message)];
                _seen = text;
            }

            logger.LogWarning(ex, "The ship core file could not be read");
            Changed?.Invoke();
            return true;
        }

        var bindings = new List<ShipCoreBinding>();
        var problems = new List<ShipCoreProblem>();

        foreach (var line in file?.Ships ?? [])
        {
            var where = line.Name is { Length: > 0 } named ? named : $"ship {line.ShipId?.ToString() ?? "(none)"}";

            if (line.ShipId is not { } shipId)
            {
                problems.Add(new ShipCoreProblem(where, "it names no ship id, and that is the key."));
                continue;
            }

            var core = (line.Core ?? string.Empty).Trim();

            if (core.Length == 0)
            {
                problems.Add(new ShipCoreProblem(where, "it names no core."));
                continue;
            }

            // A core d47 does not know is refused rather than resolved to the default. The
            // settings row resolves a stale id because a Commander who once picked a core that no
            // longer ships should still get *a* companion; here, resolving would put a core they
            // never chose aboard a ship they did — silently, on boarding it.
            if (!PersonaCatalog.Knows(core))
            {
                problems.Add(new ShipCoreProblem(where, $"there is no core called \"{core}\"."));
                continue;
            }

            // Per Commander, because the ship id alone only names a ship within one Commander's
            // journal. Two Commanders each binding their ship 7 is two ships, not a duplicate.
            var fid = (line.CommanderFid ?? string.Empty).Trim();

            if (bindings.Any(existing => Same(existing, fid, shipId)))
            {
                problems.Add(new ShipCoreProblem(where, $"ship {shipId} is bound twice, and a ship has one core."));
                continue;
            }

            if (bindings.Count >= MaxBindings)
            {
                problems.Add(new ShipCoreProblem(where, $"the file already holds {MaxBindings} bindings."));
                continue;
            }

            bindings.Add(new ShipCoreBinding(fid, shipId, core, Blank(line.Hull), Blank(line.Name))
            {
                CommanderName = Blank(line.CommanderName),
            });
        }

        lock (_gate)
        {
            _bindings = bindings;
            _problems = problems;
            _seen = text;
        }

        Changed?.Invoke();
        return true;
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record BindingFile
    {
        public IReadOnlyList<BindingLine> Ships { get; init; } = [];
    }

    private sealed record BindingLine
    {
        public string? CommanderFid { get; init; }

        public string? CommanderName { get; init; }

        public int? ShipId { get; init; }

        public string? Core { get; init; }

        public string? Hull { get; init; }

        public string? Name { get; init; }
    }
}

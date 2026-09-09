using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace D47.Core.Persona;

/// <summary>One ship, and the core aboard it (Phase 35).</summary>
/// <param name="CommanderFid">
/// Whose binding this is — the Frontier id, inside the document rather than in a path, which is <see
/// cref="Checklists.ChecklistDocument"/>'s rule for the same untrusted input.
/// </param>
/// <param name="ShipId">The journal's id for this ship.</param>
/// <param name="Core">A core id from <see cref="PersonaCatalog"/>.</param>
/// <param name="Hull">
/// The hull symbol as the journal writes it, and <paramref name="Name"/> what the Commander christened
/// it.
/// </param>
public sealed record ShipCoreBinding(
    string CommanderFid, int ShipId, string Core, string? Hull = null, string? Name = null)
{
    /// <summary>
    /// The Commander's name at the time of writing, for a person reading a file two Commanders now
    /// share.
    /// </summary>
    public string? CommanderName { get; init; }
}

/// <summary>One binding the file was asked to hold, and why it was refused.</summary>
public sealed record ShipCoreProblem(string Where, string Reason);

/// <summary>
/// The Commander's ship-to-core bindings, in one file beside the executable (Phase 35, "Nothing is
/// bound until it is asked for").
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

    /// <summary>The most bindings one file may hold.</summary>
    public const int MaxBindings = 128;

    private readonly Lock _gate = new();

    private IReadOnlyList<ShipCoreBinding> _bindings = [];
    private IReadOnlyList<ShipCoreProblem> _problems = [];

    /// <summary>The file's contents as last read.</summary>
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

    /// <summary>Bindings that were refused, and why.</summary>
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

    /// <summary>The core this Commander bound to this ship, or null when nothing is.</summary>
    public ShipCoreBinding? For(string? fid, int shipId) =>
        Bindings.FirstOrDefault(binding =>
            binding.ShipId == shipId
            && string.Equals(binding.CommanderFid, fid ?? string.Empty, StringComparison.Ordinal));

    /// <summary>Stamps this Commander's id onto every binding from before the file carried one.</summary>
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

    /// <summary>Re-reads if the file changed.</summary>
    public bool Poll()
    {
        string text;

        try
        {
            if (!File.Exists(path))
            {
                // Not an error: nothing bound is the normal state, and stays it until the Commander says
                // otherwise.
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

    /// <summary>Binds one ship, replacing whatever it was bound to.</summary>
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

    /// <summary>Unbinds one Commander's ship.</summary>
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
    /// Writes a new set, and re-reads what landed so the store never believes something the file does
    /// not say.
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

            // A core d47 does not know is refused rather than resolved to the default.
            if (!PersonaCatalog.Knows(core))
            {
                problems.Add(new ShipCoreProblem(where, $"there is no core called \"{core}\"."));
                continue;
            }

            // Per Commander, because the ship id alone only names a ship within one Commander's journal.
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

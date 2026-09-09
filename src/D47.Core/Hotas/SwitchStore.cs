using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Hotas;

/// <summary>The Commander's switch mappings, in one file beside the executable (Phase 21).</summary>
public sealed class SwitchStore(string path, ILogger<SwitchStore> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly Lock _gate = new();

    private IReadOnlyList<SwitchMapping> _switches = [];
    private IReadOnlyList<SwitchProblem> _problems = [];

    /// <summary>The file's contents as last read, which is what "has it changed" is answered against.</summary>
    private string? _seen;

    public string Path => path;

    public IReadOnlyList<SwitchMapping> Switches
    {
        get
        {
            lock (_gate)
            {
                return _switches;
            }
        }
    }

    /// <summary>Switches that were refused, and why.</summary>
    public IReadOnlyList<SwitchProblem> Problems
    {
        get
        {
            lock (_gate)
            {
                return _problems;
            }
        }
    }

    public SwitchMapping? Find(string name) =>
        Switches.FirstOrDefault(mapping => string.Equals(mapping.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Re-reads if the file changed.</summary>
    public bool Poll()
    {
        string text;

        try
        {
            if (!File.Exists(path))
            {
                // Not an error: no switches is the normal state, and will be for most Commanders.
                if (_seen is null)
                {
                    return false;
                }

                lock (_gate)
                {
                    _switches = [];
                    _problems = [];
                    _seen = null;
                }

                return true;
            }

            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            using var reader = new StreamReader(stream);
            text = reader.ReadToEnd();
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, "Could not read the switch file");
            return false;
        }

        return !string.Equals(text, _seen, StringComparison.Ordinal) && Reload(text);
    }

    private bool Reload(string text)
    {
        SwitchFile? file;

        try
        {
            file = JsonSerializer.Deserialize<SwitchFile>(text, Json);
        }
        catch (JsonException ex)
        {
            lock (_gate)
            {
                _switches = [];
                _problems = [new SwitchProblem(System.IO.Path.GetFileName(path), ex.Message)];
                _seen = text;
            }

            logger.LogWarning(ex, "The switch file could not be read");
            return true;
        }

        var accepted = new List<SwitchMapping>();
        var problems = new List<SwitchProblem>();
        var taken = new List<string>();

        foreach (var mapping in file?.Switches ?? [])
        {
            var trimmed = mapping with { Name = mapping.Name?.Trim() ?? string.Empty };

            if (SwitchValidation.Problem(trimmed) is { } reason)
            {
                problems.Add(new SwitchProblem(trimmed.Name, reason));
                continue;
            }

            if (taken.Contains(trimmed.Name, StringComparer.OrdinalIgnoreCase))
            {
                problems.Add(new SwitchProblem(trimmed.Name, "There is already a switch with that name."));
                continue;
            }

            if (accepted.Count >= SwitchValidation.MaxSwitches)
            {
                problems.Add(new SwitchProblem(
                    trimmed.Name, $"D47 maps at most {SwitchValidation.MaxSwitches} switches."));
                continue;
            }

            taken.Add(trimmed.Name);
            accepted.Add(trimmed);
        }

        lock (_gate)
        {
            _switches = accepted;
            _problems = problems;
            _seen = text;
        }

        logger.LogInformation(
            "Loaded {Count} switches from {Path} ({Problems} refused)", accepted.Count, path, problems.Count);

        return true;
    }

    /// <summary>Writes the file.</summary>
    public void Save(IReadOnlyList<SwitchMapping> switches)
    {
        var directory = System.IO.Path.GetDirectoryName(path);

        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(new SwitchFile { Switches = [.. switches] }, Json));

        // Forces the next Poll to re-read rather than trusting what was just written, so the in-memory set is
        // always the validated one rather than the one that was submitted.
        _seen = null;
    }

    private sealed class SwitchFile
    {
        public List<SwitchMapping> Switches { get; set; } = [];
    }
}

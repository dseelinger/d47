using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Hotas;

/// <summary>
/// The Commander's switch mappings, in one file beside the executable (list.md Phase 21).
/// <para>
/// <b>A file rather than a settings section</b>, for the reason macros are: this is not a value
/// with a vocabulary, it is a capture — the whole of what d47 will ever know about a Commander's
/// stick — and it is written by a walk rather than chosen from a list. Keeping it out of
/// <c>settings.json</c> also keeps the settings loader's "unknown keys are rejected" rule meaning
/// what it says, and keeps the append-only settings rule from having to cover a shape that
/// changes with somebody's hardware.
/// </para>
/// <para>
/// <b>A bad mapping is reported, never silently dropped.</b> A switch that quietly vanished would
/// be a Commander flipping a toggle into the dark. Whatever is wrong with it is available as a
/// sentence naming the switch, and the rest of the file still loads.
/// </para>
/// </summary>
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
    private DateTime _stamp;

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

    /// <summary>Switches that were refused, and why. Empty in the ordinary case.</summary>
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

    /// <summary>
    /// Re-reads if the file changed. Pull-based and clock-free like every other reader in Core,
    /// so a mapping edited by hand is live without a restart.
    /// </summary>
    public bool Poll()
    {
        DateTime written;

        try
        {
            var info = new FileInfo(path);

            if (!info.Exists)
            {
                // Not an error: no switches is the normal state, and will be for most Commanders.
                if (_stamp == default)
                {
                    return false;
                }

                lock (_gate)
                {
                    _switches = [];
                    _problems = [];
                    _stamp = default;
                }

                return true;
            }

            written = info.LastWriteTimeUtc;
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, "Could not stat the switch file");
            return false;
        }

        return written != _stamp && Reload(written);
    }

    private bool Reload(DateTime written)
    {
        SwitchFile? file;

        try
        {
            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            file = JsonSerializer.Deserialize<SwitchFile>(stream, Json);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            lock (_gate)
            {
                _switches = [];
                _problems = [new SwitchProblem(System.IO.Path.GetFileName(path), ex.Message)];
                _stamp = written;
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
            _stamp = written;
        }

        logger.LogInformation(
            "Loaded {Count} switches from {Path} ({Problems} refused)", accepted.Count, path, problems.Count);

        return true;
    }

    /// <summary>
    /// Writes the file. Used by the panel; the file is equally editable by hand, and both routes
    /// land in the same place.
    /// </summary>
    public void Save(IReadOnlyList<SwitchMapping> switches)
    {
        var directory = System.IO.Path.GetDirectoryName(path);

        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(new SwitchFile { Switches = [.. switches] }, Json));

        // Forces the next Poll to re-read rather than trusting what was just written, so the
        // in-memory set is always the validated one rather than the one that was submitted.
        _stamp = default;
    }

    private sealed class SwitchFile
    {
        public List<SwitchMapping> Switches { get; set; } = [];
    }
}

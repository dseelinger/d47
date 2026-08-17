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

    /// <summary>
    /// The file's contents as last read, which is what "has it changed" is answered against.
    /// <para>
    /// It was a last-write time, and that is not good enough <b>here</b>. Windows updates the
    /// file-system clock about every 15.6 ms, so two writes inside one tick carry the same stamp
    /// and the second is invisible. <see cref="GameStatus"/> stamps for the same reason and is
    /// fine, because Elite rewrites Status.json every second or so and a missed read self-corrects
    /// on the next one. A hand edit is a one-off: miss it and it stays missed until the Commander
    /// edits again, having watched d47 ignore them once already.
    /// </para>
    /// <para>
    /// The airtight version has to be the content, because the alternative needs the clock —
    /// "is this stamp too fresh to trust?" is a question about now — and no Core component reads
    /// the clock. The file holds at most <see cref="SwitchValidation.MaxSwitches"/> switches, so
    /// this is a few kilobytes per poll against a correctness hole that only appears on a fast
    /// machine. It appeared on CI first.
    /// </para>
    /// </summary>
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
        _seen = null;
    }

    private sealed class SwitchFile
    {
        public List<SwitchMapping> Switches { get; set; } = [];
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Actions;

/// <summary>
/// The Commander's macros, in one file beside the executable (list.md Phase 10, "Macros").
/// <para>
/// <b>A file rather than a settings section</b>, because a macro is not a setting: it is a
/// small program, it is the one input whose vocabulary cannot be closed in advance, and it is
/// the checklist's stated exception to "every setting can be set by voice". Keeping it out of
/// <c>settings.json</c> also keeps the settings loader's "unknown keys are rejected" rule
/// meaning what it says.
/// </para>
/// <para>
/// <b>A bad macro is reported, never silently dropped.</b> A macro that quietly vanished would
/// be a Commander saying a phrase into the dark for a week. Whatever is wrong with it is
/// available as a sentence naming the macro, and the rest of the file still loads — one typo
/// must not cost somebody their other nine macros.
/// </para>
/// </summary>
public sealed class MacroStore(string path, ILogger<MacroStore> logger)
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

    private IReadOnlyList<Macro> _macros = [];
    private IReadOnlyList<MacroProblem> _problems = [];
    private DateTime _stamp;

    public string Path => path;

    public IReadOnlyList<Macro> Macros
    {
        get
        {
            lock (_gate)
            {
                return _macros;
            }
        }
    }

    /// <summary>Macros that were refused, and why. Empty in the ordinary case.</summary>
    public IReadOnlyList<MacroProblem> Problems
    {
        get
        {
            lock (_gate)
            {
                return _problems;
            }
        }
    }

    public Macro? Find(string name) =>
        Macros.FirstOrDefault(macro => string.Equals(macro.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Re-reads if the file changed. Pull-based and clock-free like every other reader in Core:
    /// the tick loop calls it, so a macro edited in a text editor is live without a restart.
    /// </summary>
    public bool Poll(IReadOnlyCollection<string> reservedPhrases)
    {
        DateTime written;

        try
        {
            var info = new FileInfo(path);

            if (!info.Exists)
            {
                // Not an error: no macros is the normal state.
                if (_stamp == default)
                {
                    return false;
                }

                lock (_gate)
                {
                    _macros = [];
                    _problems = [];
                    _stamp = default;
                }

                return true;
            }

            written = info.LastWriteTimeUtc;
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, "Could not stat the macro file");
            return false;
        }

        if (written == _stamp)
        {
            return false;
        }

        return Reload(reservedPhrases, written);
    }

    private bool Reload(IReadOnlyCollection<string> reservedPhrases, DateTime written)
    {
        MacroFile? file;

        try
        {
            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            file = JsonSerializer.Deserialize<MacroFile>(stream, Json);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // The whole file being unreadable is one problem with one name, not a reason to
            // throw: d47 stays usable and says what is wrong with the file.
            lock (_gate)
            {
                _macros = [];
                _problems = [new MacroProblem(System.IO.Path.GetFileName(path), ex.Message)];
                _stamp = written;
            }

            logger.LogWarning(ex, "The macro file could not be read");
            return true;
        }

        var accepted = new List<Macro>();
        var problems = new List<MacroProblem>();
        var taken = new List<string>(reservedPhrases);

        foreach (var macro in file?.Macros ?? [])
        {
            var trimmed = macro with { Name = macro.Name?.Trim() ?? string.Empty };

            if (MacroValidation.Problem(trimmed, taken) is { } reason)
            {
                problems.Add(new MacroProblem(trimmed.Name, reason));
                continue;
            }

            // Added as it is accepted, so a file with the same macro twice reports the second
            // one rather than silently preferring one of them.
            taken.Add(trimmed.Name);
            accepted.Add(trimmed);
        }

        lock (_gate)
        {
            _macros = accepted;
            _problems = problems;
            _stamp = written;
        }

        logger.LogInformation(
            "Loaded {Count} macros from {Path} ({Problems} refused)", accepted.Count, path, problems.Count);

        return true;
    }

    /// <summary>
    /// Writes the file. Used by the panel's editor; the file is equally editable by hand, and
    /// both routes land in the same place.
    /// </summary>
    public void Save(IReadOnlyList<Macro> macros)
    {
        var directory = System.IO.Path.GetDirectoryName(path);

        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(new MacroFile { Macros = [.. macros] }, Json));

        // Forces the next Poll to re-read rather than trusting what was just written, so the
        // in-memory set is always the validated one rather than the one that was submitted.
        _stamp = default;
    }

    private sealed class MacroFile
    {
        public List<Macro> Macros { get; set; } = [];
    }
}

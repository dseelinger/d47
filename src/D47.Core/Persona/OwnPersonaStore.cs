using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Persona;

/// <summary>
/// One core the Commander wrote (remediation.md 11, item 9).
/// </summary>
/// <param name="Id">
/// Minted from the name and never shown. Stable across a rename, because the settings file names
/// the selected core by id and a Commander correcting a typo in a name should not find themselves
/// talking to Warden again.
/// </param>
/// <param name="Name">What it is called, and what it answers to.</param>
/// <param name="Body">
/// The character, in the Commander's own words. The whole of what makes one core different from
/// another — everything else about the frame is supplied.
/// </param>
/// <param name="Voice">
/// How it should sound, for the voice pairing to match against. The same kind of sentence the
/// shipped cores carry, and free text rather than a picker because a provider's catalogue is its
/// own and a description outlives it.
/// </param>
/// <param name="Gender">
/// Which voices it may be paired with, where the Commander has an opinion. Unspecified is the
/// ordinary answer and admits any of them.
/// </param>
public sealed record OwnPersona(
    string Id,
    string Name,
    string Body,
    string Voice = "",
    VoiceGender Gender = VoiceGender.Unspecified)
{
    /// <summary>
    /// The longest a body may be. Long enough for a real character — the shipped cores run to
    /// about this — and short enough that it cannot quietly become most of the prompt.
    /// </summary>
    public const int MaxBodyLength = 4000;

    /// <summary>As long as a name anybody will say out loud.</summary>
    public const int MaxNameLength = 40;

    /// <summary>
    /// The core, as the rest of d47 sees one.
    /// <para>
    /// <b>The frame is supplied and only the character is the Commander's.</b> The shared preamble
    /// and the standing instructions wrap this exactly as they wrap a shipped core — they are what
    /// hold the cast together and what stops a model sanding a persona toward pleasant and helpful
    /// over a long session, and a core that opted out of them would drift out of the fiction
    /// within an evening.
    /// </para>
    /// <para>
    /// The lines it does not carry fall back rather than being demanded. A Commander should get a
    /// working core out of a name and a paragraph, and an empty first-meeting line is a box that
    /// stops somebody finishing rather than a thing worth insisting on.
    /// </para>
    /// </summary>
    public Persona AsPersona() => new(
        Id,
        Name,
        Tagline: "Yours. Written by you.",
        Body: Body,
        Intro: $"Commander. I am {Name}. You wrote what I am, so you already know the shape of it.",
        Return: $"Commander. {Name}, still here. There is a gap in my log I cannot account for.",
        VoiceHint: new VoiceHint(
            Voice is { Length: > 0 } said ? said : $"The voice of {Name}, as the Commander imagines it.",
            Gender));
}

/// <summary>One entry the file got wrong, and why. Reported rather than silently dropped.</summary>
public sealed record OwnPersonaProblem(string Which, string Reason);

/// <summary>
/// The cores the Commander wrote, in one file beside the executable (remediation.md 11, item 9).
/// <para>
/// <b>A file rather than a settings section</b>, for the reason <see cref="Actions.MacroStore"/>
/// already gives about macros: a persona is not a setting, it is a piece of writing, and it is one
/// of the two inputs whose vocabulary cannot be closed in advance. Keeping it out of
/// <c>settings.json</c> also keeps that loader's "unknown keys are rejected" rule meaning what it
/// says.
/// </para>
/// <para>
/// <b>Polled on write time</b>, so a core edited in a text editor is live without a restart — the
/// same shape as the macro, checklist, memory and lore stores, because it is the same kind of
/// thing and a second shape would be a second set of bugs.
/// </para>
/// <para>
/// <b>A bad entry is reported, never silently dropped.</b> One typo must not cost a Commander the
/// other three cores they wrote, and a core that quietly vanished would be somebody wondering why
/// their companion had gone back to being Warden.
/// </para>
/// </summary>
public sealed class OwnPersonaStore(string path, ILogger<OwnPersonaStore> logger)
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
    /// How many a Commander may keep. A cast, not a career: the picker is a list somebody reads at
    /// a glance, and the eleven shipped cores are already most of one.
    /// </summary>
    public const int MaxCores = 12;

    private readonly Lock _gate = new();

    private IReadOnlyList<OwnPersona> _cores = [];
    private IReadOnlyList<OwnPersonaProblem> _problems = [];
    private DateTime _stamp;

    public string Path => path;

    /// <summary>Raised when the contents changed, whoever changed them.</summary>
    public event Action? Changed;

    public IReadOnlyList<OwnPersona> Cores
    {
        get
        {
            lock (_gate)
            {
                return _cores;
            }
        }
    }

    /// <summary>Entries that were refused, and why. Empty in the ordinary case.</summary>
    public IReadOnlyList<OwnPersonaProblem> Problems
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
    /// An id for a name, so a core can be selected by the settings file and renamed afterwards
    /// without the selection following the name.
    /// </summary>
    public static string IdFor(string name, IEnumerable<OwnPersona> existing)
    {
        var stem = new string([.. name.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit)]);

        if (stem.Length == 0)
        {
            stem = "core";
        }

        // Prefixed, so a Commander cannot write a core called "Warden" that shadows the shipped
        // one and leaves two rows in the picker with one id between them.
        var wanted = "own." + stem;
        var taken = existing.Select(core => core.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!taken.Contains(wanted))
        {
            return wanted;
        }

        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{wanted}{suffix}";

            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>Null when the core is fine, or the reason it is not.</summary>
    public static string? Problem(OwnPersona? core)
    {
        if (core is null)
        {
            return "The core could not be read at all.";
        }

        if (string.IsNullOrWhiteSpace(core.Id))
        {
            return "It has no id, so nothing can say which core the settings file means.";
        }

        if (string.IsNullOrWhiteSpace(core.Name))
        {
            return $"\"{core.Id}\" has no name, so there is nothing to call it.";
        }

        if (core.Name.Length > OwnPersona.MaxNameLength)
        {
            return $"\"{core.Name}\" is {core.Name.Length} characters; the most is {OwnPersona.MaxNameLength}.";
        }

        if (string.IsNullOrWhiteSpace(core.Body))
        {
            return $"\"{core.Name}\" has nothing written on it, so it is a name with no character behind it.";
        }

        return core.Body.Length > OwnPersona.MaxBodyLength
            ? $"\"{core.Name}\" runs to {core.Body.Length} characters; the most is {OwnPersona.MaxBodyLength}."
            : null;
    }

    /// <summary>
    /// Re-reads if the file changed. Pull-based and clock-free like every other reader in Core.
    /// </summary>
    public bool Poll()
    {
        DateTime written;

        try
        {
            var info = new FileInfo(path);

            if (!info.Exists)
            {
                // Not an error: no cores of your own is the normal state.
                if (_stamp == default)
                {
                    return false;
                }

                lock (_gate)
                {
                    _cores = [];
                    _problems = [];
                    _stamp = default;
                }

                Changed?.Invoke();
                return true;
            }

            written = info.LastWriteTimeUtc;
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, "Could not stat the personas file");
            return false;
        }

        if (written == _stamp)
        {
            return false;
        }

        Reload(written);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Writes the file, keeping only what is fit to load.</summary>
    public void Save(IReadOnlyList<OwnPersona> cores)
    {
        var directory = System.IO.Path.GetDirectoryName(path);

        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        AtomicFile.WriteAllText(
            path,
            JsonSerializer.Serialize(new PersonaFile { Cores = [.. cores] }, Json));

        // Forces the next Poll to re-read rather than trusting what was just written, so the set
        // held in memory is always the validated one rather than the one that was submitted.
        _stamp = default;
        Poll();
    }

    private void Reload(DateTime written)
    {
        PersonaFile? file;

        try
        {
            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            file = JsonSerializer.Deserialize<PersonaFile>(stream, Json);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // The whole file being unreadable is one problem with one name, not a reason to throw.
            lock (_gate)
            {
                _cores = [];
                _problems = [new OwnPersonaProblem(System.IO.Path.GetFileName(path), ex.Message)];
                _stamp = written;
            }

            logger.LogWarning(ex, "The personas file could not be read");
            return;
        }

        var kept = new List<OwnPersona>();
        var refused = new List<OwnPersonaProblem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var core in file?.Cores ?? [])
        {
            if (Problem(core) is { } wrong)
            {
                refused.Add(new OwnPersonaProblem(core?.Name ?? core?.Id ?? "an entry", wrong));
                continue;
            }

            if (!seen.Add(core!.Id))
            {
                refused.Add(new OwnPersonaProblem(core.Name, "Two cores share this id, so one of them can never be selected."));
                continue;
            }

            if (kept.Count >= MaxCores)
            {
                refused.Add(new OwnPersonaProblem(core.Name, $"There are already {MaxCores} cores, which is the most."));
                continue;
            }

            kept.Add(core);
        }

        lock (_gate)
        {
            _cores = kept;
            _problems = refused;
            _stamp = written;
        }

        logger.LogInformation(
            "Loaded {Count} cores of the Commander's own from {Path} ({Problems} refused)",
            kept.Count,
            path,
            refused.Count);
    }

    private sealed class PersonaFile
    {
        public List<OwnPersona> Cores { get; set; } = [];
    }
}

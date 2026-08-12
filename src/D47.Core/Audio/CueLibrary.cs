using System.Reflection;

namespace D47.Core.Audio;

public sealed class CueSetException(string message) : Exception(message);

/// <summary>
/// Where the shipped set is read from. An interface rather than an <see cref="Assembly"/>
/// directly, so the "a state has no cue" and "a cue has no state" failures can be tested
/// without building a second assembly to be wrong in — and so Phase 12's drop-in folder has
/// somewhere to plug in without reopening this class.
/// </summary>
public interface ICueSource
{
    /// <summary>Resource names, prefixed. What actually shipped — never a list of what should have.</summary>
    IEnumerable<string> Names { get; }

    Stream Open(string name);
}

/// <summary>The real one: the cues embedded in this assembly at build time.</summary>
public sealed class EmbeddedCueSource(Assembly assembly) : ICueSource
{
    public IEnumerable<string> Names => assembly.GetManifestResourceNames();

    public Stream Open(string name) =>
        assembly.GetManifestResourceStream(name)
        ?? throw new CueSetException($"Embedded resource {name} could not be opened.");
}

/// <summary>
/// The shipped cues and beds, read from the embedded resources rather than from a table of
/// names someone maintains by hand (list.md Phase 5, #20).
/// <para>
/// The direction matters. This does not ask "where is the file for <see cref="LoopState.Thinking"/>";
/// it enumerates what actually shipped and then checks that set against the enum. A state
/// added in code with no cue committed alongside it fails at construction with the state
/// named — rather than at runtime, as a cue that never plays and a silence nobody attributes
/// to anything.
/// </para>
/// </summary>
public sealed class CueLibrary
{
    private const string CuePrefix = "D47.Core.Cues.";
    private const string BedPrefix = "D47.Core.Beds.";

    private readonly IReadOnlyDictionary<LoopState, AudioClip> _cues;
    private readonly IReadOnlyDictionary<string, AudioClip> _beds;

    private CueLibrary(
        IReadOnlyDictionary<LoopState, AudioClip> cues,
        IReadOnlyDictionary<string, AudioClip> beds)
    {
        _cues = cues;
        _beds = beds;
    }

    /// <summary>Bed names as shipped. The settings row's choices are these, not a literal list.</summary>
    public IReadOnlyCollection<string> BedNames => (IReadOnlyCollection<string>)_beds.Keys;

    /// <summary>
    /// The bed played while a turn runs (list.md Phase 5, #18). Named rather than positional
    /// so which one is the default survives a second bed being added.
    /// </summary>
    public const string DefaultBed = "thinking-hum";

    public static CueLibrary Load() => Load(new EmbeddedCueSource(typeof(CueLibrary).Assembly));

    public static CueLibrary Load(ICueSource source)
    {
        var cues = new Dictionary<LoopState, AudioClip>();
        var beds = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        var unclaimed = new List<string>();

        foreach (var resource in source.Names)
        {
            if (resource.StartsWith(CuePrefix, StringComparison.Ordinal))
            {
                var stem = resource[CuePrefix.Length..];

                // Enum.TryParse is the whole binding between a filename and a state. Case
                // insensitive because the files are lowercase and the enum is Pascal — that
                // is a spelling difference, not a naming scheme worth encoding twice.
                if (Enum.TryParse<LoopState>(stem, ignoreCase: true, out var state))
                {
                    cues[state] = ReadResource(source, resource, stem);
                }
                else
                {
                    unclaimed.Add(stem);
                }
            }
            else if (resource.StartsWith(BedPrefix, StringComparison.Ordinal))
            {
                var stem = resource[BedPrefix.Length..];
                beds[stem] = ReadResource(source, resource, stem);
            }
        }

        var missing = Enum.GetValues<LoopState>().Where(state => !cues.ContainsKey(state)).ToList();

        if (missing.Count > 0)
        {
            throw new CueSetException(
                $"No cue shipped for {string.Join(", ", missing)}. Add assets/cues/<state>.wav " +
                "and regenerate with tools/gen-cues.py.");
        }

        // A cue named for nothing is the other half of the same mistake — usually a state
        // that was renamed while its file was not. Silence in exactly one state is the
        // symptom, so it is worth failing over rather than ignoring.
        if (unclaimed.Count > 0)
        {
            throw new CueSetException(
                $"Shipped cues match no loop state: {string.Join(", ", unclaimed)}.");
        }

        if (beds.Count == 0 || !beds.ContainsKey(DefaultBed))
        {
            throw new CueSetException($"No bed named {DefaultBed} shipped.");
        }

        return new CueLibrary(cues, beds);
    }

    public AudioClip For(LoopState state) => _cues[state];

    public AudioClip Bed(string? name) =>
        name is not null && _beds.TryGetValue(name, out var clip) ? clip : _beds[DefaultBed];

    private static AudioClip ReadResource(ICueSource source, string resource, string name)
    {
        using var stream = source.Open(resource);

        // Copied because WavReader seeks, and a manifest resource stream is seekable but the
        // clip outlives the stream either way.
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;

        var clip = WavReader.Read(buffer, name);

        if (clip.Format != AudioFormat.Standard)
        {
            throw new CueSetException(
                $"{name} is {clip.Format.SampleRate} Hz / {clip.Format.Channels}ch; " +
                $"the shipped set must be {AudioFormat.Standard.SampleRate} Hz mono.");
        }

        return clip;
    }
}

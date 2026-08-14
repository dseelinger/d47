using System.Reflection;
using Microsoft.Extensions.Logging;

namespace D47.Core.Audio;

public sealed class CueSetException(string message) : Exception(message);

/// <summary>
/// Where a set of clips is read from. An interface rather than an <see cref="Assembly"/>
/// directly, so the "a state has no cue" and "a cue has no state" failures can be tested
/// without building a second assembly to be wrong in — and so Phase 12's drop-in folder has
/// somewhere to plug in without reopening this class.
/// </summary>
public interface ICueSource
{
    /// <summary>Resource names, prefixed. What actually shipped — never a list of what should have.</summary>
    IEnumerable<string> Names { get; }

    Stream Open(string name);

    /// <summary>
    /// Whether a clip that will not load takes the library down with it.
    /// <para>
    /// True for the shipped set, where a cue that does not load is a build error and should stop
    /// the world. False for a Commander's drop-in folder, where it is a file to tell them about.
    /// Defaulted, because every source that existed before Phase 12 is the first kind.
    /// </para>
    /// </summary>
    bool Required => true;
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
/// The cues, beds and ambience d47 can play: what it ships with, plus whatever the Commander
/// has dropped into <c>data/audio/</c> (list.md Phase 5 #20, Phase 12 "Custom Sound Cues").
/// <para>
/// The direction matters. This does not ask "where is the file for <see cref="LoopState.Thinking"/>";
/// it enumerates what actually shipped and then checks that set against the enum. A state
/// added in code with no cue committed alongside it fails at construction with the state
/// named — rather than at runtime, as a cue that never plays and a silence nobody attributes
/// to anything.
/// </para>
/// <para>
/// Sources are merged in order and later ones win by name, which is what makes a drop-in an
/// override rather than a second list. Which source each clip came from is kept rather than
/// discarded: a settings row can then label a Commander's own bed as theirs, and the
/// diagnostics can say how many were picked up — both of which are the difference between a
/// drop-in folder that works and one you have to test by ear.
/// </para>
/// </summary>
public sealed class CueLibrary
{
    internal const string CuePrefix = "D47.Core.Cues.";
    internal const string BedPrefix = "D47.Core.Beds.";
    internal const string MusicPrefix = "D47.Core.Music.";

    private readonly IReadOnlyDictionary<LoopState, AudioClip> _cues;
    private readonly IReadOnlyDictionary<string, AudioClip> _beds;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<AudioClip>> _music;
    private readonly IReadOnlySet<string> _custom;

    private CueLibrary(
        IReadOnlyDictionary<LoopState, AudioClip> cues,
        IReadOnlyDictionary<string, AudioClip> beds,
        IReadOnlyDictionary<string, IReadOnlyList<AudioClip>> music,
        IReadOnlySet<string> custom,
        IReadOnlyList<string> skipped)
    {
        _cues = cues;
        _beds = beds;
        _music = music;
        _custom = custom;
        Skipped = skipped;
    }

    /// <summary>Bed names, shipped and dropped in. The settings row's choices are these.</summary>
    public IReadOnlyCollection<string> BedNames => (IReadOnlyCollection<string>)_beds.Keys;

    /// <summary>
    /// Drop-in files that would not load, each with the reason, in words a Commander can act on.
    /// Empty on a normal run. Reported rather than thrown: a Commander's file failing is a file
    /// to tell them about, not a reason d47 does not start.
    /// </summary>
    public IReadOnlyList<string> Skipped { get; }

    /// <summary>How many clips came from the Commander's folder rather than from the build.</summary>
    public int CustomCount => _custom.Count;

    /// <summary>
    /// Whether this clip is the Commander's own — a bed name, or a loop state's name for a cue
    /// they have overridden. Both live in one namespace of stems, which is also how the folder
    /// is laid out.
    /// </summary>
    public bool IsCustom(string name) => _custom.Contains(name);

    /// <summary>
    /// The bed played while a turn runs (list.md Phase 5, #18). Named rather than positional
    /// so which one is the default survives a second bed being added.
    /// </summary>
    public const string DefaultBed = "thinking-hum";

    public static CueLibrary Load() => Load(new EmbeddedCueSource(typeof(CueLibrary).Assembly));

    /// <summary>One source, which is the shipped set and every test that is about it.</summary>
    public static CueLibrary Load(ICueSource source) => Load(logger: null, source);

    /// <summary>
    /// Loads from every source in order, later sources winning by name.
    /// </summary>
    /// <param name="logger">
    /// Where a skipped drop-in is reported. Optional, because the reasons are on
    /// <see cref="Skipped"/> either way and a test does not need a logger to read them.
    /// </param>
    public static CueLibrary Load(ILogger? logger, params ICueSource[] sources)
    {
        var cues = new Dictionary<LoopState, AudioClip>();
        var beds = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        var music = new Dictionary<string, List<AudioClip>>(StringComparer.OrdinalIgnoreCase);
        var custom = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skipped = new List<string>();
        var unclaimed = new List<string>();

        foreach (var source in sources)
        {
            foreach (var resource in source.Names)
            {
                if (resource.StartsWith(CuePrefix, StringComparison.Ordinal))
                {
                    var stem = resource[CuePrefix.Length..];

                    // Enum.TryParse is the whole binding between a filename and a state. Case
                    // insensitive because the files are lowercase and the enum is Pascal — that
                    // is a spelling difference, not a naming scheme worth encoding twice.
                    if (!Enum.TryParse<LoopState>(stem, ignoreCase: true, out var state))
                    {
                        // A shipped cue named for nothing is usually a state that was renamed
                        // while its file was not, and it fails the build. The same file dropped
                        // into data/audio/cues is a Commander who guessed a name, and it is
                        // reported rather than fatal.
                        if (source.Required)
                        {
                            unclaimed.Add(stem);
                        }
                        else
                        {
                            skipped.Add(
                                $"cues/{stem}.wav matches no loop state — expected one of "
                                + $"{string.Join(", ", Enum.GetNames<LoopState>().Select(name => name.ToLowerInvariant()))}.");
                        }

                        continue;
                    }

                    if (TryRead(source, resource, stem, skipped) is { } cue)
                    {
                        cues[state] = cue;
                        Claim(custom, stem, source);
                    }
                }
                else if (resource.StartsWith(BedPrefix, StringComparison.Ordinal))
                {
                    var stem = resource[BedPrefix.Length..];

                    if (TryRead(source, resource, stem, skipped) is { } bed)
                    {
                        beds[stem] = bed;
                        Claim(custom, stem, source);
                    }
                }
                else if (resource.StartsWith(MusicPrefix, StringComparison.Ordinal))
                {
                    // "<situation>.<track>". Split at the first dot only, because a track name is
                    // whatever the Commander called their file and may well hold more of them.
                    var rest = resource[MusicPrefix.Length..];
                    var dot = rest.IndexOf('.', StringComparison.Ordinal);

                    if (dot <= 0)
                    {
                        continue;
                    }

                    var situation = rest[..dot];
                    var stem = rest[(dot + 1)..];

                    if (!Situations.All.Contains(situation, StringComparer.OrdinalIgnoreCase))
                    {
                        skipped.Add(
                            $"music/{situation}/{stem}.wav is in no situation D47 knows — expected one of "
                            + $"{string.Join(", ", Situations.All)}.");
                        continue;
                    }

                    if (TryRead(source, resource, stem, skipped) is { } track)
                    {
                        if (!music.TryGetValue(situation, out var tracks))
                        {
                            music[situation] = tracks = [];
                        }

                        tracks.Add(track);
                        Claim(custom, $"{situation}/{stem}", source);
                    }
                }
            }
        }

        var missing = Enum.GetValues<LoopState>().Where(state => !cues.ContainsKey(state)).ToList();

        if (missing.Count > 0)
        {
            throw new CueSetException(
                $"No cue shipped for {string.Join(", ", missing)}. Add assets/cues/<state>.wav " +
                "and regenerate with tools/gen-cues.py.");
        }

        if (unclaimed.Count > 0)
        {
            throw new CueSetException(
                $"Shipped cues match no loop state: {string.Join(", ", unclaimed)}.");
        }

        if (beds.Count == 0 || !beds.ContainsKey(DefaultBed))
        {
            throw new CueSetException($"No bed named {DefaultBed} shipped.");
        }

        foreach (var reason in skipped)
        {
            logger?.LogWarning("Skipped a drop-in audio file: {Reason}", reason);
        }

        return new CueLibrary(
            cues,
            beds,
            music.ToDictionary(
                entry => entry.Key,
                entry => (IReadOnlyList<AudioClip>)entry.Value,
                StringComparer.OrdinalIgnoreCase),
            custom,
            skipped);
    }

    /// <summary>
    /// What came out of the Commander's own folder, and what would not load, in words for a
    /// settings row. A skipped file is silent in exactly the way a missing one is, so without
    /// this the only symptom of a wrong sample rate is a cue that never plays.
    /// </summary>
    public string DescribeDrops()
    {
        var picked = CustomCount switch
        {
            0 => "Nothing dropped in yet.",
            1 => "1 file picked up from data/audio.",
            _ => $"{CustomCount} files picked up from data/audio.",
        };

        return Skipped.Count == 0
            ? picked
            : $"{picked}{Environment.NewLine}Skipped: {string.Join($"{Environment.NewLine}Skipped: ", Skipped)}";
    }

    public AudioClip For(LoopState state) => _cues[state];

    /// <summary>
    /// The ambience tracks for one situation, in the order the folder was read. Empty is the
    /// normal answer: d47 ships with no music at all, so every Commander who has not dropped any
    /// in is here, and empty means quiet rather than broken.
    /// </summary>
    public IReadOnlyList<AudioClip> Music(string situation) =>
        _music.TryGetValue(situation, out var tracks) ? tracks : [];

    /// <summary>Every situation that actually has something in it. The diagnostics row reads this.</summary>
    public IReadOnlyCollection<string> MusicSituations => (IReadOnlyCollection<string>)_music.Keys;

    public AudioClip Bed(string? name) =>
        name is not null && _beds.TryGetValue(name, out var clip) ? clip : _beds[DefaultBed];

    /// <summary>
    /// A clip from a source that is allowed to fail, or null with the reason recorded. The
    /// shipped set still throws: a build that cannot load its own cues is broken, and finding
    /// that out at startup is finding it out too late.
    /// </summary>
    private static AudioClip? TryRead(ICueSource source, string resource, string name, List<string> skipped)
    {
        if (source.Required)
        {
            return ReadResource(source, resource, name);
        }

        try
        {
            return ReadResource(source, resource, name);
        }
        catch (Exception ex) when (ex is CueSetException or IOException or UnauthorizedAccessException)
        {
            skipped.Add($"{name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Records that a clip came from somewhere other than the build — or that it no longer does,
    /// which is what a shipped source listed after a drop-in would mean.
    /// </summary>
    private static void Claim(HashSet<string> custom, string name, ICueSource source)
    {
        if (source.Required)
        {
            custom.Remove(name);
        }
        else
        {
            custom.Add(name);
        }
    }

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
                $"it is {clip.Format.SampleRate} Hz / {clip.Format.Channels}ch; " +
                $"audio must be {AudioFormat.Standard.SampleRate} Hz mono 16-bit.");
        }

        return clip;
    }
}

using System.Reflection;
using Microsoft.Extensions.Logging;

namespace D47.Core.Audio;

public sealed class CueSetException(string message) : Exception(message);

/// <summary>Where a set of clips is read from.</summary>
public interface ICueSource
{
    /// <summary>Resource names, prefixed.</summary>
    IEnumerable<string> Names { get; }

    Stream Open(string name);

    /// <summary>Whether a clip that will not load takes the library down with it.</summary>
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
/// The cues, beds and ambience d47 can play: what it ships with, plus whatever the Commander has
/// dropped into <c>data/audio/</c> (Phase 5 #20, Phase 12 "Custom Sound Cues").
/// </summary>
public sealed class CueLibrary
{
    internal const string CuePrefix = "D47.Core.Cues.";
    internal const string AlertPrefix = "D47.Core.Alerts.";
    internal const string BedPrefix = "D47.Core.Beds.";
    internal const string MusicPrefix = "D47.Core.Music.";

    private readonly IReadOnlyDictionary<LoopState, AudioClip> _cues;
    private readonly IReadOnlyDictionary<AlertCue, AudioClip> _alerts;
    private readonly IReadOnlyDictionary<string, AudioClip> _beds;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<AudioClip>> _music;
    private readonly IReadOnlySet<string> _custom;

    private CueLibrary(
        IReadOnlyDictionary<LoopState, AudioClip> cues,
        IReadOnlyDictionary<AlertCue, AudioClip> alerts,
        IReadOnlyDictionary<string, AudioClip> beds,
        IReadOnlyDictionary<string, IReadOnlyList<AudioClip>> music,
        IReadOnlySet<string> custom,
        IReadOnlyList<string> skipped)
    {
        _cues = cues;
        _alerts = alerts;
        _beds = beds;
        _music = music;
        _custom = custom;
        Skipped = skipped;
    }

    /// <summary>Bed names, shipped and dropped in.</summary>
    public IReadOnlyCollection<string> BedNames => (IReadOnlyCollection<string>)_beds.Keys;

    /// <summary>
    /// Drop-in files that would not load, each with the reason, in words a Commander can act on.
    /// </summary>
    public IReadOnlyList<string> Skipped { get; }

    /// <summary>How many clips came from the Commander's folder rather than from the build.</summary>
    public int CustomCount => _custom.Count;

    /// <summary>
    /// Whether this clip is the Commander's own — a bed name, or a loop state's name for a cue they
    /// have overridden.
    /// </summary>
    public bool IsCustom(string name) => _custom.Contains(name);

    /// <summary>The bed played while a turn runs (Phase 5, #18).</summary>
    public const string DefaultBed = "thinking-hum";

    public static CueLibrary Load() => Load(new EmbeddedCueSource(typeof(CueLibrary).Assembly));

    /// <summary>One source, which is the shipped set and every test that is about it.</summary>
    public static CueLibrary Load(ICueSource source) => Load(logger: null, source);

    /// <summary>Loads from every source in order, later sources winning by name.</summary>
    /// <param name="logger">Where a skipped drop-in is reported.</param>
    public static CueLibrary Load(ILogger? logger, params ICueSource[] sources)
    {
        var cues = new Dictionary<LoopState, AudioClip>();
        var alerts = new Dictionary<AlertCue, AudioClip>();
        var beds = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        var music = new Dictionary<string, List<AudioClip>>(StringComparer.OrdinalIgnoreCase);
        var custom = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skipped = new List<string>();
        var unclaimed = new List<string>();

        foreach (var source in sources)
        {
            // Two families are keyed by an enum — loop states and alerts — and the rule binding a filename to
            // a member is the same for both, so it is written once.
            void ReadInto<TKey>(Dictionary<TKey, AudioClip> into, string resource, string prefix, string folder, string subject)
                where TKey : struct, Enum
            {
                var stem = resource[prefix.Length..];

                if (!Enum.TryParse<TKey>(stem, ignoreCase: true, out var key))
                {
                    // A shipped clip named for nothing is usually a member that was renamed while its file
                    // was not, and it fails the build.
                    if (source.Required)
                    {
                        unclaimed.Add($"{folder}/{stem}");
                    }
                    else
                    {
                        skipped.Add(
                            $"{folder}/{stem}.wav matches no {subject} — expected one of "
                            + $"{string.Join(", ", Enum.GetNames<TKey>().Select(name => name.ToLowerInvariant()))}.");
                    }

                    return;
                }

                if (TryRead(source, resource, stem, skipped) is { } clip)
                {
                    into[key] = clip;
                    Claim(custom, stem, source);
                }
            }

            foreach (var resource in source.Names)
            {
                if (resource.StartsWith(CuePrefix, StringComparison.Ordinal))
                {
                    ReadInto(cues, resource, CuePrefix, "cues", "loop state");
                }
                else if (resource.StartsWith(AlertPrefix, StringComparison.Ordinal))
                {
                    ReadInto(alerts, resource, AlertPrefix, "alerts", "alert");
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
                    // "<situation>.<track>".
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

        // The same rule for alerts, and it matters more: a loop state with no cue is a turn that sounds
        // wrong, while an alert with no cue is a warning that arrives with nothing to mark it — and a warning
        // that did not fire sounds exactly like one whose cue would not load.
        var silent = Enum.GetValues<AlertCue>().Where(alert => !alerts.ContainsKey(alert)).ToList();

        if (silent.Count > 0)
        {
            throw new CueSetException(
                $"No alert cue shipped for {string.Join(", ", silent)}. Add assets/alerts/<alert>.wav " +
                "and regenerate with tools/gen-cues.py.");
        }

        if (unclaimed.Count > 0)
        {
            throw new CueSetException(
                $"Shipped clips match no loop state or alert: {string.Join(", ", unclaimed)}.");
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
            alerts,
            beds,
            music.ToDictionary(
                entry => entry.Key,
                entry => (IReadOnlyList<AudioClip>)entry.Value,
                StringComparer.OrdinalIgnoreCase),
            custom,
            skipped);
    }

    /// <summary>
    /// What came out of the Commander's own folder, and what would not load, in words for a settings
    /// row.
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

    /// <summary>The marker played ahead of one warning (Phase 15).</summary>
    public AudioClip For(AlertCue alert) => _alerts[alert];

    /// <summary>The ambience tracks for one situation, in the order the folder was read.</summary>
    public IReadOnlyList<AudioClip> Music(string situation) =>
        _music.TryGetValue(situation, out var tracks) ? tracks : [];

    /// <summary>Every situation that actually has something in it.</summary>
    public IReadOnlyCollection<string> MusicSituations => (IReadOnlyCollection<string>)_music.Keys;

    public AudioClip Bed(string? name) =>
        name is not null && _beds.TryGetValue(name, out var clip) ? clip : _beds[DefaultBed];

    /// <summary>A clip from a source that is allowed to fail, or null with the reason recorded.</summary>
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
    /// Records that a clip came from somewhere other than the build — or that it no longer does, which
    /// is what a shipped source listed after a drop-in would mean.
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

        // Copied because WavReader seeks, and a manifest resource stream is seekable but the clip outlives
        // the stream either way.
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

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

    /// <summary>The whole clip in <see cref="AudioFormat.Standard"/>.</summary>
    AudioClip Decode(string name, string clipName)
    {
        using var stream = Open(name);

        // Copied because WavReader seeks, and a manifest resource stream is seekable but the clip outlives
        // the stream either way.
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;

        return WavReader.ReadStandard(buffer, clipName);
    }

    /// <summary>The clip in <see cref="AudioFormat.Standard"/>, read on demand.</summary>
    IPcmStream Stream(string name, string clipName) => WavReader.OpenStandard(Open(name), clipName);
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
/// The cues, bed and ambience d47 can play: what it ships with, plus whatever the Commander has
/// dropped into <c>data/audio/</c>. A cue, alert or bed with files of the Commander's plays one of
/// those, picked at call time; one with none plays the shipped sound.
/// </summary>
/// <remarks>
/// Shipped clips are named <c>&lt;prefix&gt;&lt;member&gt;</c>. Pool clips are named
/// <c>&lt;prefix&gt;&lt;folder&gt;.&lt;file&gt;</c> for cues and alerts, and
/// <c>&lt;prefix&gt;&lt;file&gt;</c> for beds from a source that is not <see cref="ICueSource.Required"/>.
/// </remarks>
public sealed class CueLibrary
{
    internal const string CuePrefix = "D47.Core.Cues.";
    internal const string AlertPrefix = "D47.Core.Alerts.";
    internal const string BedPrefix = "D47.Core.Beds.";
    internal const string MusicPrefix = "D47.Core.Music.";

    /// <summary>The shipped bed, played while a turn runs when <c>beds/</c> is empty.</summary>
    public const string DefaultBed = "thinking-hum";

    private readonly IReadOnlyDictionary<LoopState, AudioClip> _cues;
    private readonly IReadOnlyDictionary<AlertCue, AudioClip> _alerts;
    private readonly AudioClip _bed;
    private readonly IReadOnlyDictionary<LoopState, ClipPool> _cuePools;
    private readonly IReadOnlyDictionary<AlertCue, ClipPool> _alertPools;
    private readonly ClipPool? _bedPool;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<MusicTrack>> _music;

    private CueLibrary(
        IReadOnlyDictionary<LoopState, AudioClip> cues,
        IReadOnlyDictionary<AlertCue, AudioClip> alerts,
        AudioClip bed,
        IReadOnlyDictionary<LoopState, ClipPool> cuePools,
        IReadOnlyDictionary<AlertCue, ClipPool> alertPools,
        ClipPool? bedPool,
        IReadOnlyDictionary<string, IReadOnlyList<MusicTrack>> music,
        int customCount,
        IReadOnlyList<string> skipped)
    {
        _cues = cues;
        _alerts = alerts;
        _bed = bed;
        _cuePools = cuePools;
        _alertPools = alertPools;
        _bedPool = bedPool;
        _music = music;
        CustomCount = customCount;
        Skipped = skipped;
    }

    /// <summary>
    /// Drop-in files that would not load, each with the reason, in words a Commander can act on.
    /// </summary>
    public IReadOnlyList<string> Skipped { get; }

    /// <summary>How many clips came from the Commander's folder rather than from the build.</summary>
    public int CustomCount { get; }

    /// <summary>The folder under <c>cues/</c> or <c>alerts/</c> that holds one member's files: lower kebab case.</summary>
    public static string FolderName<TKey>(TKey member)
        where TKey : struct, Enum
    {
        var name = member.ToString();
        var folder = new System.Text.StringBuilder(name.Length + 4);

        for (var index = 0; index < name.Length; index++)
        {
            if (index > 0 && char.IsUpper(name[index]))
            {
                folder.Append('-');
            }

            folder.Append(char.ToLowerInvariant(name[index]));
        }

        return folder.ToString();
    }

    public static CueLibrary Load() => Load(new EmbeddedCueSource(typeof(CueLibrary).Assembly));

    /// <summary>One source, which is the shipped set and every test that is about it.</summary>
    public static CueLibrary Load(ICueSource source) => Load(logger: null, source);

    /// <summary>Loads from every source in order; a later shipped clip replaces an earlier one.</summary>
    /// <param name="logger">Where a skipped drop-in is reported.</param>
    public static CueLibrary Load(ILogger? logger, params ICueSource[] sources) => Load(logger, random: null, sources);

    /// <param name="logger">Where a skipped drop-in is reported.</param>
    /// <param name="random">Shuffles the pools; <see cref="Random.Shared"/> when null.</param>
    public static CueLibrary Load(ILogger? logger, Random? random, params ICueSource[] sources)
    {
        var cues = new Dictionary<LoopState, AudioClip>();
        var alerts = new Dictionary<AlertCue, AudioClip>();
        AudioClip? bed = null;
        var cuePools = new Dictionary<LoopState, List<AudioClip>>();
        var alertPools = new Dictionary<AlertCue, List<AudioClip>>();
        var bedPool = new List<AudioClip>();
        var music = new Dictionary<string, List<MusicTrack>>(StringComparer.OrdinalIgnoreCase);
        var custom = 0;
        var skipped = new List<string>();
        var unclaimed = new List<string>();

        foreach (var source in sources)
        {
            // Loop states and alerts are both keyed by an enum, with the same rule binding a name to a member.
            void ReadInto<TKey>(
                Dictionary<TKey, AudioClip> shipped,
                Dictionary<TKey, List<AudioClip>> pools,
                string resource,
                string prefix,
                string folder,
                string subject)
                where TKey : struct, Enum
            {
                var rest = resource[prefix.Length..];
                var dot = rest.IndexOf('.', StringComparison.Ordinal);
                var member = dot > 0 ? rest[..dot] : rest;
                var stem = dot > 0 ? rest[(dot + 1)..] : rest;

                if (Member<TKey>(member) is not { } key)
                {
                    // A shipped clip named for nothing is usually a member that was renamed while its file
                    // was not, and it fails the build.
                    if (source.Required)
                    {
                        unclaimed.Add($"{folder}/{rest}");
                    }
                    else
                    {
                        skipped.Add(
                            $"{folder}/{member}/{stem} is in no {subject} folder — expected one of "
                            + $"{string.Join(", ", Enum.GetValues<TKey>().Select(FolderName))}.");
                    }

                    return;
                }

                if (TryRead(source, resource, stem, skipped) is not { } clip)
                {
                    return;
                }

                if (dot > 0)
                {
                    if (!pools.TryGetValue(key, out var pool))
                    {
                        pools[key] = pool = [];
                    }

                    pool.Add(clip);
                    custom++;
                }
                else
                {
                    shipped[key] = clip;
                }
            }

            foreach (var resource in source.Names)
            {
                if (resource.StartsWith(CuePrefix, StringComparison.Ordinal))
                {
                    ReadInto(cues, cuePools, resource, CuePrefix, "cues", "loop state");
                }
                else if (resource.StartsWith(AlertPrefix, StringComparison.Ordinal))
                {
                    ReadInto(alerts, alertPools, resource, AlertPrefix, "alerts", "alert");
                }
                else if (resource.StartsWith(BedPrefix, StringComparison.Ordinal))
                {
                    var stem = resource[BedPrefix.Length..];

                    if (source.Required && !stem.Equals(DefaultBed, StringComparison.OrdinalIgnoreCase))
                    {
                        unclaimed.Add($"beds/{stem}");
                    }
                    else if (TryRead(source, resource, stem, skipped) is { } clip)
                    {
                        if (source.Required)
                        {
                            bed = clip;
                        }
                        else
                        {
                            bedPool.Add(clip);
                            custom++;
                        }
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
                            $"music/{situation}/{stem} is in no situation D47 knows — expected one of "
                            + $"{string.Join(", ", Situations.All)}.");
                        continue;
                    }

                    if (TryReference(source, resource, stem, skipped) is { } track)
                    {
                        if (!music.TryGetValue(situation, out var tracks))
                        {
                            music[situation] = tracks = [];
                        }

                        tracks.Add(track);

                        if (!source.Required)
                        {
                            custom++;
                        }
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

        // An alert with no cue is a warning that arrives with nothing to mark it, which sounds exactly like
        // a warning that did not fire.
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
                $"Shipped clips match no loop state, alert or bed: {string.Join(", ", unclaimed)}.");
        }

        if (bed is null)
        {
            throw new CueSetException($"No bed named {DefaultBed} shipped.");
        }

        foreach (var reason in skipped)
        {
            logger?.LogWarning("Skipped a drop-in audio file: {Reason}", reason);
        }

        var shuffle = random ?? Random.Shared;

        return new CueLibrary(
            cues,
            alerts,
            bed,
            cuePools.ToDictionary(entry => entry.Key, entry => new ClipPool(entry.Value, shuffle)),
            alertPools.ToDictionary(entry => entry.Key, entry => new ClipPool(entry.Value, shuffle)),
            bedPool.Count > 0 ? new ClipPool(bedPool, shuffle) : null,
            music.ToDictionary(
                entry => entry.Key,
                entry => (IReadOnlyList<MusicTrack>)entry.Value,
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

    /// <summary>The cue for one loop state, picked from the Commander's pool on each call.</summary>
    public AudioClip For(LoopState state) =>
        _cuePools.TryGetValue(state, out var pool) ? pool.Next() : _cues[state];

    /// <summary>The marker played ahead of one warning, picked from the Commander's pool on each call.</summary>
    public AudioClip For(AlertCue alert) =>
        _alertPools.TryGetValue(alert, out var pool) ? pool.Next() : _alerts[alert];

    /// <summary>The ambience tracks for one situation, in the order the folder was read.</summary>
    public IReadOnlyList<MusicTrack> Music(string situation) =>
        _music.TryGetValue(situation, out var tracks) ? tracks : [];

    /// <summary>Every situation that actually has something in it.</summary>
    public IReadOnlyCollection<string> MusicSituations => (IReadOnlyCollection<string>)_music.Keys;

    /// <summary>The bed for one turn, picked from the Commander's pool on each call.</summary>
    public AudioClip Bed() => _bedPool?.Next() ?? _bed;

    /// <summary>The member a folder or shipped file names: hyphens ignored, case ignored.</summary>
    private static TKey? Member<TKey>(string name)
        where TKey : struct, Enum
    {
        var bare = name.Replace("-", "", StringComparison.Ordinal);

        foreach (var member in Enum.GetValues<TKey>())
        {
            if (member.ToString().Equals(bare, StringComparison.OrdinalIgnoreCase))
            {
                return member;
            }
        }

        return null;
    }

    /// <summary>A clip from a source that is allowed to fail, or null with the reason recorded.</summary>
    private static AudioClip? TryRead(ICueSource source, string resource, string name, List<string> skipped)
    {
        if (source.Required)
        {
            return source.Decode(resource, name);
        }

        try
        {
            return source.Decode(resource, name);
        }
        catch (Exception ex) when (IsSkippable(ex))
        {
            skipped.Add($"{name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// A track whose header was checked and whose samples are left on disk, or null with the reason
    /// recorded.
    /// </summary>
    private static MusicTrack? TryReference(ICueSource source, string resource, string name, List<string> skipped)
    {
        try
        {
            source.Stream(resource, name).Dispose();
        }
        catch (Exception ex) when (!source.Required && IsSkippable(ex))
        {
            skipped.Add($"{name}: {ex.Message}");
            return null;
        }

        return new MusicTrack(name, () => source.Stream(resource, name));
    }

    private static bool IsSkippable(Exception ex) =>
        ex is CueSetException or WavFormatException or AudioDecodeException or IOException or UnauthorizedAccessException;
}

/// <summary>
/// Clips dealt in shuffled order: every clip once before any repeats, and never the same clip twice
/// in a row across a reshuffle. Safe to call from any thread.
/// </summary>
internal sealed class ClipPool(IReadOnlyList<AudioClip> clips, Random random)
{
    private readonly Lock _gate = new();
    private readonly Queue<int> _order = new();
    private int _last = -1;

    public AudioClip Next()
    {
        lock (_gate)
        {
            if (_order.Count == 0)
            {
                Deal();
            }

            _last = _order.Dequeue();
            return clips[_last];
        }
    }

    private void Deal()
    {
        var order = Enumerable.Range(0, clips.Count).ToArray();

        lock (random)
        {
            random.Shuffle(order);
        }

        if (order.Length > 1 && order[0] == _last)
        {
            (order[0], order[^1]) = (order[^1], order[0]);
        }

        foreach (var index in order)
        {
            _order.Enqueue(index);
        }
    }
}

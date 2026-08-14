using Microsoft.Extensions.Logging;

namespace D47.Core.Audio;

/// <summary>
/// The Commander's own audio, from a convention folder beside the executable
/// (list.md Phase 12, "Custom Sound Cues").
/// <para>
/// <c>ICueSource</c> was written for this — "so Phase 12's drop-in folder has somewhere to plug
/// in without reopening this class" — so a drop-in is a second source rather than a second code
/// path. The layout under <c>data/audio/</c> is:
/// </para>
/// <code>
/// cues/&lt;loop-state&gt;.wav      overrides a shipped cue
/// beds/&lt;name&gt;.wav            adds a thinking bed
/// music/&lt;situation&gt;/*.wav    ambience for docked, supercruise, normal-space, on-foot, general
/// </code>
/// <para>
/// The folders are created at startup whether or not anything is in them, so the convention is
/// discoverable by opening the data folder rather than by reading the documentation.
/// </para>
/// <para>
/// Owns no thread and reads no clock. Like the journal reader it exposes <see cref="Poll"/> and
/// the tick loop calls it; a test calls it directly (architecture.md §8).
/// </para>
/// </summary>
public sealed class FolderAudioSource : ICueSource
{
    public const string CuesFolder = "cues";
    public const string BedsFolder = "beds";
    public const string MusicFolder = "music";

    private readonly string _root;
    private readonly ILogger _logger;

    /// <summary>Prefixed name to the file behind it. Rebuilt whole on every scan.</summary>
    private Dictionary<string, string> _files = new(StringComparer.Ordinal);

    /// <summary>Path and last-write time, which is what a rescan compares against.</summary>
    private Dictionary<string, DateTime> _stamps = new(StringComparer.OrdinalIgnoreCase);

    public FolderAudioSource(string root, ILogger logger)
    {
        _root = root;
        _logger = logger;
        Scan();
    }

    /// <summary>
    /// A Commander's file that will not load is a file to tell them about, not a reason d47 does
    /// not start. The shipped set is the opposite, and that difference is the whole reason this
    /// is a property of the source.
    /// </summary>
    public bool Required => false;

    public IEnumerable<string> Names => _files.Keys;

    /// <summary>How many rebuilds this has done. A test asserts an unchanged folder does none.</summary>
    public int Rebuilds { get; private set; }

    public Stream Open(string name) =>
        _files.TryGetValue(name, out var path)
            ? File.OpenRead(path)
            : throw new CueSetException($"{name} is no longer in {_root}.");

    /// <summary>
    /// Re-reads the folder if anything in it has changed, and says whether it did
    /// (list.md Phase 12, "Pick up dropped-in audio without a restart").
    /// <para>
    /// Compares names and write times against what was loaded, so an unchanged folder costs one
    /// directory enumeration and nothing else. The tick loop calls this; nothing here owns a
    /// thread or a watcher.
    /// </para>
    /// </summary>
    public bool Poll()
    {
        var found = Stamps();

        if (Same(found, _stamps))
        {
            return false;
        }

        Scan(found);
        Rebuilds++;

        return true;
    }

    private static bool Same(
        IReadOnlyDictionary<string, DateTime> left,
        IReadOnlyDictionary<string, DateTime> right) =>
        left.Count == right.Count
        && left.All(entry => right.TryGetValue(entry.Key, out var when) && when == entry.Value);

    private void Scan(Dictionary<string, DateTime>? found = null)
    {
        _stamps = found ?? Stamps();

        var files = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var path in _stamps.Keys)
        {
            if (NameOf(path) is { } name)
            {
                // Last one wins, and the enumeration is sorted, so two files that resolve to the
                // same name resolve deterministically rather than by whatever order the file
                // system happened to answer in.
                files[name] = path;
            }
        }

        _files = files;
    }

    /// <summary>
    /// Every wav under the three folders, with its write time. Missing folders are not an error:
    /// a Commander who has dropped nothing in has nothing to report.
    /// </summary>
    private Dictionary<string, DateTime> Stamps()
    {
        var stamps = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        foreach (var folder in new[] { CuesFolder, BedsFolder, MusicFolder })
        {
            var directory = Path.Combine(_root, folder);

            if (!Directory.Exists(directory))
            {
                continue;
            }

            try
            {
                foreach (var path in Directory.EnumerateFiles(directory, "*.wav", SearchOption.AllDirectories)
                             .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    stamps[path] = File.GetLastWriteTimeUtc(path);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A folder that cannot be read is a folder with nothing in it, as far as this is
                // concerned. It is the Commander's own directory and d47 must not fail over it.
                _logger.LogWarning(ex, "Could not read {Folder}", directory);
            }
        }

        return stamps;
    }

    /// <summary>
    /// The prefixed name a file answers to, in the same shape the embedded source uses — which
    /// is what lets <see cref="CueLibrary"/> merge the two without knowing which is which.
    /// </summary>
    private string? NameOf(string path)
    {
        var relative = Path.GetRelativePath(_root, path).Replace('\\', '/');
        var stem = Path.GetFileNameWithoutExtension(path);

        if (relative.StartsWith($"{CuesFolder}/", StringComparison.OrdinalIgnoreCase))
        {
            return CueLibrary.CuePrefix + stem;
        }

        if (relative.StartsWith($"{BedsFolder}/", StringComparison.OrdinalIgnoreCase))
        {
            return CueLibrary.BedPrefix + stem;
        }

        if (!relative.StartsWith($"{MusicFolder}/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // music/<situation>/<file>.wav. A file loose in music/ with no situation folder has not
        // said when it should play, so it is left alone rather than guessed at.
        var parts = relative.Split('/');

        return parts.Length >= 3 ? $"{CueLibrary.MusicPrefix}{parts[1]}.{stem}" : null;
    }
}

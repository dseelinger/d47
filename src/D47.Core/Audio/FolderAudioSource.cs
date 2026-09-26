using Microsoft.Extensions.Logging;

namespace D47.Core.Audio;

/// <summary>
/// The Commander's own audio, from a convention folder beside the executable (Phase 12, "Custom Sound
/// Cues").
/// </summary>
public sealed class FolderAudioSource : ICueSource
{
    public const string CuesFolder = "cues";
    public const string BedsFolder = "beds";
    public const string MusicFolder = "music";

    private readonly string _root;
    private readonly ILogger _logger;

    /// <summary>Prefixed name to the file behind it, as found when this was built.</summary>
    private readonly Dictionary<string, string> _files;

    public FolderAudioSource(string root, ILogger logger)
    {
        _root = root;
        _logger = logger;
        _files = Scan();
    }

    /// <summary>
    /// A Commander's file that will not load is a file to tell them about, not a reason d47 does not
    /// start.
    /// </summary>
    public bool Required => false;

    public IEnumerable<string> Names => _files.Keys;

    public Stream Open(string name) =>
        _files.TryGetValue(name, out var path)
            ? File.OpenRead(path)
            : throw new CueSetException($"{name} is no longer in {_root}.");

    private Dictionary<string, string> Scan()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var path in Wavs())
        {
            if (NameOf(path) is { } name)
            {
                // Last one wins, and the enumeration is sorted, so two files that resolve to the same name
                // resolve deterministically rather than by whatever order the file system happened to answer
                // in.
                files[name] = path;
            }
        }

        return files;
    }

    /// <summary>Every wav under the three folders, sorted.</summary>
    private List<string> Wavs()
    {
        var paths = new List<string>();

        foreach (var folder in new[] { CuesFolder, BedsFolder, MusicFolder })
        {
            var directory = Path.Combine(_root, folder);

            if (!Directory.Exists(directory))
            {
                continue;
            }

            try
            {
                paths.AddRange(
                    Directory.EnumerateFiles(directory, "*.wav", SearchOption.AllDirectories)
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A folder that cannot be read is a folder with nothing in it, as far as this is concerned.
                _logger.LogWarning(ex, "Could not read {Folder}", directory);
            }
        }

        return paths;
    }

    /// <summary>
    /// The prefixed name a file answers to, in the same shape the embedded source uses — which is what
    /// lets <see cref="CueLibrary"/> merge the two without knowing which is which.
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

        // music/<situation>/<file>.wav.
        var parts = relative.Split('/');

        return parts.Length >= 3 ? $"{CueLibrary.MusicPrefix}{parts[1]}.{stem}" : null;
    }
}

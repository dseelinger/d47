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

    private const string Wav = ".wav";

    private readonly string _root;
    private readonly ILogger _logger;
    private readonly IAudioDecoder? _decoder;

    /// <summary>Prefixed name to the file behind it, as found when this was built.</summary>
    private readonly Dictionary<string, string> _files;

    /// <param name="decoder">
    /// Reads every extension it lists. Without one only <c>.wav</c> is picked up, through
    /// <see cref="WavReader"/>.
    /// </param>
    public FolderAudioSource(string root, ILogger logger, IAudioDecoder? decoder = null)
    {
        _root = root;
        _logger = logger;
        _decoder = decoder;
        _files = Scan();
    }

    /// <summary>
    /// A Commander's file that will not load is a file to tell them about, not a reason d47 does not
    /// start.
    /// </summary>
    public bool Required => false;

    public IEnumerable<string> Names => _files.Keys;

    public Stream Open(string name) => File.OpenRead(PathOf(name));

    /// <summary>
    /// Through the decoder where it reads the extension. A WAV it cannot read falls back to
    /// <see cref="WavReader"/>, which needs no codec.
    /// </summary>
    public AudioClip Decode(string name, string clipName)
    {
        var path = PathOf(name);

        if (!Decodes(path))
        {
            return ReadWav(path, clipName);
        }

        try
        {
            return _decoder!.Decode(path, clipName);
        }
        catch (AudioDecodeException) when (IsWav(path))
        {
            return ReadWav(path, clipName);
        }
    }

    public IPcmStream Stream(string name, string clipName)
    {
        var path = PathOf(name);

        if (!Decodes(path))
        {
            return WavReader.OpenStandard(File.OpenRead(path), clipName);
        }

        try
        {
            return _decoder!.Open(path);
        }
        catch (AudioDecodeException) when (IsWav(path))
        {
            return WavReader.OpenStandard(File.OpenRead(path), clipName);
        }
    }

    private static AudioClip ReadWav(string path, string clipName)
    {
        using var stream = File.OpenRead(path);
        return WavReader.ReadStandard(stream, clipName);
    }

    private static bool IsWav(string path) => Path.GetExtension(path).Equals(Wav, StringComparison.OrdinalIgnoreCase);

    private bool Decodes(string path) =>
        _decoder is not null && _decoder.Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    private string PathOf(string name) =>
        _files.TryGetValue(name, out var path)
            ? path
            : throw new CueSetException($"{name} is no longer in {_root}.");

    private Dictionary<string, string> Scan()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var path in AudioFiles())
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

    /// <summary>Every readable file under the three folders, sorted.</summary>
    private List<string> AudioFiles()
    {
        var paths = new List<string>();
        var extensions = new HashSet<string>(_decoder?.Extensions ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase) { Wav };

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
                    Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                        .Where(path => extensions.Contains(Path.GetExtension(path)))
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

        // music/<situation>/<file>.
        var parts = relative.Split('/');

        return parts.Length >= 3 ? $"{CueLibrary.MusicPrefix}{parts[1]}.{stem}" : null;
    }
}

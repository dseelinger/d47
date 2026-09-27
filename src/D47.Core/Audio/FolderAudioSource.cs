using Microsoft.Extensions.Logging;

namespace D47.Core.Audio;

/// <summary>
/// The Commander's own audio, from a convention folder beside the executable (Phase 12, "Custom Sound
/// Cues").
/// </summary>
public sealed class FolderAudioSource : ICueSource
{
    public const string CuesFolder = "cues";
    public const string AlertsFolder = "alerts";
    public const string BedsFolder = "beds";
    public const string MusicFolder = "music";

    private const string Wav = ".wav";

    private static readonly string[] KnownFolders = [CuesFolder, AlertsFolder, BedsFolder, MusicFolder];

    private readonly string _root;
    private readonly ILogger _logger;
    private readonly IAudioDecoder? _decoder;

    /// <summary>Prefixed name to the file behind it, as found when this was built.</summary>
    private readonly Dictionary<string, string> _files;

    private readonly List<string> _ignored = [];

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

    /// <summary>Every file under the root that was never a candidate to load, with where it should go.</summary>
    public IReadOnlyList<string> Ignored => _ignored;

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

    /// <summary>
    /// Classifies every file under the root: into <see cref="_files"/> under the prefixed name the embedded
    /// source uses, or into <see cref="_ignored"/> with where it should go instead.
    /// </summary>
    private Dictionary<string, string> Scan()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        var extensions = new HashSet<string>(_decoder?.Extensions ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase) { Wav };

        foreach (var path in AllFiles())
        {
            var relative = Path.GetRelativePath(_root, path).Replace('\\', '/');
            var parts = relative.Split('/');

            if (!KnownFolders.Contains(parts[0], StringComparer.OrdinalIgnoreCase))
            {
                _ignored.Add($"{relative} is in a folder D47 does not read; folders are cues, alerts, beds and music.");
                continue;
            }

            if (!extensions.Contains(Path.GetExtension(path)))
            {
                _ignored.Add($"{relative} is not an audio format D47 reads.");
                continue;
            }

            var stem = Path.GetFileNameWithoutExtension(path);

            if (parts[0].Equals(BedsFolder, StringComparison.OrdinalIgnoreCase))
            {
                // Last one wins, and the enumeration is sorted, so two files that resolve to the same name
                // resolve deterministically rather than by whatever order the file system happened to answer
                // in.
                files[CueLibrary.BedPrefix + stem] = path;
                continue;
            }

            // cues/<state>/<file>, alerts/<alert>/<file>, music/<situation>/<file>; a file loose in the folder
            // has not said what it is for.
            if (parts.Length < 3)
            {
                _ignored.Add($"{Path.GetFileName(path)} is directly in {parts[0].ToLowerInvariant()}; put it in a folder such as {LooseExampleFor(parts[0])}.");
                continue;
            }

            var prefix = parts[0].ToLowerInvariant() switch
            {
                CuesFolder => CueLibrary.CuePrefix,
                AlertsFolder => CueLibrary.AlertPrefix,
                MusicFolder => CueLibrary.MusicPrefix,
                _ => null,
            };

            if (prefix is not null)
            {
                files[$"{prefix}{parts[1]}.{stem}"] = path;
            }
        }

        return files;
    }

    /// <summary>A real folder a Commander could use for the top-level folder named, for the ignored line.</summary>
    private static string LooseExampleFor(string folder) => folder.ToLowerInvariant() switch
    {
        CuesFolder => $"{CuesFolder}/{CueLibrary.FolderName(LoopState.Listening)}",
        AlertsFolder => $"{AlertsFolder}/{CueLibrary.FolderName(AlertCue.UnderFire)}",
        _ => $"{MusicFolder}/{Situations.General}",
    };

    /// <summary>Every file under the root, sorted; empty where the root does not exist or cannot be read.</summary>
    private List<string> AllFiles()
    {
        if (!Directory.Exists(_root))
        {
            return [];
        }

        var paths = new List<string>();

        try
        {
            foreach (var path in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            {
                paths.Add(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A root that cannot be read is a root with nothing in it, as far as this is concerned.
            _logger.LogWarning(ex, "Could not read {Folder}", _root);
        }

        // Grouped cues, alerts, beds, music first — the order folder counts are reported in — then
        // anything outside those folders, alphabetically within each group.
        return paths
            .OrderBy(FolderRank)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private int FolderRank(string path)
    {
        var top = Path.GetRelativePath(_root, path).Split(Path.DirectorySeparatorChar)[0];
        var rank = Array.FindIndex(KnownFolders, folder => folder.Equals(top, StringComparison.OrdinalIgnoreCase));
        return rank >= 0 ? rank : KnownFolders.Length;
    }
}

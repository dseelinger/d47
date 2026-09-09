using System.Reflection;
using D47.Core.Audio;
using D47.Core.Speech;

namespace D47.Core;

/// <summary>
/// Everything d47 writes lives in one folder beside the executable (Phase 1, "TheApp keeps your key and
/// your state safe").
/// </summary>
public sealed class AppPaths
{
    public const string DataFolderName = "data";

    /// <summary>
    /// <param name="installRoot">Where d47 writes: <c>data\</c> is made inside it.</param> <param
    /// name="buildRoot"> Where the build's own read-only files sit, which is the executable's folder
    /// and is the same place for everything published.
    /// </summary>
    /// <param name="installRoot">Where d47 writes: <c>data\</c> is made inside it.</param>
    /// <param name="buildRoot">
    /// Where the build's own read-only files sit, which is the executable's folder and is the same
    /// place for everything published.
    /// </param>
    public AppPaths(string installRoot, string? buildRoot = null)
    {
        InstallRoot = Path.GetFullPath(installRoot);
        Data = Path.Combine(InstallRoot, DataFolderName);
        Logs = Path.Combine(Data, "logs");
        Audio = Path.Combine(Data, "audio");
        SettingsFile = Path.Combine(Data, "settings.json");
        SecretsFile = Path.Combine(Data, "secrets.json");
        ViewStateFile = Path.Combine(Data, "view-state.json");
        SpendFile = Path.Combine(Data, "spend.jsonl");
        PronunciationsFile = Path.Combine(Data, PronunciationOverrides.FileName);
        VrActions = Path.Combine(Data, "vr-actions");
        DonorTokenFile = Path.Combine(Data, "donor-token.txt");
        Donations = Path.Combine(Data, "donations");
        Ships = Path.Combine(Data, "ships");
        ShippedShips = Path.Combine(Path.GetFullPath(buildRoot ?? InstallRoot), "ships");
    }

    /// <summary>Where this build writes.</summary>
    public static AppPaths ForRunningBuild() =>
        new(DevInstallRoot() ?? AppContext.BaseDirectory, AppContext.BaseDirectory);

    /// <summary>The Debug-only redirect, or null.</summary>
    private static string? DevInstallRoot() =>
        System.Reflection.Assembly.GetEntryAssembly()
            ?.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute =>
                string.Equals(attribute.Key, "DevInstallRoot", StringComparison.Ordinal))
            ?.Value is { Length: > 0 } root
            ? root
            : null;

    public string InstallRoot { get; }
    public string Data { get; }
    public string Logs { get; }

    /// <summary>Where the Commander drops their own cues, beds and ambience (Phase 12).</summary>
    public string Audio { get; }
    public string SettingsFile { get; }
    public string SecretsFile { get; }

    /// <summary>How the panel was left.</summary>
    public string ViewStateFile { get; }

    /// <summary>Every charge d47 has made, one JSON row per line.</summary>
    public string SpendFile { get; }

    /// <summary>How the Commander wants a word said, where the local voice gets one wrong (#150).</summary>
    public string PronunciationsFile { get; }

    /// <summary>
    /// The OpenVR action manifest and its binding files, written here rather than shipped as content
    /// beside the executable.
    /// </summary>
    public string VrActions { get; }

    /// <summary>
    /// The random per-installation donor token (#176), or where it will be written the first time a
    /// donation is made.
    /// </summary>
    public string DonorTokenFile { get; }

    /// <summary>
    /// The Commander's own copy of every donation they have made (#175) — what was shown, what was
    /// sent, and the hash that ties the two together.
    /// </summary>
    public string Donations { get; }

    /// <summary>
    /// The hull art that arrives after the install: the 4K picture Ship Details shows and the turntable
    /// a card plays, one of each per hull symbol.
    /// </summary>
    public string Ships { get; }

    /// <summary>The card stills that came with the build, read-only, beside the executable.</summary>
    public string ShippedShips { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Data);
        Directory.CreateDirectory(Logs);

        // The three drop-in folders by name, empty.
        Directory.CreateDirectory(Path.Combine(Audio, FolderAudioSource.CuesFolder));
        Directory.CreateDirectory(Path.Combine(Audio, FolderAudioSource.BedsFolder));
        Directory.CreateDirectory(Path.Combine(Audio, FolderAudioSource.MusicFolder));
        Directory.CreateDirectory(Ships);
    }
}

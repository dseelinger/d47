using D47.Core.Audio;

namespace D47.Core;

/// <summary>
/// Everything d47 writes lives in one folder beside the executable (list.md Phase 1,
/// "TheApp keeps your key and your state safe"). Portable by construction: copy the
/// folder and the state comes with it.
/// </summary>
public sealed class AppPaths
{
    public const string DataFolderName = "data";

    public AppPaths(string installRoot)
    {
        InstallRoot = Path.GetFullPath(installRoot);
        Data = Path.Combine(InstallRoot, DataFolderName);
        Logs = Path.Combine(Data, "logs");
        Audio = Path.Combine(Data, "audio");
        SettingsFile = Path.Combine(Data, "settings.json");
        SecretsFile = Path.Combine(Data, "secrets.json");
        ViewStateFile = Path.Combine(Data, "view-state.json");
        SpendFile = Path.Combine(Data, "spend.jsonl");
    }

    /// <summary>
    /// For a single-file publish <see cref="AppContext.BaseDirectory"/> is the executable's
    /// own directory rather than the native-extraction temp folder, which is what the
    /// "one writable folder beside the executable" requirement needs.
    /// </summary>
    public static AppPaths BesideExecutable() => new(AppContext.BaseDirectory);

    public string InstallRoot { get; }
    public string Data { get; }
    public string Logs { get; }

    /// <summary>
    /// Where the Commander drops their own cues, beds and ambience (list.md Phase 12). Created
    /// whether or not anything is in it, so the convention is discoverable by opening the data
    /// folder rather than by reading the documentation.
    /// </summary>
    public string Audio { get; }
    public string SettingsFile { get; }
    public string SecretsFile { get; }

    /// <summary>How the panel was left. A view preference, not a setting.</summary>
    public string ViewStateFile { get; }

    /// <summary>
    /// Every charge d47 has made, one JSON row per line. Append-only, so a crash costs the last
    /// row rather than the history, and readable by anything that can read a line at a time.
    /// </summary>
    public string SpendFile { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Data);
        Directory.CreateDirectory(Logs);

        // The three drop-in folders by name, empty. A convention nobody can see is a convention
        // nobody uses, and "make a folder called beds" is a step to get wrong.
        Directory.CreateDirectory(Path.Combine(Audio, FolderAudioSource.CuesFolder));
        Directory.CreateDirectory(Path.Combine(Audio, FolderAudioSource.BedsFolder));
        Directory.CreateDirectory(Path.Combine(Audio, FolderAudioSource.MusicFolder));
    }
}

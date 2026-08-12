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
        SettingsFile = Path.Combine(Data, "settings.json");
        SecretsFile = Path.Combine(Data, "secrets.json");
        ViewStateFile = Path.Combine(Data, "view-state.json");
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
    public string SettingsFile { get; }
    public string SecretsFile { get; }

    /// <summary>How the panel was left. A view preference, not a setting.</summary>
    public string ViewStateFile { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Data);
        Directory.CreateDirectory(Logs);
    }
}

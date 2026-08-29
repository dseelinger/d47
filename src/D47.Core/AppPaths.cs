using System.Reflection;
using D47.Core.Audio;
using D47.Core.Speech;

namespace D47.Core;

/// <summary>
/// Everything d47 writes lives in one folder beside the executable (Phase 1,
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
        PronunciationsFile = Path.Combine(Data, PronunciationOverrides.FileName);
        VrActions = Path.Combine(Data, "vr-actions");
    }

    /// <summary>
    /// Where this build writes. <b>Beside the executable</b>, which is the shipped rule and the
    /// whole of it for anything published.
    /// <para>
    /// For a single-file publish <see cref="AppContext.BaseDirectory"/> is the executable's
    /// own directory rather than the native-extraction temp folder, which is what the
    /// "one writable folder beside the executable" requirement needs.
    /// </para>
    /// <para>
    /// <b>A Debug build is the one exception, and it exists because of what beside-the-executable
    /// means there.</b> A Debug executable lives in <c>bin\Debug\…</c>, so the rule put a running
    /// Commander's checklist, settings and secrets inside build output — and on 2026-08-23 the
    /// obvious remedy for a stale artifact, deleting <c>bin\Debug</c>, took the lot with it. The
    /// path comes from <see cref="AssemblyMetadataAttribute"/> on the entry assembly, written by
    /// <c>D47.App.csproj</c> for the Debug configuration only, so a published build has no such
    /// attribute and cannot take this road. A test host has none either, which is why the suite is
    /// unaffected.
    /// </para>
    /// </summary>
    public static AppPaths ForRunningBuild() => new(DevInstallRoot() ?? AppContext.BaseDirectory);

    /// <summary>
    /// The Debug-only redirect, or null.
    /// <para>
    /// <b>An install root, and not the data folder.</b> This class puts <c>data\</c> inside
    /// whatever root it is handed, so the folder a Debug build actually writes to is
    /// <c>dev-install\data\</c> — named for what it is, because <c>dev-data\data\</c> is a path
    /// nobody can read twice without wondering which one is which.
    /// </para>
    /// <para>
    /// Empty is null too: a metadata value that failed to expand must not resolve the data folder
    /// to the drive root.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// Where the Commander drops their own cues, beds and ambience (Phase 12). Created
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

    /// <summary>
    /// How the Commander wants a word said, where the local voice gets one wrong (#150).
    /// <para>
    /// <b>Optional and absent by default, and nothing here creates it.</b> Deleting it has to
    /// restore shipped behaviour exactly, which a file d47 writes back on the next start would
    /// not — so this is a path rather than a folder in <see cref="EnsureCreated"/>, and the
    /// diagnostics page is where a Commander is told the name.
    /// </para>
    /// </summary>
    public string PronunciationsFile { get; }

    /// <summary>
    /// The OpenVR action manifest and its binding files, written here rather than shipped as
    /// content beside the executable.
    /// <para>
    /// Written at runtime for the reason the <c>.vrmanifest</c> beside them has to be: SteamVR
    /// resolves these paths itself, in another process, with a different working directory, so
    /// they have to be absolute — and one of them names whichever executable is actually running,
    /// which a checked-in file cannot. Putting all of them here means one seam rather than a
    /// shipped-content path and a generated one.
    /// </para>
    /// </summary>
    public string VrActions { get; }

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

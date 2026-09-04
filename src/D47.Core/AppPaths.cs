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

    /// <param name="installRoot">Where d47 writes: <c>data\</c> is made inside it.</param>
    /// <param name="buildRoot">
    /// Where the build's own read-only files sit, which is the executable's folder and is the
    /// same place for everything published. Separate only because a Debug build redirects
    /// <paramref name="installRoot"/> to <c>dev-install\</c> and the files that shipped are not
    /// there — they are in <c>bin\Debug\</c> beside the executable that is running. Defaults to
    /// the install root, so a test that wants one folder gets one folder.
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
    public static AppPaths ForRunningBuild() =>
        new(DevInstallRoot() ?? AppContext.BaseDirectory, AppContext.BaseDirectory);

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

    /// <summary>
    /// The random per-installation donor token
    /// (<a href="https://github.com/dseelinger/d47/issues/176">#176</a>), or where it will be
    /// written the first time a donation is made.
    /// <para>
    /// <b>A file of its own, and a plain one.</b> Deleting it is the documented withdrawal route,
    /// so it has to be a thing a Commander can find and delete without editing JSON around it —
    /// which rules out both <c>settings.json</c> (append-only, and a token is not something anyone
    /// typed) and <c>view-state.json</c> (how the panel was left).
    /// </para>
    /// </summary>
    public string DonorTokenFile { get; }

    /// <summary>
    /// The Commander's own copy of every donation they have made
    /// (<a href="https://github.com/dseelinger/d47/issues/175">#175</a>) — what was shown, what was
    /// sent, and the hash that ties the two together.
    /// <para>
    /// Written here rather than through a file picker, unlike the excerpt window's "Save a file
    /// instead…": a receipt is evidence the donor did not ask to be asked about, and one that
    /// interrupts the send to ask where to put it is one that gets cancelled.
    /// </para>
    /// </summary>
    public string Donations { get; }

    /// <summary>
    /// The hull art that arrives after the install: the 4K picture Ship Details shows and the
    /// turntable a card plays, one of each per hull symbol.
    /// <para>
    /// <b>Beside the executable rather than inside it, and that is the load-bearing part.</b>
    /// These were built into the binary first, which made three ordinary things impossible: a new
    /// hull meant a rebuild, a change to how the hulls are drawn meant a rebuild, and every
    /// Commander carried every hull whether they owned one ship or forty. As a folder, a hull is a
    /// file that appears - dropped in by hand, or fetched when a fleet turns out to need it.
    /// </para>
    /// <para>
    /// A drop-in folder like the audio ones, so it is created empty rather than left as a
    /// convention nobody can see.
    /// </para>
    /// </summary>
    public string Ships { get; }

    /// <summary>
    /// The card stills that came with the build, read-only, beside the executable.
    /// <para>
    /// <b>The one piece of hull art that is not a file that appears</b>
    /// (<a href="https://github.com/dseelinger/d47/issues/289">#289</a>). Every hull's resting
    /// drawing is 240 KB, so all forty-seven fit in eleven megabytes and a fresh installation has
    /// a fleet with pictures on it before anything is fetched. The 4K stills and the turntables
    /// are two hundred and sixty megabytes between them and stay out of the download.
    /// </para>
    /// <para>
    /// <b>Its own folder rather than seeding <see cref="Ships"/>, because the two are owned by
    /// different people.</b> <c>data\ships\</c> is the Commander's — an update never touches it,
    /// and an uninstall asks before removing it. This one is the build's, replaced wholesale by
    /// every update the way <c>runtimes\</c> is, so a corrected drawing actually arrives. A hull
    /// present in both reads from <c>data\</c>, which is what makes dropping a file in still work.
    /// </para>
    /// </summary>
    public string ShippedShips { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Data);
        Directory.CreateDirectory(Logs);

        // The three drop-in folders by name, empty. A convention nobody can see is a convention
        // nobody uses, and "make a folder called beds" is a step to get wrong.
        Directory.CreateDirectory(Path.Combine(Audio, FolderAudioSource.CuesFolder));
        Directory.CreateDirectory(Path.Combine(Audio, FolderAudioSource.BedsFolder));
        Directory.CreateDirectory(Path.Combine(Audio, FolderAudioSource.MusicFolder));
        Directory.CreateDirectory(Ships);
    }
}

using D47.Core.Storage;

namespace D47.Core.Capabilities.Builtin;

/// <summary>What this build is, and what came with it (#50).</summary>
public static class AboutCapability
{
    public const string Id = "about";

    public const string VersionKey = "about.version";

    public const string BuildKey = "about.build";

    public const string DataFolderKey = "about.dataFolder";

    public const string AttributionKey = "about.attribution";

    public const string ChangelogKey = "about.changelog";

    public const string ChangelogOnlineKey = "about.changelogOnline";

    public const string InstallUpdateKey = "about.installUpdate";

    public const string StartMenuKey = "about.startMenu";

    public const string SetUpKeysKey = "about.setUpKeys";

    public const string CommunityKey = "about.community";

    /// <summary>Frontier's long-form attribution, as their media usage rules word it.</summary>
    public const string Attribution =
        "This app is unofficial and is not endorsed by Frontier Developments plc. "
        + "Elite Dangerous is a registered trademark of Frontier Developments plc. "
        + "All game data is the property of Frontier Developments plc.";

    /// <param name="version">The version a Commander would quote — <c>BuildInfo.Semantic</c>.</param>
    /// <param name="build">The full build string including the commit.</param>
    /// <param name="channel">
    /// Whether GitHub calls this build's Release a pre-release, asked each time the row is drawn because
    /// the answer arrives over the network after the page exists (#92).
    /// </param>
    /// <param name="checkForUpdate">Checks GitHub for a newer release, on demand (#193).</param>
    /// <param name="installUpdate">Installs the release <paramref name="checkForUpdate"/> found (#193).</param>
    /// <param name="pendingUpdateVersion">
    /// The version <paramref name="checkForUpdate"/> found, or null while none is pending (#193).
    /// </param>
    public static CapabilityDescriptor Create(
        AppPaths paths,
        string version,
        string build,
        Action? showChangelog = null,
        Action? showChangelogOnline = null,
        Action? addToStartMenu = null,
        Func<bool>? startMenuWanted = null,
        Action? setUpKeys = null,
        Action? showCommunity = null,
        Func<Updates.ReleaseChannel>? channel = null,
        LongPress? checkForUpdate = null,
        LongPress? installUpdate = null,
        Func<string?>? pendingUpdateVersion = null,

        // Opens the folder the row above it names.
        Action? openDataFolder = null)
    {
        var rows = new List<SettingRow>
        {
            Live(
                VersionKey,
                "Version",
                () => Updates.ReleaseChannelText.Marked(version, channel?.Invoke() ?? Updates.ReleaseChannel.Unknown),
                "version",
                "Which release this is. A pre-release says so: it is a build offered to nobody "
                + "automatically, and it is not final.",
                checkForUpdate,
                checkForUpdate is null ? null : "Check for updates"),
            Stated(
                BuildKey,
                "Build",
                build,
                "build",
                "The exact commit this was built from. Quote it in a bug report — a version alone "
                + "cannot tell two builds of the same release apart."),
        };

        if (installUpdate is not null)
        {
            rows.Add(new SettingRow
            {
                Key = InstallUpdateKey,
                Label = "Install",
                Help =
                    "Downloads the release Check for updates found, replaces this build with it, and "
                    + "restarts. Present only while a newer release is known.",
                Kind = SettingKind.Info,
                DocsAnchor = "install-update",
                AppliesWhen = _ => pendingUpdateVersion?.Invoke() is not null,
                PressLabelFor = () => pendingUpdateVersion?.Invoke() is { } pending ? $"Install {pending}" : "Install",
                PressAsync = installUpdate,
            });
        }

        rows.Add(new SettingRow
        {
            Key = DataFolderKey,
            Label = "Data folder",
            Help =
                "Where D47 keeps everything it writes. Settings are saved as you go, to "
                + $"{paths.SettingsFile}. Keys are encrypted separately in secrets.json, and "
                + "how the panel is left is remembered in view-state.json.",
            Kind = SettingKind.Info,
            DocsAnchor = "data-folder",
            PressLabel = openDataFolder is null ? null : "Open data folder",
            Press = openDataFolder,
            Binding = new SettingBinding { Read = _ => paths.Data },
        });

        rows.Add(Stated(AttributionKey, "Attribution", Attribution, "attribution", "Frontier's own wording, verbatim."));

        if (showChangelog is { } changelog)
        {
            rows.Add(new SettingRow
            {
                Key = ChangelogKey,
                Label = "What changed",
                Help =
                    "Every release, newest first, as it shipped inside this build — so it reads "
                    + "with no internet at all.",
                Kind = SettingKind.Info,
                DocsAnchor = "changelog",
                Press = changelog,
                PressLabel = "Changelog",
            });
        }

        if (showChangelogOnline is { } online)
        {
            rows.Add(new SettingRow
            {
                Key = ChangelogOnlineKey,
                Label = "What changed since",
                Help =
                    "The changelog on the web, which is the only place a release newer than this "
                    + "one appears. Opens a browser.",
                Kind = SettingKind.Info,
                DocsAnchor = "changelog",
                Press = online,
                PressLabel = "Open on GitHub",
            });
        }

        if (showCommunity is { } community)
        {
            rows.Add(new SettingRow
            {
                Key = CommunityKey,
                Label = "Community",
                Help =
                    "Where the other Commanders are, and the fastest way to reach a person. "
                    + "Opens a browser.",
                Kind = SettingKind.Info,
                DocsAnchor = "community",
                Press = community,
                PressLabel = "Open the Discord",
            });
        }

        if (setUpKeys is { } keys)
        {
            rows.Add(new SettingRow
            {
                Key = SetUpKeysKey,
                Label = "Set up keys",
                Help = "Walks through the API keys again. Keys get rotated, so this is not only a first-run thing.",
                Kind = SettingKind.Info,
                DocsAnchor = "set-up-keys",
                Press = keys,
                PressLabel = "Set up keys",
            });
        }

        if (addToStartMenu is { } add)
        {
            rows.Add(new SettingRow
            {
                Key = StartMenuKey,
                Label = "Add to Start Menu",
                Help = "Puts a shortcut where Windows looks for one. Absent once there is one.",
                Kind = SettingKind.Info,
                DocsAnchor = "start-menu",

                // Absent once the shortcut exists, rather than a button that reports it already did the thing
                // — which is what "a row that does not apply is absent" means here.
                AppliesWhen = _ => startMenuWanted?.Invoke() ?? true,
                Press = add,
                PressLabel = "Add to Start Menu",
            });
        }

        return new CapabilityDescriptor
        {
            Id = Id,
            Group = "Interface",
            Name = "About",
            Summary = "Which build this is, where it keeps its files, and what changed.",

            // No tools and no keywords.
            Tools = [],
            Settings = rows,

            // Last, past Diagnostics' 90 and past Privacy's 95 (<a
            // href=".com/dseelinger/d47/issues/83">#83</a>).
            Display = new CapabilityDisplay { PanelTitle = "About", Order = 99 },
        };
    }

    /// <summary>
    /// A stated row whose value is asked for each time it is drawn, for the one fact here that can
    /// change while d47 is running: the release channel arrives from the network some moments after the
    /// page is built (#92).
    /// </summary>
    private static SettingRow Live(
        string key,
        string label,
        Func<string> value,
        string anchor,
        string help,
        LongPress? pressAsync = null,
        string? pressLabel = null) =>
        new()
        {
            Key = key,
            Label = label,
            Help = help,
            Kind = SettingKind.Info,
            DocsAnchor = anchor,
            Binding = new SettingBinding { Read = _ => value() },
            PressAsync = pressAsync,
            PressLabel = pressLabel,
        };

    private static SettingRow Stated(string key, string label, string value, string anchor, string help) =>
        new()
        {
            Key = key,
            Label = label,
            Help = help,
            Kind = SettingKind.Info,
            DocsAnchor = anchor,
            Binding = new SettingBinding { Read = _ => value },
        };
}

/// <summary>What the host can tell the About area about this build (#50).</summary>
public sealed record AboutSurface
{
    /// <summary>The full build string including the commit.</summary>
    public string? Build { get; init; }

    /// <summary>Whether GitHub calls this build's Release a pre-release.</summary>
    public Func<Updates.ReleaseChannel>? Channel { get; init; }

    /// <summary>Shows the changelog that shipped inside this build.</summary>
    public Action? ShowChangelog { get; init; }

    /// <summary>Opens the changelog on the web, where a newer release can appear.</summary>
    public Action? ShowChangelogOnline { get; init; }

    /// <summary>Creates the Start Menu shortcut.</summary>
    public Action? AddToStartMenu { get; init; }

    /// <summary>Whether there is not already one, asked each time the row is drawn.</summary>
    public Func<bool>? StartMenuWanted { get; init; }

    /// <summary>Reopens the guided key setup.</summary>
    public Action? SetUpKeys { get; init; }

    /// <summary>Opens the community page, which is where the Discord invite lives.</summary>
    public Action? ShowCommunity { get; init; }

    /// <summary>
    /// Opens the folder D47 writes to, or null where nothing composed one — under the designer, and in
    /// a test with no shell to open it with.
    /// </summary>
    public Action? OpenDataFolder { get; init; }

    /// <summary>Checks GitHub for a release newer than this build, on demand (#193).</summary>
    public LongPress? CheckForUpdate { get; init; }

    /// <summary>Downloads and installs the update <see cref="CheckForUpdate"/> found (#193).</summary>
    public LongPress? InstallUpdate { get; init; }

    /// <summary>The version <see cref="CheckForUpdate"/> found, or null while none is pending (#193).</summary>
    public Func<string?>? PendingUpdateVersion { get; init; }

    /// <summary>
    /// Every member supplied, each doing nothing — the surface a test binds when the test is not about
    /// About (#79).
    /// </summary>
    public static AboutSurface Inert => new()
    {
        Build = "0.0.0-test+0000000",
        Channel = () => Updates.ReleaseChannel.Unknown,
        ShowChangelog = () => { },
        ShowChangelogOnline = () => { },
        AddToStartMenu = () => { },
        StartMenuWanted = () => true,
        SetUpKeys = () => { },
        ShowCommunity = () => { },
        OpenDataFolder = () => { },
        CheckForUpdate = (_, _) => Task.FromResult<string?>(null),
        InstallUpdate = (_, _) => Task.FromResult<string?>(null),
        PendingUpdateVersion = () => null,
    };
}

using D47.Core.Storage;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// What this build is, and what came with it
/// (<a href="https://github.com/dseelinger/d47/issues/50">#50</a>).
/// <para>
/// Asked for 2026-08-25: an About <em>area</em> in Settings rather than a button in the footer.
/// The footer's own reasoning was that About and <em>Open data folder</em> "both answer 'where is
/// this thing and what is it', and one click from the tab strip is close enough for something read
/// once". The ask overturns that — About goes in the nav, where a Commander looks for it — and the
/// footer button goes with it, because two ways in that can drift is the thing this repository
/// keeps writing rules about.
/// </para>
/// <para>
/// <b>A capability with no tools and no keywords, and that was the design question.</b> The
/// alternative was teaching <c>SettingsView</c> a second source for the nav, which is cheaper here
/// and is a seam that did not exist before. A descriptor costs a documentation page — the gate
/// enforces one per registered capability — and buys the nav item, the search, the help link and
/// the card for free, all of which would otherwise need answering one at a time. The Commander
/// chose the descriptor, 2026-08-26.
/// </para>
/// <para>
/// <b>Nothing here is a setting</b>, which is why every row is <see cref="SettingKind.Info"/> — a
/// value d47 states, rendered as a row so it sits where the settings it describes do. The buttons
/// are <c>Info</c> rows carrying a <see cref="SettingRow.Press"/>, which is a shape the renderer
/// already had.
/// </para>
/// </summary>
public static class AboutCapability
{
    public const string Id = "about";

    public const string VersionKey = "about.version";

    public const string BuildKey = "about.build";

    public const string DataFolderKey = "about.dataFolder";

    public const string AttributionKey = "about.attribution";

    public const string ChangelogKey = "about.changelog";

    public const string ChangelogOnlineKey = "about.changelogOnline";

    public const string StartMenuKey = "about.startMenu";

    public const string SetUpKeysKey = "about.setUpKeys";

    public const string CommunityKey = "about.community";

    /// <summary>
    /// Frontier's long-form attribution, as their media usage rules word it.
    /// <para>
    /// <b>Here rather than in the App</b>, so the text that ships in the binary and the text the
    /// tests read are the same bytes. It is Frontier's wording and is not paraphrased; see
    /// <c>NOTICE</c>, which is where it lives once for the repository.
    /// </para>
    /// </summary>
    public const string Attribution =
        "This app is unofficial and is not endorsed by Frontier Developments plc. "
        + "Elite Dangerous is a registered trademark of Frontier Developments plc. "
        + "All game data is the property of Frontier Developments plc.";

    /// <param name="version">The version a Commander would quote — <c>BuildInfo.Semantic</c>.</param>
    /// <param name="channel">
    /// Whether GitHub calls this build's Release a pre-release, asked each time the row is drawn
    /// because the answer arrives over the network after the page exists — and because promoting a
    /// pre-release changes it without changing the binary (#92). Null, or
    /// <c>ReleaseChannel.Unknown</c>, shows the bare version and claims nothing.
    /// </param>
    /// <param name="build">
    /// The full build string including the commit, which is the thing a bug report cannot do
    /// without and the reason this area exists at all.
    /// </param>
    /// <param name="showChangelog">
    /// Opens the changelog that shipped inside this build. Null where there is no window to open
    /// one over — the designer, and every test that is not about it — and the row is then absent
    /// rather than offering a button that does nothing.
    /// </param>
    /// <param name="showChangelogOnline">
    /// Opens the changelog on the web. <b>Kept beside the offline one rather than replaced by
    /// it</b>: the shipped copy works with no internet, which the browser never did, and the
    /// browser is the only way to read a changelog <em>newer</em> than the running build — which
    /// is exactly what a Commander one release behind is asking for.
    /// </param>
    /// <param name="addToStartMenu">
    /// The permanent way in, since declining the first-run offer once would otherwise make that
    /// decision irreversible. Null hides the row; so does a shortcut that already exists.
    /// </param>
    /// <param name="setUpKeys">
    /// Reopens the guided key setup (list.md Phase 16). Here because <b>keys get rotated and
    /// revoked</b>, so the state that triggers the guide is one a working install can return to.
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
        Func<Updates.ReleaseChannel>? channel = null)
    {
        var rows = new List<SettingRow>
        {
            Live(
                VersionKey,
                "Version",
                () => Updates.ReleaseChannelText.Marked(version, channel?.Invoke() ?? Updates.ReleaseChannel.Unknown),
                "version",
                "Which release this is. A pre-release says so: it is a build offered to nobody "
                + "automatically, and it is not final."),
            Stated(
                BuildKey,
                "Build",
                build,
                "build",
                "The exact commit this was built from. Quote it in a bug report — a version alone "
                + "cannot tell two builds of the same release apart."),
            Stated(DataFolderKey, "Data folder", paths.Data, "data-folder", "Where D47 keeps everything it writes."),
            Stated(AttributionKey, "Attribution", Attribution, "attribution", "Frontier's own wording, verbatim."),
        };

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

                // Absent once the shortcut exists, rather than a button that reports it already
                // did the thing — which is what "a row that does not apply is absent" means here.
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

            // No tools and no keywords. There is nothing here for a model to do: every row is a
            // fact d47 already states elsewhere or a button only a person presses.
            Tools = [],
            Settings = rows,

            // Last, past Diagnostics' 90 and past Privacy's 95
            // (<a href="https://github.com/dseelinger/d47/issues/83">#83</a>). It is read once and
            // it is the bottom of the page in every application that has one.
            //
            // **99 rather than 96, and above Privacy rather than beside it.** This used to read 95,
            // which is Privacy's, and `SettingsService` sorts with a *stable* `OrderBy` — so the
            // last item in the left nav was decided by which of two lines came first in
            // `BuiltinCapabilities.All`, and nothing on either capability said so. The intent was
            // written down, in a comment on that list, where the sort cannot read it.
            //
            // The bottom of the page runs diagnose, disclose, identify. Privacy is read while
            // *deciding* something — it is the page opened before turning a provider on. This one
            // is read once, or while filing a bug. 99 leaves room, matching how the rest of the
            // scale is spaced.
            Display = new CapabilityDisplay { PanelTitle = "About", Order = 99 },
        };
    }

    /// <summary>
    /// A stated row whose value is asked for each time it is drawn, for the one fact here that can
    /// change while d47 is running: the release channel arrives from the network some moments
    /// after the page is built (#92).
    /// </summary>
    private static SettingRow Live(string key, string label, Func<string> value, string anchor, string help) =>
        new()
        {
            Key = key,
            Label = label,
            Help = help,
            Kind = SettingKind.Info,
            DocsAnchor = anchor,
            Binding = new SettingBinding { Read = _ => value() },
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

/// <summary>
/// What the host can tell the About area about this build
/// (<a href="https://github.com/dseelinger/d47/issues/50">#50</a>).
/// <para>
/// A record rather than eight more parameters on <c>BuiltinCapabilities.All</c>, which is already
/// long enough that adding to it positionally is how a caller ends up passing the wrong thing.
/// Every member is optional and a null one makes its row absent rather than dead.
/// </para>
/// <para>
/// <b>Core cannot reach any of this itself</b>, which is the point of the seam: the commit string
/// is an assembly attribute the App reads, a Start Menu shortcut is a shell object, and opening a
/// browser is a process. Core depends on nothing and stays that way.
/// </para>
/// </summary>
public sealed record AboutSurface
{
    /// <summary>The full build string including the commit.</summary>
    public string? Build { get; init; }

    /// <summary>
    /// Whether GitHub calls this build's Release a pre-release. Asked rather than held: it arrives
    /// over the network after startup, and it changes when a pre-release is promoted (#92).
    /// </summary>
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

    /// <summary>
    /// Opens the community page, which is where the Discord invite lives.
    /// <para>
    /// <b>The page, not the invite.</b> A <c>discord.gg</c> link compiled into a build is
    /// permanent — revoke that invite and every copy already installed has a dead button that
    /// only a release can fix. The page is a file in this repository, so reissuing an invite is
    /// a commit.
    /// </para>
    /// </summary>
    public Action? ShowCommunity { get; init; }

    /// <summary>
    /// Every member supplied, each doing nothing — the surface a test binds when the test is
    /// not about About (<a href="https://github.com/dseelinger/d47/issues/79">#79</a>).
    /// <para>
    /// <b>This exists because a null member is not a smaller surface, it is a different app.</b>
    /// Each optional member above makes its row <em>absent</em> when null, and an absent row is
    /// one no test can see. That is how <a href="https://github.com/dseelinger/d47/issues/78">#78</a>
    /// shipped two releases that could not start: four button-only rows reached a Commander
    /// having never once been bound by the 5,042 tests, because every test built this surface
    /// with nulls.
    /// </para>
    /// <para>
    /// So a test registry uses <b>this</b> rather than constructing one inline, and
    /// <c>HostSurfaceTests</c> asserts every property here is non-null. Add a member to the
    /// record without adding it here and that test fails, which is the whole point: the
    /// omission is caught at the moment it is made rather than at a Commander's next launch.
    /// </para>
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
    };
}

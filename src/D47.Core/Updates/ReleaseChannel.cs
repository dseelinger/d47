namespace D47.Core.Updates;

/// <summary>
/// Whether the running build is a release, a pre-release, or something d47 has not been able to ask
/// about (#92).
/// </summary>
public enum ReleaseChannel
{
    /// <summary>Nobody has been able to ask.</summary>
    Unknown,

    /// <summary>GitHub calls this release final.</summary>
    Release,

    /// <summary>GitHub carries its <c>prerelease</c> flag on this version's Release.</summary>
    PreRelease,

    /// <summary>Not a published build at all: something built from a working tree and installed by hand.</summary>
    Local,
}

/// <summary>How a channel is worded, in the two lengths the surfaces need.</summary>
public static class ReleaseChannelText
{
    /// <summary>For the title bar, which is on screen the whole time.</summary>
    public static string? Short(ReleaseChannel channel) => channel switch
    {
        ReleaseChannel.PreRelease => "pre-release",
        ReleaseChannel.Local => "local build",
        _ => null,
    };

    /// <summary>
    /// For About's Version row, which is the line a bug report quotes and so the one place the state
    /// should be spelled out rather than abbreviated.
    /// </summary>
    public static string? Full(ReleaseChannel channel) => channel switch
    {
        ReleaseChannel.PreRelease =>
            "pre-release — not offered to anyone automatically, and not final",
        ReleaseChannel.Local =>
            "local build — built from a working tree and installed by hand, not from any release",
        _ => null,
    };

    /// <summary>The version as a Commander should see it, marker included.</summary>
    public static string Marked(string version, ReleaseChannel channel) =>
        Short(channel) is { } marker ? $"{version} ({marker})" : version;
}

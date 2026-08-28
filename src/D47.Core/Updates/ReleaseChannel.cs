namespace D47.Core.Updates;

/// <summary>
/// Whether the running build is a release, a pre-release, or something d47 has not been able to
/// ask about (<a href="https://github.com/dseelinger/d47/issues/92">#92</a>).
/// <para>
/// <b>Three states, not two.</b> The obvious pair — release and pre-release — has no room for the
/// ordinary case of being offline, rate-limited, or started before the answer arrives. Collapsing
/// that into "release" is the lie in the dangerous direction: a Commander is told a build is final
/// precisely when nothing has confirmed that it is. <see cref="Unknown"/> shows no marker at all,
/// which is what a stable build looks like anyway, and says nothing untrue.
/// </para>
/// </summary>
public enum ReleaseChannel
{
    /// <summary>Nobody has been able to ask. No marker is shown, and none is implied.</summary>
    Unknown,

    /// <summary>GitHub calls this release final.</summary>
    Release,

    /// <summary>GitHub carries its <c>prerelease</c> flag on this version's Release.</summary>
    PreRelease,

    /// <summary>
    /// Not a published build at all: something built from a working tree and installed by hand.
    /// <para>
    /// <b>Answered from the binary rather than from GitHub, and it has to be.</b> A local build
    /// carries a version that resolves to a real published one — <c>get-local</c> stamps
    /// <c>0.84.3-local</c>, and comparison deliberately ignores everything after the dash — so
    /// asking GitHub about it gets a truthful answer about a <em>different binary</em>. That is how
    /// a hand-installed build came up calling itself the published pre-release 0.84.3, which is the
    /// one thing a build marker exists to prevent.
    /// </para>
    /// </summary>
    Local,
}

/// <summary>
/// How a channel is worded, in the two lengths the surfaces need.
/// <para>
/// <b>Both forms live here so the three sites cannot disagree</b>, which is the same reason
/// <c>BuildInfo</c> exists at all: the title bar, About and the panel badge answer one question and
/// must answer it identically.
/// </para>
/// <para>
/// <b>And why it is read at run time rather than stamped at build time.</b> Pre-release is a
/// property of the <em>Release</em>, which is mutable — <c>gh release edit … --prerelease=false</c>
/// promotes one, and <c>release.ps1</c> prints that command as the intended next step. It is not a
/// property of the binary, which is immutable: a published tag never moves, so a stamped build
/// would go on calling itself a pre-release for ever after being promoted, on every machine that
/// installed it, with no correction possible short of spending a version number.
/// </para>
/// </summary>
public static class ReleaseChannelText
{
    /// <summary>
    /// For the title bar, which is on screen the whole time. Short for the reason the commit hash
    /// is excluded from it: that strip is chrome a Commander cannot dismiss, so anything living
    /// there earns its width.
    /// </summary>
    public static string? Short(ReleaseChannel channel) => channel switch
    {
        ReleaseChannel.PreRelease => "pre-release",
        ReleaseChannel.Local => "local build",
        _ => null,
    };

    /// <summary>
    /// For About's Version row, which is the line a bug report quotes and so the one place the
    /// state should be spelled out rather than abbreviated.
    /// </summary>
    public static string? Full(ReleaseChannel channel) => channel switch
    {
        ReleaseChannel.PreRelease =>
            "pre-release — not offered to anyone automatically, and not final",
        ReleaseChannel.Local =>
            "local build — built from a working tree and installed by hand, not from any release",
        _ => null,
    };

    /// <summary>
    /// The version as a Commander should see it, marker included.
    /// <para>
    /// Null and <see cref="ReleaseChannel.Release"/> both produce the bare version, because a
    /// final release is the unmarked case and a build nobody could ask about must not be dressed
    /// as one thing or the other.
    /// </para>
    /// </summary>
    public static string Marked(string version, ReleaseChannel channel) =>
        Short(channel) is { } marker ? $"{version} ({marker})" : version;
}

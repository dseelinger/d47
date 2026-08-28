using System.Reflection;
using D47.Core.Updates;

namespace D47.App;

/// <summary>
/// Which build this is, from the one place that knows: the informational version the compiler
/// stamps in. Read once here rather than at each site that wants it, so the title bar, the
/// update check, the log and the About dialog cannot disagree about what is running.
/// </summary>
public static class BuildInfo
{
    /// <summary>
    /// The full stamp, version and commit both — "0.2.2+8b21b3d…". What a bug report needs, and
    /// therefore what the About dialog shows and lets you select.
    /// </summary>
    public static string Full { get; } =
        Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "unknown";

    /// <summary>
    /// Just the version — "0.2.2". What a title bar wants: the commit hash is forty characters
    /// of noise in the one piece of chrome that is on screen the entire time.
    /// <para>
    /// Falls back to the full string when there is no version to find, which is the case for a
    /// build run straight out of the repository.
    /// </para>
    /// </summary>
    public static string Semantic { get; } =
        ReleaseVersion.TryParse(Full, out var version) ? version.ToString() : Full;

    /// <summary>
    /// The pre-release label the version carries — <c>local</c> for a hand-installed build — or
    /// null for a published one.
    /// <para>
    /// <b>Any label at all means this did not come from a release</b>, and that is a rule rather
    /// than a guess about one tool's wording: the release workflow builds with the tag's bare
    /// version, so a published <c>d47.exe</c> never carries one. <c>get-local</c> stamps
    /// <c>-local</c>; anything else a person builds by hand is equally not a release.
    /// </para>
    /// <para>
    /// <see cref="Semantic"/> deliberately throws this away, because comparing versions must
    /// ignore it. That is exactly why it has to be read separately here — a local build compares
    /// equal to the release it was cut from, so it asked GitHub about that release and came up
    /// wearing its badge.
    /// </para>
    /// </summary>
    public static string? Label { get; } = LabelOf(Full);

    /// <summary>Whether this build came from a working tree rather than a published release.</summary>
    public static bool IsLocal => Label is not null;

    /// <summary>
    /// The label between the version and the commit — <c>0.84.3-local+8b21b3d</c> gives
    /// <c>local</c>. The build metadata is cut first, because the SDK appends it to every build and
    /// a hash is not a label.
    /// </summary>
    internal static string? LabelOf(string stamp)
    {
        var version = stamp.Split('+')[0];
        var dash = version.IndexOf('-', StringComparison.Ordinal);

        return dash >= 0 && dash < version.Length - 1 ? version[(dash + 1)..] : null;
    }
}
